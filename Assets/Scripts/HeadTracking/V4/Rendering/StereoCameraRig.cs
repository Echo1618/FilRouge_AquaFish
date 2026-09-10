using UnityEngine;

/// <summary>Parallel cameras and independent IPD in a movable, metric screen reference.</summary>
[DefaultExecutionOrder(100)]
public sealed class StereoCameraRig : MonoBehaviour
{
    [Header("Stereo cameras")]
    [SerializeField] private Camera leftCamera;
    [SerializeField] private Camera rightCamera;
    [SerializeField] private Transform screenTransform;
    [Header("Physical screen, meters")]
    [Min(0.01f)] [SerializeField] private float screenWidth = 0.60f;
    [Min(0.01f)] [SerializeField] private float screenHeight = 0.3375f;
    [Header("Stereo")]
    [Min(0f)] [SerializeField] private float eyeSeparation = 0.03f;
    [Tooltip("Initial centered pose before the first measurement.")]
    [Min(0.01f)] [SerializeField] private float defaultViewerDistance = 0.60f;
    private ViewerPose currentPose;
    private bool hasValidPose;
    public Camera LeftCamera => leftCamera;
    public Camera RightCamera => rightCamera;
    public Transform ScreenReference => screenTransform != null ? screenTransform : transform;
    public float ScreenWidth => screenWidth;
    public float ScreenHeight => screenHeight;
    public ViewerPose CurrentPose => currentPose;

    private void OnEnable()
    {
        if (!ValidateReferences()) { enabled = false; return; }
        if (!hasValidPose) SetViewerPose(new ViewerPose(true, new Vector3(0f, 0f, -Mathf.Max(0.01f, defaultViewerDistance))));
    }

    public void SetViewerPose(ViewerPose pose)
    {
        if (!pose.valid || !TrackingMath.IsFinite(pose.position) || pose.position.z >= -0.001f ||
            !TrackingMath.IsFinite(pose.rollDegrees)) return;
        currentPose = pose;
        hasValidPose = true;
    }

    // Apply even without new measurements so moving the screen reference remains effective.
    private void LateUpdate() => RefreshCameras();

    public void RefreshCameras()
    {
        if (!hasValidPose || leftCamera == null || rightCamera == null || leftCamera == rightCamera) return;
        if (!TrackingMath.IsFinite(screenWidth) || screenWidth <= 0f ||
            !TrackingMath.IsFinite(screenHeight) || screenHeight <= 0f ||
            !TrackingMath.IsFinite(eyeSeparation) || eyeSeparation < 0f) return;
        float angle = currentPose.rollDegrees * Mathf.Deg2Rad;
        Vector3 halfBaseline = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * eyeSeparation * 0.5f;
        UpdateEye(leftCamera, currentPose.position - halfBaseline);
        UpdateEye(rightCamera, currentPose.position + halfBaseline);
    }

    private void UpdateEye(Camera camera, Vector3 eye)
    {
        float near = camera.nearClipPlane, far = camera.farClipPlane;
        if (near <= 0f || far <= near) return;
        Transform screen = ScreenReference;
        Vector3 world = StereoProjection.ToWorld(eye, screen);
        camera.transform.SetPositionAndRotation(world, screen.rotation);
        camera.orthographic = false;
        // Explicit unit-scale view matrix keeps ancestor scale out of metric geometry.
        camera.worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) *
            Matrix4x4.TRS(world, screen.rotation, Vector3.one).inverse;
        camera.projectionMatrix = StereoProjection.Build(eye, screenWidth, screenHeight, near, far);
    }

    public bool ValidateReferences()
    {
        if (leftCamera == null || rightCamera == null || leftCamera == rightCamera ||
            leftCamera.transform == rightCamera.transform)
        {
            Debug.LogError("StereoCameraRig: assign two different camera objects.", this);
            return false;
        }
        Transform screen = ScreenReference;
        if (screen.IsChildOf(leftCamera.transform) || screen.IsChildOf(rightCamera.transform))
        {
            Debug.LogError("StereoCameraRig: screen reference must not be a camera or a camera descendant.", this);
            return false;
        }
        if (leftCamera.targetTexture == null || rightCamera.targetTexture == null ||
            leftCamera.targetTexture == rightCamera.targetTexture)
            Debug.LogWarning("StereoCameraRig: assign a separate RenderTexture to each eye.", this);
        return true;
    }

    private void OnDisable()
    {
        ResetCamera(leftCamera);
        ResetCamera(rightCamera);
    }
    private static void ResetCamera(Camera camera)
    {
        if (camera == null) return;
        camera.ResetProjectionMatrix();
        camera.ResetWorldToCameraMatrix();
    }

    private void OnDrawGizmosSelected()
    {
        Transform screen = ScreenReference;
        Vector3 a = StereoProjection.ToWorld(new Vector3(-screenWidth, -screenHeight, 0f) * 0.5f, screen);
        Vector3 b = StereoProjection.ToWorld(new Vector3(screenWidth, -screenHeight, 0f) * 0.5f, screen);
        Vector3 c = StereoProjection.ToWorld(new Vector3(screenWidth, screenHeight, 0f) * 0.5f, screen);
        Vector3 d = StereoProjection.ToWorld(new Vector3(-screenWidth, screenHeight, 0f) * 0.5f, screen);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        Gizmos.DrawLine(screen.position, screen.position + screen.forward * 0.25f);
    }
}
