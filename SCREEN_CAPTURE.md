# Screen capture

Windows Graphics Capture captures a single eligible HWND on explicit activation. The app records PID, DWM window bounds, pointer location and DPI before displaying its panel. Process exclusions and screen permission apply to every AI capture. Protected/secure/elevated surfaces may be unavailable; protections are never bypassed.

Rectangle overview and freehand circle selection resolve the app at the selection center after removing the input overlay. The source window frame is held only in memory. The requested bounding region is intersected with that window, scaled to a maximum 2200-pixel selected dimension and cropped before PNG encoding/upload. Circle input selects its bounding rectangle, not a polygon mask. Images larger than 12 MB are rejected. Full-panel follow-ups retain the selected image until a new explicit capture or cancellation lifecycle replaces it.

Normal launches do not save PNGs. Synthetic self-test modes intentionally save only project-owned test images. Automatic sensitive-field redaction is not implemented yet; exclusions are process-based, not browser-tab or content-aware. See PRIVACY.md.
