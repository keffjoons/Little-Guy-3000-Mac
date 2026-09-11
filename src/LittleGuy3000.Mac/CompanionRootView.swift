import AppKit
import SwiftUI
import UniformTypeIdentifiers

struct CompanionRootView: View {
    @State var session: CompanionSession
    let speech: SpeechController
    let quick: QuickCompanion
    let enableScreen: () -> Void
    let enableVoice: () -> Void
    let capture: () -> Void
    let loadImage: (URL) -> Void
    let pasteImage: () -> Void
    let cancelCapture: () -> Void
    let chooseCodex: () -> Void
    let stopSpeech: () -> Void
    let previewSpeech: () -> Void
    @FocusState private var composerFocused: Bool
    @State private var importingImage = false

    var body: some View {
        NavigationSplitView {
            sidebar
                .navigationSplitViewColumnWidth(min: 195, ideal: 210, max: 250)
        } detail: {
            VStack(spacing: 0) {
                header
                Divider()
                conversation
                composer
            }
            .background(Color(nsColor: .textBackgroundColor))
        }
        .frame(minWidth: 780, minHeight: 580)
        .sheet(isPresented: $session.settingsVisible) {
            CompanionSettings(session: session, speech: speech, quick: quick, enableScreen: enableScreen, enableVoice: enableVoice, chooseCodex: chooseCodex, stopSpeech: stopSpeech, previewSpeech: previewSpeech)
        }
        .onAppear { composerFocused = true }
        .fileImporter(isPresented: $importingImage, allowedContentTypes: [.image]) { result in
            switch result {
            case .success(let url): loadImage(url)
            case .failure(let error): session.error = error.localizedDescription
            }
        }
    }

