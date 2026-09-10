import AVFoundation
import Speech

@MainActor
protocol VoiceTranscribing: AnyObject {
    var update: ((String) -> Void)? { get set }
    var completed: ((Result<String, Error>) -> Void)? { get set }
    func start() async throws
    func finish()
    func cancel()
}

@MainActor
final class VoiceInput: VoiceTranscribing {
    var update: ((String) -> Void)?
    var completed: ((Result<String, Error>) -> Void)?
    private let engine = AVAudioEngine()
    private var request: SFSpeechAudioBufferRecognitionRequest?
    private var recognition: SFSpeechRecognitionTask?
    private var recognizer: SFSpeechRecognizer?
    private var timer: Task<Void, Never>?
    private var generation = UUID()
    private var tapped = false
    private var ending = false
    private var finalText: String?

    static func authorize() async -> Bool {
        let microphone = await AVCaptureDevice.requestAccess(for: .audio)
        guard microphone else { return false }
        let status = await withCheckedContinuation { continuation in
            SFSpeechRecognizer.requestAuthorization { continuation.resume(returning: $0) }
        }
        return status == .authorized
    }

    func start() async throws {
        cancel()
        let token = generation
        guard await Self.authorize() else {
            throw CompanionError.message("Allow Little Guy microphone and speech access in System Settings → Privacy & Security.")
        }
        guard generation == token else { throw CancellationError() }
        let recognizer = SFSpeechRecognizer(locale: Locale(identifier: Locale.current.identifier))
            ?? SFSpeechRecognizer(locale: Locale(identifier: "en-GB"))
        guard let recognizer, recognizer.isAvailable, recognizer.supportsOnDeviceRecognition else {
            throw CompanionError.message("Local speech recognition isn’t available for your Mac’s language. Enable Dictation in System Settings → Keyboard to download speech support, then try again.")
        }
        self.recognizer = recognizer
        let request = SFSpeechAudioBufferRecognitionRequest()
        request.requiresOnDeviceRecognition = true; request.shouldReportPartialResults = true
        self.request = request
        recognition = recognizer.recognitionTask(with: request) { [weak self] result, error in
            let text = result?.bestTranscription.formattedString
            let final = result?.isFinal == true
            Task { @MainActor in
                guard let self, self.generation == token else { return }
                if let text { self.update?(text) }
                if final {
                    self.finalText = text ?? ""
                    if self.ending { self.deliver(.success(text ?? "")) }
                } else if let error {
                    self.deliver(.failure(CompanionError.message("Speech recognition stopped (\((error as NSError).code)). Try again or type your question.")))
                }
            }
        }
        let input = engine.inputNode, format = engine.inputNode.outputFormat(forBus: 0)
        guard format.sampleRate > 0, format.channelCount > 0 else {
            cancel(); throw CompanionError.message("No microphone is available. Connect one and try again.")
        }
        input.installTap(onBus: 0, bufferSize: 1024, format: format) { buffer, _ in request.append(buffer) }
        tapped = true
        do { engine.prepare(); try engine.start() }
        catch { cancel(); throw CompanionError.message("The microphone couldn’t start. Check your sound input in System Settings.") }
        timer = Task { [weak self] in
            do { try await Task.sleep(for: .seconds(55)) } catch { return }
            guard let self, self.generation == token else { return }
            self.deliver(.failure(CompanionError.message("Recording stopped after 55 seconds. Your words are kept below; press Send to ask.")))
        }
    }

    func finish() {
        guard request != nil, !ending else { return }
        ending = true; stopMicrophone(); request?.endAudio(); timer?.cancel()
        if let finalText { deliver(.success(finalText)); return }
        let token = generation
        timer = Task { [weak self] in
            do { try await Task.sleep(for: .seconds(10)) } catch { return }
            guard let self, self.generation == token else { return }
            self.deliver(.failure(CompanionError.message("Transcription didn’t finish. Review your words below and press Send, or try the microphone again.")))
        }
    }

    func cancel() {
        generation = UUID(); timer?.cancel(); timer = nil
        stopMicrophone(); request?.endAudio(); recognition?.cancel()
        recognition = nil; request = nil; recognizer = nil; ending = false; finalText = nil
    }

    private func stopMicrophone() {
        engine.stop()
        if tapped { engine.inputNode.removeTap(onBus: 0); tapped = false }
    }

    private func deliver(_ result: Result<String, Error>) {
        cancel(); completed?(result)
    }
}
