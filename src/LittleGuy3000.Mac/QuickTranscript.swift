import Foundation

struct QuickTranscript: Equatable {
    var user = ""
    var agent = ""
    var isEmpty: Bool { user.isEmpty && agent.isEmpty }
}

// Timing is independent of the voice connection: fading must not close a warm call.
struct TranscriptVisibility {
    private var requested = false
    private var previous = QuickTranscript()
    private var lastActivity: TimeInterval = 0

    mutating func show(at time: TimeInterval) { requested = true; lastActivity = time }
    mutating func dismiss() { requested = false }

    mutating func opacity(for transcript: QuickTranscript, busy: Bool, at time: TimeInterval) -> Double {
        guard requested, !transcript.isEmpty else { return 0 }
        if transcript != previous || busy { lastActivity = time }
        previous = transcript
        return max(0, min(1, 1 - (time - lastActivity - 6) / 0.25))
    }
}
