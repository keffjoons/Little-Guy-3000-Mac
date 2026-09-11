import AppKit

@MainActor private final class HandoffCheck: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    var live: LiveConversation!
    func applicationDidFinishLaunching(_ notification: Notification) {
        window = NSWindow(contentRect: NSRect(x: 100, y: 100, width: 340, height: 100), styleMask: [.titled], backing: .buffered, defer: false)
        window.title = "Little Guy Screen Handoff Test"; window.makeKeyAndOrderFront(nil)
        Task {
            do {
                let backend = CodexConnection(clientTools: true)
                live = LiveConversation(transport: backend)
                var inspected = false
                let image = try Data(contentsOf: URL(fileURLWithPath: ".local/image-test-card.png"))
                // Only the external window is simulated. Codex voice, delegation,
                // Astra, JSON-RPC dynamic-tool dispatch and image delivery are real.
                backend.toolCall = { params in
                    guard params["tool"] as? String == "inspect_window" else {
                        return WindowActions.result("This test permits inspection only.", success: false)
                    }
                    inspected = true
                    return WindowActions.result("Synthetic test window. The attached image is current visual evidence. No action controls are available.", image: image)
                }
                live.testAudio = try Data(contentsOf: URL(fileURLWithPath: ".local/live-screen.wav"))
                let home = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("LittleGuy3000/codex")
                let config = try String(contentsOfFile: "artifacts/Little Guy 3000.app/Contents/Resources/guide-config.toml", encoding: .utf8)
                live.start(in: window.contentView!, executable: CodexConnection.executable()!, home: home, configuration: config, target: nil, screenEnabled: true, syntheticInput: true)
                var previous = ""
                let deadline = Date().addingTimeInterval(150)
                while Date() < deadline {
                    if let error = live.error { throw CompanionError.message(error) }
                    if live.status != previous { previous = live.status; print(previous) }
                    if inspected && live.replyText.lowercased().contains("green") && live.replyText.contains("472") {
                        print("PASS: spoken screen question delegated to Astra, which called inspect_window, understood its image result, and returned the correct spoken answer in the open live call")
                        live.stop(); NSApp.terminate(nil); return
                    }
                    try await Task.sleep(for: .milliseconds(100))
                }
                print("Inspection called:", inspected, "Synthetic reply:", live.replyText)
                throw CompanionError.message("Voice-to-Astra tool handoff timed out")
            } catch { live?.stop(); print("FAIL:", error.localizedDescription); exit(1) }
        }
    }
}
@main struct RealtimeHandoffTests {
    @MainActor static func main() {
        let app = NSApplication.shared, delegate = HandoffCheck()
        app.setActivationPolicy(.accessory); app.delegate = delegate; app.run()
    }
}
