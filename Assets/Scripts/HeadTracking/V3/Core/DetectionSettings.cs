using System;
using UnityEngine;

public enum ColorOrder
{
    Any,
    RedOnImageLeft,
    RedOnImageRight
}

[Serializable]
public struct HsvRange
{
    [Range(0f, 1f)] public float minH;
    [Range(0f, 1f)] public float maxH;
    [Range(0f, 1f)] public float minS;
    [Range(0f, 1f)] public float minV;

    public HsvRange(float minH, float maxH, float minS, float minV)
    {
        this.minH = minH;
        this.maxH = maxH;
        this.minS = minS;
        this.minV = minV;
    }
}

[Serializable]
public sealed class GlassesPairSettings
{
    public ColorOrder expectedColorOrder = ColorOrder.Any;

    [Min(0f)] public float maxVerticalDifference = 50f;
    [Min(1f)] public float minEyeDistance = 30f;
    [Min(1f)] public float maxEyeDistance = 400f;

    [Header("Pair score")]
    [Range(0f, 1f)] public float verticalWeight = 0.55f;
    [Range(0f, 1f)] public float shapeWeight = 0.35f;
    [Range(0f, 1f)] public float areaWeight = 0.10f;

    [Tooltip("Maximum score accepted for a glasses pair. Lower is better.")]
    [Min(0f)]
    public float maxAcceptedScore = 0.55f;

}

[Serializable]
public sealed class RegionSegmentationSettings
{
    [Tooltip("Compute Sobel gradient, edge mask and region labels for diagnostics.")]
    public bool enabled = false;

    [Tooltip("When enabled, connected components cannot cross a segmented region boundary.")]
    public bool separateBlobsByRegion = false;

    [Range(0, 255)] public int gradientThreshold = 50;
    public bool dilateEdges = true;
}

[Serializable]
public sealed class DetectionSettings
{
    [Header("HSV - painted frame")]
    public HsvRange redHSV = new HsvRange(0.95f, 0.05f, 0.40f, 0.15f);
    public HsvRange blueHSV = new HsvRange(0.52f, 0.72f, 0.35f, 0.15f);

    [Header("Pre-processing")]
    [Tooltip("Averages RGB before HSV. Thin colored paint can lose saturation, so keep this disabled initially.")]
    public bool useBlur = false;

    [Tooltip("Erosion then dilation. Useful for isolated noise, but it can erase a thin frame.")]
    public bool useOpening = false;

    [Tooltip("Dilation then erosion. Useful for reconnecting small gaps in the frame.")]
    public bool useClosing = true;

    [Header("Regions")]
    [Min(1)] public int minRegionArea = 50;

    [Header("Pairing")]
    public GlassesPairSettings pairing = new GlassesPairSettings();

    [Header("Gradient segmentation - experimental")]
    public RegionSegmentationSettings segmentation = new RegionSegmentationSettings();
}
