import AppKit
import AVFoundation
import Carbon
import SwiftUI
import UniformTypeIdentifiers

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    private let session = CompanionSession(transport: CodexConnection())
    private var window: NSWindow!
    private var companion: NSPanel!
    private var face: CompanionView!
    private var statusItem: NSStatusItem!
    private var hotKey: EventHotKeyRef?
    private var hotKeyHandler: EventHandlerRef?
    private var companionTimer: Timer?
    private var picker: ScreenCapturePicker?
    private var localMonitor: Any?
    private var hidden = false
    private var terminating = false
    private let speech = AVSpeechSynthesizer()

    func applicationDidFinishLaunching(_ notification: Notification) {
        buildWindow(); buildCompanion(); buildMenu(); registerHotKey()
        session.completedAnswer = { [weak self] answer in
            guard let self else { return }
            if self.session.speakAnswers { self.speak(answer) }
        }
        localMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            guard event.keyCode == 53 else { return event }
            MainActor.assumeIsolated { self?.cancelInteraction() }; return nil
        }
        showPanel()
        if !CommandLine.arguments.contains("--ui-test") { connect() }
    }

    private func buildWindow() {
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 980, height: 740),
                          styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.title = "Little Guy"; window.subtitle = "Your Mac companion"
        window.minSize = NSSize(width: 800, height: 620)
        window.delegate = self; window.isReleasedWhenClosed = false
        window.setFrameAutosaveName("LittleGuyMainWindow-v2"); window.center()
        window.contentView = NSHostingView(rootView: CompanionRootView(session: session,
            capture: { [weak self] in self?.capture() }, attachImage: { [weak self] in self?.attachImage() },
            cancelCapture: { [weak self] in self?.picker?.cancel() },
            chooseCodex: { [weak self] in self?.chooseCodex() }, stopSpeech: { [weak self] in self?.stopSpeech() },
            previewSpeech: { [weak self] in self?.speak("Hi, I’m Little Guy. I’m here when you need a hand.") }))
    }

    private func buildCompanion() {
        companion = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 76, height: 76),
                            styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        companion.isOpaque = false; companion.backgroundColor = .clear; companion.hasShadow = false
        companion.level = .floating; companion.hidesOnDeactivate = false
        companion.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        companion.isMovableByWindowBackground = true
        face = CompanionView(frame: NSRect(x: 0, y: 0, width: 76, height: 76))
        face.clicked = { [weak self] in self?.showPanel() }; companion.contentView = face
        if let frame = NSScreen.main?.visibleFrame { companion.setFrameOrigin(NSPoint(x: frame.maxX - 100, y: frame.minY + 40)) }
        companionTimer = Timer.scheduledTimer(withTimeInterval: 0.1, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated {
                guard let self else { return }
                self.face.thinking = self.session.busy; self.face.reduceMotion = self.session.reducedMotion
                let visible = !self.hidden && self.session.showCompanion && !self.session.isCapturing
                if visible && !self.companion.isVisible { self.companion.orderFrontRegardless() }
                else if !visible && self.companion.isVisible { self.companion.orderOut(nil) }
                guard visible, self.session.followPointer, !self.window.isVisible else { return }
                let point = NSEvent.mouseLocation
                guard let screen = NSScreen.screens.first(where: { $0.frame.contains(point) }) else { return }
                let frame = screen.visibleFrame
                self.companion.setFrameOrigin(NSPoint(x: min(max(point.x + 24, frame.minX), frame.maxX - 76),
                                                     y: min(max(point.y - 84, frame.minY), frame.maxY - 76)))
            }
        }
    }

    private func buildMenu() {
        let main = NSMenu(), appMenu = NSMenu(), edit = NSMenu(title: "Edit")
        let appItem = NSMenuItem(); appItem.submenu = appMenu; main.addItem(appItem)
        appMenu.addItem(withTitle: "Show Little Guy", action: #selector(showPanel), keyEquivalent: "0").target = self
        appMenu.addItem(withTitle: "Settings…", action: #selector(showSettings), keyEquivalent: ",").target = self
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "Quit Little Guy", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        let editItem = NSMenuItem(title: "Edit", action: nil, keyEquivalent: ""); editItem.submenu = edit; main.addItem(editItem)
        for (title, action, key) in [("Undo", "undo:", "z"), ("Cut", "cut:", "x"), ("Copy", "copy:", "c"),
                                      ("Paste", "paste:", "v"), ("Select All", "selectAll:", "a")] {
            edit.addItem(withTitle: title, action: Selector(action), keyEquivalent: key)
        }
        NSApp.mainMenu = main
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        statusItem.button?.image = NSImage(systemSymbolName: "face.smiling", accessibilityDescription: "Little Guy")
        let menu = NSMenu()
        menu.addItem(withTitle: "Ask Little Guy  ⌃⌥Space", action: #selector(showPanel), keyEquivalent: "").target = self
        menu.addItem(withTitle: "Attach window…", action: #selector(capture), keyEquivalent: "").target = self
        menu.addItem(withTitle: "Settings…", action: #selector(showSettings), keyEquivalent: "").target = self
        menu.addItem(withTitle: "Hide Little Guy", action: #selector(hideAll), keyEquivalent: "").target = self
        menu.addItem(.separator()); menu.addItem(withTitle: "Quit", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        statusItem.menu = menu
    }

    private func registerHotKey() {
        var event = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        let handlerResult = InstallEventHandler(GetApplicationEventTarget(), { _, _, context in
            guard let context else { return OSStatus(eventNotHandledErr) }
            MainActor.assumeIsolated { Unmanaged<AppDelegate>.fromOpaque(context).takeUnretainedValue().showPanel() }
            return noErr
        }, 1, &event, Unmanaged.passUnretained(self).toOpaque(), &hotKeyHandler)
        let result = RegisterEventHotKey(UInt32(kVK_Space), UInt32(controlKey | optionKey),
                                        EventHotKeyID(signature: 0x4C47334D, id: 1), GetApplicationEventTarget(), 0, &hotKey)
        session.shortcutAvailable = handlerResult == noErr && result == noErr
    }

    private func connect() {
        guard let executable = CodexConnection.executable() else {
            session.error = "Codex wasn’t found. Open Settings and choose its executable."; return
        }
        guard let url = Bundle.main.url(forResource: "guide-config", withExtension: "toml"),
              let configuration = try? String(contentsOf: url, encoding: .utf8) else {
            session.error = "The app’s resources are missing. Rebuild Little Guy."; return
        }
        let home = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("LittleGuy3000/codex")
        Task { await session.connect(executable: executable, home: home, configuration: configuration) }
    }

    @objc private func capture() {
        guard !session.busy, !session.isCapturing else { return }
        session.isCapturing = true; session.error = nil; session.notice = nil
        companion.orderOut(nil)
        let picker = ScreenCapturePicker(); self.picker = picker
        picker.present { [weak self] result in
            guard let self, !self.terminating else { return }
            self.session.isCapturing = false; self.picker = nil
            switch result {
            case .success(let data):
                if let data { self.session.imageData = data; self.session.imageName = "Selected window" }
                self.session.notice = data == nil ? "Window selection cancelled." : nil
            case .failure(let error): self.session.error = error.localizedDescription
            }
            self.showPanel()
        }
    }

    private func attachImage() {
        // Let the SwiftUI menu finish tracking before presenting AppKit's file panel.
        DispatchQueue.main.async { [weak self] in self?.presentImagePicker() }
    }

    private func presentImagePicker() {
        guard !session.busy, !session.isCapturing else { return }
        let panel = NSOpenPanel(); panel.allowedContentTypes = [.image]
        panel.canChooseFiles = true; panel.canChooseDirectories = false; panel.allowsMultipleSelection = false
        panel.message = "Choose an image to review before sending it to Little Guy."
        panel.beginSheetModal(for: window) { [weak self] response in
            guard let self, response == .OK, let url = panel.url else { return }
            do {
                let attributes = try FileManager.default.attributesOfItem(atPath: url.path)
                guard ((attributes[.size] as? NSNumber)?.intValue ?? Int.max) < 25 * 1024 * 1024,
                      let image = NSImage(contentsOf: url), let cgImage = image.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
                    throw CompanionError.message("Choose a supported image smaller than 25 MB.")
                }
                self.session.imageData = try ImageAttachment.png(cgImage)
                self.session.imageName = url.lastPathComponent; self.session.error = nil
            } catch { self.session.error = error.localizedDescription }
        }
    }

    private func chooseCodex() {
        guard !session.busy else { return }
        let panel = NSOpenPanel(); panel.canChooseDirectories = false; panel.message = "Choose the Codex executable"
        if panel.runModal() == .OK, let url = panel.url {
            guard FileManager.default.isExecutableFile(atPath: url.path) else { session.error = "Choose an executable file."; return }
            UserDefaults.standard.set(url.path, forKey: "codexExecutable"); connect()
        }
    }

    private func speak(_ text: String) {
        stopSpeech()
        let utterance = AVSpeechUtterance(string: text)
        utterance.voice = AVSpeechSynthesisVoice(language: Locale.current.language.languageCode?.identifier ?? "en")
        speech.speak(utterance)
    }
    private func stopSpeech() { speech.stopSpeaking(at: .immediate) }
    private func cancelInteraction() {
        stopSpeech()
        if session.isCapturing { picker?.cancel() }
        else if session.busy { Task { await session.cancel() } }
        else if session.settingsVisible { session.settingsVisible = false }
        else { window.orderOut(nil) }
    }
    @objc private func showPanel() {
        guard !terminating else { return }
        hidden = false; window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
    }
    @objc private func showSettings() { showPanel(); session.settingsVisible = true }
    @objc private func hideAll() {
        if session.isCapturing { picker?.cancel() }
        if session.busy { Task { await session.cancel() } }
        stopSpeech(); hidden = true; window.orderOut(nil); companion.orderOut(nil)
    }
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool { showPanel(); return true }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }
    func windowShouldClose(_ sender: NSWindow) -> Bool { sender.orderOut(nil); return false }
    func applicationWillTerminate(_ notification: Notification) {
        terminating = true; companionTimer?.invalidate(); stopSpeech(); picker?.cancel(); session.shutdown()
        if let localMonitor { NSEvent.removeMonitor(localMonitor) }
        if let hotKey { UnregisterEventHotKey(hotKey) }; if let hotKeyHandler { RemoveEventHandler(hotKeyHandler) }
    }
}
