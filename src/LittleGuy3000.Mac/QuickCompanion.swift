import Foundation
import Observation

@MainActor @Observable
final class QuickCompanion {
    enum Phase: Equatable { case idle, preparing, listening, transcribing, capturing }
    private(set) var phase: Phase = .idle
    private(set) var target: PointerTarget?
    var screenEnabled: Bool { didSet { defaults.set(screenEnabled, forKey: "quickScreenEnabled") } }
    var screenPermitted = false
    let session: CompanionSession
    @ObservationIgnored private let voice: VoiceTranscribing
    @ObservationIgnored private let capture: (PointerTarget) async throws -> Data
    @ObservationIgnored private let defaults: UserDefaults
    @ObservationIgnored private var generation = UUID()
    @ObservationIgnored private var work: Task<Void, Never>?
    @ObservationIgnored private var hold: Task<Void, Never>?
    @ObservationIgnored private var deadline: Task<Void, Never>?
    @ObservationIgnored private var pressed = false
    @ObservationIgnored var stopSpeech: (() -> Void)?
    @ObservationIgnored var beginLiveVoice: (() -> Void)?
    @ObservationIgnored var endLiveInput: (() -> Void)?

    init(session: CompanionSession, voice: VoiceTranscribing, defaults: UserDefaults = .standard,
         capture: @escaping (PointerTarget) async throws -> Data) {
        self.session = session; self.voice = voice; self.defaults = defaults; self.capture = capture
        screenEnabled = defaults.object(forKey: "quickScreenEnabled") == nil || defaults.bool(forKey: "quickScreenEnabled")
        voice.update = { [weak self] text in
            guard let self, self.phase == .listening || self.phase == .transcribing else { return }
            self.session.draft = text
        }
        voice.completed = { [weak self] result in
            guard let self, self.phase == .listening || self.phase == .transcribing else { return }
            self.phase = .idle
            switch result {
            case .success(let text):
                self.session.draft = text
                if text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    self.session.error = "I didn’t hear anything. Hold the shortcut and speak, or type below."
                } else { self.send() }
            case .failure(let error): self.session.error = error.localizedDescription
            }
        }
    }

    var active: Bool { phase != .idle }
    var shortcutHeld: Bool { pressed }
    var status: String {
        switch phase {
        case .preparing: "Preparing microphone…"
        case .listening: "Listening · release shortcut or click Done"
        case .transcribing: "Finishing your words…"
        case .capturing: "Looking at \(target?.name ?? "your window")…"
        case .idle: session.busy ? "Thinking…" : "Hold ⌃⌥Space to talk"
        }
    }

    func point(at value: PointerTarget?) {
        guard !active, !session.busy else { return }
        target = value
    }

    func keyDown() {
        guard !pressed, !active, !session.busy else { return }
        pressed = true
        hold = Task { [weak self] in
            do { try await Task.sleep(for: .milliseconds(300)) } catch { return }
            guard let self, self.pressed else { return }
            if let begin = self.beginLiveVoice { begin() } else { self.startVoice() }
        }
    }

    func keyUp() {
        pressed = false; hold?.cancel(); hold = nil
        endLiveInput?()
        if phase == .preparing { cancel(); session.notice = "Microphone setup finished? Hold the shortcut again to speak." }
        else if phase == .listening { finishVoice() }
    }

    func startVoice() {
        guard !active, !session.busy else { return }
        guard session.connection == .ready else { session.error = "Connect Little Guy in Settings first."; return }
        guard session.draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            session.notice = "Send or clear the question below before recording a new one."; return
        }
        stopSpeech?(); session.error = nil; session.notice = nil
        phase = .preparing
        let token = UUID(); generation = token
        work = Task { [weak self] in
            guard let self else { return }
            do {
                try await self.voice.start()
                guard self.generation == token else { return }
                self.phase = .listening
            } catch {
                guard self.generation == token else { return }
                self.phase = .idle; self.session.error = error.localizedDescription
            }
        }
    }

    func finishVoice() {
        guard phase == .listening else { return }
        phase = .transcribing; voice.finish()
    }

    func send() {
        guard !active, !session.busy else { return }
        session.setCompact(true)
        guard session.canSend else { return }
        guard session.quickModelAvailable else { session.error = "Astra with Low reasoning is unavailable. Reconnect in Settings."; return }
        stopSpeech?(); session.error = nil; session.notice = nil
        // Every quick turn captures afresh. Never silently reuse a previous attachment.
        session.imageData = nil
        let token = UUID(); generation = token
        let selected = target, includeScreen = screenEnabled
        if includeScreen {
            guard screenPermitted else { session.error = "Enable Screen Recording below, then ask again."; return }
            guard selected != nil else { session.error = "Point at the window you want help with and press ⌃⌥Space."; return }
            phase = .capturing; session.isCapturing = true
            deadline = Task { [weak self] in
                do { try await Task.sleep(for: .seconds(15)) } catch { return }
                guard let self, self.generation == token, self.phase == .capturing else { return }
                self.cancel(); self.session.error = "The window capture timed out. Point at it and try again."
            }
        }
        work = Task { [weak self] in
            guard let self else { return }
            do {
                if includeScreen, let selected {
                    let data = try await self.capture(selected)
                    guard self.generation == token else { return }
                    self.session.imageData = data; self.session.imageName = selected.name
                }
                guard self.generation == token else { return }
                self.deadline?.cancel(); self.phase = .idle; self.session.isCapturing = false
                await self.session.send()
            } catch {
                guard self.generation == token else { return }
                self.deadline?.cancel(); self.phase = .idle; self.session.isCapturing = false
                self.session.error = error.localizedDescription
            }
        }
    }

    func cancel() {
        generation = UUID(); pressed = false
        endLiveInput?()
        hold?.cancel(); work?.cancel(); deadline?.cancel(); voice.cancel()
        if phase == .capturing { session.isCapturing = false }
        phase = .idle
    }
}
