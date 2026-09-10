using System.Text;
using UnityEngine;

/// <summary>Calibration diagnostics. Capturing a reference never silently changes the optical calibration.</summary>
public sealed class TrackingCalibrationHUD : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private bool visible = true;
    [SerializeField] private Rect panelRect = new Rect(15f, 15f, 540f, 690f);
    [SerializeField] private int fontSize = 16;
    [Min(1f)] [SerializeField] private float refreshRate = 5f;
    [Header("Optional physical references")]
    [Tooltip("Axial viewer distance from the webcam in meters. Zero disables the physical comparison.")]
    [Min(0f)] [SerializeField] private float referencePhysicalDistance;
    [Tooltip("Actual displacement magnitude between captured and current positions. Zero disables fx estimation.")]
    [Min(0f)] [SerializeField] private float knownHorizontalOffsetMeters;
    [Min(0f)] [SerializeField] private float knownVerticalOffsetMeters;
    [Header("Statistics")]
    [Range(5, 600)] [SerializeField] private int averagingWindow = 30;

    private readonly CalibrationStatistics statistics = new CalibrationStatistics();
    private readonly StringBuilder text = new StringBuilder(2400);
    private TrackingSnapshot snapshot;
    private PoseMappingSettings mapping;
    private Vector3 rigPosition;
    private string sourceStatus = "Waiting";
    private long lastSampleId = -1, frameCount, pairCount, acceptedCount, rejectedCount, missingCount;
    private int lastSession = -1;
    private bool hasCenterReference;
    private Vector2 centerReference, referenceCenterOverEye;
    private float referenceInverseEye;
    private float nextRefresh;
    private string cachedText = "Waiting for tracking data";
    private GUIStyle style;
    private Vector2 scroll;

    public void Present(TrackingSnapshot data, PoseMappingSettings settings, Vector3 rigWorldPosition, string status)
    {
        mapping = settings;
        rigPosition = rigWorldPosition;
        sourceStatus = status;
        if (lastSession != data.session)
        {
            ResetStatistics();
            ClearCenterReference();
            lastSession = data.session;
            lastSampleId = -1;
        }
        snapshot = data;
        statistics.Configure(averagingWindow);
        if (data.sampleId <= 0 || data.sampleId == lastSampleId) return;
        lastSampleId = data.sampleId;
        frameCount++;
        if (data.rawDetection.hasCandidate || data.rawDetection.detected) pairCount++;
        if (data.measurement == MeasurementStatus.Accepted)
        {
            // Do not mix pre-loss and post-reacquisition measurements in one average.
            if (data.reacquired) statistics.Reset();
            acceptedCount++;
            statistics.Add(data.acceptedDetection);
        }
        else if (data.measurement == MeasurementStatus.Rejected) rejectedCount++;
        else missingCount++;
    }

    private void Update()
    {
        if (!visible || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 1f / Mathf.Max(1f, refreshRate);
        BuildText();
    }

    private void BuildText()
    {
        if (mapping == null) return;
        text.Clear();
        text.AppendLine("TRACKING CALIBRATION");
        text.AppendLine($"Source: {sourceStatus}");
        text.AppendLine($"Resolution: {snapshot.frameWidth} x {snapshot.frameHeight} | {snapshot.state}");
        text.AppendLine($"Continuity: {(snapshot.continuityEnabled ? "ON" : "OFF")} | Smoothing: {(snapshot.smoothingEnabled ? "ON" : "OFF")}");
        bool fresh = snapshot.state == TrackingState.Tracking && snapshot.measurement == MeasurementStatus.Accepted;
        text.AppendLine($"Last sample: {snapshot.measurement.ToString().ToUpperInvariant()} | age: {Mathf.Max(0f, Time.unscaledTime - snapshot.sampleTime):F2} s");
        text.AppendLine($"Mapping: {snapshot.mappingStatus} | XY: {mapping.xyMode}");
        DetectionResult raw = snapshot.rawDetection;
        text.AppendLine("\nRAW DETECTION (last processed frame)");
        text.AppendLine($"Detected: {raw.detected} | candidate: {raw.hasCandidate} | score: {(raw.hasCandidate ? raw.pairScore.ToString("F3") : "n/a")}");
        if (raw.hasCandidate || raw.detected)
        {
            text.AppendLine($"HeadCenter: {raw.headCenter.x:F2}, {raw.headCenter.y:F2} px");
            text.AppendLine($"EyeDistance: {raw.eyeDistance:F2} px | HeadAngle: {raw.headAngle:F2} deg");
            if (raw.eyeDistance > 0f) text.AppendLine($"Instant Z, unclamped: {mapping.distanceCalibrationConstant / raw.eyeDistance:F3} m");
            Vector2 delta = raw.headCenter - new Vector2(snapshot.frameWidth, snapshot.frameHeight) * 0.5f;
            text.AppendLine($"Image-center offset: {delta.x:F2}, {delta.y:F2} px");
        }
        text.AppendLine($"\nACCEPTED WINDOW ({statistics.Count}/{averagingWindow}){(fresh ? "" : " - HELD / NOT CURRENT")}");
        Vector2 mean = statistics.MeanCenter;
        float k = mapping.distanceCalibrationConstant;
        float meanZ = k * statistics.MeanInverseEye;
        text.AppendLine($"HeadCenter mean: {mean.x:F2}, {mean.y:F2} px");
        text.AppendLine($"EyeDistance mean / std: {statistics.MeanEyeDistance:F2} / {statistics.EyeDistanceStdDev:F2} px");
        float relativeNoise = statistics.MeanInverseEye > 0f ? statistics.InverseEyeStdDev / statistics.MeanInverseEye * 100f : 0f;
        text.AppendLine($"Z mean: {meanZ:F3} m | relative Z noise: {relativeNoise:F2} %");
        text.AppendLine($"Configured K: {k:F3} px*m | clamp: {mapping.minViewerDistance:F2} .. {mapping.maxViewerDistance:F2} m");
        if (referencePhysicalDistance > 0f && statistics.Count > 0)
        {
            text.AppendLine($"Physical reference: {referencePhysicalDistance:F3} m | K estimate: {referencePhysicalDistance * statistics.MeanEyeDistance:F3}");
            text.AppendLine($"Z error: {(meanZ - referencePhysicalDistance) * 100f:F2} cm");
        }
        else text.AppendLine("Physical distance reference: not set");
        text.AppendLine("\nPOSES (screen-axis meters)");
        AppendPose("Raw", snapshot.rawPose);
        AppendPose("Accepted target", snapshot.targetPose);
        AppendPose("Filtered", snapshot.filteredPose);
        text.AppendLine($"StereoRig world: {rigPosition.x:F3}, {rigPosition.y:F3}, {rigPosition.z:F3}");
        text.AppendLine("\nXY CALIBRATION");
        text.AppendLine($"Optical center: {mapping.calibratedCenterX:F2}, {mapping.calibratedCenterY:F2} px");
        text.AppendLine($"Configured fx/fy: {mapping.focalX:F2} / {mapping.focalY:F2} px");
        text.AppendLine($"Webcam offset: {mapping.webcamOffsetFromScreen.x:F3}, {mapping.webcamOffsetFromScreen.y:F3}, {mapping.webcamOffsetFromScreen.z:F3} m");
        if (hasCenterReference)
        {
            Vector2 delta = mean - centerReference;
            text.AppendLine($"Captured reference: {centerReference.x:F2}, {centerReference.y:F2} px");
            text.AppendLine($"Delta XY: {delta.x:F2}, {delta.y:F2} px");
            text.AppendLine($"Reference Z / current Z: {k * referenceInverseEye:F3} / {meanZ:F3} m");
            Vector2 opticalCenter = new Vector2(mapping.calibratedCenterX, mapping.calibratedCenterY);
            // Difference of Z * (u-cx), averaged per sample; valid beyond equal-depth trials if cx is known.
            Vector2 pixelDepthDelta = k * (statistics.MeanCenterOverEye - referenceCenterOverEye -
                opticalCenter * (statistics.MeanInverseEye - referenceInverseEye));
            AppendFocal("fx", pixelDepthDelta.x, delta.x, knownHorizontalOffsetMeters, fresh);
            AppendFocal("fy", pixelDepthDelta.y, delta.y, knownVerticalOffsetMeters, fresh);
        }
        else text.AppendLine("XY reference: not captured");
        text.AppendLine("Capture stores a measurement reference only; set optical calibration in Inspector.");
        text.AppendLine("\nCOUNTERS (processed frames only)");
        text.AppendLine($"Frames: {frameCount} | candidate pairs: {pairCount} | missing: {missingCount}");
        text.AppendLine($"Accepted: {acceptedCount} | rejected: {rejectedCount}");
        text.AppendLine($"Acceptance among candidates: {(pairCount == 0 ? 0.0 : 100.0 * acceptedCount / pairCount):F1} %");
        cachedText = text.ToString();
    }

    private void AppendPose(string label, ViewerPose pose)
    {
        text.AppendLine(pose.valid ? $"{label}: {pose.position.x:F3}, {pose.position.y:F3}, {pose.position.z:F3} | roll {pose.rollDegrees:F1}" : label + ": INVALID (render may hold last pose)");
    }
    private void AppendFocal(string label, float pixelDepthDelta, float pixelDelta, float physicalDelta, bool fresh)
    {
        if (fresh && statistics.Count >= 5 && physicalDelta > 0.001f && Mathf.Abs(pixelDelta) >= 3f)
            text.AppendLine($"Estimated {label}: {Mathf.Abs(pixelDepthDelta) / physicalDelta:F2} px for {physicalDelta:F3} m");
        else text.AppendLine("Estimated " + label + ": n/a (need live samples and a measured displacement)");
    }

    public void ResetStatistics()
    {
        statistics.Reset();
        frameCount = pairCount = acceptedCount = rejectedCount = missingCount = 0;
        nextRefresh = 0f;
    }
    public void CaptureCenterReference()
    {
        if (snapshot.state != TrackingState.Tracking || snapshot.measurement != MeasurementStatus.Accepted || statistics.Count < 5) return;
        centerReference = statistics.MeanCenter;
        referenceInverseEye = statistics.MeanInverseEye;
        referenceCenterOverEye = statistics.MeanCenterOverEye;
        hasCenterReference = true;
        nextRefresh = 0f;
    }
    public void ClearCenterReference() { hasCenterReference = false; nextRefresh = 0f; }

    private void OnGUI()
    {
        if (!visible) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { wordWrap = true };
        style.fontSize = Mathf.Max(10, fontSize);
        Rect rect = panelRect;
        rect.width = Mathf.Min(rect.width, Mathf.Max(200f, Screen.width - rect.x - 5f));
        rect.height = Mathf.Min(rect.height, Mathf.Max(180f, Screen.height - rect.y - 5f));
        GUILayout.BeginArea(rect, GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label(cachedText, style);
        GUILayout.EndScrollView();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset averages")) ResetStatistics();
        if (GUILayout.Button("Capture center")) CaptureCenterReference();
        if (GUILayout.Button("Clear center")) ClearCenterReference();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