    private var sidebar: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 8) {
                MascotView(thinking: false, reducedMotion: session.reducedMotion).frame(width: 34, height: 34)
                Text("Little Guy").font(.system(size: 17, weight: .semibold))
            }
            .padding(.horizontal, 16).padding(.top, 17).padding(.bottom, 22)

            Text("YOUR COMPANION").font(.system(size: 10, weight: .semibold)).foregroundStyle(.secondary)
                .padding(.horizontal, 22).padding(.bottom, 8)
            VStack(spacing: 4) {
                ForEach(CompanionMode.allCases) { mode in
                    Button {
                        session.selectMode(mode); composerFocused = true
                    } label: {
                        HStack(spacing: 10) {
                            Image(systemName: mode.symbol).font(.system(size: 15)).frame(width: 20)
                            Text(mode.rawValue).font(.system(size: 13, weight: session.mode == mode ? .medium : .regular))
                            Spacer(minLength: 0)
                        }
                        .padding(.horizontal, 12).padding(.vertical, 10)
                        .foregroundStyle(session.mode == mode ? Color.accentColor : Color.primary)
                        .background(session.mode == mode ? Color.accentColor.opacity(0.12) : .clear, in: RoundedRectangle(cornerRadius: 8))
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain).disabled(session.busy || session.isCapturing)
                    .accessibilityAddTraits(session.mode == mode ? .isSelected : [])
                }
            }.padding(.horizontal, 10)
            Spacer()
            VStack(alignment: .leading, spacing: 12) {
                HStack(spacing: 7) {
                    Circle().fill(session.connection == .ready ? Color.green : Color.secondary).frame(width: 6, height: 6)
                    Text(session.connectionLabel).font(.system(size: 11)).foregroundStyle(.secondary)
                        .accessibilityIdentifier("connectionStatus")
                }
                Button { session.settingsVisible = true } label: {
                    Label("Settings", systemImage: "gearshape").font(.system(size: 13))
                }.buttonStyle(.plain).keyboardShortcut(",", modifiers: .command)
            }.padding(20)
        }
        .background(SidebarMaterial())
    }

    private var header: some View {
        HStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 3) {
                Text(session.mode.rawValue).font(.system(size: 16, weight: .semibold))
                Text(session.busy ? "Little Guy is thinking…" : "Here when you need a hand")
                    .font(.system(size: 11)).foregroundStyle(.secondary)
            }
            Spacer()
            Menu {
                ForEach(session.models) { model in
                    Button {
                        session.selectModel(model.id)
                    } label: {
                        if model.id == session.modelID { Label(model.name, systemImage: "checkmark") }
                        else { Text(model.name) }
                    }
                }
            } label: {
                Text(session.selectedModelName).font(.system(size: 11)).lineLimit(1)
            }
            .menuStyle(.borderlessButton).fixedSize().disabled(session.busy || session.models.isEmpty)
            .accessibilityLabel("Model")
            Button {
                stopSpeech(); session.newConversation(); composerFocused = true
            } label: { Image(systemName: "square.and.pencil").font(.system(size: 16)) }
                .buttonStyle(.borderless).help("New conversation (⌘N)").accessibilityLabel("New conversation")
                .keyboardShortcut("n", modifiers: .command).disabled(session.busy || session.isCapturing)
        }.padding(.horizontal, 24).padding(.vertical, 15)
    }

    @ViewBuilder private var conversation: some View {
        if session.messages.isEmpty {
            VStack(spacing: 18) {
                Spacer(minLength: 24)
                MascotView(thinking: false, reducedMotion: session.reducedMotion).frame(width: 86, height: 86)
                VStack(spacing: 10) {
                    Text(session.mode.title).font(.system(size: 25, weight: .semibold)).multilineTextAlignment(.center)
                    Text(session.mode.subtitle).font(.system(size: 13)).foregroundStyle(.secondary)
                        .multilineTextAlignment(.center).lineSpacing(3).frame(maxWidth: 370)
                }
                if session.mode.requiresImage {
                    Button(action: capture) { Label("Choose a window", systemImage: "macwindow.badge.plus") }
                        .buttonStyle(.bordered).controlSize(.large).disabled(session.isCapturing)
                } else {
                    HStack(spacing: 10) {
                        suggestion("Explain a window", icon: "macwindow") { session.selectMode(.explain); capture() }
                        suggestion("Help me get started", icon: "list.bullet") { session.selectMode(.walkthrough); composerFocused = true }
                    }.padding(.top, 4)
                }
                Spacer(minLength: 24)
            }.frame(maxWidth: .infinity, maxHeight: .infinity).padding(.horizontal, 24)
        } else {
            ScrollViewReader { proxy in
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 28) {
                        ForEach(session.messages) { message in
                            MessageView(message: message, isStreaming: session.busy && message.id == session.messages.last?.id)
                                .id(message.id)
                        }
                        Color.clear.frame(height: 1).id("bottom")
                    }.padding(28).frame(maxWidth: 760).frame(maxWidth: .infinity)
                }
                .onChange(of: session.messages.last?.text) { _, _ in proxy.scrollTo("bottom", anchor: .bottom) }
                .onChange(of: session.messages.count) { _, _ in proxy.scrollTo("bottom", anchor: .bottom) }
            }
        }
    }

    private func suggestion(_ title: String, icon: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Label(title, systemImage: icon).font(.system(size: 12)).padding(.horizontal, 5).padding(.vertical, 6)
        }.buttonStyle(.bordered).disabled(session.isCapturing)
    }

    private var composer: some View {
        VStack(alignment: .leading, spacing: 10) {
            if session.connection != .ready && session.connection != .connecting {
                HStack {
                    Image(systemName: "person.crop.circle.badge.exclamationmark")
                    Text(session.connection == .disconnected ? "Reconnect to ask Little Guy." : "Sign in with ChatGPT to start a conversation.")
                        .font(.system(size: 12))
                    Spacer()
                    Button(session.connection == .disconnected ? "Reconnect" : "Sign in") {
                        Task {
                            if session.connection == .disconnected { await session.reconnect() }
                            else if let url = await session.signIn() { NSWorkspace.shared.open(url) }
                        }
                    }.disabled(session.connection == .signingIn)
                }.padding(12).background(.quaternary.opacity(0.5), in: RoundedRectangle(cornerRadius: 10))
            }
            if let error = session.error { feedback(error, isError: true) }
            else if let notice = session.notice { feedback(notice, isError: false) }

            VStack(alignment: .leading, spacing: 8) {
                if let data = session.imageData, let image = NSImage(data: data) {
                    HStack(spacing: 10) {
                        Image(nsImage: image).resizable().scaledToFit().frame(width: 76, height: 52)
                            .background(.quaternary, in: RoundedRectangle(cornerRadius: 6))
                        VStack(alignment: .leading, spacing: 3) {
                            Text(session.imageName).font(.system(size: 12, weight: .medium)).lineLimit(1)
                            Text("Sent with your next question").font(.system(size: 10)).foregroundStyle(.secondary)
                        }
                        Spacer()
                        Button { session.imageData = nil } label: { Image(systemName: "xmark.circle.fill").foregroundStyle(.secondary) }
                            .buttonStyle(.plain).accessibilityLabel("Remove attachment")
                    }.padding(8).background(.quaternary.opacity(0.5), in: RoundedRectangle(cornerRadius: 8))
                }
                ZStack(alignment: .topLeading) {
                    if session.draft.isEmpty {
                        Text(session.mode.placeholder).foregroundStyle(.tertiary).padding(.leading, 5).padding(.top, 6).allowsHitTesting(false)
                    }
                    TextEditor(text: $session.draft).font(.system(size: 14)).scrollContentBackground(.hidden)
                        .frame(minHeight: 55, maxHeight: 110).focused($composerFocused)
                        .accessibilityLabel("Your question").accessibilityIdentifier("questionInput")
                }
                HStack(spacing: 12) {
                    Menu {
                        Button("Choose a window…", systemImage: "macwindow", action: capture)
                        Button("Choose an image…", systemImage: "photo") { importingImage = true }
                        Button("Paste image", systemImage: "doc.on.clipboard", action: pasteImage)
                            .keyboardShortcut("v", modifiers: [.command, .shift])
                    } label: { Image(systemName: "plus").font(.system(size: 16)) }
                    .menuStyle(.borderlessButton).fixedSize().disabled(session.busy || session.isCapturing)
                    .help("Attach a window or image").accessibilityLabel("Add attachment")
                    if session.isCapturing {
                        ProgressView().controlSize(.small)
                        Text("Choose a window in the macOS picker…").font(.system(size: 11)).foregroundStyle(.secondary)
                        Button("Cancel", action: cancelCapture).buttonStyle(.plain).font(.system(size: 11))
                    } else {
                        Text(session.mode.requiresImage && session.imageData == nil ? "Attach a window or image to continue" : "⌘Return to send")
                            .font(.system(size: 10)).foregroundStyle(.tertiary)
                    }
                    Spacer()
                    if session.busy {
                        Button { Task { await session.cancel() } } label: {
                            Image(systemName: "stop.fill").frame(width: 22, height: 22)
                        }.buttonStyle(.bordered).help("Stop answer").accessibilityLabel("Stop answer")
                    } else {
                        Button { Task { stopSpeech(); await session.send() } } label: {
                            Image(systemName: "arrow.up").font(.system(size: 14, weight: .semibold)).frame(width: 22, height: 22)
                        }.buttonStyle(.borderedProminent).clipShape(RoundedRectangle(cornerRadius: 9))
                            .keyboardShortcut(.return, modifiers: .command).disabled(!session.canSend)
                            .help("Send question").accessibilityLabel("Send question")
                    }
                }
            }
            .padding(12)
            .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 14))
            .overlay(RoundedRectangle(cornerRadius: 14).strokeBorder(Color.primary.opacity(0.09)))
            HStack {
                Text("Only what you choose to share. You stay in control.").font(.system(size: 10)).foregroundStyle(.tertiary)
                Spacer()
                if !speech.status.isEmpty {
                    Text(speech.status).font(.system(size: 10)).foregroundStyle(.secondary)
                    Button("Stop speech", action: stopSpeech).buttonStyle(.plain).font(.system(size: 10)).foregroundStyle(.secondary)
                }
            }.padding(.horizontal, 3)
        }.padding(.horizontal, 24).padding(.top, 10).padding(.bottom, 18)
    }

    private func feedback(_ text: String, isError: Bool) -> some View {
        HStack(alignment: .top, spacing: 8) {
            Image(systemName: isError ? "exclamationmark.circle" : "info.circle")
            Text(text).font(.system(size: 12)).textSelection(.enabled)
            Spacer()
            Button { session.error = nil; session.notice = nil } label: { Image(systemName: "xmark") }
                .buttonStyle(.plain).accessibilityLabel("Dismiss message")
        }.foregroundStyle(isError ? Color.orange : Color.secondary).padding(.horizontal, 4)
    }
}

