using UnityEngine;

/// <summary>Scene orchestration only. Algorithms live in plain C# services.</summary>
public sealed class TrackingController : MonoBehaviour
{
    [Header("Scene components")]
    [SerializeField] private FrameSource frameSource;
    [SerializeField] private DetectionView detectionView;
    [SerializeField] private StereoCameraRig stereoCameraRig;
    [SerializeField] private TrackingCalibrationHUD calibrationHUD;
    [Header("Detection")]
    [SerializeField] private DetectionSettings detectionSettings = new DetectionSettings();
    [Header("Tracking")]
    [SerializeField] private ContinuitySettings continuitySettings = new ContinuitySettings();
    [SerializeField] private PoseMappingSettings mappingSettings = new PoseMappingSettings();
    [SerializeField] private SmoothingSettings smoothingSettings = new SmoothingSettings();
    [Header("Simulation")]
    [SerializeField] private bool useTrackingSimulator;
    [SerializeField] private HeadTrackingSimulator trackingSimulator;

    private HeadDetector detector;
    private DetectionSettings detectorSettingsReference;
    private readonly DetectionDiagnostics diagnostics = new DetectionDiagnostics();
    private readonly TrackingPipeline pipeline = new TrackingPipeline();
    private FrameSource activeSource;
    private HeadTrackingSimulator activeSimulator;
    private bool modeKnown, activeSimulation;
    private int sourceRevision = -1, imageWidth, imageHeight;
    private long lastFrameId = -1;
    public PoseMappingSettings MappingSettings => mappingSettings;
    public TrackingSnapshot Snapshot => pipeline.Snapshot;
    public string SourceStatus => activeSimulation ? "Simulator" : activeSource != null ? activeSource.Status : "No active source";
    public StereoCameraRig StereoRig => stereoCameraRig;

    private void Awake()
    {
        if (frameSource == null) frameSource = GetComponent<FrameSource>();
        if (detectionView == null) detectionView = GetComponent<DetectionView>();
        EnsureSettings();
        if (stereoCameraRig == null) Debug.LogWarning("TrackingController: no stereo rig; detection diagnostics remain available.", this);
    }

    private void OnEnable() { modeKnown = false; ResetTracking(); }

    private void Update()
    {
        EnsureSettings();
        SelectInput();
        pipeline.Configure(continuitySettings, smoothingSettings);
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        if (activeSimulation)
        {
            if (activeSimulator != null && activeSimulator.TryGetDetection(now, dt, out DetectionResult raw))
            {
                CheckGeometry(activeSimulator.Revision, activeSimulator.FrameWidth, activeSimulator.FrameHeight);
                // The simulator bypasses image processing but uses the same final quality threshold.
                raw.detected &= TrackingMath.IsFinite(raw.pairScore) && raw.pairScore <= detectionSettings.pairing.maxAcceptedScore;
                pipeline.Process(raw, imageWidth, imageHeight, now, false, continuitySettings);
            }
            else if (activeSimulator != null && sourceRevision != activeSimulator.Revision)
                ResetTracking();
        }
        else if (activeSource != null)
        {
            bool available = activeSource.TryGetFrame(out ImageFrame frame);
            if (sourceRevision != activeSource.Revision) ResetTracking();
            sourceRevision = activeSource.Revision;
            if (available && frame.IsValid)
            {
                CheckGeometry(sourceRevision, frame.Width, frame.Height);
                if (frame.Id != lastFrameId)
                {
                    lastFrameId = frame.Id;
                    bool showPreview = detectionView != null && detectionView.ShouldRefresh(now);
                    DetectionResult raw = detector.Detect(frame, diagnostics, showPreview);
                    pipeline.Process(raw, frame.Width, frame.Height, now, activeSource.IsStatic, continuitySettings);
                    if (showPreview) detectionView.Show(frame, diagnostics, raw);
                }
            }
        }
        TrackingSnapshot snapshot = pipeline.Tick(now, dt, mappingSettings, continuitySettings, smoothingSettings);
        if (stereoCameraRig != null && snapshot.filteredPose.valid) stereoCameraRig.SetViewerPose(snapshot.filteredPose);
        if (calibrationHUD != null) calibrationHUD.Present(snapshot, mappingSettings,
            stereoCameraRig != null ? stereoCameraRig.transform.position : Vector3.zero, SourceStatus);
    }

    private void SelectInput()
    {
        FrameSource desiredSource = !useTrackingSimulator && frameSource != null && frameSource.isActiveAndEnabled ? frameSource : null;
        HeadTrackingSimulator desiredSimulator = useTrackingSimulator && trackingSimulator != null && trackingSimulator.isActiveAndEnabled ? trackingSimulator : null;
        if (modeKnown && activeSimulation == useTrackingSimulator && activeSource == desiredSource && activeSimulator == desiredSimulator) return;
        if (activeSource != null) activeSource.StopSource();
        activeSource = desiredSource;
        activeSimulator = desiredSimulator;
        activeSimulation = useTrackingSimulator;
        modeKnown = true;
        ResetTracking();
        if (activeSource != null) activeSource.StartSource();
        if (activeSimulator != null) activeSimulator.BeginSession();
        if (activeSource == null && activeSimulator == null)
            Debug.LogWarning("TrackingController: selected input is missing or disabled.", this);
    }

    private void CheckGeometry(int revision, int width, int height)
    {
        if (sourceRevision != revision || imageWidth != width || imageHeight != height) ResetTracking();
        sourceRevision = revision;
        imageWidth = width;
        imageHeight = height;
    }

    private void EnsureSettings()
    {
        if (detectionSettings == null) detectionSettings = new DetectionSettings();
        if (detectionSettings.pairing == null) detectionSettings.pairing = new GlassesPairSettings();
        if (detectionSettings.segmentation == null) detectionSettings.segmentation = new RegionSegmentationSettings();
        if (mappingSettings == null) mappingSettings = new PoseMappingSettings();
        if (continuitySettings == null) continuitySettings = new ContinuitySettings();
        if (smoothingSettings == null) smoothingSettings = new SmoothingSettings();
        if (detector == null || detectorSettingsReference != detectionSettings)
        {
            detectorSettingsReference = detectionSettings;
            detector = new HeadDetector(detectionSettings);
        }
    }

    [ContextMenu("Reset tracking")]
    public void ResetTracking()
    {
        pipeline.Reset();
        sourceRevision = -1;
        imageWidth = imageHeight = 0;
        lastFrameId = -1;
        if (detectionView != null) detectionView.Clear();
    }

    private void OnDisable()
    {
        if (activeSource != null) activeSource.StopSource();
        activeSource = null;
        activeSimulator = null;
        modeKnown = false;
        ResetTracking();
    }
}
