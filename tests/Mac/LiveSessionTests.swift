import Foundation

// Optional acceptance check. Uses Little Guy's existing sign-in and sends only the
// synthetic card produced by ImageTests. Quit the app before running this check.
@main struct LiveSessionTests {
    @MainActor static func main() async throws {
        guard let executable = CodexConnection.executable() else { throw CompanionError.message("Codex is required") }
        let home = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("LittleGuy3000/codex")
        let config = try String(contentsOfFile: "artifacts/Little Guy 3000.app/Contents/Resources/guide-config.toml", encoding: .utf8)
        let suite = "LittleGuyLiveTests-\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        let session = CompanionSession(transport: CodexConnection(), defaults: defaults)
        defer { session.shutdown() }
        await session.connect(executable: executable, home: home, configuration: config)
        guard session.connection == .ready else { throw CompanionError.message(session.error ?? "Sign in in Little Guy first") }
        session.imageData = try Data(contentsOf: URL(fileURLWithPath: ".local/image-test-card.png"))
        session.draft = "What colour is the square and what verification code is shown in this image? Answer in one sentence."
        await session.send()
        try await waitForAnswer(session)
        let answer = session.messages.last?.text ?? ""
        guard answer.lowercased().contains("green"), answer.contains("MAPLE"), answer.contains("472") else {
            throw CompanionError.message("Image answer did not match the synthetic fixture: \(answer)")
        }
        print("PASS: real model recognized the green square and MAPLE 472 using the production session")
        await session.reconnect()
        session.draft = "Repeat the verification code from our previous exchange. Do not claim you can see a current image."
        await session.send()
        try await waitForAnswer(session)
        guard session.messages.last?.text.contains("472") == true else {
            throw CompanionError.message("Follow-up lost conversation context after reconnect")
        }
        print("PASS: real follow-up retained completed conversation after reconnect without resending the image")
    }

    @MainActor private static func waitForAnswer(_ session: CompanionSession) async throws {
        let deadline = Date().addingTimeInterval(190)
        while session.busy && Date() < deadline { try await Task.sleep(for: .milliseconds(100)) }
        if let error = session.error { throw CompanionError.message(error) }
        guard !session.busy else { throw CompanionError.message("Live check timed out") }
    }
}
