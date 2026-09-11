import AppKit
import SwiftUI

final class CompanionPanel: NSPanel {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}

struct QuickCompanionView: View {
    @State var quick: QuickCompanion
    let speech: SpeechController
    let live: LiveConversation
    let startLive: () -> Void
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
            if live.active || !live.heardText.isEmpty || !live.replyText.isEmpty {
                if !live.heardText.isEmpty { Text(live.heardText).font(.caption).foregroundStyle(.secondary).lineLimit(3) }
                ScrollView { Text(live.replyText.isEmpty ? (live.muted ? "Hold ⌃⌥Space to talk" : "I'm listening…") : live.replyText).font(.system(size: 14)).frame(maxWidth: .infinity, alignment: .leading) }.frame(maxHeight: 140)
                if !live.active { Button("New conversation") { live.clear(); session.newConversation() }.font(.caption) }
            } else if let answer = session.messages.last(where: { $0.role == .assistant }) {
                ScrollView {
                    Text(answer.text.isEmpty ? "Thinking…" : answer.text)
                        .font(.system(size: 14)).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading)
                }.frame(maxHeight: 140)
                HStack {
                    Button("Full answer", action: details)
                    Button("Copy") { NSPasteboard.general.clearContents(); NSPasteboard.general.setString(answer.text, forType: .string) }
                    Spacer()
                    Button("New") { speech.stop(); session.newConversation() }.disabled(quick.active || session.busy)
                }.font(.caption).buttonStyle(.plain).foregroundStyle(.secondary)
            } else {
                Text("What do you need a hand with?").font(.system(size: 16, weight: .medium))
                Text("Point at a window. Hold ⌃⌥Space to talk, then release to mute. Replies play automatically.")
                    .font(.system(size: 12)).foregroundStyle(.secondary)
            }
            if session.connection != .ready {
                Button(session.connectionLabel, action: settings).font(.caption)
            }
            if let error = session.error { Text(error).font(.caption).foregroundStyle(.orange).fixedSize(horizontal: false, vertical: true) }
            if let notice = session.notice { Text(notice).font(.caption).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true) }
            if let error = live.error { Text(error).font(.caption).foregroundStyle(.orange) }
            HStack(alignment: .bottom, spacing: 8) {
                TextField("Or type a question…", text: $session.draft, axis: .vertical)
                    .lineLimit(1...3).textFieldStyle(.plain).onSubmit { send() }
                    .disabled(quick.active || session.busy || (live.active && !live.connected))
                    .accessibilityIdentifier("quickQuestion")
                Button { send() } label: { Image(systemName: "arrow.up.circle.fill").font(.title2) }
                    .buttonStyle(.plain).disabled(!session.canSend || quick.active || (live.active && !live.connected))
                    .keyboardShortcut(.return, modifiers: .command).help("Send question")
            }.padding(11).background(.quaternary.opacity(0.5), in: RoundedRectangle(cornerRadius: 12))
            HStack {
                if live.active {
                    Button("End voice") { live.stop() }
                } else if quick.active || session.busy {
                    if quick.phase == .listening { Button("Done") { quick.finishVoice() } }
                    Button("Stop", action: stop)
                } else {
                    Button(action: startLive) { Label("Connect voice", systemImage: "waveform") }
                        .disabled(session.connection != .ready)
                }
                Text(live.active ? live.status : "Hold ⌃⌥Space to talk").font(.system(size: 10)).foregroundStyle(.secondary)
                Spacer(minLength: 0)
            }.controlSize(.small)
            if !live.active { Text("Microphone audio is sent only while you hold ⌃⌥Space.").font(.system(size: 10)).foregroundStyle(.secondary) }
            HStack {
                Toggle(isOn: $quick.screenEnabled) { Image(systemName: "macwindow") }
                    .toggleStyle(.switch).controlSize(.mini).help("Include a fresh image of the pointed window")
                    .disabled(quick.active || session.busy)
                Text(quick.screenEnabled ? (quick.target?.name ?? "Point at a window") : "Voice and text only")
                    .lineLimit(1).font(.system(size: 10)).foregroundStyle(.secondary)
                Spacer()
                Text(live.active ? "Live · Astra tools" : "Astra · Low").font(.system(size: 10)).foregroundStyle(.secondary)
            }
            .onChange(of: quick.screenEnabled) { _, enabled in live.setContext(target: quick.target, enabled: enabled) }
            if quick.screenEnabled && !WindowActions.permitted { Button("Enable window controls…") { WindowActions.requestAccess() }.font(.caption) }
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

    private func send() {
        if live.active { live.send(quick.session.draft); quick.session.draft = "" }
        else { live.clear(); quick.send() }
    }
}
