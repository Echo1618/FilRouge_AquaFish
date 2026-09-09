using UnityEngine;

/// <summary>
/// Scene-level coordinator: source -> detector -> view.
/// </summary>
public sealed class TrackingController : MonoBehaviour
{
    [Header("Scene components")]
    [SerializeField] private FrameSource frameSource;
    [SerializeField] private DetectionView detectionView;

    [Header("Tracking Axis")]

    [SerializeField]
    private bool invertTrackingX = true;

    [SerializeField]
    private bool invertTrackingY = false;

    [Header("Detection")]
    [SerializeField] private DetectionSettings detectionSettings = new DetectionSettings();

    [Header("Stereo Tracking")]
    [SerializeField]
    private StereoCameraRig stereoCameraRig;

    [SerializeField]
    private float maxHorizontal = 0.40f;

    [SerializeField]
    private float maxVertical = 0.25f;

    private HeadDetector headDetector;
    private readonly DetectionDiagnostics diagnostics = new DetectionDiagnostics();

    private HeadPoseMapper headPoseMapper;
    private ViewerPoseSmoother poseSmoother;
    private DetectionContinuityFilter continuityFilter;


    // =========================================================
    // TRACKING SMOOTHING
    // =========================================================

    [Header("Tracking Smoothing")]

    [SerializeField]
    private bool usePoseSmoothing = true;

    [Tooltip(
        "XY response speed. Higher = faster but less stable."
    )]
    [SerializeField]
    [Min(0.01f)]
    private float xySmoothingResponse = 7f;

    [Tooltip(
        "Depth response speed. Higher = faster but less stable."
    )]
    [SerializeField]
    [Min(0.01f)]
    private float zSmoothingResponse = 4f;

    // =========================================================
    // TRACKING CONTINUITY
    // =========================================================

    [Header("Tracking Continuity")]

    [SerializeField]
    private bool useContinuityFilter = true;


    [Tooltip(
        "Initial acquisition radius relative to the smallest image dimension."
    )]
    [Range(0.1f, 1f)]
    [SerializeField]
    private float initialCenterRadiusRatio = 0.35f;


    [Tooltip(
        "Maximum center jump relative to the current eye distance."
    )]
    [SerializeField]
    private float centerJumpFactor = 0.45f;


    [SerializeField]
    private float minCenterJumpPixels = 20f;


    [SerializeField]
    private float maxCenterJumpPixels = 90f;


    [Tooltip(
        "Maximum eye-distance change accepted between measurements."
    )]
    [Range(0.01f, 1f)]
    [SerializeField]
    private float maxEyeDistanceRelativeChange = 0.15f;


    [Tooltip(
        "Time during which the previous pose is considered a short loss."
    )]
    [SerializeField]
    private float shortLossTimeout = 0.18f;


    [Tooltip(
        "Number of consistent frames required to accept a new distant target."
    )]
    [Range(1, 10)]
    [SerializeField]
    private int reacquisitionFrames = 3;
    
    [Header("Z Calibration")]

    [Tooltip(
        "Calibration constant K in px*m. " +
        "Viewer distance = K / detected eye distance."
    )]
    [SerializeField]
    private float distanceCalibrationConstant = 67.0f;

    [SerializeField]
    private float minViewerDistance = 0.35f;

    [SerializeField]
    private float maxViewerDistance = 0.90f;

    // =========================================================
    // SIMULATION
    // =========================================================

    [Header("Simulation")]
    [SerializeField] private bool useTrackingSimulator = false;

    [SerializeField] private HeadTrackingSimulator trackingSimulator;

    [Header("Calibration")]

    [SerializeField]
    private TrackingCalibrationHUD calibrationHUD;

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
        distanceCalibrationConstant,
        minViewerDistance,
        maxViewerDistance,
        invertTrackingX,
        invertTrackingY
    );
        poseSmoother = new ViewerPoseSmoother();

        continuityFilter = new DetectionContinuityFilter();
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
    DetectionResult rawResult,
    int frameWidth,
    int frameHeight)
{
    if (stereoCameraRig == null)
        return;


    float now =
        Time.unscaledTime;


    // =========================================================
    // 1 - RAW POSE
    // =========================================================

    ViewerPose rawPose =
        headPoseMapper.Map(
            rawResult,
            frameWidth,
            frameHeight
        );


    // =========================================================
    // 2 - CONTINUITY FILTER
    // =========================================================

    DetectionResult filteredResult =
        rawResult;


    bool resetSmoothing =
        false;


    bool hasTrackingResult =
        rawResult.detected;


    bool currentMeasurementAccepted =
        rawResult.detected;


    TrackingState trackingState =
        rawResult.detected
            ? TrackingState.Tracking
            : TrackingState.Searching;


    if (useContinuityFilter)
    {
        continuityFilter.Configure(
            initialCenterRadiusRatio,
            centerJumpFactor,
            minCenterJumpPixels,
            maxCenterJumpPixels,
            maxEyeDistanceRelativeChange,
            shortLossTimeout,
            reacquisitionFrames
        );


        hasTrackingResult =
            continuityFilter.Filter(
                rawResult,
                frameWidth,
                frameHeight,
                now,
                out filteredResult,
                out resetSmoothing
            );


        trackingState =
            continuityFilter.State;


        currentMeasurementAccepted =
            rawResult.detected &&
            trackingState ==
            TrackingState.Tracking;
    }


    // =========================================================
    // 3 - FILTERED POSE
    // =========================================================

    ViewerPose filteredPose =
        hasTrackingResult
            ? headPoseMapper.Map(
                filteredResult,
                frameWidth,
                frameHeight
            )
            : new ViewerPose(
                false,
                Vector3.zero
            );


    // =========================================================
    // 4 - SMOOTHING
    // =========================================================

    ViewerPose finalPose =
        filteredPose;


    if (resetSmoothing &&
        poseSmoother != null)
    {
        poseSmoother.Reset();
    }


    if (hasTrackingResult &&
        filteredPose.valid &&
        usePoseSmoothing)
    {
        poseSmoother.Configure(
            xySmoothingResponse,
            zSmoothingResponse
        );


        finalPose =
            poseSmoother.Filter(
                filteredPose,
                now
            );
    }


    // =========================================================
    // 5 - STEREO RIG
    // =========================================================

    if (hasTrackingResult &&
        finalPose.valid)
    {
        stereoCameraRig.SetViewerPose(
            finalPose
        );
    }


    // =========================================================
    // 6 - CALIBRATION HUD
    // =========================================================

    if (calibrationHUD != null)
    {
        calibrationHUD.UpdateCalibrationData(
            rawResult,
            filteredResult,
            rawPose,
            finalPose,
            trackingState,
            currentMeasurementAccepted,
            frameWidth,
            frameHeight,
            stereoCameraRig.transform.position,
            distanceCalibrationConstant,
            usePoseSmoothing,
            useContinuityFilter
        );
    }
}

    private void OnDisable()
    {
        if (frameSource != null)
            frameSource.StopSource();
    }
}
