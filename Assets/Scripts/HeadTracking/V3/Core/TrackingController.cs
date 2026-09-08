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

    private HeadDetector headDetector;
    private readonly DetectionDiagnostics diagnostics = new DetectionDiagnostics();

    private void Awake()
    {
        if (frameSource == null)
            frameSource = GetComponent<FrameSource>();

        if (detectionView == null)
            detectionView = GetComponent<DetectionView>();

        headDetector = new HeadDetector(detectionSettings);
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
        detectionView.Show(frame, diagnostics, result);
    }

    private void OnDisable()
    {
        if (frameSource != null)
            frameSource.StopSource();
    }
}
