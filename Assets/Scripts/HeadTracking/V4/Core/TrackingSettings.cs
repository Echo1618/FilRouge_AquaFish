using System;
using UnityEngine;

public enum XYMappingMode { LegacyGains, Metric }

[Serializable]
public sealed class PoseMappingSettings
{
    public XYMappingMode xyMode = XYMappingMode.LegacyGains;
    public bool invertTrackingX = true;
    public bool invertTrackingY;
    [Min(0f)] public float maxHorizontal = 0.4f;
    [Min(0f)] public float maxVertical = 0.25f;

    [Header("Calibration at the processed, upright resolution")]
    [Min(1)] public int calibrationWidth = 640;
    [Min(1)] public int calibrationHeight = 480;
    public bool requireCalibrationResolution = true;
    public float calibratedCenterX = 320f;
    public float calibratedCenterY = 240f;
    [Tooltip("Measured focal length in pixels. Zero means not calibrated.")]
    [Min(0f)] public float focalX;
    [Min(0f)] public float focalY;
    [Tooltip("K in pixel * meter, measured at the calibration resolution.")]
    [Min(0.001f)] public float distanceCalibrationConstant = 67f;
    [Min(0.01f)] public float minViewerDistance = 0.35f;
    [Min(0.01f)] public float maxViewerDistance = 0.90f;

    [Tooltip("Webcam optical origin relative to screen center, in screen-axis meters. Axes must be aligned.")]
    public Vector3 webcamOffsetFromScreen;
    [Tooltip("Optional roll of the eye baseline; camera orientations remain parallel.")]
    public bool useHeadRoll;
    [Range(0f, 60f)] public float maxHeadRollDegrees = 35f;
}

[Serializable]
public sealed class ContinuitySettings
{
    public bool enabled = true;
    [Tooltip("Off-center first acquisition is allowed after confirmation. Zero disables the center preference.")]
    [Range(0f, 1f)] public float initialCenterRadiusRatio = 0.35f;
    [Min(0.01f)] public float centerJumpFactor = 0.45f;
    [Min(1f)] public float minCenterJumpPixels = 20f;
    [Min(1f)] public float maxCenterJumpPixels = 90f;
    [Range(0.01f, 1f)] public float maxEyeDistanceRelativeChange = 0.15f;
    [Min(0f)] public float shortLossTimeout = 0.18f;
    [Tooltip("A live stream is stale after this delay without a new frame.")]
    [Min(0.01f)] public float frameStaleTimeout = 0.10f;
    [Range(1, 20)] public int reacquisitionFrames = 3;
    [Tooltip("Maximum gap between confirmation samples. Render ticks are not samples.")]
    [Min(0.01f)] public float maxCandidateGap = 0.15f;
    [Tooltip("Confirmation radius relative to apparent glasses size.")]
    [Min(0.01f)] public float reacquisitionCenterFactor = 0.15f;
}

[Serializable]
public sealed class SmoothingSettings
{
    public bool enabled = true;
    [Min(0.01f)] public float xyResponse = 7f;
    [Min(0.01f)] public float zResponse = 4f;
    [Min(0.01f)] public float rollResponse = 7f;
}
