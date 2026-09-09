using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Displays live tracking information used to calibrate
/// webcam-to-viewer mapping.
///
/// Z model:
///     distance = K / eyeDistance
///
/// where:
///     distance    = viewer distance in meters
///     eyeDistance = detected glasses separation in pixels
///     K           = calibration constant in px*m
///
/// The HUD also maintains rolling averages to reduce
/// measurement noise during calibration.
/// </summary>
public sealed class TrackingCalibrationHUD : MonoBehaviour
{
    // =========================================================
    // DISPLAY
    // =========================================================

    [Header("Display")]

    [SerializeField]
    private bool visible = true;

    [SerializeField]
    private Rect panelRect =
        new Rect(15f, 15f, 500f, 700f);

    [SerializeField]
    private int fontSize = 16;


    // =========================================================
    // OPTIONAL PHYSICAL REFERENCES
    // =========================================================

    [Header("Optional Physical References")]

    [Tooltip(
        "Optional measured viewer distance in meters. " +
        "Set to 0 if the physical distance is only estimated."
    )]
    [Min(0f)]
    [SerializeField]
    private float referencePhysicalDistance = 0f;


    [Tooltip(
        "Known horizontal displacement used later for X calibration."
    )]
    [Min(0f)]
    [SerializeField]
    private float knownHorizontalOffsetMeters = 0.10f;


    [Tooltip(
        "Known vertical displacement used later for Y calibration."
    )]
    [Min(0f)]
    [SerializeField]
    private float knownVerticalOffsetMeters = 0.10f;


    // =========================================================
    // STATISTICS
    // =========================================================

    [Header("Statistics")]

    [Range(5, 120)]
    [SerializeField]
    private int averagingWindow = 30;


    // =========================================================
    // CURRENT FRAME DATA
    // =========================================================

    private int frameWidth;
    private int frameHeight;

    private DetectionResult rawDetection;
    private DetectionResult filteredDetection;

    private ViewerPose rawPose;
    private ViewerPose finalPose;

    private TrackingState trackingState;

    private bool currentMeasurementAccepted;

    private Vector3 rigPosition;

    private bool smoothingEnabled;
    private bool continuityEnabled;


    // =========================================================
    // Z CALIBRATION
    // =========================================================

    private float configuredCalibrationConstant;


    // =========================================================
    // ROLLING STATISTICS
    // =========================================================

    private readonly Queue<CalibrationSample> samples =
        new Queue<CalibrationSample>();

    private Vector2 averageHeadCenter;
    private float averageEyeDistance;

    private float eyeDistanceStdDev;


    // =========================================================
    // XY REFERENCE
    // =========================================================

    private bool hasCenterReference;

    private Vector2 centerReference;


    // =========================================================
    // COUNTERS
    // =========================================================

    private int rawDetectionCount;
    private int acceptedCount;
    private int rejectedCount;


    // =========================================================
    // GUI
    // =========================================================

    private GUIStyle labelStyle;
    private GUIStyle headerStyle;


    // =========================================================
    // PUBLIC API
    // =========================================================

    /// <summary>
    /// Sends one tracking update to the calibration HUD.
    /// </summary>
    public void UpdateCalibrationData(
        DetectionResult raw,
        DetectionResult filtered,
        ViewerPose newRawPose,
        ViewerPose newFinalPose,
        TrackingState state,
        bool measurementAccepted,
        int width,
        int height,
        Vector3 newRigPosition,
        float calibrationConstant,
        bool useSmoothing,
        bool useContinuity)
    {
        frameWidth =
            width;

        frameHeight =
            height;

        rawDetection =
            raw;

        filteredDetection =
            filtered;

        rawPose =
            newRawPose;

        finalPose =
            newFinalPose;

        trackingState =
            state;

        currentMeasurementAccepted =
            measurementAccepted;

        rigPosition =
            newRigPosition;

        configuredCalibrationConstant =
            calibrationConstant;

        smoothingEnabled =
            useSmoothing;

        continuityEnabled =
            useContinuity;


        // -----------------------------------------------------
        // Counters
        // -----------------------------------------------------

        if (raw.detected)
        {
            rawDetectionCount++;
        }


        if (measurementAccepted &&
            filtered.detected)
        {
            acceptedCount++;

            AddSample(
                filtered
            );
        }
        else if (raw.detected)
        {
            rejectedCount++;
        }
    }


