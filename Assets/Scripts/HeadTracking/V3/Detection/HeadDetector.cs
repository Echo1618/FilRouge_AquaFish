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
        pairSelector = new GlassesPairSelector(settings.pairing);
        regionSegmenter = new RegionSegmenter(settings.segmentation);
    }

    public DetectionResult Detect(ImageFrame frame, DetectionDiagnostics diagnostics)
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

        // 2. Independent raw HSV masks.
        diagnostics.RawRedPixels = HsvColorFilter.Apply(
            source,
            diagnostics.RawRedMask,
            settings.redHSV
        );

        diagnostics.RawBluePixels = HsvColorFilter.Apply(
            source,
            diagnostics.RawBlueMask,
            settings.blueHSV
        );

        // Keep raw masks for diagnostics, and process separate copies.
        Array.Copy(diagnostics.RawRedMask, diagnostics.ProcessedRedMask, pixelCount);
        Array.Copy(diagnostics.RawBlueMask, diagnostics.ProcessedBlueMask, pixelCount);

        // 3. Morphological cleanup.
        ProcessMask(diagnostics.ProcessedRedMask);
        ProcessMask(diagnostics.ProcessedBlueMask);

        diagnostics.ProcessedRedPixels = CountPixels(diagnostics.ProcessedRedMask);
        diagnostics.ProcessedBluePixels = CountPixels(diagnostics.ProcessedBlueMask);

        // 4. Optional gradient segmentation.
        int[] segmentLabels = null;

        if (settings.segmentation.enabled || settings.segmentation.separateBlobsByRegion)
        {
            regionSegmenter.Segment(source, width, height, diagnostics);

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

        // 6. Select the most plausible glasses pair.
        if (!pairSelector.TrySelect(
                diagnostics.RedCandidates,
                diagnostics.BlueCandidates,
                out Region red,
                out Region blue))
        {
            diagnostics.PairDetected = false;
            return default;
        }

        diagnostics.PairDetected = true;

        // 7. Build the final geometric result.
        return BuildResult(red, blue);
    }

    private DetectionResult BuildResult(Region red, Region blue)
    {
        Vector2 redEye = red.Center;
        Vector2 blueEye = blue.Center;
        Vector2 direction = blueEye - redEye;

        // Keep a horizontal head close to 0 degrees regardless of color order.
        if (direction.x < 0f)
            direction = -direction;

        return new DetectionResult
        {
            Detected = true,
            RedBox = red.Bounds,
            BlueBox = blue.Bounds,
            RedEye = redEye,
            BlueEye = blueEye,
            HeadCenter = (redEye + blueEye) * 0.5f,
            EyeDistance = Vector2.Distance(redEye, blueEye),
            HeadAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
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
