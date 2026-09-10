import AppKit
import ScreenCaptureKit

@main struct ScreenFrameTests {
    static func main() throws {
        var pixels: CVPixelBuffer?
        precondition(CVPixelBufferCreate(nil, 16, 12, kCVPixelFormatType_32BGRA, nil, &pixels) == kCVReturnSuccess)
        let pixelBuffer = pixels!
        CVPixelBufferLockBaseAddress(pixelBuffer, [])
        memset(CVPixelBufferGetBaseAddress(pixelBuffer), 255, CVPixelBufferGetDataSize(pixelBuffer))
        CVPixelBufferUnlockBaseAddress(pixelBuffer, [])
        var format: CMVideoFormatDescription?
        precondition(CMVideoFormatDescriptionCreateForImageBuffer(allocator: nil, imageBuffer: pixelBuffer, formatDescriptionOut: &format) == noErr)
        var timing = CMSampleTimingInfo(duration: .invalid, presentationTimeStamp: .zero, decodeTimeStamp: .invalid)
        var sample: CMSampleBuffer?
        precondition(CMSampleBufferCreateReadyWithImageBuffer(allocator: nil, imageBuffer: pixelBuffer,
            formatDescription: format!, sampleTiming: &timing, sampleBufferOut: &sample) == noErr)
        let buffer = sample!
        precondition(ScreenFrame.image(buffer) == nil, "Frames without complete status must be ignored")
        let array = CMSampleBufferGetSampleAttachmentsArray(buffer, createIfNecessary: true)! as NSArray
        let attachment = array[0] as! NSMutableDictionary
        attachment[SCStreamFrameInfo.status.rawValue] = SCFrameStatus.idle.rawValue
        precondition(ScreenFrame.image(buffer) == nil, "Idle frames must not become screenshots")
        attachment[SCStreamFrameInfo.status.rawValue] = SCFrameStatus.complete.rawValue
        let image = ScreenFrame.image(buffer)
        precondition(image?.width == 16 && image?.height == 12)
        CMSampleBufferInvalidate(buffer)
        precondition(ScreenFrame.image(buffer) == nil, "Invalid frames must be ignored")
        print("PASS: complete screen frames decode; absent, idle, and invalid frames are ignored")
    }
}
