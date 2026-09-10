# Overlay coordinates

WindowContext.Bounds and all selector points are physical desktop pixels, including negative monitor origins. ScreenTransform maps the actual encoded image rectangle to its exact desktop region. Cropped images have a crop-specific transform while WindowContext retains the whole window for PID/bounds freshness checks. WinRT scaling occurs before cropping; crop coordinates are rounded inward.

Model annotation coordinates are image pixels. Only validated ring/arrow geometry is rendered, with capture identity, age, bounds, size and count checks. Window move, click, focus change, display change and lock invalidate overlays. The freehand/rectangle selector owns input while active; annotations and bubble tails are click-through and nonactivating. Esc and right-click dismiss selection.

Speech cards are rounded native-clipped WinUI windows with a separate layered curved tail; thinking uses three shrinking circular islands. Reduced motion disables tail animation. Work-area placement is bounded; physical mixed-DPI and screen-reader checks remain acceptance work.
