using UnityEngine;

/// <summary>
/// Controls the two stereo cameras from a viewer position
/// expressed relative to a movable screen reference.
///
/// The StereoRig transform represents the center and orientation
/// of the virtual/physical screen plane.
///
/// Screen local coordinates:
/// X = horizontal
/// Y = vertical
/// Z = depth
///
/// The viewer is expected to be on negative Z.
/// The scene is expected to extend toward positive Z.
/// </summary>
public sealed class StereoCameraRig : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("Stereo Cameras")]
    [SerializeField] private Camera leftCamera;
    [SerializeField] private Camera rightCamera;


    [Tooltip(
        "Optional screen reference. " +
        "If empty, this StereoRig transform is used."
    )]
    [SerializeField] private Transform screenTransform;


    // =========================================================
    // PHYSICAL SCREEN
    // =========================================================

    [Header("Screen")]

    [Tooltip("Screen width in world units.")]
    [SerializeField] private float screenWidth = 0.60f;

    [Tooltip("Screen height in world units.")]
    [SerializeField] private float screenHeight = 0.34f;


    // =========================================================
    // STEREO
    // =========================================================

    [Header("Stereo")]

    [Tooltip("Distance between virtual eyes.")]
    [SerializeField] private float eyeSeparation = 0.03f;


    // =========================================================
    // STATE
    // =========================================================

    private ViewerPose currentPose;

    private bool hasValidPose;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (screenTransform == null)
            screenTransform = transform;

        ValidateReferences();
    }


    private void OnDisable()
    {
        ResetProjectionMatrices();
    }


    private void OnDestroy()
    {
        ResetProjectionMatrices();
    }


    // =========================================================
    // PUBLIC API
    // =========================================================

    /// <summary>
    /// Updates the stereo rig from a viewer pose expressed
    /// relative to the screen coordinate system.
    /// </summary>
    public void SetViewerPose(ViewerPose pose)
    {
        if (!pose.valid)
            return;


        currentPose = pose;
        hasValidPose = true;


        UpdateStereoCameras();
    }


    // =========================================================
    // CAMERA UPDATE
    // =========================================================

    private void UpdateStereoCameras()
    {
        if (!hasValidPose)
            return;

        if (leftCamera == null ||
            rightCamera == null)
        {
            return;
        }


        float halfIPD =
            eyeSeparation * 0.5f;


        // ViewerPose.position is expressed
        // in screen-local coordinates.
        Vector3 viewer =
            currentPose.position;


        Vector3 leftEyeLocal =
            viewer +
            Vector3.left * halfIPD;


        Vector3 rightEyeLocal =
            viewer +
            Vector3.right * halfIPD;


        Transform screen =
            GetScreenTransform();


        // Convert screen-local eye coordinates
        // into world coordinates.
        Vector3 leftEyeWorld =
            ScreenToWorld(
                leftEyeLocal,
                screen
            );


        Vector3 rightEyeWorld =
            ScreenToWorld(
                rightEyeLocal,
                screen
            );


        // Both cameras remain parallel to the screen.
        leftCamera.transform.SetPositionAndRotation(
            leftEyeWorld,
            screen.rotation
        );


        rightCamera.transform.SetPositionAndRotation(
            rightEyeWorld,
            screen.rotation
        );


        // Build an asymmetric projection for each eye.
        UpdateProjection(
            leftCamera,
            leftEyeLocal
        );


        UpdateProjection(
            rightCamera,
            rightEyeLocal
        );
    }


    // =========================================================
    // OFF-AXIS PROJECTION
    // =========================================================

    private void UpdateProjection(
        Camera camera,
        Vector3 eyeLocal)
    {
        float near =
            camera.nearClipPlane;

        float far =
            camera.farClipPlane;


        // Screen boundaries in screen-local coordinates.
        float screenLeft =
            -screenWidth * 0.5f;

        float screenRight =
            screenWidth * 0.5f;

        float screenBottom =
            -screenHeight * 0.5f;

        float screenTop =
            screenHeight * 0.5f;


        // Viewer is located on negative Z.
        // Screen plane is Z = 0.
        float distance =
            -eyeLocal.z;


        if (distance <= 0.001f)
        {
            Debug.LogWarning(
                "StereoCameraRig: viewer must stay " +
                "in front of the screen plane."
            );

            return;
        }


        // Project the physical screen edges
        // onto the camera near plane.
        float left =
            (screenLeft - eyeLocal.x) *
            near /
            distance;


        float right =
            (screenRight - eyeLocal.x) *
            near /
            distance;


        float bottom =
            (screenBottom - eyeLocal.y) *
            near /
            distance;


        float top =
            (screenTop - eyeLocal.y) *
            near /
            distance;


        camera.projectionMatrix =
            Matrix4x4.Frustum(
                left,
                right,
                bottom,
                top,
                near,
                far
            );
    }


    // =========================================================
    // SCREEN COORDINATES
    // =========================================================

    private Transform GetScreenTransform()
    {
        return screenTransform != null
            ? screenTransform
            : transform;
    }


    /// <summary>
    /// Converts a point expressed in screen coordinates
    /// into world coordinates.
    ///
    /// Transform scale is intentionally ignored so that
    /// screen dimensions and IPD remain metric values.
    /// </summary>
    private Vector3 ScreenToWorld(
        Vector3 localPoint,
        Transform screen)
    {
        return
            screen.position +

            screen.right *
            localPoint.x +

            screen.up *
            localPoint.y +

            screen.forward *
            localPoint.z;
    }


    // =========================================================
    // RESET
    // =========================================================

    private void ResetProjectionMatrices()
    {
        if (leftCamera != null)
            leftCamera.ResetProjectionMatrix();

        if (rightCamera != null)
            rightCamera.ResetProjectionMatrix();
    }


    // =========================================================
    // VALIDATION
    // =========================================================

    private void ValidateReferences()
    {
        if (leftCamera == null)
        {
            Debug.LogError(
                "StereoCameraRig: Left Camera is missing."
            );
        }


        if (rightCamera == null)
        {
            Debug.LogError(
                "StereoCameraRig: Right Camera is missing."
            );
        }
    }


    // =========================================================
    // DEBUG GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Transform screen =
            screenTransform != null
                ? screenTransform
                : transform;


        float halfWidth =
            screenWidth * 0.5f;

        float halfHeight =
            screenHeight * 0.5f;


        Vector3 bottomLeft =
            ScreenToWorld(
                new Vector3(
                    -halfWidth,
                    -halfHeight,
                    0f
                ),
                screen
            );


        Vector3 bottomRight =
            ScreenToWorld(
                new Vector3(
                    halfWidth,
                    -halfHeight,
                    0f
                ),
                screen
            );


        Vector3 topRight =
            ScreenToWorld(
                new Vector3(
                    halfWidth,
                    halfHeight,
                    0f
                ),
                screen
            );


        Vector3 topLeft =
            ScreenToWorld(
                new Vector3(
                    -halfWidth,
                    halfHeight,
                    0f
                ),
                screen
            );


        // Draw screen rectangle.
        Gizmos.DrawLine(
            bottomLeft,
            bottomRight
        );

        Gizmos.DrawLine(
            bottomRight,
            topRight
        );

        Gizmos.DrawLine(
            topRight,
            topLeft
        );

        Gizmos.DrawLine(
            topLeft,
            bottomLeft
        );


        // Draw screen forward direction.
        Gizmos.DrawLine(
            screen.position,
            screen.position +
            screen.forward * 0.25f
        );
    }
}