    // =========================================================
    // SAMPLE BUFFER
    // =========================================================

    private void AddSample(
        DetectionResult detection)
    {
        samples.Enqueue(
            new CalibrationSample(
                detection.headCenter,
                detection.eyeDistance
            )
        );


        while (samples.Count >
               averagingWindow)
        {
            samples.Dequeue();
        }


        RecalculateStatistics();
    }


    private void RecalculateStatistics()
    {
        if (samples.Count == 0)
        {
            averageHeadCenter =
                Vector2.zero;

            averageEyeDistance =
                0f;

            eyeDistanceStdDev =
                0f;

            return;
        }


        Vector2 centerSum =
            Vector2.zero;

        float distanceSum =
            0f;


        foreach (CalibrationSample sample
                 in samples)
        {
            centerSum +=
                sample.headCenter;

            distanceSum +=
                sample.eyeDistance;
        }


        averageHeadCenter =
            centerSum /
            samples.Count;


        averageEyeDistance =
            distanceSum /
            samples.Count;


        // -----------------------------------------------------
        // Eye-distance standard deviation
        // -----------------------------------------------------

        float variance =
            0f;


        foreach (CalibrationSample sample
                 in samples)
        {
            float delta =
                sample.eyeDistance -
                averageEyeDistance;

            variance +=
                delta * delta;
        }


        variance /=
            samples.Count;


        eyeDistanceStdDev =
            Mathf.Sqrt(
                variance
            );
    }


    // =========================================================
    // STATISTICS RESET
    // =========================================================

    private void ResetStatistics()
    {
        samples.Clear();


        averageHeadCenter =
            Vector2.zero;

        averageEyeDistance =
            0f;

        eyeDistanceStdDev =
            0f;


        rawDetectionCount =
            0;

        acceptedCount =
            0;

        rejectedCount =
            0;
    }


    // =========================================================
    // CENTER REFERENCE
    // =========================================================

    private void CaptureCenterReference()
    {
        if (samples.Count == 0)
            return;


        centerReference =
            averageHeadCenter;

        hasCenterReference =
            true;
    }


    private void ClearCenterReference()
    {
        hasCenterReference =
            false;

        centerReference =
            Vector2.zero;
    }


    // =========================================================
    // GUI
    // =========================================================

