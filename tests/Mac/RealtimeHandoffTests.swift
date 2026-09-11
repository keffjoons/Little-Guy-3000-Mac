import AppKit

@MainActor private final class HandoffCheck: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    var live: LiveConversation!
    private static func fixture(_ text: String, background: NSColor = .black) -> Data {
        let image = NSImage(size: NSSize(width: 640, height: 300))
        image.lockFocus()
        background.setFill(); NSRect(x: 0, y: 0, width: 640, height: 300).fill()
        (text as NSString).draw(in: NSRect(x: 30, y: 80, width: 580, height: 190),
            withAttributes: [.font: NSFont.systemFont(ofSize: 28), .foregroundColor: NSColor.white])
        image.unlockFocus()
        return NSBitmapImageRep(data: image.tiffRepresentation!)!.representation(using: .png, properties: [:])!
    }
    func applicationDidFinishLaunching(_ notification: Notification) {
        setbuf(stdout, nil)
        window = NSWindow(contentRect: NSRect(x: 100, y: 100, width: 340, height: 100), styleMask: [.titled], backing: .buffered, defer: false)
        window.title = "Little Guy Screen Handoff Test"; window.makeKeyAndOrderFront(nil)
        Task {
            do {
                let backend = CodexConnection(clientTools: true)
                var inspected = false
                var checkedWarmTurn = false
                let playlist = CommandLine.arguments.contains("--playlist")
                var image: Data
                if playlist {
                    image = Self.fixture("Spotify — synthetic test window\nYour Library\n+ Create playlist")
                } else { image = try Data(contentsOf: URL(fileURLWithPath: ".local/image-test-card.png")) }
                let voicePlayer = CodexVoicePlayer()
                live = LiveConversation(transport: backend, player: voicePlayer, readScreen: { _ in (image, try await PointerCapture.recognizeText(image)) })
                // Only the external window is simulated. Codex voice, delegation,
                // Astra, JSON-RPC dynamic-tool dispatch and image delivery are real.
                backend.toolCall = { params in
                    guard params["tool"] as? String == "inspect_window" else {
                        return WindowActions.result("This test permits inspection only.", success: false)
                    }
                    inspected = true
                    return WindowActions.result(checkedWarmTurn ? "Synthetic Notes window. Its visible heading is Cedar Notebook 731." : playlist ? "Synthetic Spotify window. Your Library contains [create] AXButton: Create playlist. This test is guidance only; no actions are available."
                        : "Synthetic test window. The attached image is current visual evidence. No action controls are available.", image: checkedWarmTurn ? nil : image)
                }
                live.testAudio = try Data(contentsOf: URL(fileURLWithPath: playlist ? ".local/live-playlist.wav" : ".local/live-screen.wav"))
                let home = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("LittleGuy3000/codex")
                let config = try String(contentsOfFile: "artifacts/Little Guy 3000.app/Contents/Resources/guide-config.toml", encoding: .utf8)
                live.start(in: window.contentView!, executable: CodexConnection.executable()!, home: home, configuration: config,
                    target: PointerTarget(id: 42, pid: 9, name: playlist ? "Spotify" : "Synthetic test window"), screenEnabled: true, syntheticInput: true)
                live.setShortcutHeld(true)
                var previous = ""
                var previousReply = "", replyChangedAt = Date()
                let deadline = Date().addingTimeInterval(150)
                while Date() < deadline {
                    if let error = live.error { throw CompanionError.message(error) }
                    if live.status != previous { previous = live.status; print(previous) }
                    if live.replyText != previousReply { previousReply = live.replyText; replyChangedAt = Date() }
                    let correct = checkedWarmTurn ? live.replyText.contains("731") && live.replyText.lowercased().contains("blue") : playlist ? live.replyText.lowercased().contains("create playlist")
                        : live.replyText.lowercased().contains("green") && live.replyText.contains("472")
                    if (inspected || checkedWarmTurn) && correct && !live.speaking && Date().timeIntervalSince(replyChangedAt) > 1.5 {
                        if playlist && !checkedWarmTurn {
                            print("PASS: spoken playlist question inspected the selected Spotify window. Checking context update in the same call.")
                            inspected = false; checkedWarmTurn = true
                            image = Self.fixture("Notes — synthetic test window\nCedar Notebook 731\n+ New note", background: .blue)
                            live.setShortcutHeld(false)
                            live.setContext(target: PointerTarget(id: 43, pid: 10, name: "Notes"), enabled: true)
                            live.setShortcutHeld(true)
                            voicePlayer.playTestInput(try Data(contentsOf: URL(fileURLWithPath: ".local/live-window-change.wav")))
                            continue
                        }
                        print("PASS: selected-window guidance and fresh automatic capture after switching apps in the same voice call")
                        print("Synthetic reply:", live.replyText)
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
