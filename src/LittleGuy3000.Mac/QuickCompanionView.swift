import AppKit
import SwiftUI

final class CompanionPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

struct QuickCompanionView: View {
    let transcript: QuickTranscript
    var indicator: ListeningIndicator = .idle
    var reduceMotion = false

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            if !transcript.isEmpty { message }
            ListeningFace(indicator: indicator, reduceMotion: reduceMotion)
                .frame(width: 72, height: 72)
        }
        .padding(6)
        .accessibilityElement(children: .contain)
    }

    private var message: some View {
        VStack(alignment: .leading, spacing: 7) {
            if !transcript.user.isEmpty {
                Text(transcript.user)
                    .font(.system(size: 12)).foregroundStyle(.secondary)
                    .lineLimit(3).fixedSize(horizontal: false, vertical: true)
            }
            if !transcript.agent.isEmpty {
                Text(transcript.agent)
                    .font(.system(size: 14)).foregroundStyle(.primary)
                    .lineLimit(10).fixedSize(horizontal: false, vertical: true)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(13).frame(width: 320)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 15, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 15, style: .continuous).stroke(.white.opacity(0.08), lineWidth: 0.5))
    }
}

private struct ListeningFace: NSViewRepresentable {
    let indicator: ListeningIndicator
    let reduceMotion: Bool

    func makeNSView(context: Context) -> CompanionView { CompanionView(frame: .zero) }
    func updateNSView(_ view: CompanionView, context: Context) {
        view.listening = indicator == .listening
        view.thinking = indicator == .preparing
        view.reduceMotion = reduceMotion
        view.setAccessibilityElement(true)
        view.setAccessibilityRole(.image)
        view.setAccessibilityLabel(indicator == .listening ? "Little Guy is listening" :
            (indicator == .preparing ? "Little Guy is connecting" : "Little Guy, microphone muted"))
        view.needsDisplay = true
    }
}
