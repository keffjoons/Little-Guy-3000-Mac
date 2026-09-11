import Foundation

@main struct TranscriptTests {
    static func main() {
        var display = TranscriptVisibility()
        let first = QuickTranscript(user: "Play some morning jazz", agent: "Playing Good Morning Jazz.")
        precondition(display.opacity(for: first, busy: false, at: 0) == 0, "Launch stays invisible")
        display.show(at: 0)
        precondition(display.opacity(for: QuickTranscript(), busy: true, at: 0) == 0, "Never show empty chrome")
        precondition(display.opacity(for: first, busy: false, at: 1) == 1)
        precondition(display.opacity(for: first, busy: true, at: 20) == 1, "Keep the reply during speech")
        precondition(display.opacity(for: first, busy: false, at: 26.125) == 0.5)
        precondition(display.opacity(for: first, busy: false, at: 27) == 0, "Fade after six seconds")
        let second = QuickTranscript(user: "Pause it", agent: "Paused.")
        precondition(display.opacity(for: second, busy: false, at: 30) == 1, "New reply returns after auto-hide")
        display.dismiss()
        precondition(display.opacity(for: first, busy: false, at: 31) == 0, "Late messages cannot undo dismissal")
        display.show(at: 40)
        precondition(display.opacity(for: second, busy: false, at: 40) == 1, "Shortcut recalls the latest exchange")
        print("PASS: empty launch, speech visibility, timed fade, new replies, explicit dismissal and shortcut recall")
    }
}
