import AppKit

final class CompanionView: NSView {
    var thinking = false
    var clicked: (() -> Void)?
    private var tick = 0.0
    private var timer: Timer?

    override init(frame: NSRect) {
        super.init(frame: frame)
        timer = Timer.scheduledTimer(withTimeInterval: 1.0 / 20, repeats: true) { [weak self] _ in
            guard let self else { return }
            self.tick += 0.05; self.needsDisplay = true
        }
        setAccessibilityElement(true)
        setAccessibilityRole(.button)
        setAccessibilityLabel("Little Guy. Open Ask panel.")
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    override func accessibilityPerformPress() -> Bool { clicked?(); return true }
    override func mouseDown(with event: NSEvent) { clicked?() }
    override func draw(_ dirtyRect: NSRect) {
        let reduced = NSWorkspace.shared.accessibilityDisplayShouldReduceMotion
        let t = reduced ? 0 : tick
        let scale = min(bounds.width, bounds.height) / 100
        let transform = NSAffineTransform(); transform.scale(by: scale); transform.concat()
        let bob = sin(t * 1.8) * 1.2
        NSColor.black.withAlphaComponent(0.10).setFill()
        NSBezierPath(ovalIn: NSRect(x: 20, y: 6, width: 60, height: 7)).fill()
        let face = NSBezierPath(ovalIn: NSRect(x: 13, y: 15 + bob, width: 74, height: 72))
        NSGradient(starting: NSColor(red: 0.99, green: 1, blue: 0.81, alpha: 1),
                   ending: NSColor(red: 0.93, green: 0.97, blue: 0.60, alpha: 1))?.draw(in: face, angle: -70)
        let ink = NSColor(red: 0.15, green: 0.18, blue: 0.15, alpha: 1)
        ink.withAlphaComponent(0.55).setStroke(); face.lineWidth = 1.4; face.stroke()
        ink.setFill(); ink.setStroke()
        let blink = !reduced && t.truncatingRemainder(dividingBy: 5.8) > 5.58
        for x in [31.0, 59.0] {
            NSBezierPath(ovalIn: NSRect(x: x + (thinking ? -2 : 0), y: 53 + bob + (thinking ? 4 : 0),
                                       width: 8, height: blink ? 2 : 11)).fill()
        }
        let smile = NSBezierPath(); smile.lineWidth = 3.8; smile.lineCapStyle = .round
        smile.move(to: NSPoint(x: 36, y: 42 + bob))
        smile.curve(to: NSPoint(x: 65, y: 42 + bob), controlPoint1: NSPoint(x: 41, y: 29 + bob),
                    controlPoint2: NSPoint(x: 58, y: 29 + bob)); smile.stroke()
        if thinking {
            for i in 0..<3 {
                let angle = t * 2 + Double(i) * 0.5
                ink.withAlphaComponent(0.8 - Double(i) * 0.2).setFill()
                NSBezierPath(ovalIn: NSRect(x: 47 + cos(angle) * 43, y: 47 + sin(angle) * 43,
                                           width: 6, height: 6)).fill()
            }
        }
    }
}
