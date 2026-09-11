import Foundation

enum CompanionError: LocalizedError {
    case message(String)
    var errorDescription: String? { if case .message(let text) = self { return text }; return nil }
}

// All protocol state belongs to the main actor. Pipe readers only frame bytes.
@MainActor
protocol CodexTransport: AnyObject {
    var notification: ((String, [String: Any]) -> Void)? { get set }
    var disconnected: (() -> Void)? { get set }
    func start(executable: String, home: URL, configuration: String) async throws
    func request(_ method: String, _ params: [String: Any]) async throws -> [String: Any]
    func stop()
}

@MainActor
protocol CodexVoiceBackend: AnyObject {
    var voiceNotification: ((String, [String: Any]) -> Void)? { get set }
    func voiceRequest(_ method: String, _ params: [String: Any]) async throws -> [String: Any]
}

@MainActor
protocol CodexActionTransport: CodexTransport {
    var toolCall: (([String: Any]) async -> [String: Any])? { get set }
    var appAccessRequest: (([String: Any]) -> [String: Any])? { get set }
}

@MainActor
final class CodexConnection: CodexActionTransport {
    private let clientTools: Bool
    private var process: Process?
    private var input: FileHandle?
    private var nextID = 0
    private var pending: [Int: CheckedContinuation<[String: Any], Error>] = [:]
    private var timeouts: [Int: Task<Void, Never>] = [:]
    var notification: ((String, [String: Any]) -> Void)?
    var disconnected: (() -> Void)?
    // Installed only by a live session. Ordinary question sessions continue to deny tools.
    var toolCall: (([String: Any]) async -> [String: Any])?
    var appAccessRequest: (([String: Any]) -> [String: Any])?

    init(clientTools: Bool = false) { self.clientTools = clientTools }

    static func executable() -> String? {
        let saved = UserDefaults.standard.string(forKey: "codexExecutable")
        let companionRuntime = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("LittleGuy3000/runtime/node_modules/.bin/codex").path
        let candidates = [saved, companionRuntime, "/opt/homebrew/bin/codex", "/usr/local/bin/codex"]
        guard let path = candidates.compactMap({ $0 }).first(where: { FileManager.default.isExecutableFile(atPath: $0) }) else { return nil }
        return nativeExecutable(path)
    }

    static func nativeExecutable(_ path: String) -> String {
        let resolved = URL(fileURLWithPath: path).resolvingSymlinksInPath()
        guard resolved.lastPathComponent == "codex.js" else { return path }
        #if arch(arm64)
        let platform = "arm64", triple = "aarch64"
        #else
        let platform = "x64", triple = "x86_64"
        #endif
        let package = resolved.deletingLastPathComponent().deletingLastPathComponent()
        let native = package.appendingPathComponent("node_modules/@openai/codex-darwin-\(platform)/vendor/\(triple)-apple-darwin/bin/codex")
        return FileManager.default.isExecutableFile(atPath: native.path) ? native.path : path
    }

    func start(executable: String, home: URL, configuration: String) async throws {
        stop()
        let fm = FileManager.default
        let workspace = home.appendingPathComponent("empty-workspace", isDirectory: true)
        try fm.createDirectory(at: workspace, withIntermediateDirectories: true,
                               attributes: [.posixPermissions: 0o700])
        try configuration.write(to: home.appendingPathComponent("config.toml"), atomically: true, encoding: .utf8)
        let child = Process(), stdinPipe = Pipe(), stdoutPipe = Pipe(), stderrPipe = Pipe()
        child.executableURL = URL(fileURLWithPath: executable)
        // Astra's client functions require the code-mode dispatcher. Enable that
        // host only for the live helper; shell/file/environment tools stay disabled.
        child.arguments = (clientTools ? ["-c", "features.code_mode_host=true", "-c", "features.code_mode=true"] : [])
            + ["app-server", "--listen", "stdio://"]
        child.currentDirectoryURL = workspace
        let inherited = ProcessInfo.processInfo.environment
        var environment: [String: String] = [:]
        for key in ["HOME", "USER", "LOGNAME", "TMPDIR", "LANG", "LC_ALL", "__CF_USER_TEXT_ENCODING"] {
            environment[key] = inherited[key]
        }
        environment["PATH"] = "/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin"
        environment["CODEX_HOME"] = home.path
        child.environment = environment
        child.standardInput = stdinPipe; child.standardOutput = stdoutPipe; child.standardError = stderrPipe
        try child.run()
        process = child; input = stdinPipe.fileHandleForWriting
        // Identity-check callbacks so an old reader cannot disconnect a new connection.
        DispatchQueue.global(qos: .userInitiated).async { [weak self, weak child] in
            var buffer = Data()
            let reader = stdoutPipe.fileHandleForReading
            while true {
                let data = reader.availableData
                if data.isEmpty { break }
                buffer.append(data)
                if buffer.count > 8 * 1024 * 1024 { break }
                while let newline = buffer.firstIndex(of: 10) {
                    let line = Data(buffer[..<newline])
                    buffer.removeSubrange(...newline)
                    Task { @MainActor in
                        guard let self, let child, self.process === child else { return }
                        self.receive(line)
                    }
                }
            }
            Task { @MainActor in
                guard let self, let child, self.process === child else { return }
                self.stop(); self.disconnected?()
            }
        }
        DispatchQueue.global(qos: .utility).async {
            // Drain without persisting payloads, credentials, or model errors.
            while !stderrPipe.fileHandleForReading.availableData.isEmpty {}
        }
        do {
            _ = try await request("initialize", [
                "clientInfo": ["name": "LittleGuy3000Mac", "title": "Little Guy 3000", "version": "0.6.1"],
                "capabilities": ["experimentalApi": true, "mcpServerOpenaiFormElicitation": true]
            ])
            try write(["method": "initialized"])
        } catch { stop(); throw error }
    }

