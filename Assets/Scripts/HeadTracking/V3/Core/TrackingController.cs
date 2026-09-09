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
        if (!frameSource.TryGetFrame(out ImageFrame frame))
            return;

        DetectionResult result = headDetector.Detect(frame, diagnostics);
        ViewerPose pose =
        headPoseMapper.Map(
            result,
            frame.width,
            frame.height
        );

stereoCameraRig.SetViewerPose(
    pose
);
        detectionView.Show(frame, diagnostics, result);
    }

    private void OnDisable()
    {
        if (frameSource != null)
            frameSource.StopSource();
    }
}
