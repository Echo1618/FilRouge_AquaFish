using System;
using UnityEngine;

/// <summary>
/// High-level detection pipeline.
/// This is a plain C# class: it knows nothing about Unity scene objects,
/// webcams, keyboard input or rendering.
/// </summary>
public sealed class HeadDetector
{
    private readonly DetectionSettings settings;
    private readonly RegionExtractor regionExtractor;
    private readonly GlassesPairSelector pairSelector;
    private readonly RegionSegmenter regionSegmenter;

    private int width;
    private int height;
    private int pixelCount;

    private Color32[] blurBuffer;
    private byte[] tempMaskA;
    private byte[] tempMaskB;

    public HeadDetector(DetectionSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

        regionExtractor = new RegionExtractor();
        pairSelector = new GlassesPairSelector();
        regionSegmenter = new RegionSegmenter();
    }

    public DetectionResult Detect(ImageFrame frame, DetectionDiagnostics diagnostics, bool collectDiagnostics = true)
    {
        if (!frame.IsValid)
            return default;

        if (diagnostics == null)
            throw new ArgumentNullException(nameof(diagnostics));

        EnsureSize(frame.Width, frame.Height);

        diagnostics.EnsureSize(pixelCount);
        diagnostics.ResetFrame();

        Color32[] source = frame.Pixels;

        // 1. Optional pre-filtering.
        if (settings.useBlur)
        {
            ImageFilters.BoxBlur3x3(frame.Pixels, blurBuffer, width, height);
            source = blurBuffer;
        }

        // 2. Both HSV masks share a single conversion per pixel.
        HsvColorFilter.Apply(source, diagnostics.RawRedMask, diagnostics.RawBlueMask,
            settings.redHSV, settings.blueHSV, out diagnostics.RawRedPixels, out diagnostics.RawBluePixels);

        // Keep raw masks for diagnostics, and process separate copies.
        Array.Copy(diagnostics.RawRedMask, diagnostics.ProcessedRedMask, pixelCount);
        Array.Copy(diagnostics.RawBlueMask, diagnostics.ProcessedBlueMask, pixelCount);

        // 3. Morphological cleanup.
        ProcessMask(diagnostics.ProcessedRedMask);
        ProcessMask(diagnostics.ProcessedBlueMask);

        diagnostics.ProcessedRedPixels = collectDiagnostics ? CountPixels(diagnostics.ProcessedRedMask) : 0;
        diagnostics.ProcessedBluePixels = collectDiagnostics ? CountPixels(diagnostics.ProcessedBlueMask) : 0;

        // 4. Optional gradient segmentation.
        int[] segmentLabels = null;

        if ((collectDiagnostics && settings.segmentation.enabled) || settings.segmentation.separateBlobsByRegion)
        {
            regionSegmenter.Segment(source, width, height, diagnostics, settings.segmentation);

            if (settings.segmentation.separateBlobsByRegion)
                segmentLabels = diagnostics.RegionLabels;
        }

        // 5. Connected components.
        regionExtractor.Extract(
            diagnostics.ProcessedRedMask,
            width,
            height,
            settings.minRegionArea,
            diagnostics.RedCandidates,
            segmentLabels
        );

        regionExtractor.Extract(
            diagnostics.ProcessedBlueMask,
            width,
            height,
            settings.minRegionArea,
            diagnostics.BlueCandidates,
            segmentLabels
        );

        bool accepted = pairSelector.TrySelect(diagnostics.RedCandidates, diagnostics.BlueCandidates,
            settings.pairing, out Region red, out Region blue, out float score);
        diagnostics.PairDetected = accepted;
        if (!TrackingMath.IsFinite(score)) return default;
        Vector2 direction = blue.Center - red.Center;
        if (direction.x < 0f) direction = -direction;
        return new DetectionResult
        {
            detected = accepted,
            hasCandidate = true,
            pairScore = score,
            redBox = red.Bounds,
            blueBox = blue.Bounds,
            redEye = red.Center,
            blueEye = blue.Center,
            headCenter = (red.Center + blue.Center) * 0.5f,
            eyeDistance = Vector2.Distance(red.Center, blue.Center),
            headAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
        };
    }

    private void ProcessMask(byte[] mask)
    {
        if (settings.useOpening)
        {
            ImageFilters.Opening3x3(mask, tempMaskA, tempMaskB, width, height);
            Array.Copy(tempMaskB, mask, pixelCount);
        }

        if (settings.useClosing)
        {
            ImageFilters.Closing3x3(mask, tempMaskA, tempMaskB, width, height);
            Array.Copy(tempMaskB, mask, pixelCount);
        }
    }

    private void EnsureSize(int newWidth, int newHeight)
    {
        if (blurBuffer != null && width == newWidth && height == newHeight)
            return;

        width = newWidth;
        height = newHeight;
        pixelCount = width * height;

        blurBuffer = new Color32[pixelCount];
        tempMaskA = new byte[pixelCount];
        tempMaskB = new byte[pixelCount];
    }

    private static int CountPixels(byte[] mask)
    {
        int count = 0;

        for (int i = 0; i < mask.Length; i++)
            if (mask[i] != 0) count++;

        return count;
    }
}
