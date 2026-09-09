using UnityEngine;

/// <summary>
/// Smooths viewer position before it is sent to the stereo rig.
///
/// X/Y and Z use independent response values because depth
/// estimation is usually noisier than lateral tracking.
///
/// Higher response:
/// - more reactive
/// - less smoothing
///
/// Lower response:
/// - more stable
/// - more latency
/// </summary>
public sealed class ViewerPoseSmoother
{
    // =========================================================
    // SETTINGS
    // =========================================================

    private float xyResponse = 7f;
    private float zResponse = 4f;


    // =========================================================
    // STATE
    // =========================================================

    private Vector3 smoothedPosition;

    private float lastTimestamp;

    private bool initialized;


    // =========================================================
    // CONFIGURATION
    // =========================================================

    public void Configure(
        float newXYResponse,
        float newZResponse)
    {
        xyResponse =
            Mathf.Max(0.01f, newXYResponse);

        zResponse =
            Mathf.Max(0.01f, newZResponse);
    }


    // =========================================================
    // FILTER
    // =========================================================

    /// <summary>
    /// Filters one viewer pose.
    ///
    /// timestamp must be an absolute time value such as
    /// Time.unscaledTime.
    /// </summary>
    public ViewerPose Filter(
        ViewerPose rawPose,
        float timestamp)
    {
        if (!rawPose.valid)
            return rawPose;


        // First valid pose:
        // initialize directly to avoid sliding from Vector3.zero.
        if (!initialized)
        {
            smoothedPosition =
                rawPose.position;

            lastTimestamp =
                timestamp;

            initialized = true;

            return new ViewerPose(
                true,
                smoothedPosition
            );
        }


        float deltaTime =
            timestamp -
            lastTimestamp;


        lastTimestamp =
            timestamp;


        // Protect against invalid or extremely small time steps.
        if (deltaTime <= 0f)
        {
            return new ViewerPose(
                true,
                smoothedPosition
            );
        }


        float xyAlpha =
            ComputeAlpha(
                xyResponse,
                deltaTime
            );


        float zAlpha =
            ComputeAlpha(
                zResponse,
                deltaTime
            );


        smoothedPosition.x =
            Mathf.Lerp(
                smoothedPosition.x,
                rawPose.position.x,
                xyAlpha
            );


        smoothedPosition.y =
            Mathf.Lerp(
                smoothedPosition.y,
                rawPose.position.y,
                xyAlpha
            );


        smoothedPosition.z =
            Mathf.Lerp(
                smoothedPosition.z,
                rawPose.position.z,
                zAlpha
            );


        return new ViewerPose(
            true,
            smoothedPosition
        );
    }


    // =========================================================
    // RESET
    // =========================================================

    /// <summary>
    /// Clears the filter state.
    ///
    /// The next valid pose will immediately become the
    /// new reference position.
    /// </summary>
    public void Reset()
    {
        initialized = false;

        smoothedPosition =
            Vector3.zero;

        lastTimestamp =
            0f;
    }


    // =========================================================
    // UTILITIES
    // =========================================================

    private float ComputeAlpha(
        float response,
        float deltaTime)
    {
        return
            1f -
            Mathf.Exp(
                -response *
                deltaTime
            );
    }
}