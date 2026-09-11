import AppKit

@MainActor private final class RealtimeCheck: NSObject, NSApplicationDelegate {
    let live = LiveConversation()
    let player = CodexVoicePlayer()
    var window: NSWindow!
    func applicationDidFinishLaunching(_ notification: Notification) {
        window = NSWindow(contentRect: NSRect(x: 100, y: 100, width: 340, height: 100), styleMask: [.titled], backing: .buffered, defer: false)
        window.title = "Little Guy Live Voice Test"; window.makeKeyAndOrderFront(nil)
        Task {
            do {
                let first = try Data(contentsOf: URL(fileURLWithPath: ".local/live-first.wav"))
                let second = try Data(contentsOf: URL(fileURLWithPath: ".local/live-interrupt.wav"))
                let tested = LiveConversation(player: player)
                tested.testAudio = first
                let home = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("LittleGuy3000/codex")
                let config = try String(contentsOfFile: "artifacts/Little Guy 3000.app/Contents/Resources/guide-config.toml", encoding: .utf8)
                tested.start(in: window.contentView!, executable: CodexConnection.executable()!, home: home, configuration: config, target: nil, screenEnabled: false, syntheticInput: true)
                var interrupted = false, previous = ""
                let deadline = Date().addingTimeInterval(100)
                while Date() < deadline {
                    if let error = tested.error { throw CompanionError.message(error) }
                    if tested.status != previous { previous = tested.status; print(previous) }
                    if tested.status.hasPrefix("Speaking"), !interrupted {
                        interrupted = true; print("Injecting synthetic interruption into the SAME open call")
                        player.playTestInput(second)
                    }
                    if interrupted, tested.heardText.lowercased().contains("peach"), tested.replyText.lowercased().contains("peach") {
                        precondition(tested.active && tested.connected)
                        print("PASS: live audio input transcribed, streamed spoken answer, second spoken request during playback answered in the same open call")
                        tested.stop(); precondition(!tested.active && !tested.connected)
                        NSApp.terminate(nil); return
                    }
                    try await Task.sleep(for: .milliseconds(100))
                }
                print("Last synthetic input:", tested.heardText, "Reply:", tested.replyText)
                tested.stop(); throw CompanionError.message("Live interruption test timed out")
            } catch { print("FAIL:", error.localizedDescription); exit(1) }
        }
    }
}
@main struct RealtimeLiveTests {
    @MainActor static func main() {
        let app = NSApplication.shared, delegate = RealtimeCheck()
        app.setActivationPolicy(.accessory); app.delegate = delegate; app.run()
    }
}
