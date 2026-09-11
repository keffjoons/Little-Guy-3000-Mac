import AppKit
import AVFoundation

@MainActor private final class DelayedVoiceTransport: CodexActionTransport {
    let base = CodexConnection(clientTools: true)
    var notification: ((String, [String: Any]) -> Void)? { get { base.notification } set { base.notification = newValue } }
    var disconnected: (() -> Void)? { get { base.disconnected } set { base.disconnected = newValue } }
    var toolCall: (([String: Any]) async -> [String: Any])? { get { base.toolCall } set { base.toolCall = newValue } }
    var appAccessRequest: (([String: Any]) -> [String: Any])? { get { base.appAccessRequest } set { base.appAccessRequest = newValue } }
    func start(executable: String, home: URL, configuration: String) async throws {
        try await Task.sleep(for: .seconds(6))
        try await base.start(executable: executable, home: home, configuration: configuration)
    }
    func request(_ method: String, _ params: [String: Any]) async throws -> [String: Any] { try await base.request(method, params) }
    func stop() { base.stop() }
}
@MainActor private final class ColdVoiceAcceptance: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    var live: LiveConversation!
    func applicationDidFinishLaunching(_ notification: Notification) {
        setbuf(stdout, nil)
        window = NSWindow(contentRect: NSRect(x: 100, y: 100, width: 200, height: 60), styleMask: [.titled], backing: .buffered, defer: false)
        window.title = "Little Guy early speech test"
        Task {
            do {
                let player = CodexVoicePlayer(), backend = DelayedVoiceTransport()
                live = LiveConversation(transport: backend, player: player)
                let audioURL = URL(fileURLWithPath: ".local/cold-voice.wav")
                let audio = try Data(contentsOf: audioURL), file = try AVAudioFile(forReading: audioURL)
                let duration = Double(file.length) / file.processingFormat.sampleRate
                let home = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/LittleGuy3000/codex")
                let config = try String(contentsOfFile: "artifacts/Little Guy 3000.app/Contents/Resources/guide-config.toml", encoding: .utf8)
                let started = Date()
                live.start(in: window.contentView!, executable: CodexConnection.executable()!, home: home, configuration: config,
                           target: nil, screenEnabled: false, syntheticInput: true)
                live.setShortcutHeld(true)
                let receive = player.event
                var sent = false, releasedBeforeReady = false, heardAnswer = false, warm = false
                var utteranceStarted = started, firstTranscript = false
                var readyEvents = 0
                player.event = { body in
                    receive?(body)
                    if body["ready"] as? Bool == true { readyEvents += 1; print("Voice connected after", Date().timeIntervalSince(started), "seconds") }
                    if body["audible"] as? Bool == true {
                        heardAnswer = true
                        print("Received audible reply after", Date().timeIntervalSince(utteranceStarted), "seconds from speech injection")
                    }
                    if body["microphone"] as? Bool == true && !sent {
                        sent = true
                        precondition(!self.live.connected)
                        print("Local input ready after", Date().timeIntervalSince(started), "seconds; sending first words before Codex connects")
                        utteranceStarted = Date(); player.playTestInput(audio)
                        Task {
                            try await Task.sleep(for: .seconds(duration + 0.2))
                            releasedBeforeReady = !self.live.connected
                            self.live.setShortcutHeld(false)
                            print("Released before connection:", releasedBeforeReady)
                        }
                    }
                }
                let deadline = Date().addingTimeInterval(100)
                var previous = ""
                while Date() < deadline {
                    if let error = live.error { throw CompanionError.message(error) }
                    if !live.heardText.isEmpty && !firstTranscript {
                        firstTranscript = true
                        print(warm ? "Warm" : "Cold", "first transcript after", Date().timeIntervalSince(utteranceStarted), "seconds from speech injection")
                    }
                    let current = live.heardText + " | " + live.replyText
                    if current != previous { previous = current; print(current) }
                    if live.heardText.lowercased().contains("please repeat the words"), live.heardText.lowercased().contains("banana"), live.replyText.lowercased().contains("banana"), heardAnswer, !live.speaking {
                        precondition(sent && releasedBeforeReady && live.muted)
                        print(warm ? "Warm request completed" : "PASS: first words preserved through six-second cold start and release before connection")
                        print("Transcript:", live.heardText)
                        print("Reply:", live.replyText)
                        if !warm {
                            warm = true; heardAnswer = false; firstTranscript = false
                            window.orderOut(nil)
                            live.setShortcutHeld(true)
                            precondition(live.connected && live.inputReady)
                            utteranceStarted = Date(); player.playTestInput(audio)
                            Task { try await Task.sleep(for: .seconds(duration + 0.2)); self.live.setShortcutHeld(false) }
                            continue
                        }
                        precondition(readyEvents == 1)
                        print("PASS: immediate warm request with hidden popup, same connection, complete speech and audible reply")
                        live.stop(); NSApp.terminate(nil); return
                    }
                    try await Task.sleep(for: .milliseconds(100))
                }
                print("Transcript:", live.heardText, "Reply:", live.replyText)
                throw CompanionError.message("Voice acceptance timed out (warm: \(warm), audible: \(heardAnswer))")
            } catch { live?.stop(); print("FAIL:", error.localizedDescription); exit(1) }
        }
    }
}
@main struct ColdVoiceLiveTests {
    @MainActor static func main() {
        let app = NSApplication.shared, delegate = ColdVoiceAcceptance()
        app.setActivationPolicy(.accessory); app.delegate = delegate; app.run()
    }
}
