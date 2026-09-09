using UnityEngine;

/// <summary>
/// Scene-level coordinator: source -> detector -> view.
/// </summary>
public sealed class TrackingController : MonoBehaviour
{
    [Header("Scene components")]
    [SerializeField] private FrameSource frameSource;
    [SerializeField] private DetectionView detectionView;

    [Header("Detection")]
    [SerializeField] private DetectionSettings detectionSettings = new DetectionSettings();

    [Header("Stereo Tracking")]
    [SerializeField]
    private StereoCameraRig stereoCameraRig;

    [SerializeField]
    private float maxHorizontal = 0.40f;

    [SerializeField]
    private float maxVertical = 0.25f;

    [SerializeField]
    private float calibrationDistance = 0.70f;

    [SerializeField]
    private float calibrationEyeDistance = 120f;

    [SerializeField]
    private float minViewerDistance = 0.30f;

    [SerializeField]
    private float maxViewerDistance = 2.0f;

    private HeadDetector headDetector;
    private readonly DetectionDiagnostics diagnostics = new DetectionDiagnostics();

    private HeadPoseMapper headPoseMapper;

    // =========================================================
    // SIMULATION
    // =========================================================

    [Header("Simulation")]
    [SerializeField] private bool useTrackingSimulator = false;

    [SerializeField] private HeadTrackingSimulator trackingSimulator;

    private void Awake()
    {
        if (frameSource == null)
            frameSource = GetComponent<FrameSource>();

        if (detectionView == null)
            detectionView = GetComponent<DetectionView>();

        headDetector = new HeadDetector(detectionSettings);

        headPoseMapper =
        new HeadPoseMapper(
            maxHorizontal,
            maxVertical,
            calibrationDistance,
            calibrationEyeDistance,
            minViewerDistance,
            maxViewerDistance
    );
    }

    private void OnEnable()
    {
        if (frameSource == null)
        {
            Debug.LogError("TrackingController: no FrameSource assigned.");
            enabled = false;
            return;
        }

        if (detectionView == null)
        {
            Debug.LogError("TrackingController: no DetectionView assigned.");
            enabled = false;
            return;
        }

        frameSource.StartSource();
    }

    private void Update()
    {
        // ---------------------------------------------------------
        // SIMULATION MODE
        // ---------------------------------------------------------

        if (useTrackingSimulator)
        {
            UpdateSimulation();
            return;
        }


        // ---------------------------------------------------------
        // REAL DETECTION MODE
        // ---------------------------------------------------------

        if (!frameSource.TryGetFrame(out ImageFrame frame))
            return;


        DetectionResult result =
            headDetector.Detect(
                frame,
                diagnostics
            );


        UpdateStereoRig(
            result,
            frame.Width,
            frame.Height
        );


        detectionView.Show(
            frame,
            diagnostics,
            result
        );
    }

    private void UpdateSimulation()
    {
        if (trackingSimulator == null)
            return;


        DetectionResult result =
            trackingSimulator.GetDetectionResult();


        UpdateStereoRig(
            result,
            trackingSimulator.FrameWidth,
            trackingSimulator.FrameHeight
        );
    }

    private void UpdateStereoRig(
    DetectionResult result,
    int frameWidth,
    int frameHeight)
    {
        if (stereoCameraRig == null)
            return;


        ViewerPose pose =
            headPoseMapper.Map(
                result,
                frameWidth,
                frameHeight
            );


        stereoCameraRig.SetViewerPose(
            pose
        );
    }

    private void OnDisable()
    {
        if (frameSource != null)
            frameSource.StopSource();
    }
}
