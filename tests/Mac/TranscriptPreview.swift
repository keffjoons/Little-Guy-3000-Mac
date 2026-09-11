import AppKit
import SwiftUI

@main struct TranscriptPreview {
    @MainActor static func main() {
        let app = NSApplication.shared
        app.setActivationPolicy(.accessory)
        let menu = NSMenu(), item = NSMenuItem(), actions = NSMenu()
        actions.addItem(withTitle: "Quit Preview", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        item.submenu = actions; menu.addItem(item); app.mainMenu = menu
        let panel = CompanionPanel(contentRect: NSRect(x: 500, y: 500, width: 332, height: 140),
            styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.title = "Little Guy transcript layout preview"
        panel.isOpaque = false; panel.backgroundColor = .clear
        panel.level = .floating; panel.hasShadow = true
        let host = NSHostingView(rootView: QuickCompanionView(transcript: QuickTranscript(
            user: "What does this button do?",
            agent: "It starts playback for the playlist you’re viewing.")))
        panel.contentView = host
        panel.setContentSize(NSSize(width: 332, height: host.fittingSize.height))
        panel.center(); panel.orderFrontRegardless()
        app.run()
    }
}
