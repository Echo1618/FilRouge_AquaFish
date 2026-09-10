using UnityEngine;

/// <summary>One exponential filter, stepped at render frequency with unscaled delta time.</summary>
public sealed class ViewerPoseSmoother
{
    private ViewerPose value;
    private bool initialized;

    public ViewerPose Filter(ViewerPose target, float deltaTime, SmoothingSettings settings)
    {
        if (!target.valid || !TrackingMath.IsFinite(target.position)) return default;
        if (!initialized || !settings.enabled)
        {
            initialized = true;
            value = target;
            return value;
        }
        if (!TrackingMath.IsFinite(deltaTime) || deltaTime <= 0f) return value;
        float xy = Alpha(settings.xyResponse, deltaTime);
        float z = Alpha(settings.zResponse, deltaTime);
        value.position.x = Mathf.Lerp(value.position.x, target.position.x, xy);
        value.position.y = Mathf.Lerp(value.position.y, target.position.y, xy);
        value.position.z = Mathf.Lerp(value.position.z, target.position.z, z);
        value.rollDegrees = Mathf.LerpAngle(value.rollDegrees, target.rollDegrees,
            Alpha(settings.rollResponse, deltaTime));
        return value;
    }

    public void Reset() { initialized = false; value = default; }
    public static float Alpha(float response, float dt) => 1f - Mathf.Exp(-Mathf.Max(0.01f, response) * dt);
}
