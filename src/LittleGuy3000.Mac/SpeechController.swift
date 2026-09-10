import AppKit
import Observation

@MainActor @Observable
final class SpeechController {
    private(set) var status = ""
    private(set) var speaking = false
    @ObservationIgnored private let backend: CodexVoiceBackend
    @ObservationIgnored private let player: VoicePlaying
    @ObservationIgnored private var threadID: String?
    @ObservationIgnored private var generation = UUID()
    @ObservationIgnored private var work: Task<Void, Never>?
    @ObservationIgnored private var timeout: Task<Void, Never>?
    @ObservationIgnored private var text = ""
    @ObservationIgnored private var submitted = false
    @ObservationIgnored private var starting = false

    init(backend: CodexVoiceBackend, player: VoicePlaying? = nil) {
        self.backend = backend; self.player = player ?? CodexVoicePlayer()
        backend.voiceNotification = { [weak self] method, body in self?.receive(method, body) }
    }

    func speak(_ text: String, in host: NSView) {
        stop()
        guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
        guard text.count <= 20_000 else { status = "This answer is too long to read aloud. Ask for a shorter version."; return }
        self.text = text; speaking = true; status = "Connecting Codex voice…"
        let token = generation
        player.event = { [weak self] body in
            guard let self, self.generation == token, self.speaking else { return }
            self.playerEvent(body, token: token)
        }
        timeout = Task { [weak self] in
            do { try await Task.sleep(for: .seconds(40)) } catch { return }
            guard let self, self.generation == token else { return }
            self.fail("Codex voice timed out. Check the connection and try again.")
        }
        work = Task { [weak self] in
            guard let self else { return }
            do {
                let response = try await self.backend.voiceRequest("thread/start", Self.threadParameters)
                guard let id = (response["thread"] as? [String: Any])?["id"] as? String else {
                    throw CompanionError.message("Codex did not create a voice session.")
                }
                guard self.generation == token else { self.closeThread(id); return }
                self.threadID = id
                self.player.prepare(in: host)
            } catch {
                guard self.generation == token else { return }
                self.fail(error.localizedDescription)
            }
        }
    }

    func stop() {
        generation = UUID(); work?.cancel(); timeout?.cancel()
        player.event = nil; player.stop()
        let id = threadID; threadID = nil
        text = ""; submitted = false; starting = false; speaking = false; status = ""
        if let id { closeThread(id) }
    }

    private func closeThread(_ id: String) {
        let backend = backend
        Task {
            _ = try? await backend.voiceRequest("thread/realtime/stop", ["threadId": id])
            _ = try? await backend.voiceRequest("thread/unsubscribe", ["threadId": id])
        }
    }

    private func fail(_ message: String) { stop(); status = message }

    private func playerEvent(_ body: [String: Any], token: UUID) {
        if let error = body["error"] as? String { fail(error); return }
        if let sdp = body["sdp"] as? String, let id = threadID, !starting {
            starting = true
            work = Task { [weak self] in
                guard let self else { return }
                do {
                    _ = try await self.backend.voiceRequest("thread/realtime/start", Self.startParameters(id: id, sdp: sdp))
                    if self.generation != token { self.closeThread(id) }
                } catch {
                    guard self.generation == token else { return }; self.fail(error.localizedDescription)
                }
            }
        }
        if body["ready"] as? Bool == true, let id = threadID, !submitted {
            submitted = true; status = "Preparing Codex voice…"
            let words = text
            work = Task { [weak self] in
                guard let self else { return }
                do { _ = try await self.backend.voiceRequest("thread/realtime/appendSpeech", ["threadId": id, "text": words]) }
                catch { guard self.generation == token else { return }; self.fail(error.localizedDescription) }
            }
        }
        if body["audible"] as? Bool == true {
            status = "Speaking · Codex voice"; timeout?.cancel()
            timeout = Task { [weak self] in
                do { try await Task.sleep(for: .seconds(180)) } catch { return }
                guard let self, self.generation == token else { return }
                self.fail("Voice stopped after its playback limit. The full answer is still available.")
            }
        }
        if body["finished"] as? Bool == true { stop(); status = "Finished speaking · Codex voice" }
    }

    private func receive(_ method: String, _ body: [String: Any]) {
        if method == "connection/closed" { if speaking { fail("Codex reconnected. Try the voice again.") }; return }
        guard speaking, body["threadId"] as? String == threadID else { return }
        switch method {
        case "thread/realtime/sdp":
            if let sdp = body["sdp"] as? String { player.answer(sdp) }
        case "thread/realtime/transcript/done":
            if body["role"] as? String == "assistant" { player.finishWhenQuiet() }
        case "thread/realtime/error": fail("Codex voice could not connect. Reconnect in Settings and try again.")
        case "thread/realtime/closed": fail("Codex voice disconnected. Try again.")
        default: break
        }
    }

    static let threadParameters: [String: Any] = [
        "ephemeral": true, "environments": [], "selectedCapabilityRoots": [], "dynamicTools": [],
        "approvalPolicy": "never", "sandbox": "read-only", "model": "gpt-6-astra",
        "developerInstructions": "This session is used only for speech output. Never run tools, perform actions, or generate independent answers."
    ]
    static func startParameters(id: String, sdp: String) -> [String: Any] {
        ["threadId": id, "outputModality": "audio", "version": "v3", "transport": ["type": "webrtc", "sdp": sdp],
         "includeStartupContext": false, "clientManagedHandoffs": true, "delegationAckFiller": false,
         "prompt": "Read the supplied speakable text aloud faithfully. Do not greet the user or speak until text is supplied. Do not add commentary, questions, or new answers. Never call tools or delegate work."]
    }
}
