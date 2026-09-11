import AppKit

@MainActor private final class RealtimeBackend: CodexActionTransport {
    var notification: ((String, [String: Any]) -> Void)?
    var disconnected: (() -> Void)?
    var toolCall: (([String: Any]) async -> [String: Any])?
    var calls: [(String, [String: Any])] = []
    var stops = 0
    func start(executable: String, home: URL, configuration: String) async throws {}
    func stop() { stops += 1 }
    func request(_ method: String, _ params: [String: Any]) async throws -> [String: Any] {
        calls.append((method, params))
        if method == "account/read" { return ["account": ["planType": "pro"]] }
        if method == "thread/start" { return ["thread": ["id": "live"]] }
        return [:]
    }
    func emit(_ method: String, _ body: [String: Any]) { notification?(method, body) }
}
@MainActor private final class RealtimePlayer: LiveVoicePlaying {
    var event: (([String: Any]) -> Void)?
    var prepares = 0, stops = 0
    var muted = false
    var sdp = ""
    func prepare(in host: NSView) {}
    func prepareConversation(in host: NSView, syntheticInput: Bool) { prepares += 1 }
    func answer(_ sdp: String) { self.sdp = sdp }
    func finishWhenQuiet() { fatalError("Live calls must not stop on a completed answer") }
    func stop() { stops += 1 }
    func setMuted(_ muted: Bool) { self.muted = muted }
    func playTestInput(_ data: Data) {}
}
@main struct RealtimeTests {
    @MainActor static func settle() async { for _ in 0..<30 { await Task.yield() } }
    @MainActor static func main() async {
        let backend = RealtimeBackend(), player = RealtimePlayer(), host = NSView()
        let live = LiveConversation(transport: backend, player: player, readScreen: { target in (Data([1]), "Visible heading in \(target.name): Test library") })
        live.start(in: host, executable: "fixture", home: URL(fileURLWithPath: "/tmp"), configuration: "", target: nil, screenEnabled: false, syntheticInput: true)
        await settle(); precondition(live.active && player.prepares == 1)
        player.event?(["sdp": "offer"]); await settle()
        let params = backend.calls.first { $0.0 == "thread/realtime/start" }!.1
        precondition(params["clientManagedHandoffs"] as? Bool == false && params["version"] as? String == "v3")
        precondition((params["initialItems"] as? [[String: String]])?.first?["text"]?.contains("Screen context is OFF") == true)
        player.event?(["ready": true]); precondition(live.connected)
        precondition(live.muted, "Connecting must never open the microphone")
        live.setShortcutHeld(true); precondition(!live.muted && !player.muted)
        live.setShortcutHeld(false); precondition(live.muted && player.muted)
        player.muted = false
        player.event?(["microphone": true]); precondition(player.muted, "Mute must survive delayed microphone authorization")
        live.setShortcutHeld(true); precondition(!live.muted && !player.muted)
        backend.emit("thread/realtime/sdp", ["threadId": "wrong", "sdp": "wrong"])
        precondition(player.sdp.isEmpty)
        backend.emit("thread/realtime/sdp", ["threadId": "live", "sdp": "answer"])
        precondition(player.sdp == "answer")
        live.send("hello"); await settle()
        backend.emit("thread/realtime/transcript/done", ["threadId": "live", "role": "assistant", "text": "Hello"])
        player.event?(["audible": true]); precondition(live.speaking)
        player.event?(["quiet": true]); precondition(!live.speaking)
        precondition(live.active && live.connected && live.replyText == "Hello")
        live.send("second request"); await settle()
        precondition(backend.calls.filter { $0.0 == "thread/start" }.count == 1)
        precondition(backend.calls.filter { $0.0 == "thread/realtime/start" }.count == 1)
        precondition(backend.calls.filter { $0.0 == "thread/realtime/appendText" && $0.1["role"] as? String == "user" }.count == 2)
        let wrong = await backend.toolCall!(["threadId": "stale", "tool": "press_control", "arguments": ["control": "x"]])
        precondition(wrong["success"] as? Bool == false)
        let noScreen = await backend.toolCall!(["threadId": "live", "tool": "inspect_window", "arguments": [:]])
        precondition(noScreen["success"] as? Bool == false)
        let spotify = PointerTarget(id: 42, pid: 9, name: "Spotify")
        live.setContext(target: spotify, enabled: true); await settle()
        let context = backend.calls.last!.1
        precondition(context["role"] as? String == "developer" && (context["text"] as? String)?.contains("Spotify") == true)
        live.send("How do I make a new playlist?"); await settle()
        let requestIndex = backend.calls.lastIndex { $0.1["text"] as? String == "How do I make a new playlist?" }!
        precondition(backend.calls[requestIndex - 1].1["role"] as? String == "developer", "Current context precedes the question")
        let snapshot = backend.calls[..<requestIndex].last { $0.0 == "thread/inject_items" }!.1
        let items = snapshot["items"] as! [[String: Any]]
        let content = items[0]["content"] as! [[String: Any]]
        precondition(content.contains { $0["type"] as? String == "input_image" }, "Automatically deliver the full screenshot before the question")
        precondition((backend.calls[requestIndex - 1].1["text"] as? String)?.contains("Test library") == true, "Voice receives visible content too")
        live.setContext(target: nil, enabled: true); await settle()
        precondition((backend.calls.last!.1["text"] as? String)?.contains("no window was selected") == true)
        let cleared = backend.calls.last { $0.0 == "thread/inject_items" }!.1["items"] as! [[String: Any]]
        precondition((cleared[0]["content"] as! [[String: Any]]).count == 1, "Missing window must not resend an old screenshot")
        live.setContext(target: spotify, enabled: false); await settle()
        precondition((backend.calls.last!.1["text"] as? String)?.contains("Screen context is OFF") == true)
        backend.emit("thread/realtime/transcript/done", ["threadId": "live", "role": "user", "text": "Previous question"])
        backend.emit("thread/realtime/transcript/done", ["threadId": "live", "role": "assistant", "text": "Previous answer"])
        live.setShortcutHeld(false); live.setShortcutHeld(true)
        precondition(live.heardText.isEmpty && live.replyText.isEmpty, "A new spoken request replaces the previous exchange")
        let staleCallback = player.event
        live.stop(); precondition(!live.active && !live.connected && backend.stops > 0)
        staleCallback?(["ready": true]); precondition(!live.connected)
        precondition(WindowActions.isRequestedPlayback(bundleID: "com.spotify.client", label: "Play Test Playlist", intent: "play this playlist"))
        precondition(!WindowActions.isRequestedPlayback(bundleID: "com.spotify.client", label: "Play", intent: "don't play this"))
        precondition(!WindowActions.isRequestedPlayback(bundleID: "com.other.app", label: "Play", intent: "play this"))
        precondition(!WindowActions.isRequestedPlayback(bundleID: "com.spotify.client", label: "Delete", intent: "play this"))
        precondition(WindowActions.playbackChanged(before: "Play", after: "Pause"))
        precondition(!WindowActions.playbackChanged(before: "Play", after: "Play"))
        precondition(WindowActions.spotifyURI("https://xpui.app.spotify.com/playlist/37i9dQZF1E39P9bu7Fxig1") == URL(string: "spotify:playlist:37i9dQZF1E39P9bu7Fxig1"))
        precondition(WindowActions.spotifyURI("https://evil.example/playlist/37i9dQZF1E39P9bu7Fxig1") == nil)
        precondition(WindowActions.spotifyURI("https://open.spotify.com/playlist/../../anything") == nil)
        live.start(in: host, executable: "fixture", home: URL(fileURLWithPath: "/tmp"), configuration: "", target: nil, screenEnabled: false, syntheticInput: true)
        live.setShortcutHeld(true); live.setShortcutHeld(false)
        await settle(); player.muted = false; player.event?(["microphone": true]); player.event?(["ready": true])
        precondition(live.muted && player.muted, "Release during connection must survive late microphone startup")
        live.stop()
        live.start(in: host, executable: "fixture", home: URL(fileURLWithPath: "/tmp"), configuration: "", target: spotify, screenEnabled: true, syntheticInput: true)
        await settle(); player.event?(["sdp": "offer"]); await settle()
        let initial = backend.calls.last { $0.0 == "thread/realtime/start" }!.1["initialItems"] as! [[String: String]]
        precondition(initial.first!["text"]!.contains("Spotify"), "The first voice turn knows the selected app")
        live.setContext(target: PointerTarget(id: 43, pid: 10, name: "Notes"), enabled: true)
        player.event?(["ready": true]); await settle()
        precondition((backend.calls.last!.1["text"] as? String)?.contains("Notes") == true, "A window change during connection replaces startup context")
        live.stop()
        let delayedBackend = RealtimeBackend(), delayedPlayer = RealtimePlayer()
        var pendingCapture: CheckedContinuation<(image: Data, text: String), Error>?
        let delayed = LiveConversation(transport: delayedBackend, player: delayedPlayer, readScreen: { _ in
            try await withCheckedThrowingContinuation { pendingCapture = $0 }
        })
        delayed.start(in: host, executable: "fixture", home: URL(fileURLWithPath: "/tmp"), configuration: "", target: spotify, screenEnabled: true, syntheticInput: true)
        await settle(); delayedPlayer.event?(["ready": true]); delayed.setShortcutHeld(true); await settle()
        precondition(pendingCapture != nil)
        delayed.setContext(target: nil, enabled: false); await settle()
        pendingCapture?.resume(returning: (Data([1]), "STALE SPOTIFY CONTENT")); await settle()
        let updates = delayedBackend.calls.filter { $0.0 == "thread/inject_items" }
        precondition(!updates.contains { String(describing: $0.1).contains("input_image") }, "Late captures cannot upload after screen context is disabled")
        precondition(!delayedBackend.calls.contains { String(describing: $0.1).contains("STALE SPOTIFY CONTENT") })
        delayed.stop()
        let page = CodexVoicePlayer.page(conversation: true)
        precondition(page.contains("getUserMedia") && page.contains("echoCancellation:true") && page.contains("track.stop()"))
        precondition(!CodexVoicePlayer.html.contains("getUserMedia"))
        print("PASS: persistent realtime session, mute, transcript completion without teardown, stale sessions, scope gating, shortcut-only microphone input and playback receipt policy")
    }
}
