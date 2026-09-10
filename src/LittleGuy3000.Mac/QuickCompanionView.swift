import AppKit
import SwiftUI

final class CompanionPanel: NSPanel {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}

struct QuickCompanionView: View {
    @State var quick: QuickCompanion
    let speech: SpeechController
    let settings: () -> Void
    let details: () -> Void
    let dismiss: () -> Void
    let enableScreen: () -> Void
    let stop: () -> Void

    var body: some View {
        @Bindable var session = quick.session
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 8) {
                Image(systemName: "sparkle").foregroundStyle(.tint)
                Text("Little Guy").font(.headline)
                Spacer()
                Button(action: settings) { Image(systemName: "gearshape") }.help("Settings")
                Button(action: dismiss) { Image(systemName: "xmark") }.help("Dismiss")
            }.buttonStyle(.plain)
            if let answer = session.messages.last(where: { $0.role == .assistant }) {
                ScrollView {
                    Text(answer.text.isEmpty ? "Thinking…" : answer.text)
                        .font(.system(size: 14)).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading)
                }.frame(maxHeight: 140)
                HStack {
                    Button("Full answer", action: details)
                    Button("Copy") { NSPasteboard.general.clearContents(); NSPasteboard.general.setString(answer.text, forType: .string) }
                    Spacer()
                    Button("New") { session.newConversation() }.disabled(quick.active || session.busy)
                }.font(.caption).buttonStyle(.plain).foregroundStyle(.secondary)
            } else {
                Text("What do you need a hand with?").font(.system(size: 16, weight: .medium))
                Text("Point at a window, hold ⌃⌥Space, and speak. Release to ask.")
                    .font(.system(size: 12)).foregroundStyle(.secondary)
            }
            if session.connection != .ready {
                Button(session.connectionLabel, action: settings).font(.caption)
            }
            if let error = session.error { Text(error).font(.caption).foregroundStyle(.orange).fixedSize(horizontal: false, vertical: true) }
            if let notice = session.notice { Text(notice).font(.caption).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true) }
            HStack(alignment: .bottom, spacing: 8) {
                TextField("Or type a question…", text: $session.draft, axis: .vertical)
                    .lineLimit(1...3).textFieldStyle(.plain).onSubmit { quick.send() }
                    .disabled(quick.active || session.busy)
                    .accessibilityIdentifier("quickQuestion")
                Button { quick.send() } label: { Image(systemName: "arrow.up.circle.fill").font(.title2) }
                    .buttonStyle(.plain).disabled(!session.canSend || quick.active)
                    .keyboardShortcut(.return, modifiers: .command).help("Send question")
            }.padding(11).background(.quaternary.opacity(0.5), in: RoundedRectangle(cornerRadius: 12))
            HStack {
                if quick.active || session.busy {
                    if quick.phase == .listening { Button("Done") { quick.finishVoice() } }
                    Button("Stop", action: stop)
                } else {
                    Button { quick.startVoice() } label: { Label("Talk", systemImage: "mic.fill") }
                        .disabled(session.connection != .ready)
                }
                Text(quick.status).font(.system(size: 10)).foregroundStyle(.secondary)
                Spacer(minLength: 0)
            }.controlSize(.small)
            HStack {
                Toggle(isOn: $quick.screenEnabled) { Image(systemName: "macwindow") }
                    .toggleStyle(.switch).controlSize(.mini).help("Include a fresh image of the pointed window")
                    .disabled(quick.active || session.busy)
                Text(quick.screenEnabled ? (quick.target?.name ?? "Point at a window") : "Voice and text only")
                    .lineLimit(1).font(.system(size: 10)).foregroundStyle(.secondary)
                Spacer()
                Text("Astra · Low").font(.system(size: 10)).foregroundStyle(.secondary)
            }
            if quick.screenEnabled && !quick.screenPermitted {
                Button("Enable Screen Recording…", action: enableScreen).font(.caption)
            }
            if !speech.status.isEmpty {
                HStack { Text(speech.status).font(.caption); Button("Stop voice", action: speech.stop).font(.caption) }.foregroundStyle(.secondary)
            }
        }
        .padding(18).frame(width: 360)
        .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 22))
        .overlay(RoundedRectangle(cornerRadius: 22).stroke(.white.opacity(0.15), lineWidth: 1))
        .padding(8)
    }
}
