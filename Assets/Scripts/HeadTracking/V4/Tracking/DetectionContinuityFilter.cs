using UnityEngine;

/// <summary>Temporal acceptance only; no interpolation or pose mapping.</summary>
public sealed class DetectionContinuityFilter
{
    private DetectionResult lastAccepted, pendingAnchor, pendingLast;
    private bool hasAccepted, hasPending;
    private int pendingFrames;
    private float lastAcceptedTime, pendingTime;

    public TrackingState State { get; private set; } = TrackingState.Searching;
    public DetectionResult Accepted => lastAccepted;
    public bool MeasurementAccepted { get; private set; }
    public bool ResetSmoothing { get; private set; }

    public void Process(DetectionResult raw, int width, int height, float now, ContinuitySettings settings)
    {
        MeasurementAccepted = false;
        ResetSmoothing = false;
        bool expired = hasAccepted && now - lastAcceptedTime > Mathf.Max(0f, settings.shortLossTimeout);
        if (!TrackingMath.IsUsable(raw) || raw.headCenter.x < 0f || raw.headCenter.x > width ||
            raw.headCenter.y < 0f || raw.headCenter.y > height)
        {
            ClearPending();
            Hold(now, settings);
            return;
        }
        if (!settings.enabled)
        {
            Accept(raw, now, !hasAccepted || expired || State == TrackingState.Lost);
            return;
        }
        if (!hasAccepted)
        {
            float radius = Mathf.Min(width, height) * settings.initialCenterRadiusRatio;
            if (radius <= 0f || Vector2.Distance(raw.headCenter, new Vector2(width, height) * 0.5f) <= radius)
            {
                Accept(raw, now, true);
                return;
            }
        }
        else if (!expired && State != TrackingState.Lost && IsContinuous(raw, lastAccepted, settings, false))
        {
            Accept(raw, now, false);
            return;
        }

        // Even a nearby result must be confirmed after a prolonged loss.
        // Compare with both the first and previous pending samples to reject random walks.
        if (!hasPending || now - pendingTime > Mathf.Max(0.01f, settings.maxCandidateGap) ||
            !IsContinuous(raw, pendingAnchor, settings, true) ||
            !IsContinuous(raw, pendingLast, settings, true))
        {
            pendingAnchor = raw;
            pendingFrames = 0;
            hasPending = true;
        }
        pendingLast = raw;
        pendingTime = now;
        pendingFrames++;
        if (pendingFrames >= Mathf.Max(1, settings.reacquisitionFrames))
            Accept(raw, now, true);
        else
            Hold(now, settings);
    }

    public void Tick(float now, bool streamStale, ContinuitySettings settings)
    {
        if (hasPending && now - pendingTime > Mathf.Max(0.01f, settings.maxCandidateGap)) ClearPending();
        if (streamStale)
        {
            MeasurementAccepted = false;
            ResetSmoothing = false;
            Hold(now, settings);
        }
    }

    private static bool IsContinuous(DetectionResult current, DetectionResult previous,
        ContinuitySettings settings, bool confirmation)
    {
        float factor = confirmation ? settings.reacquisitionCenterFactor : settings.centerJumpFactor;
        float min = Mathf.Max(1f, settings.minCenterJumpPixels);
        float limit = Mathf.Clamp(previous.eyeDistance * Mathf.Max(0.01f, factor), min,
            Mathf.Max(min, settings.maxCenterJumpPixels));
        return Vector2.Distance(current.headCenter, previous.headCenter) <= limit &&
            Mathf.Abs(current.eyeDistance - previous.eyeDistance) / previous.eyeDistance <=
            Mathf.Clamp01(settings.maxEyeDistanceRelativeChange);
    }

    private void Accept(DetectionResult raw, float now, bool reset)
    {
        lastAccepted = raw;
        lastAcceptedTime = now;
        hasAccepted = true;
        MeasurementAccepted = true;
        ResetSmoothing = reset;
        State = TrackingState.Tracking;
        ClearPending();
    }

    private void Hold(float now, ContinuitySettings settings)
    {
        State = !hasAccepted ? TrackingState.Searching :
            now - lastAcceptedTime <= Mathf.Max(0f, settings.shortLossTimeout)
                ? TrackingState.ShortLost : TrackingState.Lost;
    }

    public void Reset()
    {
        hasAccepted = false;
        lastAccepted = default;
        lastAcceptedTime = 0f;
        MeasurementAccepted = ResetSmoothing = false;
        State = TrackingState.Searching;
        ClearPending();
    }

    private void ClearPending() { hasPending = false; pendingFrames = 0; }
}
