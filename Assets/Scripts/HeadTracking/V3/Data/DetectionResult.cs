using UnityEngine;

/// <summary>
/// Final geometric result produced by the head detector.
/// </summary>
public struct DetectionResult
{
    public bool Detected;

    public RectInt RedBox;
    public RectInt BlueBox;

    // These are frame-center estimates, not measured pupil positions.
    public Vector2 RedEye;
    public Vector2 BlueEye;
    public Vector2 HeadCenter;

    public float EyeDistance;
    public float HeadAngle;
}
