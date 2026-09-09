using UnityEngine;

public class HeadPoseMapper
{
    private readonly float maxHorizontal;
    private readonly float maxVertical;

    private readonly float distanceCalibrationConstant;
    private readonly float minDistance;
    private readonly float maxDistance;

    private readonly bool invertX;
    private readonly bool invertY;

    public HeadPoseMapper(
    float maxHorizontal,
    float maxVertical,
    float distanceCalibrationConstant,
    float minDistance,
    float maxDistance,
    bool invertX,
    bool invertY)
    {
        this.maxHorizontal =
            maxHorizontal;

        this.maxVertical =
            maxVertical;

        this.distanceCalibrationConstant =
            distanceCalibrationConstant;

        this.minDistance =
            minDistance;

        this.maxDistance =
            maxDistance;

        this.invertX =
            invertX;

        this.invertY =
            invertY;
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

        if (invertX)
            normalizedX = -normalizedX;

        if (invertY)
            normalizedY = -normalizedY;

        // Convert webcam coordinates to world coordinates.
        float x = normalizedX * maxHorizontal;
        float y = normalizedY * maxVertical;


        // Basic inverse-distance calibration:
        // pixelEyeDistance * realDistance ≈ constant.
        float distance =
    distanceCalibrationConstant /
    detection.eyeDistance;


        distance =
            Mathf.Clamp(
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