using System;
using UnityEngine;

/// <summary>Fixed-size rolling window with O(1) updates and no per-sample allocation.</summary>
public sealed class CalibrationStatistics
{
    private struct Sample { public double u, v, e, inverse; }
    private Sample[] buffer;
    private int next, count;
    private double uSum, vSum, eSum, eSquaredSum, invSum, invSquaredSum, uInvSum, vInvSum;
    public int Count => count;
    public Vector2 MeanCenter => count == 0 ? Vector2.zero : new Vector2((float)(uSum / count), (float)(vSum / count));
    public float MeanEyeDistance => count == 0 ? 0f : (float)(eSum / count);
    public float EyeDistanceStdDev => StdDev(eSum, eSquaredSum);
    public float MeanInverseEye => count == 0 ? 0f : (float)(invSum / count);
    public float InverseEyeStdDev => StdDev(invSum, invSquaredSum);
    public Vector2 MeanCenterOverEye => count == 0 ? Vector2.zero : new Vector2((float)(uInvSum / count), (float)(vInvSum / count));

    public void Configure(int capacity)
    {
        capacity = Mathf.Clamp(capacity, 5, 600);
        if (buffer != null && buffer.Length == capacity) return;
        buffer = new Sample[capacity];
        Reset();
    }
    public void Add(DetectionResult detection)
    {
        if (!TrackingMath.IsUsable(detection)) return;
        if (buffer == null) Configure(30);
        if (count == buffer.Length) Accumulate(buffer[next], -1.0);
        else count++;
        Sample sample = new Sample { u = detection.headCenter.x, v = detection.headCenter.y,
            e = detection.eyeDistance, inverse = 1.0 / detection.eyeDistance };
        buffer[next] = sample;
        Accumulate(sample, 1.0);
        next = (next + 1) % buffer.Length;
    }
    private void Accumulate(Sample s, double sign)
    {
        uSum += sign * s.u; vSum += sign * s.v;
        eSum += sign * s.e; eSquaredSum += sign * s.e * s.e;
        invSum += sign * s.inverse; invSquaredSum += sign * s.inverse * s.inverse;
        uInvSum += sign * s.u * s.inverse; vInvSum += sign * s.v * s.inverse;
    }
    private float StdDev(double sum, double squares) => count == 0 ? 0f :
        (float)Math.Sqrt(Math.Max(0.0, squares / count - sum * sum / (count * (double)count)));
    public void Reset()
    {
        next = count = 0;
        uSum = vSum = eSum = eSquaredSum = invSum = invSquaredSum = uInvSum = vInvSum = 0.0;
    }
}
