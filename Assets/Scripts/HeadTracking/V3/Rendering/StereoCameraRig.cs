using UnityEngine;

public class StereoCameraRig : MonoBehaviour
{
    [Header("Stereo Cameras")]
    [SerializeField] private Camera leftCamera;
    [SerializeField] private Camera rightCamera;


    [Header("Physical Screen")]
    [SerializeField] private float screenWidth = 0.60f;
    [SerializeField] private float screenHeight = 0.34f;


    [Header("Stereo")]
    [SerializeField] private float eyeSeparation = 0.064f;


    private Vector3 viewerPosition;


    public void SetViewerPose(ViewerPose pose)
    {
        if (!pose.valid)
            return;

        viewerPosition = pose.position;

        UpdateStereoCameras();
    }


    private void UpdateStereoCameras()
    {
        float halfIPD =
            eyeSeparation * 0.5f;


        Vector3 leftEye =
            viewerPosition +
            Vector3.left * halfIPD;

        Vector3 rightEye =
            viewerPosition +
            Vector3.right * halfIPD;


        leftCamera.transform.position =
            leftEye;

        rightCamera.transform.position =
            rightEye;


        // Cameras remain parallel.
        leftCamera.transform.rotation =
            Quaternion.identity;

        rightCamera.transform.rotation =
            Quaternion.identity;


        UpdateProjection(
            leftCamera,
            leftEye
        );

        UpdateProjection(
            rightCamera,
            rightEye
        );
    }


    private void UpdateProjection(
        Camera cam,
        Vector3 eyePosition)
    {
        float near = cam.nearClipPlane;
        float far = cam.farClipPlane;


        // Screen is centered at world (0,0,0)
        // and lies in the XY plane.
        float screenLeft =
            -screenWidth * 0.5f;

        float screenRight =
            screenWidth * 0.5f;

        float screenBottom =
            -screenHeight * 0.5f;

        float screenTop =
            screenHeight * 0.5f;


        // Distance from eye to physical screen plane Z = 0.
        float distance =
            -eyePosition.z;


        if (distance <= 0.001f)
            return;


        // Project physical screen boundaries onto the camera near plane.
        float left =
            (screenLeft - eyePosition.x) *
            near / distance;

        float right =
            (screenRight - eyePosition.x) *
            near / distance;

        float bottom =
            (screenBottom - eyePosition.y) *
            near / distance;

        float top =
            (screenTop - eyePosition.y) *
            near / distance;


        cam.projectionMatrix =
            Matrix4x4.Frustum(
                left,
                right,
                bottom,
                top,
                near,
                far
            );
    }
}