    private void OnGUI()
    {
        if (!visible)
            return;


        EnsureStyles();


        GUI.Box(
            panelRect,
            GUIContent.none
        );


        GUILayout.BeginArea(
            new Rect(
                panelRect.x + 10f,
                panelRect.y + 10f,
                panelRect.width - 20f,
                panelRect.height - 20f
            )
        );


        // =====================================================
        // GENERAL
        // =====================================================

        DrawHeader(
            "TRACKING CALIBRATION"
        );


        DrawLine(
            $"Resolution : " +
            $"{frameWidth} x {frameHeight}"
        );


        DrawLine(
            $"Tracking state : " +
            $"{trackingState}"
        );


        DrawLine(
            $"Continuity : " +
            $"{(continuityEnabled ? "ON" : "OFF")}   " +
            $"Smoothing : " +
            $"{(smoothingEnabled ? "ON" : "OFF")}"
        );


        DrawLine(
            $"Current measurement : " +
            $"{(currentMeasurementAccepted ? "ACCEPTED" : "REJECTED / HELD")}"
        );


        GUILayout.Space(8f);


        // =====================================================
        // RAW DETECTION
        // =====================================================

        DrawHeader(
            "RAW DETECTION"
        );


        DrawLine(
            $"Detected : " +
            $"{rawDetection.detected}"
        );


        if (rawDetection.detected)
        {
            DrawLine(
                $"Head center : " +
                $"X {rawDetection.headCenter.x:F1} px   " +
                $"Y {rawDetection.headCenter.y:F1} px"
            );


            DrawLine(
                $"Eye distance : " +
                $"{rawDetection.eyeDistance:F2} px"
            );


            DrawLine(
                $"Head angle : " +
                $"{rawDetection.headAngle:F2} deg"
            );


            // -------------------------------------------------
            // Estimated raw Z
            // -------------------------------------------------

            if (configuredCalibrationConstant > 0f &&
                rawDetection.eyeDistance > 0.001f)
            {
                float rawDistance =
                    configuredCalibrationConstant /
                    rawDetection.eyeDistance;


                DrawLine(
                    $"Raw estimated distance : " +
                    $"{rawDistance * 100f:F1} cm"
                );
            }


            // -------------------------------------------------
            // Offset from image center
            // -------------------------------------------------

            Vector2 imageCenter =
                new Vector2(
                    frameWidth * 0.5f,
                    frameHeight * 0.5f
                );


            Vector2 imageOffset =
                rawDetection.headCenter -
                imageCenter;


            DrawLine(
                $"Offset from image center : " +
                $"X {imageOffset.x:F1} px   " +
                $"Y {imageOffset.y:F1} px"
            );
        }


        GUILayout.Space(8f);


        // =====================================================
        // AVERAGED MEASUREMENTS
        // =====================================================

        DrawHeader(
            $"AVERAGE ({samples.Count}/{averagingWindow})"
        );


        if (samples.Count > 0)
        {
            DrawLine(
                $"Head center AVG : " +
                $"X {averageHeadCenter.x:F2} px   " +
                $"Y {averageHeadCenter.y:F2} px"
            );


            DrawLine(
                $"Eye distance AVG : " +
                $"{averageEyeDistance:F2} px"
            );


            DrawLine(
                $"Eye distance noise : " +
                $"{eyeDistanceStdDev:F2} px"
            );


            float relativeNoise =
                averageEyeDistance > 0.001f
                    ? eyeDistanceStdDev /
                      averageEyeDistance *
                      100f
                    : 0f;


            DrawLine(
                $"Relative Z noise : " +
                $"{relativeNoise:F2} %"
            );


            // -------------------------------------------------
            // Averaged Z estimate
            // -------------------------------------------------

            if (configuredCalibrationConstant > 0f &&
                averageEyeDistance > 0.001f)
            {
                float averageDistance =
                    configuredCalibrationConstant /
                    averageEyeDistance;


                DrawLine(
                    $"Estimated viewer distance : " +
                    $"{averageDistance * 100f:F1} cm"
                );
            }
        }


        GUILayout.Space(8f);


        // =====================================================
        // Z CALIBRATION
        // =====================================================

        DrawHeader(
            "Z CALIBRATION"
        );


        DrawLine(
            $"Configured K : " +
            $"{configuredCalibrationConstant:F3} px*m"
        );


        // Only display physical comparison if the distance
        // was actually measured.
        if (referencePhysicalDistance > 0f &&
            averageEyeDistance > 0.001f)
        {
            DrawLine(
                $"Reference physical distance : " +
                $"{referencePhysicalDistance * 100f:F1} cm"
            );


            float measuredK =
                referencePhysicalDistance *
                averageEyeDistance;


            DrawLine(
                $"K from reference : " +
                $"{measuredK:F3} px*m"
            );


            if (configuredCalibrationConstant > 0f)
            {
                float estimatedDistance =
                    configuredCalibrationConstant /
                    averageEyeDistance;


                float errorMeters =
                    estimatedDistance -
                    referencePhysicalDistance;


                DrawLine(
                    $"Distance error : " +
                    $"{errorMeters * 100f:F1} cm"
                );
            }
        }
        else
        {
            DrawLine(
                "Physical reference : not set"
            );
        }


        GUILayout.Space(8f);


        // =====================================================
        // VIEWER POSE
        // =====================================================

        DrawHeader(
            "VIEWER POSE"
        );


        DrawVector(
            "Raw XYZ",
            rawPose.position
        );


        DrawVector(
            "Filtered XYZ",
            finalPose.position
        );


        DrawVector(
            "StereoRig world",
            rigPosition
        );


        GUILayout.Space(8f);


        // =====================================================
        // XY CALIBRATION
        // =====================================================

        DrawHeader(
            "X / Y CALIBRATION"
        );


        if (!hasCenterReference)
        {
            DrawLine(
                "Center reference : NOT CAPTURED"
            );
        }
        else
        {
            DrawLine(
                $"Center reference : " +
                $"X {centerReference.x:F2} px   " +
                $"Y {centerReference.y:F2} px"
            );


            Vector2 delta =
                averageHeadCenter -
                centerReference;


            DrawLine(
                $"Delta from reference : " +
                $"X {delta.x:F2} px   " +
                $"Y {delta.y:F2} px"
            );


            // -------------------------------------------------
            // Experimental focal length X
            // -------------------------------------------------

            if (knownHorizontalOffsetMeters >
                    0.001f &&
                configuredCalibrationConstant >
                    0f &&
                averageEyeDistance >
                    0.001f)
            {
                float estimatedDistance =
                    configuredCalibrationConstant /
                    averageEyeDistance;


                float fx =
                    Mathf.Abs(delta.x) *
                    estimatedDistance /
                    knownHorizontalOffsetMeters;


                DrawLine(
                    $"Estimated fx : " +
                    $"{fx:F1} px"
                );
            }


            // -------------------------------------------------
            // Experimental focal length Y
            // -------------------------------------------------

            if (knownVerticalOffsetMeters >
                    0.001f &&
                configuredCalibrationConstant >
                    0f &&
                averageEyeDistance >
                    0.001f)
            {
                float estimatedDistance =
                    configuredCalibrationConstant /
                    averageEyeDistance;


                float fy =
                    Mathf.Abs(delta.y) *
                    estimatedDistance /
                    knownVerticalOffsetMeters;


                DrawLine(
                    $"Estimated fy : " +
                    $"{fy:F1} px"
                );
            }
        }


        GUILayout.Space(8f);


        // =====================================================
        // VALIDATION COUNTERS
        // =====================================================

        DrawHeader(
            "VALIDATION"
        );


        DrawLine(
            $"Raw detections : " +
            $"{rawDetectionCount}"
        );


        DrawLine(
            $"Accepted : " +
            $"{acceptedCount}"
        );


        DrawLine(
            $"Rejected : " +
            $"{rejectedCount}"
        );


        if (rawDetectionCount > 0)
        {
            float acceptanceRate =
                acceptedCount /
                (float)rawDetectionCount *
                100f;


            DrawLine(
                $"Acceptance rate : " +
                $"{acceptanceRate:F1} %"
            );
        }


        GUILayout.Space(8f);


        // =====================================================
        // BUTTONS
        // =====================================================

        GUILayout.BeginHorizontal();


        if (GUILayout.Button(
            "Reset averages",
            GUILayout.Height(30f)))
        {
            ResetStatistics();
        }


        if (GUILayout.Button(
            "Capture center",
            GUILayout.Height(30f)))
        {
            CaptureCenterReference();
        }


        if (GUILayout.Button(
            "Clear center",
            GUILayout.Height(30f)))
        {
            ClearCenterReference();
        }


        GUILayout.EndHorizontal();


        GUILayout.EndArea();
    }


