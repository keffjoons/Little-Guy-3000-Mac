import AppKit
import Carbon
import SwiftUI

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    private let session = CompanionSession(transport: CodexConnection())
    private var window: NSWindow!
    private var companion: NSPanel!
    private var bubble: CompanionPanel!
    private var bubbleHost: NSHostingView<QuickCompanionView>!
    private lazy var quick = QuickCompanion(session: session, voice: VoiceInput(), capture: PointerCapture.capture)
    private var anchor = NSPoint.zero
    private var permissionCheck = Date.distantPast
    private var face: CompanionView!
    private var statusItem: NSStatusItem!
    private var hotKey: EventHotKeyRef?
    private var hotKeyHandler: EventHandlerRef?
    private var companionTimer: Timer?
    private var picker: ScreenCapturePicker?
    private var localMonitor: Any?
    private var localShortcutDown = false
    private var hidden = false
    private var terminating = false
    private let speech = SpeechController()

    func applicationDidFinishLaunching(_ notification: Notification) {
        buildWindow(); buildCompanion(); buildBubble(); buildMenu(); registerHotKey()
        quick.stopSpeech = { [weak self] in self?.stopSpeech() }
        session.completedAnswer = { [weak self] answer in
            guard let self else { return }
            if self.session.speakAnswers { self.speak(answer) }
        }
        localMonitor = NSEvent.addLocalMonitorForEvents(matching: [.keyDown, .keyUp]) { [weak self] event in
            let handled = MainActor.assumeIsolated {
                guard let self else { return false }
                if event.keyCode == 53, event.type == .keyDown { self.cancelInteraction(); return true }
                // Also handle app-local events (including accessibility keyboards).
                // Carbon continues to own the shortcut while another app is active.
                if event.keyCode == UInt16(kVK_Space) {
                    if event.type == .keyDown,
                       event.modifierFlags.intersection([.control, .option, .command, .shift]) == [.control, .option] {
                        if !self.localShortcutDown { self.localShortcutDown = true; self.showQuick(); self.quick.keyDown() }
                        return true
                    }
                    if event.type == .keyUp, self.localShortcutDown {
                        self.localShortcutDown = false; self.quick.keyUp(); return true
                    }
                }
                return false
            }
            return handled ? nil : event
        }
        showQuick()
        if !CommandLine.arguments.contains("--ui-test") { connect() }
    }

    private func buildWindow() {
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 980, height: 740),
                          styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.title = "Little Guy"; window.subtitle = "Your Mac companion"
        window.minSize = NSSize(width: 800, height: 620)
        window.delegate = self; window.isReleasedWhenClosed = false
        window.setFrameAutosaveName("LittleGuyMainWindow-v2"); window.center()
        window.contentView = NSHostingView(rootView: CompanionRootView(session: session, speech: speech, quick: quick,
            enableScreen: { PointerCapture.requestAccess() }, enableVoice: { [weak self] in self?.authorizeVoice() },
            capture: { [weak self] in self?.capture() }, loadImage: { [weak self] url in self?.loadImage(url) },
            pasteImage: { [weak self] in self?.pasteImage() },
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
        face.clicked = { [weak self] in self?.showQuick() }; companion.contentView = face
        if let frame = NSScreen.main?.visibleFrame { companion.setFrameOrigin(NSPoint(x: frame.maxX - 100, y: frame.minY + 40)) }
        companionTimer = Timer.scheduledTimer(withTimeInterval: 0.1, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated {
                guard let self else { return }
                self.face.thinking = self.session.busy; self.face.reduceMotion = self.session.reducedMotion
                let visible = !self.hidden && self.session.showCompanion && (!self.session.isCapturing || self.quick.active)
                if visible && !self.companion.isVisible { self.companion.orderFrontRegardless() }
                else if !visible && self.companion.isVisible { self.companion.orderOut(nil) }
                if Date().timeIntervalSince(self.permissionCheck) > 2 {
                    self.quick.screenPermitted = PointerCapture.permitted; self.permissionCheck = Date()
                }
                if self.bubble.isVisible {
                    let height = min(self.bubbleHost.fittingSize.height, 650)
                    if abs(self.bubble.frame.height - height) > 1 {
                        self.bubble.setContentSize(NSSize(width: 376, height: height))
                        self.positionBubble()
                    }
                }
                guard visible, self.session.followPointer, !self.window.isVisible, !self.bubble.isVisible else { return }
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
        appMenu.addItem(withTitle: "Show Little Guy", action: #selector(showQuick), keyEquivalent: "0").target = self
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
        menu.addItem(withTitle: "Ask Little Guy  ⌃⌥Space", action: #selector(showQuick), keyEquivalent: "").target = self
        menu.addItem(withTitle: "Attach window…", action: #selector(capture), keyEquivalent: "").target = self
        menu.addItem(withTitle: "Settings…", action: #selector(showSettings), keyEquivalent: "").target = self
        menu.addItem(withTitle: "Hide Little Guy", action: #selector(hideAll), keyEquivalent: "").target = self
        menu.addItem(.separator()); menu.addItem(withTitle: "Quit", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        statusItem.menu = menu
    }

    private func buildBubble() {
        bubble = CompanionPanel(contentRect: NSRect(x: 0, y: 0, width: 376, height: 380),
                                styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        bubble.title = "Little Guy · Quick ask"
        bubble.isOpaque = false; bubble.backgroundColor = .clear; bubble.hasShadow = true
        bubble.level = .floating; bubble.hidesOnDeactivate = false
        bubble.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        bubbleHost = NSHostingView(rootView: QuickCompanionView(quick: quick, speech: speech,
            settings: { [weak self] in self?.showSettings() }, details: { [weak self] in self?.showPanel() },
            dismiss: { [weak self] in self?.hideAll() }, enableScreen: { PointerCapture.requestAccess() },
            stop: { [weak self] in self?.cancelInteraction() }))
        bubble.contentView = bubbleHost
    }

    @objc private func showQuick() {
        guard !terminating, !session.isCapturing || quick.active else { return }
        let point = NSEvent.mouseLocation
        // Cocoa uses a bottom-left origin; capture APIs use the primary display's top-left.
        let capturePoint = CGPoint(x: point.x, y: (NSScreen.screens.first?.frame.maxY ?? 0) - point.y)
        let overBubble = bubble.isVisible && bubble.frame.contains(point)
        if !overBubble { quick.point(at: PointerCapture.target(at: capturePoint)); anchor = point }
        else if anchor == .zero { anchor = point }
        session.setCompact(true); session.settingsVisible = false
        quick.screenPermitted = PointerCapture.permitted
        hidden = false; window.orderOut(nil)
        positionBubble(); bubble.makeKeyAndOrderFront(nil)
    }

    private func positionBubble() {
        let screen = NSScreen.screens.first(where: { $0.frame.contains(anchor) }) ?? NSScreen.main
        guard let frame = screen?.visibleFrame else { return }
        let x = min(max(anchor.x + 20, frame.minX), frame.maxX - bubble.frame.width)
        let y = min(max(anchor.y - bubble.frame.height - 18, frame.minY + 76), frame.maxY - bubble.frame.height)
        bubble.setFrameOrigin(NSPoint(x: x, y: y))
        companion.setFrameOrigin(NSPoint(x: x + 14, y: y - 70))
    }

    private func registerHotKey() {
        var events = [EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed)),
                      EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyReleased))]
        let handlerResult = InstallEventHandler(GetApplicationEventTarget(), { _, event, context in
            guard let context, let event else { return OSStatus(eventNotHandledErr) }
            let pressed = GetEventKind(event) == UInt32(kEventHotKeyPressed)
            MainActor.assumeIsolated {
                let app = Unmanaged<AppDelegate>.fromOpaque(context).takeUnretainedValue()
                if pressed { app.showQuick(); app.quick.keyDown() } else { app.quick.keyUp() }
            }
            return noErr
        }, 2, &events, Unmanaged.passUnretained(self).toOpaque(), &hotKeyHandler)
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
        quick.cancel(); bubble.orderOut(nil); companion.orderOut(nil); window.orderOut(nil)
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

    private func loadImage(_ url: URL) {
        guard !session.busy, !session.isCapturing else { return }
        do {
            session.imageData = try ImageAttachment.load(url)
            session.imageName = url.lastPathComponent; session.error = nil; session.notice = nil
        } catch { session.error = error.localizedDescription }
    }

    private func pasteImage() {
        guard !session.busy, !session.isCapturing else { return }
        do {
            let pasteboard = NSPasteboard.general
            if let urls = pasteboard.readObjects(forClasses: [NSURL.self], options: [.urlReadingFileURLsOnly: true]) as? [URL], let url = urls.first {
                session.imageData = try ImageAttachment.load(url); session.imageName = url.lastPathComponent
            } else if let image = NSImage(pasteboard: pasteboard), let cg = image.cgImage(forProposedRect: nil, context: nil, hints: nil) {
                session.imageData = try ImageAttachment.png(cg); session.imageName = "Pasted image"
            } else { throw CompanionError.message("Copy an image or image file, then choose Paste image.") }
            session.error = nil; session.notice = nil
        } catch { session.error = error.localizedDescription }
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
        speech.speak(text)
    }
    private func authorizeVoice() {
        Task {
            if await VoiceInput.authorize() { session.notice = "Voice access is enabled. Hold Control–Option–Space to speak." }
            else { session.error = "Allow Little Guy in System Settings → Privacy & Security → Microphone and Speech Recognition." }
        }
    }
    private func stopSpeech() { speech.stop() }
    private func cancelInteraction() {
        stopSpeech()
        if quick.active { quick.cancel(); return }
        if session.isCapturing { picker?.cancel() }
        else if session.busy { Task { await session.cancel() } }
        else if session.settingsVisible { session.settingsVisible = false }
        else { bubble.orderOut(nil); window.orderOut(nil) }
    }
    @objc private func showPanel() {
        guard !terminating else { return }
        quick.cancel(); bubble.orderOut(nil)
        hidden = false; window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
    }
    @objc private func showSettings() { showPanel(); session.settingsVisible = true }
    @objc private func hideAll() {
        quick.cancel(); bubble.orderOut(nil)
        if session.isCapturing { picker?.cancel() }
        if session.busy { Task { await session.cancel() } }
        stopSpeech(); hidden = true; window.orderOut(nil); companion.orderOut(nil)
    }
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool { showQuick(); return true }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }
    func windowShouldClose(_ sender: NSWindow) -> Bool { sender.orderOut(nil); return false }
    func applicationWillTerminate(_ notification: Notification) {
        terminating = true; quick.cancel(); companionTimer?.invalidate(); stopSpeech(); picker?.cancel(); session.shutdown()
        if let localMonitor { NSEvent.removeMonitor(localMonitor) }
        if let hotKey { UnregisterEventHotKey(hotKey) }; if let hotKeyHandler { RemoveEventHandler(hotKeyHandler) }
    }
}
