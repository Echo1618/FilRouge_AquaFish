using UnityEngine;

/// <summary>
/// Rejects temporally inconsistent detections before they are
/// converted into a ViewerPose.
///
/// Responsibilities:
/// - Initial acquisition near the image center.
/// - Reject large head-center jumps.
/// - Reject sudden eye-distance changes.
/// - Hold the last accepted detection during short losses.
/// - Reacquire a new target after several consistent frames.
///
/// This class does not smooth valid measurements.
/// Smoothing remains the responsibility of ViewerPoseSmoother.
/// </summary>
public sealed class DetectionContinuityFilter
{
    // =========================================================
    // SETTINGS
    // =========================================================

    private float initialCenterRadiusRatio = 0.35f;

    private float centerJumpFactor = 0.45f;
    private float minCenterJumpPixels = 20f;
    private float maxCenterJumpPixels = 90f;

    private float maxEyeDistanceRelativeChange = 0.15f;

    private float shortLossTimeout = 0.18f;

    private int reacquisitionFrames = 3;


    // =========================================================
    // STATE
    // =========================================================

    private DetectionResult lastAccepted;

    private DetectionResult pendingCandidate;

    private bool hasAccepted;
    private bool hasPending;

    private int pendingFrames;

    private float lastAcceptedTime;


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public TrackingState State { get; private set; } =
        TrackingState.Searching;


    // =========================================================
    // CONFIGURATION
    // =========================================================

    public void Configure(
        float newInitialCenterRadiusRatio,
        float newCenterJumpFactor,
        float newMinCenterJumpPixels,
        float newMaxCenterJumpPixels,
        float newMaxEyeDistanceRelativeChange,
        float newShortLossTimeout,
        int newReacquisitionFrames)
    {
        initialCenterRadiusRatio =
            Mathf.Clamp01(newInitialCenterRadiusRatio);

        centerJumpFactor =
            Mathf.Max(0.01f, newCenterJumpFactor);

        minCenterJumpPixels =
            Mathf.Max(1f, newMinCenterJumpPixels);

        maxCenterJumpPixels =
            Mathf.Max(
                minCenterJumpPixels,
                newMaxCenterJumpPixels
            );

        maxEyeDistanceRelativeChange =
            Mathf.Clamp01(
                newMaxEyeDistanceRelativeChange
            );

        shortLossTimeout =
            Mathf.Max(
                0f,
                newShortLossTimeout
            );

        reacquisitionFrames =
            Mathf.Max(
                1,
                newReacquisitionFrames
            );
    }


    // =========================================================
    // FILTER
    // =========================================================

    /// <summary>
    /// Returns true when a detection can be used by the rig.
    ///
    /// During a short loss, the previous accepted result is
    /// returned so the camera remains stable.
    ///
    /// resetSmoothing becomes true after initial acquisition
    /// or reacquisition.
    /// </summary>
    public bool Filter(
        DetectionResult raw,
        int frameWidth,
        int frameHeight,
        float timestamp,
        out DetectionResult filtered,
        out bool resetSmoothing)
    {
        filtered = default;
        resetSmoothing = false;


        // ---------------------------------------------------------
        // No valid raw detection
        // ---------------------------------------------------------

        if (!raw.detected)
        {
            return HandleMissingDetection(
                timestamp,
                out filtered
            );
        }


        // ---------------------------------------------------------
        // First acquisition
        // ---------------------------------------------------------

        if (!hasAccepted)
        {
            return TryInitialAcquisition(
                raw,
                frameWidth,
                frameHeight,
                timestamp,
                out filtered,
                out resetSmoothing
            );
        }


        // ---------------------------------------------------------
        // Normal tracking
        // ---------------------------------------------------------

        if (IsContinuous(
            raw,
            lastAccepted))
        {
            Accept(
                raw,
                timestamp
            );


            filtered =
                lastAccepted;

            resetSmoothing =
                false;

            State =
                TrackingState.Tracking;


            ClearPending();

            return true;
        }


        // ---------------------------------------------------------
        // Candidate jumped too far.
        //
        // Do not immediately follow it.
        // Check whether it remains consistent for several frames.
        // ---------------------------------------------------------

        UpdateReacquisitionCandidate(
            raw
        );


        if (pendingFrames >= reacquisitionFrames)
        {
            Accept(
                pendingCandidate,
                timestamp
            );


            filtered =
                lastAccepted;

            resetSmoothing =
                true;

            State =
                TrackingState.Tracking;


            ClearPending();

            return true;
        }


        // Keep last accepted pose while candidate is uncertain.
        return HoldPrevious(
            timestamp,
            out filtered
        );
    }


