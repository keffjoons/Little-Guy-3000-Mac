import AppKit
import ScreenCaptureKit

// Use the system's per-window consent picker. No continuous recording or temporary PNG files.
@MainActor
final class ScreenCapturePicker: NSObject, SCContentSharingPickerObserver {
    private var completion: ((Result<Data?, Error>) -> Void)?
    private var timeout: Task<Void, Never>?
    private var generation = UUID()

    func present(completion: @escaping (Result<Data?, Error>) -> Void) {
        cancel()
        self.completion = completion
        let token = UUID(); generation = token
        let picker = SCContentSharingPicker.shared
        var configuration = SCContentSharingPickerConfiguration()
        configuration.allowedPickerModes = [.singleWindow]
        configuration.excludedBundleIDs = [Bundle.main.bundleIdentifier ?? "com.littleguy3000.mac"]
        configuration.allowsChangingSelectedContent = false
        picker.defaultConfiguration = configuration
        picker.add(self); picker.isActive = true; picker.present()
        timeout = Task { [weak self] in
            do { try await Task.sleep(for: .seconds(90)) } catch { return }
            guard let self, self.generation == token else { return }
            self.finish(.failure(CompanionError.message("Window selection timed out. Select Attach window to try again.")))
        }
    }

    func cancel() { finish(.success(nil)) }

    private func finish(_ result: Result<Data?, Error>) {
        timeout?.cancel(); timeout = nil; generation = UUID()
        let picker = SCContentSharingPicker.shared
        picker.remove(self); picker.isActive = false
        let callback = completion; completion = nil; callback?(result)
    }

    nonisolated func contentSharingPicker(_ picker: SCContentSharingPicker, didCancelFor stream: SCStream?) {
        Task { @MainActor in self.finish(.success(nil)) }
    }

    nonisolated func contentSharingPickerStartDidFailWithError(_ error: Error) {
        Task { @MainActor in self.finish(.failure(CompanionError.message("macOS could not open the window picker. Please try again."))) }
    }

    nonisolated func contentSharingPicker(_ picker: SCContentSharingPicker, didUpdateWith filter: SCContentFilter, for stream: SCStream?) {
        Task { @MainActor in
            guard self.completion != nil else { return }
            let token = self.generation
            do {
                let config = SCStreamConfiguration()
                let size = filter.contentRect.size
                let scale = min(Double(filter.pointPixelScale), 2048 / max(size.width, size.height, 1))
                config.width = max(1, Int(size.width * scale))
                config.height = max(1, Int(size.height * scale))
                config.showsCursor = false
                config.ignoreShadowsSingleWindow = true
                let image = try await SCScreenshotManager.captureImage(contentFilter: filter, configuration: config)
                guard self.generation == token else { return }
                guard let data = NSBitmapImageRep(cgImage: image).representation(using: .png, properties: [:]),
                      data.count < 16 * 1024 * 1024 else {
                    throw CompanionError.message("The selected window image is too large. Use a smaller window.")
                }
                self.finish(.success(data))
            } catch {
                guard self.generation == token else { return }
                self.finish(.failure(CompanionError.message("macOS could not capture that window. Select Attach window and choose it again.")))
            }
        }
    }
}
