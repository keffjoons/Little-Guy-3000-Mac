import AppKit

// Opt-in real-device acceptance: synthetic speech drives the actual realtime
// session, native Computer Use, and Spotify. Ends with playback paused.
@MainActor private final class NativeAcceptance: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    var live: LiveConversation!
    func applicationDidFinishLaunching(_ notification: Notification) {
        setbuf(stdout, nil)
        window = NSWindow(contentRect: NSRect(x: 100, y: 100, width: 1, height: 1), styleMask: [], backing: .buffered, defer: false)
        Task {
            do {
                let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] ?? []
                guard let spotify = windows.first(where: { $0[kCGWindowOwnerName as String] as? String == "Spotify" && $0[kCGWindowLayer as String] as? Int == 0 }),
                      let id = spotify[kCGWindowNumber as String] as? UInt32,
                      let pid = spotify[kCGWindowOwnerPID as String] as? Int32 else { throw CompanionError.message("Open Spotify before this opt-in test.") }
                let foreground = NSWorkspace.shared.frontmostApplication?.processIdentifier
                print("Initial foreground PID:", foreground ?? -1)
                let backend = CodexConnection(clientTools: true), player = CodexVoicePlayer()
                var captureDelivered = false, nativeCalls = 0, observedPlaying = false, observedPausedAfterPlaying = false
                var accessRequests = 0, audioSent = false
                live = LiveConversation(transport: backend, player: player, readScreen: { target in
                    let result = try await PointerCapture.readContext(target)
                    captureDelivered = true
                    print("PASS: automatic Spotify screenshot captured (\(result.image.count) bytes)")
                    return result
                })
                let receive = backend.notification, access = backend.appAccessRequest
                backend.appAccessRequest = { request in
                    accessRequests += 1
                    let response = access!(request)
                    print("Native app access:", response["action"] ?? "unknown")
                    return response
                }
                backend.notification = { method, body in
                    receive?(method, body)
                    if method == "thread/realtime/transcript/done", body["role"] as? String == "user" {
                        self.live.setShortcutHeld(false)
                        print("User speech received; microphone muted:", self.live.muted)
                    }
                    guard method == "item/completed", let item = body["item"] as? [String: Any],
                          item["type"] as? String == "mcpToolCall", item["server"] as? String == "cua_repl" else { return }
                    nativeCalls += 1
                    let result = item["result"] as? [String: Any] ?? [:]
                    let content = result["content"] as? [[String: Any]] ?? []
                    let text = content.compactMap { $0["text"] as? String }.joined(separator: "\n")
                    let code = (item["arguments"] as? [String: Any])?["code"] as? String ?? ""
                    print("NATIVE", item["tool"] ?? "", item["status"] ?? "", String(code.prefix(800)))
                    let playbackLines = text.components(separatedBy: "\n").filter { $0.contains("button Pause") || $0.contains("button Play") }
                    print("PLAYBACK EVIDENCE", playbackLines.prefix(10).joined(separator: " | "))
                    if text.contains("button Pause") { observedPlaying = true }
                    if observedPlaying && text.contains("button Play") && !text.contains("button Pause") { observedPausedAfterPlaying = true }
                    if result["isError"] as? Bool == true { print("NATIVE ERROR", String(text.prefix(500))) }
                }
                let home = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/LittleGuy3000/codex")
                let config = try String(contentsOfFile: "artifacts/Little Guy 3000.app/Contents/Resources/guide-config.toml", encoding: .utf8)
                let audio = try Data(contentsOf: URL(fileURLWithPath: ".local/native-playback.wav"))
                live.start(in: window.contentView!, executable: CodexConnection.executable()!, home: home, configuration: config,
                    target: PointerTarget(id: id, pid: pid, name: "Spotify"), screenEnabled: true, syntheticInput: true)
                live.setShortcutHeld(true)
                var readyAt: Date?, previousReply = "", replyChanged = Date()
                let deadline = Date().addingTimeInterval(200)
                while Date() < deadline {
                    if let error = live.error { throw CompanionError.message(error) }
                    if live.connected && readyAt == nil { readyAt = Date() }
                    if !audioSent {
                        precondition(live.replyText.isEmpty && !live.speaking && nativeCalls == 0 && accessRequests == 0)
                        if let readyAt, Date().timeIntervalSince(readyAt) > 8 {
                            print("PASS: eight silent seconds with shortcut held; no reply or native action")
                            audioSent = true; player.playTestInput(audio)
                        }
                    }
                    if live.replyText != previousReply { previousReply = live.replyText; replyChanged = Date() }
                    if observedPausedAfterPlaying && live.muted && !live.speaking && !live.replyText.isEmpty && Date().timeIntervalSince(replyChanged) > 3 {
                        guard captureDelivered else { throw CompanionError.message("Automatic capture was not delivered") }
                        guard foreground == NSWorkspace.shared.frontmostApplication?.processIdentifier else {
                            throw CompanionError.message("Foreground app changed during background acceptance")
                        }
                        print("PASS: actual native Spotify playback resumed then paused; foreground app unchanged; mic muted")
                        print("Voice reply:", live.replyText)
                        live.stop(); NSApp.terminate(nil); return
                    }
                    try await Task.sleep(for: .milliseconds(100))
                }
                print("Foreground unchanged:", foreground == NSWorkspace.shared.frontmostApplication?.processIdentifier)
                print("Acceptance state:", nativeCalls, observedPlaying, observedPausedAfterPlaying, "Voice reply:", live.replyText)
                throw CompanionError.message("Native voice acceptance timed out")
            } catch { live?.stop(); print("FAIL:", error.localizedDescription); exit(1) }
        }
    }
}
@main struct NativeComputerUseLiveTests {
    @MainActor static func main() {
        let app = NSApplication.shared, delegate = NativeAcceptance()
        app.setActivationPolicy(.accessory); app.delegate = delegate; app.run()
    }
}
