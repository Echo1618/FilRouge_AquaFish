using System.Collections.Generic;

/// <summary>
/// Reusable diagnostic data produced during one detection pass.
/// Buffers are kept between frames to avoid unnecessary allocations.
/// </summary>
public sealed class DetectionDiagnostics
{
    public byte[] RawRedMask { get; private set; }
    public byte[] RawBlueMask { get; private set; }
    public byte[] ProcessedRedMask { get; private set; }
    public byte[] ProcessedBlueMask { get; private set; }

    public byte[] Gradient { get; private set; }
    public byte[] EdgeMask { get; private set; }
    public int[] RegionLabels { get; private set; }

    public readonly List<Region> RedCandidates = new List<Region>();
    public readonly List<Region> BlueCandidates = new List<Region>();

    public int RawRedPixels;
    public int RawBluePixels;
    public int ProcessedRedPixels;
    public int ProcessedBluePixels;
    public int RegionCount;
    public bool HasSegmentation;
    public bool PairDetected;

    public void EnsureSize(int pixelCount)
    {
        if (RawRedMask != null && RawRedMask.Length == pixelCount)
            return;

        RawRedMask = new byte[pixelCount];
        RawBlueMask = new byte[pixelCount];
        ProcessedRedMask = new byte[pixelCount];
        ProcessedBlueMask = new byte[pixelCount];

        Gradient = new byte[pixelCount];
        EdgeMask = new byte[pixelCount];
        RegionLabels = new int[pixelCount];
    }

    public void ResetFrame()
    {
        RawRedPixels = 0;
        RawBluePixels = 0;
        ProcessedRedPixels = 0;
        ProcessedBluePixels = 0;
        RegionCount = 0;
        HasSegmentation = false;
        PairDetected = false;

        RedCandidates.Clear();
        BlueCandidates.Clear();
    }
}