    // =========================================================
    // INITIAL ACQUISITION
    // =========================================================

    private bool TryInitialAcquisition(
        DetectionResult raw,
        int frameWidth,
        int frameHeight,
        float timestamp,
        out DetectionResult filtered,
        out bool resetSmoothing)
    {
        filtered = default;
        resetSmoothing = false;


        Vector2 imageCenter =
            new Vector2(
                frameWidth * 0.5f,
                frameHeight * 0.5f
            );


        float radius =
            Mathf.Min(
                frameWidth,
                frameHeight
            ) *
            initialCenterRadiusRatio;


        float distanceToCenter =
            Vector2.Distance(
                raw.headCenter,
                imageCenter
            );


        if (distanceToCenter > radius)
        {
            State =
                TrackingState.Searching;

            return false;
        }


        Accept(
            raw,
            timestamp
        );


        filtered =
            lastAccepted;

        resetSmoothing =
            true;

        State =
            TrackingState.Tracking;


        return true;
    }


    // =========================================================
    // CONTINUITY
    // =========================================================

    private bool IsContinuous(
        DetectionResult current,
        DetectionResult previous)
    {
        if (!current.detected ||
            !previous.detected)
        {
            return false;
        }


        // ---------------------------------------------------------
        // XY continuity
        // ---------------------------------------------------------

        float centerJump =
            Vector2.Distance(
                current.headCenter,
                previous.headCenter
            );


        // Adapt allowed motion to apparent glasses size.
        float allowedCenterJump =
            previous.eyeDistance *
            centerJumpFactor;


        allowedCenterJump =
            Mathf.Clamp(
                allowedCenterJump,
                minCenterJumpPixels,
                maxCenterJumpPixels
            );


        if (centerJump >
            allowedCenterJump)
        {
            return false;
        }


        // ---------------------------------------------------------
        // Z / eye-distance continuity
        // ---------------------------------------------------------

        if (previous.eyeDistance <= 0.001f ||
            current.eyeDistance <= 0.001f)
        {
            return false;
        }


        float relativeDistanceChange =
            Mathf.Abs(
                current.eyeDistance -
                previous.eyeDistance
            ) /
            previous.eyeDistance;


        if (relativeDistanceChange >
            maxEyeDistanceRelativeChange)
        {
            return false;
        }


        return true;
    }


    // =========================================================
    // REACQUISITION
    // =========================================================

    private void UpdateReacquisitionCandidate(
        DetectionResult candidate)
    {
        if (!hasPending)
        {
            pendingCandidate =
                candidate;

            pendingFrames = 1;
            hasPending = true;

            return;
        }


        // Candidate must also be coherent with the previous
        // reacquisition candidate.
        if (IsContinuous(
            candidate,
            pendingCandidate))
        {
            pendingCandidate =
                candidate;

            pendingFrames++;

            return;
        }


        // Different candidate:
        // start confirmation again from this frame.
        pendingCandidate =
            candidate;

        pendingFrames =
            1;
    }


    // =========================================================
    // LOSS HANDLING
    // =========================================================

    private bool HandleMissingDetection(
        float timestamp,
        out DetectionResult filtered)
    {
        ClearPending();


        if (!hasAccepted)
        {
            State =
                TrackingState.Searching;

            filtered =
                default;

            return false;
        }


        return HoldPrevious(
            timestamp,
            out filtered
        );
    }


    private bool HoldPrevious(
        float timestamp,
        out DetectionResult filtered)
    {
        float lostDuration =
            timestamp -
            lastAcceptedTime;


        if (lostDuration <=
            shortLossTimeout)
        {
            State =
                TrackingState.ShortLost;
        }
        else
        {
            State =
                TrackingState.Lost;
        }


        // For now we intentionally keep the last pose.
        filtered =
            lastAccepted;

        return true;
    }


    // =========================================================
    // ACCEPTANCE
    // =========================================================

    private void Accept(
        DetectionResult detection,
        float timestamp)
    {
        lastAccepted =
            detection;

        lastAcceptedTime =
            timestamp;

        hasAccepted =
            true;
    }


    // =========================================================
    // RESET
    // =========================================================

    public void Reset()
    {
        lastAccepted =
            default;

        pendingCandidate =
            default;

        hasAccepted =
            false;

        hasPending =
            false;

        pendingFrames =
            0;

        lastAcceptedTime =
            0f;

        State =
            TrackingState.Searching;
    }


    private void ClearPending()
    {
        pendingCandidate =
            default;

        pendingFrames =
            0;

        hasPending =
            false;
    }
}


// =============================================================
// TRACKING STATE
// =============================================================

public enum TrackingState
{
    Searching,
    Tracking,
    ShortLost,
    Lost
}