private struct MessageView: View {
    let message: ConversationMessage
    let isStreaming: Bool
    @State private var copied = false

    var body: some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack(spacing: 8) {
                Image(systemName: message.role == .user ? "person.crop.circle.fill" : "face.smiling")
                    .foregroundStyle(message.role == .user ? Color.secondary : Color.accentColor)
                Text(message.role == .user ? "You" : "Little Guy").font(.system(size: 12, weight: .semibold))
                if message.hasImage { Label("Image attached", systemImage: "photo").font(.system(size: 10)).foregroundStyle(.secondary) }
                Spacer()
                if message.role == .assistant && !isStreaming && !message.interrupted {
                    Button {
                        NSPasteboard.general.clearContents(); NSPasteboard.general.setString(message.text, forType: .string)
                        copied = true
                    } label: { Image(systemName: copied ? "checkmark" : "doc.on.doc") }
                    .buttonStyle(.plain).foregroundStyle(.secondary).help(copied ? "Copied" : "Copy answer")
                    .accessibilityLabel(copied ? "Answer copied" : "Copy answer")
                }
            }
            if message.text.isEmpty && isStreaming {
                HStack(spacing: 8) { ProgressView().controlSize(.small); Text("Thinking…").font(.system(size: 13)).foregroundStyle(.secondary) }
                    .padding(.vertical, 4)
            } else {
                Text(message.text).font(.system(size: 14)).lineSpacing(5).textSelection(.enabled)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .accessibilityIdentifier(message.role == .assistant ? "assistantAnswer" : "userMessage")
            }
            if message.interrupted { Text("Not completed").font(.system(size: 10)).foregroundStyle(.secondary) }
        }
    }
}

