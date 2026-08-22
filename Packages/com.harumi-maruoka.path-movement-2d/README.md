# Path Movement 2D

A reusable Unity 2D path and follower package. Paths are evaluated by normalized distance rather than raw Bezier parameter.

## Quick start

1. Create a path with **GameObject > 2D Object > Path Movement 2D > Path**.
2. Add `PathFollower2D` to the object that should move.
3. Assign the path, then choose Duration or Speed timing.
4. Edit points and tangents in the Scene view. Shift-click adds a point; Delete removes selected points.
5. Edit the progress curve in the follower Inspector. Double-click the graph to add a key.

The path object should normally remain stationary while the follower moves. Local paths are stored relative to the `Path2D` transform, which makes them suitable for prefabs.

## Runtime API

```csharp
follower.Play();
follower.PlayForward();
follower.PlayBackward();
follower.Pause();
follower.Resume();
follower.Stop();
follower.SetProgress(0.5f);
```

Notifications are exposed as C# events: `Started`, `Paused`, `Resumed`, `Looped`, `Completed`, and `Stopped`.

## Notes

- Runtime movement writes to a Transform; Rigidbody2D movement is intentionally outside this package version.
- World-space points remain fixed when the Path2D transform moves.
- Free progress curves wrap outside values only in Loop mode. Once and PingPong clamp them.
- The Inspector preview is a Scene-view ghost and never writes preview values to the target Transform.
