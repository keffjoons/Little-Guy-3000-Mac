import AppKit

@MainActor final class QuickBackend: CodexTransport {
    var notification: ((String, [String: Any]) -> Void)?
    var disconnected: (() -> Void)?
    var available = true
    var turns: [[String: Any]] = []
    func start(executable: String, home: URL, configuration: String) async throws {}
    func stop() {}
    func request(_ method: String, _ params: [String: Any]) async throws -> [String: Any] {
        switch method {
        case "account/read": return ["account": ["planType": "pro"]]
        case "model/list": return ["data": available ? [["id": "gpt-6-astra", "supportedReasoningEfforts": [["reasoningEffort": "low"]]]] : []]
        case "thread/start":
            precondition(params["model"] as? String == "gpt-6-astra")
            return ["thread": ["id": "quick-test"]]
        case "turn/start":
            turns.append(params)
            notification?("item/completed", ["threadId": "quick-test", "turnId": "turn", "item": ["type": "agentMessage", "text": "Answer"]])
            notification?("turn/completed", ["threadId": "quick-test", "turn": ["id": "turn", "status": "completed"]])
            return ["turn": ["id": "turn"]]
        default: return [:]
        }
    }
}

@MainActor final class QuickVoice: VoiceTranscribing {
    var update: ((String) -> Void)?
    var completed: ((Result<String, Error>) -> Void)?
    var starts = 0
    var finishes = 0
    var pending: CheckedContinuation<Void, Never>?
    var delay = false
    func start() async throws {
        starts += 1
        if delay { await withCheckedContinuation { pending = $0 } }
    }
    func finish() { finishes += 1; completed?(.success("What is this control?")) }
    func cancel() {}
}

@main struct QuickTests {
    @MainActor static func main() async throws {
        let suite = "QuickTests-\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        let backend = QuickBackend(), voice = QuickVoice()
        let session = CompanionSession(transport: backend, defaults: defaults)
        await session.connect(executable: "fixture", home: URL(fileURLWithPath: "/tmp"), configuration: "")
        session.setCompact(true)
        var captures = 0
        var late: CheckedContinuation<Data, Error>?
        var delayCapture = false
        let quick = QuickCompanion(session: session, voice: voice, defaults: defaults) { _ in
            captures += 1
            if delayCapture { return try await withCheckedThrowingContinuation { late = $0 } }
            return Data([UInt8(captures)])
        }
        quick.screenPermitted = true
        quick.point(at: PointerTarget(id: 8, pid: 9, name: "Test window"))
        quick.keyDown(); quick.keyUp()
        try await Task.sleep(for: .milliseconds(350))
        precondition(voice.starts == 0, "Tapping must not open a microphone")
        quick.keyDown()
        try await Task.sleep(for: .milliseconds(350))
        precondition(quick.phase == .listening)
        voice.update?("What is this")
        precondition(backend.turns.isEmpty, "Partial speech must never submit")
        quick.keyUp()
        try await Task.sleep(for: .milliseconds(30))
        precondition(voice.finishes == 1 && backend.turns.count == 1 && captures == 1)
        precondition(backend.turns[0]["effort"] as? String == "low")
        precondition(backend.turns[0]["model"] as? String == "gpt-6-astra")
        session.draft = "Follow up"; quick.send()
        try await Task.sleep(for: .milliseconds(30))
        precondition(captures == 2, "Follow-ups need a fresh image")

        quick.screenPermitted = false; session.draft = "Blocked capture"; quick.send()
        precondition(backend.turns.count == 2 && session.draft == "Blocked capture")
        quick.screenEnabled = false; quick.send()
        try await Task.sleep(for: .milliseconds(30))
        let input = backend.turns.last?["input"] as? [[String: Any]] ?? []
        precondition(!input.contains { $0["type"] as? String == "image" })
        quick.screenEnabled = true; quick.screenPermitted = true; delayCapture = true
        session.draft = "Cancel capture"; quick.send()
        try await Task.sleep(for: .milliseconds(30))
        quick.cancel(); late?.resume(returning: Data([99]))
        try await Task.sleep(for: .milliseconds(30))
        precondition(backend.turns.count == 3 && session.imageData == nil && session.draft == "Cancel capture")
        precondition(!session.isCapturing && !quick.active)

        session.draft = ""; voice.delay = true; quick.startVoice()
        try await Task.sleep(for: .milliseconds(30))
        quick.keyUp(); voice.pending?.resume()
        try await Task.sleep(for: .milliseconds(30))
        precondition(!quick.active, "Releasing during permissions must not start late recording")
        voice.completed?(.success("Stale words"))
        precondition(session.draft.isEmpty && backend.turns.count == 3)
        var holds = 0, releases = 0
        quick.beginLiveVoice = { holds += 1 }
        quick.endLiveInput = { releases += 1 }
        quick.keyDown(); quick.keyDown()
        precondition(holds == 1, "Live input starts synchronously on keydown")
        try await Task.sleep(for: .milliseconds(350))
        precondition(holds == 1, "Repeat keydown must not restart a call")
        quick.keyUp(); precondition(releases == 1, "Releasing must mute live input")
        quick.keyDown(); quick.keyUp()
        try await Task.sleep(for: .milliseconds(350))
        precondition(holds == 2 && releases == 2, "A short press captures immediately and mutes on release, with no delayed restart")
        backend.available = false; await session.reconnect()
        session.draft = "Missing Astra"; quick.send()
        precondition(backend.turns.count == 3 && session.error?.contains("Astra") == true)

        func window(_ id: UInt32, _ pid: Int32, _ layer: Int, _ rect: CGRect) -> [String: Any] {
            [kCGWindowNumber as String: id, kCGWindowOwnerPID as String: pid, kCGWindowLayer as String: layer,
             kCGWindowBounds as String: rect.dictionaryRepresentation, kCGWindowOwnerName as String: "Test"]
        }
        let rect = CGRect(x: -900, y: -200, width: 800, height: 600)
        let list = [window(1, 42, 0, rect), window(2, 2, 3, rect), window(3, 3, 0, rect), window(4, 4, 0, rect)]
        let target = PointerCapture.target(at: CGPoint(x: -800, y: -100), windows: list, ownPID: 42)
        precondition(target?.id == 3, "Hit test must exclude self and overlays, support negative monitor coordinates, choose topmost")
        precondition(PointerCapture.target(at: CGPoint(x: 9000, y: 9000), windows: list, ownPID: 42) == nil)
        session.shutdown()
        print("PASS: tap/hold/release, final-only speech submission, Astra Low, fresh captures, denied access, text-only, stale capture/voice cancellation, missing model, multi-display pointer selection")
    }
}
