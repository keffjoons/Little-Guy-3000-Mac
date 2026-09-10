import AppKit

@MainActor private final class VoiceBackend: CodexVoiceBackend {
    var voiceNotification: ((String, [String: Any]) -> Void)?
    var requests: [(String, [String: Any])] = []
    var holdCreation = false
    var creation: CheckedContinuation<[String: Any], Error>?
    var nextID = 0
    func voiceRequest(_ method: String, _ params: [String: Any]) async throws -> [String: Any] {
        requests.append((method, params))
        if method == "thread/start" {
            if holdCreation { return try await withCheckedThrowingContinuation { creation = $0 } }
            nextID += 1; return ["thread": ["id": "voice-\(nextID)"]]
        }
        return [:]
    }
    func emit(_ method: String, id: String = "voice-1", extra: [String: Any] = [:]) {
        voiceNotification?(method, extra.merging(["threadId": id]) { _, new in new })
    }
}

@MainActor private final class Player: VoicePlaying {
    var event: (([String: Any]) -> Void)?
    var prepared = 0, stops = 0, finishes = 0
    var answers: [String] = []
    func prepare(in host: NSView) { prepared += 1 }
    func answer(_ sdp: String) { answers.append(sdp) }
    func finishWhenQuiet() { finishes += 1 }
    func stop() { stops += 1 }
}

@main struct SpeechTests {
    @MainActor static func settle() async { for _ in 0..<25 { await Task.yield() } }
    @MainActor static func main() async {
        let backend = VoiceBackend(), player = Player(), host = NSView()
        let speech = SpeechController(backend: backend, player: player)
        speech.speak("Exact answer text.", in: host); await settle()
        precondition(player.prepared == 1 && speech.speaking)
        let params = backend.requests[0].1
        precondition(params["ephemeral"] as? Bool == true && params["approvalPolicy"] as? String == "never")
        precondition((params["dynamicTools"] as? [Any])?.isEmpty == true)
        player.event?(["sdp": "offer"]); await settle()
        let start = backend.requests.first { $0.0 == "thread/realtime/start" }!.1
        precondition(start["version"] as? String == "v3" && (start["transport"] as? [String: String])?["type"] == "webrtc")
        backend.emit("thread/realtime/sdp", id: "unrelated", extra: ["sdp": "wrong"])
        backend.emit("thread/realtime/sdp", extra: ["sdp": "answer"])
        precondition(player.answers == ["answer"])
        player.event?(["ready": true]); player.event?(["ready": true]); await settle()
        let submissions = backend.requests.filter { $0.0 == "thread/realtime/appendSpeech" }
        precondition(submissions.count == 1 && submissions[0].1["text"] as? String == "Exact answer text.")
        backend.emit("thread/realtime/transcript/done", extra: ["role": "assistant"])
        precondition(player.finishes == 1 && speech.speaking, "Transcript completion must not cut buffered audio")
        player.event?(["audible": true]); precondition(speech.status == "Speaking · Codex voice")
        let staleEvent = player.event
        player.event?(["finished": true]); await settle()
        precondition(!speech.speaking && speech.status == "Finished speaking · Codex voice")
        precondition(backend.requests.contains { $0.0 == "thread/realtime/stop" })
        precondition(backend.requests.contains { $0.0 == "thread/unsubscribe" })
        speech.speak("Next answer", in: host); await settle()
        staleEvent?(["error": "stale failure"])
        backend.emit("thread/realtime/error")
        precondition(speech.speaking, "Old playback must not affect a new answer")
        backend.voiceNotification?("connection/closed", [:])
        precondition(!speech.speaking && speech.status.contains("reconnected"))
        speech.speak("", in: host); precondition(!speech.speaking)
        speech.speak(String(repeating: "x", count: 20_001), in: host)
        precondition(!speech.speaking && speech.status.contains("too long"))
        let lateBackend = VoiceBackend(), latePlayer = Player()
        lateBackend.holdCreation = true
        let lateSpeech = SpeechController(backend: lateBackend, player: latePlayer)
        lateSpeech.speak("Cancelled answer", in: host); await settle()
        precondition(lateBackend.creation != nil)
        lateSpeech.stop()
        lateBackend.creation?.resume(returning: ["thread": ["id": "late"]]); await settle()
        precondition(latePlayer.prepared == 0 && !lateSpeech.speaking)
        precondition(lateBackend.requests.contains { $0.0 == "thread/realtime/stop" && $0.1["threadId"] as? String == "late" })
        precondition(!CodexVoicePlayer.html.contains("getUserMedia"), "Playback must not record microphone input")
        print("PASS: Codex voice negotiation, exact text, audio completion, cancellation, stale events, disconnect and late-thread cleanup")
    }
}
