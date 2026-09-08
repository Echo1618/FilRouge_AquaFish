using UnityEngine;

/// <summary>
/// Unity-facing source of frames. TrackingController does not need to know
/// whether the pixels come from a webcam, an image folder or another source.
/// </summary>
public abstract class FrameSource : MonoBehaviour
{
    public abstract void StartSource();
    public abstract bool TryGetFrame(out ImageFrame frame);
    public abstract void StopSource();
}
