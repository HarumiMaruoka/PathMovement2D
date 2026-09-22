using UnityEngine;

namespace Game.PathMovement2D
{
    public static class PathMovementClipExtensions
    {
        /// <summary>
        /// Advances the clock and returns the displacement in clip coordinates, including MirrorX.
        /// Does not apply Space, origin rotation, or reference Transform motion. Loop includes the reset to the start.
        /// </summary>
        public static Vector2 Update(this PathMovementClip2D instance, float deltaTime)
        {
            if (instance == null) throw new System.ArgumentNullException(nameof(instance));
            return instance.AdvanceTime(deltaTime);
        }

        // Unity reserves Update on ScriptableObject scripts, even with parameters.
        // Keep the convenient call syntax without registering a Unity message.
        public static void Update(this PathMovementClip2D instance, Transform target, float deltaTime)
        {
            if (instance == null) throw new System.ArgumentNullException(nameof(instance));
            instance.AdvanceTime(target, deltaTime);
        }
    }
}
