import AppKit

// Uses existing Little Guy sign-in. Speaks only this synthetic sentence; no microphone or screen capture.
@MainActor private final class LiveVoiceCheck: NSObject, NSApplicationDelegate {
    private let session = CompanionSession(transport: CodexConnection())
    private lazy var speech = SpeechController(backend: session)
    private var window: NSWindow!
    func applicationDidFinishLaunching(_ notification: Notification) {
        window = NSWindow(contentRect: NSRect(x: 100, y: 100, width: 320, height: 100),
                          styleMask: [.titled], backing: .buffered, defer: false)
        window.title = "Little Guy Voice Test"
        window.contentView = NSView(frame: NSRect(x: 0, y: 0, width: 320, height: 100))
        window.makeKeyAndOrderFront(nil)
        Task {
            do {
                guard let executable = CodexConnection.executable() else { throw CompanionError.message("Codex is required") }
                let home = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
                    .appendingPathComponent("LittleGuy3000/codex")
                let configuration = try String(contentsOfFile: "artifacts/Little Guy 3000.app/Contents/Resources/guide-config.toml", encoding: .utf8)
                await session.connect(executable: executable, home: home, configuration: configuration)
                guard session.connection == .ready else { throw CompanionError.message(session.error ?? "Sign in first") }
                speech.speak("Hi, I’m Little Guy. Codex voice is connected.", in: window.contentView!)
                var heard = false, previous = ""
                let deadline = Date().addingTimeInterval(60)
                while speech.speaking && Date() < deadline {
                    if speech.status != previous { previous = speech.status; print(previous) }
                    if speech.status == "Speaking · Codex voice" { heard = true }
                    try await Task.sleep(for: .milliseconds(100))
                }
                guard heard, speech.status == "Finished speaking · Codex voice" else {
                    throw CompanionError.message("Live voice failed: \(speech.status)")
                }
                print("PASS: production Codex WebRTC voice received non-silent audio and completed playback using existing ChatGPT sign-in")
                speech.stop(); session.shutdown(); NSApp.terminate(nil)
            } catch { print("FAIL: \(error.localizedDescription)"); speech.stop(); session.shutdown(); exit(1) }
        }
    }
}

@main struct LiveVoiceTests {
    @MainActor static func main() {
        let app = NSApplication.shared, delegate = LiveVoiceCheck()
        app.setActivationPolicy(.accessory); app.delegate = delegate; app.run()
    }
}
