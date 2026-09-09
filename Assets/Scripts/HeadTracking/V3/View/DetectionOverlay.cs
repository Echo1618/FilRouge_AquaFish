using System;
using UnityEngine;

public enum DiagnosticViewMode
{
    ProcessedMask,
    RawHsvMask,
    Gradient,
    Edges,
    Regions
}

[Serializable]
public sealed class DetectionOverlaySettings
{
    [Header("Image overlay")]
    [Range(0f, 1f)] public float backgroundBrightness = 0.4f;
    [Range(0f, 1f)] public float maskOpacity = 0.7f;

    [Header("Detection geometry")]
    [Range(1, 10)] public int boxThickness = 3;
    [Range(1, 15)] public int eyePointRadius = 5;
    [Range(1, 15)] public int centerPointRadius = 5;
    [Range(1, 5)] public int lineThickness = 1;
}

/// <summary>
/// CPU-side diagnostic composition and annotation drawing.
/// It does not own a Texture2D or any scene object.
/// </summary>
public sealed class DetectionOverlay
{
    private readonly DetectionOverlaySettings settings;
    private Color32[] output;
    private int width;
    private int height;

    public DetectionOverlay(DetectionOverlaySettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public Color32[] Compose(
        ImageFrame frame,
        DetectionDiagnostics diagnostics,
        DetectionResult detection,
        DiagnosticViewMode viewMode)
    {
        EnsureSize(frame.Width, frame.Height);

        switch (viewMode)
        {
            case DiagnosticViewMode.RawHsvMask:
                ComposeMask(frame.Pixels, diagnostics.RawRedMask, diagnostics.RawBlueMask);
                break;

            case DiagnosticViewMode.Gradient:
                ComposeGradient(frame.Pixels, diagnostics);
                break;

            case DiagnosticViewMode.Edges:
                ComposeEdges(frame.Pixels, diagnostics);
                break;

            case DiagnosticViewMode.Regions:
                ComposeRegions(frame.Pixels, diagnostics);
                break;

            default:
                ComposeMask(frame.Pixels, diagnostics.ProcessedRedMask, diagnostics.ProcessedBlueMask);
                break;
        }

        if (detection.detected)
            DrawDetection(detection);

        return output;
    }

    private void ComposeMask(Color32[] original, byte[] redMask, byte[] blueMask)
    {
        for (int i = 0; i < output.Length; i++)
        {
            bool red = redMask != null && redMask[i] != 0;
            bool blue = blueMask != null && blueMask[i] != 0;

            if (!red && !blue)
            {
                output[i] = Darken(original[i], settings.backgroundBrightness);
                continue;
            }

            Color32 overlay = red && blue
                ? new Color32(255, 255, 255, 255)
                : red
                    ? new Color32(255, 0, 0, 255)
                    : new Color32(0, 100, 255, 255);

            output[i] = Blend(original[i], overlay, settings.maskOpacity);
        }
    }

    private void ComposeGradient(Color32[] original, DetectionDiagnostics diagnostics)
    {
        if (!diagnostics.HasSegmentation)
        {
            CopyDarkened(original);
            return;
        }

        for (int i = 0; i < output.Length; i++)
        {
            float strength = diagnostics.Gradient[i] / 255f;
            float opacity = settings.maskOpacity * strength;
            output[i] = Blend(
                Darken(original[i], settings.backgroundBrightness),
                new Color32(255, 255, 255, 255),
                opacity
            );
        }
    }

    private void ComposeEdges(Color32[] original, DetectionDiagnostics diagnostics)
    {
        if (!diagnostics.HasSegmentation)
        {
            CopyDarkened(original);
            return;
        }

        Color32 edgeColor = new Color32(255, 255, 0, 255);

        for (int i = 0; i < output.Length; i++)
        {
            Color32 background = Darken(original[i], settings.backgroundBrightness);
            output[i] = diagnostics.EdgeMask[i] != 0
                ? Blend(background, edgeColor, settings.maskOpacity)
                : background;
        }
    }

    private void ComposeRegions(Color32[] original, DetectionDiagnostics diagnostics)
    {
        if (!diagnostics.HasSegmentation)
        {
            CopyDarkened(original);
            return;
        }

        for (int i = 0; i < output.Length; i++)
        {
            Color32 background = Darken(original[i], settings.backgroundBrightness);
            int label = diagnostics.RegionLabels[i];

            if (label < 0)
            {
                output[i] = background;
                continue;
            }

            output[i] = Blend(background, RegionColor(label), settings.maskOpacity * 0.55f);
        }
    }

    private void DrawDetection(DetectionResult detection)
    {
        Color32 red = new Color32(255, 0, 0, 255);
        Color32 blue = new Color32(0, 100, 255, 255);
        Color32 green = new Color32(0, 255, 0, 255);
        Color32 yellow = new Color32(255, 255, 0, 255);

        DrawBoundingBox(detection.redBox, red, settings.boxThickness);
        DrawBoundingBox(detection.blueBox, blue, settings.boxThickness);

        DrawPoint(detection.redEye, red, settings.eyePointRadius);
        DrawPoint(detection.blueEye, blue, settings.eyePointRadius);

        DrawLine(detection.redEye, detection.blueEye, green, settings.lineThickness);
        DrawPoint(detection.headCenter, yellow, settings.centerPointRadius);
    }

    private void DrawBoundingBox(RectInt box, Color32 color, int thickness)
    {
        for (int t = 0; t < thickness; t++)
        {
            int minX = box.xMin + t;
            int maxX = box.xMax - 1 - t;
            int minY = box.yMin + t;
            int maxY = box.yMax - 1 - t;

            if (minX > maxX || minY > maxY)
                break;

            for (int x = minX; x <= maxX; x++)
            {
                SetPixel(x, minY, color);
                SetPixel(x, maxY, color);
            }

            for (int y = minY; y <= maxY; y++)
            {
                SetPixel(minX, y, color);
                SetPixel(maxX, y, color);
            }
        }
    }

    private void DrawPoint(Vector2 position, Color32 color, int radius)
    {
        int cx = Mathf.RoundToInt(position.x);
        int cy = Mathf.RoundToInt(position.y);

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y > radius * radius)
                    continue;

                SetPixel(cx + x, cy + y, color);
            }
        }
    }

    private void DrawLine(Vector2 start, Vector2 end, Color32 color, int thickness)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(start, end));
        if (steps <= 0)
            return;

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            DrawPoint(Vector2.Lerp(start, end, t), color, thickness);
        }
    }

    private void SetPixel(int x, int y, Color32 color)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        output[y * width + x] = color;
    }

    private void CopyDarkened(Color32[] original)
    {
        for (int i = 0; i < output.Length; i++)
            output[i] = Darken(original[i], settings.backgroundBrightness);
    }

    private static Color32 Blend(Color32 background, Color32 overlay, float opacity)
    {
        opacity = Mathf.Clamp01(opacity);

        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Lerp(background.r, overlay.r, opacity)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(background.g, overlay.g, opacity)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(background.b, overlay.b, opacity)),
            255
        );
    }

    private static Color32 Darken(Color32 color, float brightness)
    {
        brightness = Mathf.Clamp01(brightness);

        return new Color32(
            (byte)(color.r * brightness),
            (byte)(color.g * brightness),
            (byte)(color.b * brightness),
            255
        );
    }

    private static Color32 RegionColor(int id)
    {
        // Deterministic pseudo-color for debugging region labels.
        unchecked
        {
            uint hash = unchecked((uint)id * 2654435761u);
            byte r = (byte)(64 + (hash & 0x7F));
            byte g = (byte)(64 + ((hash >> 8) & 0x7F));
            byte b = (byte)(64 + ((hash >> 16) & 0x7F));
            return new Color32(r, g, b, 255);
        }
    }

    private void EnsureSize(int newWidth, int newHeight)
    {
        if (output != null && width == newWidth && height == newHeight)
            return;

        width = newWidth;
        height = newHeight;
        output = new Color32[width * height];
    }
}
