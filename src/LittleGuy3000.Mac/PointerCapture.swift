import AppKit
import ScreenCaptureKit
import Vision

struct PointerTarget: Equatable {
    let id: CGWindowID
    let pid: pid_t
    let name: String
}

@MainActor
enum PointerCapture {
    static var permitted: Bool { CGPreflightScreenCaptureAccess() }

    static func requestAccess() {
        if !CGRequestScreenCaptureAccess(),
           let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture") {
            NSWorkspace.shared.open(url)
        }
    }

    // CGWindowList is front-to-back. Resolve the target before showing our panel.
    static func target(at point: CGPoint) -> PointerTarget? {
        let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] ?? []
        return target(at: point, windows: windows, ownPID: ProcessInfo.processInfo.processIdentifier)
    }

    static func target(at point: CGPoint, windows: [[String: Any]], ownPID: pid_t) -> PointerTarget? {
        for window in windows {
            guard let pid = window[kCGWindowOwnerPID as String] as? Int32, pid != ownPID,
                  window[kCGWindowLayer as String] as? Int == 0,
                  (window[kCGWindowAlpha as String] as? Double ?? 1) > 0,
                  let bounds = window[kCGWindowBounds as String] as? NSDictionary,
                  let rect = CGRect(dictionaryRepresentation: bounds), rect.contains(point),
                  let id = window[kCGWindowNumber as String] as? UInt32 else { continue }
            return PointerTarget(id: id, pid: pid, name: window[kCGWindowOwnerName as String] as? String ?? "Window")
        }
        return nil
    }

    static func capture(_ target: PointerTarget) async throws -> Data {
        guard permitted else { throw CompanionError.message("Enable Screen Recording below, then ask again. macOS may ask you to reopen Little Guy.") }
        let content = try await SCShareableContent.excludingDesktopWindows(true, onScreenWindowsOnly: true)
        try Task.checkCancellation()
        guard let window = content.windows.first(where: { $0.windowID == target.id && $0.owningApplication?.processID == target.pid }),
              window.isOnScreen else { throw CompanionError.message("That window closed or moved off screen. Point at a window and ask again.") }
        let filter = SCContentFilter(desktopIndependentWindow: window)
        let config = SCStreamConfiguration()
        let scale = min(Double(filter.pointPixelScale), 2048 / max(filter.contentRect.width, filter.contentRect.height, 1))
        config.width = max(1, Int(filter.contentRect.width * scale))
        config.height = max(1, Int(filter.contentRect.height * scale))
        config.showsCursor = false; config.capturesAudio = false
        config.ignoreShadowsSingleWindow = true
        let image = try await SCScreenshotManager.captureImage(contentFilter: filter, configuration: config)
        try Task.checkCancellation()
        return try ImageAttachment.png(image)
    }

    static func readContext(_ target: PointerTarget) async throws -> (image: Data, text: String) {
        let image = try await capture(target)
        return (image, (try? await recognizeText(image)) ?? "")
    }

    // OCR gives live voice immediate visible context. Visual questions and actions
    // still use inspect_window's full screenshot and accessibility controls.
    nonisolated static func recognizeText(_ image: Data) async throws -> String {
        try await Task.detached(priority: .userInitiated) {
            let request = VNRecognizeTextRequest()
            request.recognitionLevel = .accurate
            try VNImageRequestHandler(data: image).perform([request])
            let lines = (request.results ?? []).compactMap { $0.topCandidates(1).first?.string }
            return String(lines.joined(separator: "\n").prefix(12_000))
        }.value
    }
}
