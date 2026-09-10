import AppKit
import CoreText

@main struct ImageTests {
    static func main() throws {
        func canvas(_ width: Int, _ height: Int) -> CGContext {
            CGContext(data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: 0,
                      space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
        }
        let large = canvas(4096, 2048)
        large.setFillColor(CGColor(red: 0.2, green: 0.6, blue: 0.4, alpha: 1)); large.fill(CGRect(x: 0, y: 0, width: 4096, height: 2048))
        let resized = try ImageAttachment.png(large.makeImage()!)
        let decoded = NSBitmapImageRep(data: resized)!
        precondition(decoded.pixelsWide == 2048 && decoded.pixelsHigh == 1024)
        let card = canvas(640, 400)
        card.setFillColor(CGColor(gray: 1, alpha: 1)); card.fill(CGRect(x: 0, y: 0, width: 640, height: 400))
        card.setFillColor(CGColor(red: 0.05, green: 0.5, blue: 0.25, alpha: 1)); card.fill(CGRect(x: 40, y: 145, width: 120, height: 120))
        for (text, y, size) in [("Little Guy image test", 330.0, 30.0), ("Verification code: MAPLE 472", 80.0, 25.0), ("Synthetic test card — no personal data", 35.0, 18.0)] {
            let string = NSAttributedString(string: text, attributes: [NSAttributedString.Key(kCTFontAttributeName as String): CTFontCreateWithName("Helvetica" as CFString, size, nil), NSAttributedString.Key(kCTForegroundColorAttributeName as String): CGColor(gray: 0.1, alpha: 1)])
            card.textPosition = CGPoint(x: 40, y: y); CTLineDraw(CTLineCreateWithAttributedString(string), card)
        }
        let data = try ImageAttachment.png(card.makeImage()!)
        let url = URL(fileURLWithPath: FileManager.default.currentDirectoryPath).appendingPathComponent(".local/image-test-card.png")
        try data.write(to: url)
        precondition(NSBitmapImageRep(data: data)?.pixelsWide == 640)
        print("PASS: image size limit, aspect ratio, PNG encoding; synthetic visual fixture created")
    }
}