    // =========================================================
    // GUI HELPERS
    // =========================================================

    private void EnsureStyles()
    {
        if (labelStyle != null)
            return;


        labelStyle =
            new GUIStyle(
                GUI.skin.label
            );


        labelStyle.fontSize =
            fontSize;


        labelStyle.normal.textColor =
            Color.white;


        headerStyle =
            new GUIStyle(
                labelStyle
            );


        headerStyle.fontStyle =
            FontStyle.Bold;


        headerStyle.fontSize =
            fontSize + 1;
    }


    private void DrawLine(
        string text)
    {
        GUILayout.Label(
            text,
            labelStyle
        );
    }


    private void DrawHeader(
        string text)
    {
        GUILayout.Label(
            text,
            headerStyle
        );
    }


    private void DrawVector(
        string name,
        Vector3 value)
    {
        DrawLine(
            $"{name} : " +
            $"X {value.x:F3}   " +
            $"Y {value.y:F3}   " +
            $"Z {value.z:F3}"
        );
    }


    // =========================================================
    // SAMPLE
    // =========================================================

    private readonly struct CalibrationSample
    {
        public readonly Vector2 headCenter;

        public readonly float eyeDistance;


        public CalibrationSample(
            Vector2 newHeadCenter,
            float newEyeDistance)
        {
            headCenter =
                newHeadCenter;

            eyeDistance =
                newEyeDistance;
        }
    }
}