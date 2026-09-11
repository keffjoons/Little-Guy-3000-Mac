import AppKit
import SwiftUI

final class CompanionPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

struct QuickCompanionView: View {
    let transcript: QuickTranscript

    var body: some View {
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
        .padding(6)
        .accessibilityElement(children: .contain)
    }
}
