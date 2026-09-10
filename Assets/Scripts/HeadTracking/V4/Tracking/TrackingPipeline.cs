/// <summary>Pure tracking coordinator. No scene, camera, webcam or UI dependencies.</summary>
public sealed class TrackingPipeline
{
    private readonly DetectionContinuityFilter continuity = new DetectionContinuityFilter();
    private readonly HeadPoseMapper mapper = new HeadPoseMapper();
    private readonly ViewerPoseSmoother smoother = new ViewerPoseSmoother();
    private TrackingSnapshot snapshot;
    private float lastFrameTime;
    private bool hasFrame, isStatic;
    private bool lastContinuityEnabled, lastSmoothingEnabled, configured;

    public TrackingSnapshot Snapshot => snapshot;

    public void Reset()
    {
        int nextSession = snapshot.session + 1;
        snapshot = new TrackingSnapshot { session = nextSession };
        continuity.Reset();
        smoother.Reset();
        hasFrame = false;
        configured = false;
    }

    public void Configure(ContinuitySettings temporal, SmoothingSettings smoothing)
    {
        if (configured && lastContinuityEnabled != temporal.enabled) Reset();
        if (configured && lastSmoothingEnabled != smoothing.enabled) smoother.Reset();
        lastContinuityEnabled = temporal.enabled;
        lastSmoothingEnabled = smoothing.enabled;
        configured = true;
    }

    public void Process(DetectionResult raw, int width, int height, float now, bool staticSample,
        ContinuitySettings temporal)
    {
        hasFrame = true;
        isStatic = staticSample;
        lastFrameTime = now;
        snapshot.sampleId++;
        snapshot.frameWidth = width;
        snapshot.frameHeight = height;
        snapshot.sampleTime = now;
        snapshot.rawDetection = raw;
        continuity.Process(raw, width, height, now, temporal);
        snapshot.measurement = continuity.MeasurementAccepted ? MeasurementStatus.Accepted :
            raw.hasCandidate || raw.detected ? MeasurementStatus.Rejected : MeasurementStatus.Missing;
        snapshot.reacquired = continuity.ResetSmoothing;
        if (continuity.ResetSmoothing) smoother.Reset();
    }

    public TrackingSnapshot Tick(float now, float dt, PoseMappingSettings mapping,
        ContinuitySettings temporal, SmoothingSettings smoothing)
    {
        bool stale = hasFrame && (!isStatic || snapshot.measurement != MeasurementStatus.Accepted) &&
            now - lastFrameTime > UnityEngine.Mathf.Max(0.01f, temporal.frameStaleTimeout);
        continuity.Tick(now, stale, temporal);
        snapshot.state = continuity.State;
        snapshot.acceptedDetection = continuity.Accepted;
        snapshot.smoothingEnabled = smoothing.enabled;
        snapshot.continuityEnabled = temporal.enabled;
        snapshot.rawPose = mapper.Map(snapshot.rawDetection, snapshot.frameWidth, snapshot.frameHeight,
            mapping, out MappingStatus rawStatus);
        snapshot.targetPose = mapper.Map(snapshot.acceptedDetection, snapshot.frameWidth, snapshot.frameHeight,
            mapping, out MappingStatus acceptedStatus);
        snapshot.mappingStatus = snapshot.acceptedDetection.detected ? acceptedStatus : rawStatus;

        if (snapshot.state == TrackingState.Tracking && snapshot.targetPose.valid)
            snapshot.filteredPose = smoother.Filter(snapshot.targetPose, dt, smoothing);
        else if (snapshot.state != TrackingState.ShortLost || !snapshot.targetPose.valid)
        {
            // Lost is invalid data. The renderer may keep its last displayed pose independently.
            snapshot.filteredPose = default;
            smoother.Reset();
        }
        // ShortLost freezes the last rendered pose exactly, without continuing interpolation.
        return snapshot;
    }
}
