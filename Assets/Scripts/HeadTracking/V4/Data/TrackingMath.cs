using UnityEngine;

public static class TrackingMath
{
    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
    public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    public static bool IsUsable(DetectionResult value)
    {
        return value.detected && IsFinite(value.headCenter) && IsFinite(value.eyeDistance) &&
               value.eyeDistance > 0.001f && IsFinite(value.headAngle) &&
               IsFinite(value.pairScore) && value.pairScore >= 0f;
    }
}
