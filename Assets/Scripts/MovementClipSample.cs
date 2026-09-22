using Game.PathMovement2D;
using System.Collections.Generic;
using UnityEngine;

public class MovementClipSample : MonoBehaviour, IPathMovementClipSource
{
    [SerializeField]
    private PathMovementClip2D _movementClip;

    private PathMovementClip2D _playbackInstance;

    private void Start()
    {
        _playbackInstance = _movementClip.CreatePlaybackInstance();
    }

    private void Update()
    {
        if (_playbackInstance != null && !_playbackInstance.IsComplete)
        {
            _playbackInstance.Update(transform, Time.deltaTime);
        }
    }

    public void GetPathMovementClips(List<PathMovementClip2D> clips)
    {
        clips.Add(_movementClip);
    }
}