    func request(_ method: String, _ params: [String: Any] = [:]) async throws -> [String: Any] {
        nextID += 1
        let id = nextID
        return try await withCheckedThrowingContinuation { continuation in
            pending[id] = continuation
            timeouts[id] = Task { [weak self] in
                do { try await Task.sleep(for: .seconds(40)) } catch { return }
                self?.resolve(id, .failure(CompanionError.message("Codex timed out. Reconnect and try again.")))
            }
            do { try write(["id": id, "method": method, "params": params]) }
            catch { resolve(id, .failure(error)) }
        }
    }

    private func write(_ payload: [String: Any]) throws {
        guard let input, process?.isRunning == true else {
            throw CompanionError.message("Codex is disconnected. Select Connect.")
        }
        var data = try JSONSerialization.data(withJSONObject: payload)
        guard data.count < 24 * 1024 * 1024 else { throw CompanionError.message("Image is too large. Select a smaller area.") }
        data.append(10)
        try input.write(contentsOf: data)
    }

    private func receive(_ data: Data) {
        guard let value = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            stop(); disconnected?(); return
        }
        if let method = value["method"] as? String {
            if let id = value["id"] {
                if method == "mcpServer/elicitation/request" {
                    let params = value["params"] as? [String: Any] ?? [:]
                    try? write(["id": id, "result": appAccessRequest?(params) ?? ["action": "decline"]])
                    return
                }
                if method == "item/tool/call", let handler = toolCall, let child = process {
                    let params = value["params"] as? [String: Any] ?? [:]
                    Task { [weak self, weak child] in
                        let result = await handler(params)
                        guard let self, let child, self.process === child else { return }
                        try? self.write(["id": id, "result": result])
                    }
                    return
                }
                // Guide mode never approves actions or supplies host tools.
                if method == "item/commandExecution/requestApproval" || method == "item/fileChange/requestApproval" {
                    try? write(["id": id, "result": ["decision": "decline"]])
                } else {
                    try? write(["id": id, "error": ["code": -32601, "message": "Unavailable in guide mode."]])
                }
            } else { notification?(method, value["params"] as? [String: Any] ?? [:]) }
        } else if let id = value["id"] as? Int {
            if let error = value["error"] as? [String: Any] {
                resolve(id, .failure(CompanionError.message("Codex request failed (\(error["code"] ?? "unknown")). Check sign-in or reconnect.")))
            } else { resolve(id, .success(value["result"] as? [String: Any] ?? [:])) }
        }
    }

    private func resolve(_ id: Int, _ result: Result<[String: Any], Error>) {
        timeouts.removeValue(forKey: id)?.cancel()
        pending.removeValue(forKey: id)?.resume(with: result)
    }

    func stop() {
        let child = process
        process = nil
        try? input?.close(); input = nil
        for id in Array(pending.keys) { resolve(id, .failure(CompanionError.message("Codex connection closed."))) }
        if let child, child.isRunning {
            child.terminate()
            DispatchQueue.global().asyncAfter(deadline: .now() + 2) {
                if child.isRunning { kill(child.processIdentifier, SIGKILL) }
            }
        }
    }
}

// Reject unrelated or stale turns before updating visible answer state.
struct AnswerStream {
    var threadID: String
    var turnID: String?
    var text = ""
    var failureMessage: String?
    private var itemID: String?
    init(threadID: String) { self.threadID = threadID }

    mutating func accept(_ method: String, _ body: [String: Any]) -> String? {
        guard body["threadId"] as? String == threadID else { return nil }
        if let incoming = body["turnId"] as? String, let turnID, incoming != turnID { return nil }
        if method == "turn/started", let turn = body["turn"] as? [String: Any] {
            turnID = turn["id"] as? String
        }
        if method == "item/completed", let item = body["item"] as? [String: Any],
           item["type"] as? String == "agentMessage", let finalText = item["text"] as? String {
            guard finalText.utf8.count <= 200_000 else { return "oversized" }
            text = finalText
            return "delta"
        }
        if method == "item/agentMessage/delta", let delta = body["delta"] as? String {
            let incoming = body["itemId"] as? String
            if incoming != itemID { text = ""; itemID = incoming }
            guard text.utf8.count + delta.utf8.count <= 200_000 else { return "oversized" }
            text += delta
            return "delta"
        }
        if method == "turn/completed", let turn = body["turn"] as? [String: Any] {
            if let turnID, turn["id"] as? String != turnID { return nil }
            if let error = turn["error"] as? [String: Any] {
                switch error["codexErrorInfo"] as? String {
                case "unauthorized": failureMessage = "Your sign-in expired. Reconnect and sign in again."
                case "usageLimitExceeded", "sessionBudgetExceeded": failureMessage = "Your Codex usage limit was reached. Try again after it resets."
                case "contextWindowExceeded": failureMessage = "This conversation is full. Start a New conversation."
                case "serverOverloaded": failureMessage = "Codex is busy. Please try again shortly."
                case "badRequest": failureMessage = "Codex could not accept this request. Try another model or a New conversation."
                default: failureMessage = "The answer failed. Your question is restored; check your connection and try again."
                }
            }
            return turn["status"] as? String ?? "failed"
        }
        return nil
    }
}
