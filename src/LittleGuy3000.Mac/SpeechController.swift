import AVFoundation
import Observation

@MainActor @Observable
final class SpeechController: NSObject, AVSpeechSynthesizerDelegate {
    private(set) var status = ""
    @ObservationIgnored private let synthesizer = AVSpeechSynthesizer()
    @ObservationIgnored private var current: AVSpeechUtterance?

    override init() { super.init(); synthesizer.delegate = self }

    static func utterance(_ text: String) -> AVSpeechUtterance {
        let value = AVSpeechUtterance(string: text)
        value.voice = AVSpeechSynthesisVoice(language: Locale.current.language.languageCode?.identifier ?? "en")
        return value
    }

    func speak(_ text: String) {
        stop()
        let value = Self.utterance(text)
        current = value; status = "Preparing voice…"
        synthesizer.speak(value)
    }

    func stop() { current = nil; synthesizer.stopSpeaking(at: .immediate); status = "" }

    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didStart utterance: AVSpeechUtterance) {
        Task { @MainActor in
            guard self.current === utterance else { return }; self.status = "Speaking…"
        }
    }
    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didFinish utterance: AVSpeechUtterance) {
        Task { @MainActor in
            guard self.current === utterance else { return }; self.current = nil; self.status = "Finished speaking"
        }
    }
    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didCancel utterance: AVSpeechUtterance) {
        Task { @MainActor in
            guard self.current === utterance else { return }; self.current = nil; self.status = ""
        }
    }
}
