using UnityEngine;

/// <summary>
/// Final geometric result produced by the head detector.
/// </summary>
public struct DetectionResult
{
    public bool detected;

    public RectInt redBox;
    public RectInt blueBox;

    // These are frame-center estimates, not measured pupil positions.
    public Vector2 redEye;
    public Vector2 blueEye;
    public Vector2 headCenter;

    public float eyeDistance;
    public float headAngle;
}
