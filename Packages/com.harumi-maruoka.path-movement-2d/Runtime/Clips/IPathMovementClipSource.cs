using System.Collections.Generic;

namespace Game.PathMovement2D
{
    public interface IPathMovementClipSource
    {
        void GetPathMovementClips(List<PathMovementClip2D> clips);
    }
}
