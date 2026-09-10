using UnityEngine;

/// <summary>Off-axis projection for a physical rectangle in screen-local Z = 0.</summary>
public static class StereoProjection
{
    public static Matrix4x4 Build(Vector3 eye, float width, float height, float near, float far)
    {
        float factor = near / -eye.z;
        return Matrix4x4.Frustum((-width * 0.5f - eye.x) * factor, (width * 0.5f - eye.x) * factor,
            (-height * 0.5f - eye.y) * factor, (height * 0.5f - eye.y) * factor, near, far);
    }

    public static Vector3 ToWorld(Vector3 point, Transform screen) =>
        screen.position + screen.rotation * point;
}
