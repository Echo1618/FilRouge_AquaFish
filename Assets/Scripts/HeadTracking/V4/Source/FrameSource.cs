using UnityEngine;

/// <summary>A source owns its frame buffers. Consumers must not retain pixels across requests.</summary>
public abstract class FrameSource : MonoBehaviour
{
    public int Revision { get; protected set; }
    public virtual bool IsStatic => false;
    public string Status { get; protected set; } = "Stopped";
    public abstract void StartSource();
    public abstract bool TryGetFrame(out ImageFrame frame);
    public abstract void StopSource();
    protected virtual void OnDisable() => StopSource();
    protected virtual void OnDestroy() => StopSource();
}
