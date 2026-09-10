import AppKit
import ScreenCaptureKit
import os
import CoreImage

// Take the first complete frame from the system-authorized window, then stop the stream.
// No audio capture or temporary PNG files.
@MainActor
final class ScreenCapturePicker: NSObject, SCContentSharingPickerObserver, SCStreamOutput, SCStreamDelegate {
    private var completion: ((Result<Data?, Error>) -> Void)?
    private var timeout: Task<Void, Never>?
    private var generation = UUID()
    private var captureStream: SCStream?

    func present(completion: @escaping (Result<Data?, Error>) -> Void) {
        guard self.completion == nil else { return }
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
        guard completion != nil || captureStream != nil else { return }
        timeout?.cancel(); timeout = nil; generation = UUID()
        let stream = captureStream; captureStream = nil
        let callback = completion; completion = nil
        Task {
            if let stream { try? await stream.stopCapture() }
            let picker = SCContentSharingPicker.shared
            picker.remove(self); picker.isActive = false
            callback?(result)
        }
    }

    nonisolated func contentSharingPicker(_ picker: SCContentSharingPicker, didCancelFor stream: SCStream?) {
        Task { @MainActor in self.finish(.success(nil)) }
    }

    nonisolated func contentSharingPickerStartDidFailWithError(_ error: Error) {
        Task { @MainActor in self.finish(.failure(CompanionError.message("macOS could not open the window picker. Please try again."))) }
    }

    nonisolated func contentSharingPicker(_ picker: SCContentSharingPicker, didUpdateWith filter: SCContentFilter, for stream: SCStream?) {
        Task { @MainActor in
            guard self.completion != nil, self.captureStream == nil else { return }
            let token = self.generation
            do {
                let config = SCStreamConfiguration()
                let size = filter.contentRect.size
                let scale = min(Double(filter.pointPixelScale), 2048 / max(size.width, size.height, 1))
                config.width = max(1, Int(size.width * scale))
                config.height = max(1, Int(size.height * scale))
                config.showsCursor = false
                config.ignoreShadowsSingleWindow = true
                config.capturesAudio = false
                config.minimumFrameInterval = CMTime(value: 1, timescale: 5)
                config.queueDepth = 3
                // The picker grants this stream access to the chosen window. A separate
                // SCScreenshotManager request can require broader screen-recording access.
                let capture = SCStream(filter: filter, configuration: config, delegate: self)
                self.captureStream = capture
                try capture.addStreamOutput(self, type: .screen, sampleHandlerQueue: .main)
                self.timeout?.cancel()
                self.timeout = Task { [weak self] in
                    do { try await Task.sleep(for: .seconds(12)) } catch { return }
                    guard let self, self.generation == token else { return }
                    self.finish(.failure(CompanionError.message("The selected window did not produce an image. Try choosing it again.")))
                }
                try await capture.startCapture()
                if self.generation != token { try? await capture.stopCapture() }
            } catch {
                guard self.generation == token else { return }
                let failure = error as NSError
                Logger(subsystem: "com.littleguy3000.mac", category: "capture").error("Capture failed: \(failure.domain, privacy: .public) code \(failure.code)")
                self.finish(.failure(CompanionError.message("macOS could not capture that window (\(failure.code)). Choose the window again.")))
            }
        }
    }

    nonisolated func stream(_ stream: SCStream, didOutputSampleBuffer sampleBuffer: CMSampleBuffer, of type: SCStreamOutputType) {
        guard type == .screen else { return }
        Task { @MainActor in
            guard self.captureStream === stream, self.completion != nil,
                  let image = ScreenFrame.image(sampleBuffer) else { return }
            do { self.finish(.success(try ImageAttachment.png(image))) }
            catch { self.finish(.failure(error)) }
        }
    }

    nonisolated func stream(_ stream: SCStream, didStopWithError error: Error) {
        Task { @MainActor in
            guard self.captureStream === stream else { return }
            self.finish(.failure(CompanionError.message("Window sharing stopped before an image was captured. Choose the window again.")))
        }
    }
}

enum ScreenFrame {
    static func image(_ buffer: CMSampleBuffer) -> CGImage? {
        guard buffer.isValid,
              let attachments = CMSampleBufferGetSampleAttachmentsArray(buffer, createIfNecessary: false) as? [[SCStreamFrameInfo: Any]],
              let status = attachments.first?[.status] as? Int, status == SCFrameStatus.complete.rawValue,
              let pixelBuffer = buffer.imageBuffer else { return nil }
        let image = CIImage(cvPixelBuffer: pixelBuffer)
        return CIContext().createCGImage(image, from: image.extent)
    }
}