private struct CompanionSettings: View {
    @Environment(\.dismiss) private var dismiss
    @Bindable var session: CompanionSession
    let speech: SpeechController
    @Bindable var quick: QuickCompanion
    let enableScreen: () -> Void
    let enableVoice: () -> Void
    let chooseCodex: () -> Void
    let stopSpeech: () -> Void
    let previewSpeech: () -> Void

    var body: some View {
        VStack(spacing: 0) {
            HStack { Text("Settings").font(.title2.weight(.semibold)); Spacer(); Button("Done") { dismiss() }.keyboardShortcut(.defaultAction) }
                .padding(24)
            Form {
                Section("Account") {
                    LabeledContent("ChatGPT", value: session.connectionLabel)
                    HStack {
                        Button("Reconnect") { Task { await session.reconnect() } }.disabled(session.busy || session.connection == .connecting)
                        if session.connection == .signedOut {
                            Button("Sign in") { Task { if let url = await session.signIn() { NSWorkspace.shared.open(url) } } }
                        }
                        Spacer()
                        Button("Choose Codex…", action: chooseCodex).disabled(session.busy)
                    }
                }
                Section("Companion") {
                    Toggle("Show floating companion", isOn: $session.showCompanion)
                    Toggle("Follow the pointer", isOn: $session.followPointer).disabled(!session.showCompanion)
                    Toggle("Reduce motion", isOn: $session.reducedMotion)
                    LabeledContent("Show Little Guy", value: session.shortcutAvailable ? "Control–Option–Space" : "Shortcut unavailable; use the menu bar")
                }
                Section("Voice") {
                    Button("Enable microphone…", action: enableVoice)
                    Toggle("Read answers aloud", isOn: $session.speakAnswers).onChange(of: session.speakAnswers) { _, enabled in if !enabled { stopSpeech() } }
                    HStack { Button("Preview voice", action: previewSpeech); Button("Stop speech", action: stopSpeech) }
                    if !speech.status.isEmpty { Text(speech.status).font(.caption).foregroundStyle(.secondary).accessibilityIdentifier("speechStatus") }
                    Text("Start Live voice in the popup for an ongoing conversation. Microphone audio is sent through your ChatGPT connection until you mute or end the call. Read answers aloud controls narration of typed answers.")
                        .font(.caption).foregroundStyle(.secondary)
                }
                Section("Your privacy") {
                    Toggle("Capture the pointed window for quick questions", isOn: $quick.screenEnabled)
                    Button("Enable Screen Recording…", action: enableScreen)
                    Button("Enable window controls…") { WindowActions.requestAccess() }
                    Text("Live voice can ask Astra to inspect and operate the selected window. Screen context must be on. Requested Spotify playback can run directly; other control actions show an approval in the popup. Screenshots and live microphone audio go through your ChatGPT connection. End voice stops the microphone and pending actions.")
                        .font(.caption).foregroundStyle(.secondary)
                }
            }.formStyle(.grouped)
        }.frame(width: 500, height: 720)
    }
}

struct MascotView: NSViewRepresentable {
    var thinking: Bool
    var reducedMotion: Bool
    func makeNSView(context: Context) -> CompanionView { CompanionView(frame: .zero) }
    func updateNSView(_ view: CompanionView, context: Context) { view.thinking = thinking; view.reduceMotion = reducedMotion }
}

private struct SidebarMaterial: NSViewRepresentable {
    func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView(); view.material = .sidebar; view.blendingMode = .behindWindow; view.state = .followsWindowActiveState; return view
    }
    func updateNSView(_ view: NSVisualEffectView, context: Context) {}
}
