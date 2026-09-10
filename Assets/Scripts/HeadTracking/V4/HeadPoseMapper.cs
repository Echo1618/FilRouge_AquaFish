using UnityEngine;

/// <summary>Stateless conversion from image pixels to screen-axis meters.</summary>
public sealed class HeadPoseMapper
{
    public ViewerPose Map(DetectionResult detection, int width, int height,
        PoseMappingSettings settings, out MappingStatus status)
    {
        status = MappingStatus.NoDetection;
        if (!TrackingMath.IsUsable(detection) || width <= 0 || height <= 0)
            return default;
        status = MappingStatus.InvalidCalibration;
        if (settings == null || !TrackingMath.IsFinite(settings.distanceCalibrationConstant) ||
            settings.distanceCalibrationConstant <= 0f ||
            !TrackingMath.IsFinite(settings.minViewerDistance) || settings.minViewerDistance <= 0f ||
            !TrackingMath.IsFinite(settings.maxViewerDistance) ||
            settings.maxViewerDistance < settings.minViewerDistance ||
            !TrackingMath.IsFinite(settings.webcamOffsetFromScreen)) return default;

        if (settings.requireCalibrationResolution &&
            (width != settings.calibrationWidth || height != settings.calibrationHeight))
        {
            status = MappingStatus.ResolutionMismatch;
            return default;
        }

        // Z is the axial distance from the webcam, before the screen offset is added.
        float z = Mathf.Clamp(settings.distanceCalibrationConstant / detection.eyeDistance,
            settings.minViewerDistance, settings.maxViewerDistance);
        float x, y;
        if (settings.xyMode == XYMappingMode.Metric)
        {
            if (!TrackingMath.IsFinite(settings.focalX) || settings.focalX <= 0f ||
                !TrackingMath.IsFinite(settings.focalY) || settings.focalY <= 0f ||
                !TrackingMath.IsFinite(settings.calibratedCenterX) ||
                !TrackingMath.IsFinite(settings.calibratedCenterY)) return default;
            x = (detection.headCenter.x - settings.calibratedCenterX) * z / settings.focalX;
            y = (detection.headCenter.y - settings.calibratedCenterY) * z / settings.focalY;
        }
        else
        {
            x = (detection.headCenter.x / width - 0.5f) * 2f * settings.maxHorizontal;
            y = (detection.headCenter.y / height - 0.5f) * 2f * settings.maxVertical;
        }
        if (settings.invertTrackingX) x = -x;
        if (settings.invertTrackingY) y = -y;
        Vector3 position = settings.webcamOffsetFromScreen + new Vector3(x, y, -z);
        status = MappingStatus.InvalidGeometry;
        if (!TrackingMath.IsFinite(position) || position.z >= -0.001f) return default;

        float roll = 0f;
        if (settings.useHeadRoll)
        {
            float angle = detection.headAngle * Mathf.Deg2Rad;
            float dx = Mathf.Cos(angle), dy = Mathf.Sin(angle);
            if (settings.xyMode == XYMappingMode.Metric)
            {
                dx /= settings.focalX;
                dy /= settings.focalY;
            }
            if (settings.invertTrackingX) dx = -dx;
            if (settings.invertTrackingY) dy = -dy;
            // The baseline is an undirected line, normalized to [-90, 90).
            roll = Mathf.Repeat(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 90f, 180f) - 90f;
            roll = Mathf.Clamp(roll, -settings.maxHeadRollDegrees, settings.maxHeadRollDegrees);
        }
        if (!TrackingMath.IsFinite(roll)) return default;
        status = MappingStatus.Valid;
        return new ViewerPose(true, position, roll);
    }
}
