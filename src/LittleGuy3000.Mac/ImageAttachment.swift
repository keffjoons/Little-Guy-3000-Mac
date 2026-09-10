import AppKit

enum ImageAttachment {
    static func png(_ image: CGImage) throws -> Data {
        let scale = min(1, 2048.0 / Double(max(image.width, image.height)))
        let width = max(1, Int(Double(image.width) * scale)), height = max(1, Int(Double(image.height) * scale))
        guard let context = CGContext(data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: 0,
                                      space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else {
            throw CompanionError.message("Couldn’t prepare the image. Try a smaller one.")
        }
        context.interpolationQuality = .high
        context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
        guard let resized = context.makeImage(), let data = NSBitmapImageRep(cgImage: resized).representation(using: .png, properties: [:]),
              data.count < 16 * 1024 * 1024 else { throw CompanionError.message("The image is too large. Try a smaller one.") }
        return data
    }
}
