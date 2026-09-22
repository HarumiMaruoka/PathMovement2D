# Changelog

## Unreleased

- Add reusable movement clip assets, isolated playback instances, and a standard player.
- Add shared SceneView editing, clip duplication, Path2D import, mirroring, and synchronized animation preview.
- Share path evaluation while retaining existing Path2D serialized fields.
- Fix interpolation across path segment boundaries and refresh distance tables after Transform scale changes.
- Add clip runtime, import, Undo/Redo, and animation preview restoration tests.

## 1.0.0

- Initial release with distance-parameterized linear and Bezier paths.
- Added custom monotonic/free progress curves and presets.
- Added reusable follower playback, looping, direction, rotation, and events.
- Added Scene view path editing, curve editing, ghost preview, and equal-time markers.
