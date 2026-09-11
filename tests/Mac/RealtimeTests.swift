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
        let live = LiveConversation(transport: backend, player: player)
        live.start(in: host, executable: "fixture", home: URL(fileURLWithPath: "/tmp"), configuration: "", target: nil, screenEnabled: false, syntheticInput: true)
        await settle(); precondition(live.active && player.prepares == 1)
        player.event?(["sdp": "offer"]); await settle()
        let params = backend.calls.first { $0.0 == "thread/realtime/start" }!.1
        precondition(params["clientManagedHandoffs"] as? Bool == false && params["version"] as? String == "v3")
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
        precondition(backend.calls.filter { $0.0 == "thread/realtime/appendText" }.count == 2)
        let wrong = await backend.toolCall!(["threadId": "stale", "tool": "press_control", "arguments": ["control": "x"]])
        precondition(wrong["success"] as? Bool == false)
        let noScreen = await backend.toolCall!(["threadId": "live", "tool": "inspect_window", "arguments": [:]])
        precondition(noScreen["success"] as? Bool == false)
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
        let page = CodexVoicePlayer.page(conversation: true)
        precondition(page.contains("getUserMedia") && page.contains("echoCancellation:true") && page.contains("track.stop()"))
        precondition(!CodexVoicePlayer.html.contains("getUserMedia"))
        print("PASS: persistent realtime session, mute, transcript completion without teardown, stale sessions, scope gating, shortcut-only microphone input and playback receipt policy")
    }
}
