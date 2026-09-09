using UnityEngine;

public class HeadPoseMapper
{
    private readonly float maxHorizontal;
    private readonly float maxVertical;

    private readonly float calibrationDistance;
    private readonly float calibrationEyeDistance;

    private readonly float minDistance;
    private readonly float maxDistance;

    public HeadPoseMapper(
        float maxHorizontal,
        float maxVertical,
        float calibrationDistance,
        float calibrationEyeDistance,
        float minDistance,
        float maxDistance)
    {
        this.maxHorizontal = maxHorizontal;
        this.maxVertical = maxVertical;

        this.calibrationDistance = calibrationDistance;
        this.calibrationEyeDistance = calibrationEyeDistance;

        this.minDistance = minDistance;
        this.maxDistance = maxDistance;
    }


    public ViewerPose Map(
        DetectionResult detection,
        int imageWidth,
        int imageHeight)
    {
        if (!detection.detected || detection.eyeDistance <= 0f)
            return new ViewerPose(false, Vector3.zero);


        // Normalize image coordinates to [-1, 1].
        float normalizedX =
            (detection.headCenter.x / imageWidth - 0.5f) * 2f;

        float normalizedY =
            (detection.headCenter.y / imageHeight - 0.5f) * 2f;


        // Convert webcam coordinates to world coordinates.
        float x = normalizedX * maxHorizontal;
        float y = normalizedY * maxVertical;


        // Basic inverse-distance calibration:
        // pixelEyeDistance * realDistance ≈ constant.
        float distance =
            calibrationDistance *
            calibrationEyeDistance /
            detection.eyeDistance;

        distance = Mathf.Clamp(
            distance,
            minDistance,
            maxDistance
        );


        // Viewer is placed in front of the screen, on negative Z.
        return new ViewerPose(
            true,
            new Vector3(x, y, -distance)
        );
    }
}