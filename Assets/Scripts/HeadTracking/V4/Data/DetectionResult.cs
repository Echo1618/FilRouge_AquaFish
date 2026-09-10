using UnityEngine;

/// <summary>Instantaneous geometry in upright, unmirrored image coordinates, with Y up.</summary>
public struct DetectionResult
{
    public bool detected;
    // A geometrically admissible pair can exist but fail the final score threshold.
    public bool hasCandidate;
    public float pairScore;
    public RectInt redBox, blueBox;
    // Painted-frame box centers are proxies, not measured pupils.
    public Vector2 redEye, blueEye, headCenter;
    public float eyeDistance;
    public float headAngle;
}
