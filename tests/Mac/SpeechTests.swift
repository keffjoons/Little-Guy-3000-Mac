import AVFoundation
import Foundation

private final class AudioResult: @unchecked Sendable {
    private let lock = NSLock()
    private var frames: UInt64 = 0
    private var peak: Float = 0
    private var complete = false
    func append(_ buffer: AVAudioPCMBuffer) {
        lock.lock(); defer { lock.unlock() }
        frames += UInt64(buffer.frameLength)
        if let samples = buffer.floatChannelData {
            for index in 0..<Int(buffer.frameLength) { peak = max(peak, abs(samples[0][index])) }
        }
        if buffer.frameLength == 0 { complete = true }
    }
    var snapshot: (UInt64, Float, Bool) {
        lock.lock(); defer { lock.unlock() }; return (frames, peak, complete)
    }
}

@main struct SpeechTests {
    @MainActor static func main() {
        let result = AudioResult(), synthesizer = AVSpeechSynthesizer()
        synthesizer.write(SpeechController.utterance("Hi, I'm Little Guy. I'm here when you need a hand.")) { buffer in
            if let pcm = buffer as? AVAudioPCMBuffer { result.append(pcm) }
        }
        let deadline = Date().addingTimeInterval(25)
        while !result.snapshot.2 && Date() < deadline {
            RunLoop.current.run(until: Date().addingTimeInterval(0.05))
        }
        let (frames, peak, complete) = result.snapshot
        precondition(complete && frames > 0 && peak > 0.001, "Installed voice must generate non-silent audio")
        print("PASS: production voice selection generated \(frames) non-silent audio frames and completed")
    }
}
