using System;
using UnityEngine;

/// <summary>
/// Stateless low-level image operations.
/// </summary>
public static class ImageFilters
{
    public static void BoxBlur3x3(Color32[] input, Color32[] output, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int r = 0;
                int g = 0;
                int b = 0;
                int count = 0;

                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = x + ox;
                        int ny = y + oy;

                        if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                            continue;

                        Color32 pixel = input[ny * width + nx];
                        r += pixel.r;
                        g += pixel.g;
                        b += pixel.b;
                        count++;
                    }
                }

                output[y * width + x] = new Color32(
                    (byte)(r / count),
                    (byte)(g / count),
                    (byte)(b / count),
                    255
                );
            }
        }
    }

    public static void Erode3x3(byte[] input, byte[] output, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool keep = true;

                for (int oy = -1; oy <= 1 && keep; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = x + ox;
                        int ny = y + oy;

                        // Pixels outside the image are treated as background.
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height ||
                            input[ny * width + nx] == 0)
                        {
                            keep = false;
                            break;
                        }
                    }
                }

                output[y * width + x] = keep ? (byte)255 : (byte)0;
            }
        }
    }

    public static void Dilate3x3(byte[] input, byte[] output, int width, int height)
    {
        // Dilation only writes white pixels, so the reusable destination must be cleared first.
        Array.Clear(output, 0, output.Length);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (input[y * width + x] == 0)
                    continue;

                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = x + ox;
                        int ny = y + oy;

                        if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                            continue;

                        output[ny * width + nx] = 255;
                    }
                }
            }
        }
    }

    public static void Opening3x3(byte[] input, byte[] temp, byte[] output, int width, int height)
    {
        Erode3x3(input, temp, width, height);
        Dilate3x3(temp, output, width, height);
    }

    public static void Closing3x3(byte[] input, byte[] temp, byte[] output, int width, int height)
    {
        Dilate3x3(input, temp, width, height);
        Erode3x3(temp, output, width, height);
    }

    public static void Grayscale(Color32[] input, byte[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            Color32 pixel = input[i];

            output[i] = (byte)(
                0.299f * pixel.r +
                0.587f * pixel.g +
                0.114f * pixel.b
            );
        }
    }

    public static void SobelGradient(byte[] input, byte[] output, int width, int height)
    {
        // Extend the border by clamping samples. Leaving a zero-gradient border
        // would let segmented regions reconnect around a visible image-wide edge.
        for (int y = 0; y < height; y++)
        {
            int row0 = Mathf.Max(0, y - 1) * width;
            int row1 = y * width;
            int row2 = Mathf.Min(height - 1, y + 1) * width;
            for (int x = 0; x < width; x++)
            {
                int x0 = Mathf.Max(0, x - 1);
                int x2 = Mathf.Min(width - 1, x + 1);
                int topLeft = input[row0 + x0], top = input[row0 + x], topRight = input[row0 + x2];
                int left = input[row1 + x0], right = input[row1 + x2];
                int bottomLeft = input[row2 + x0], bottom = input[row2 + x], bottomRight = input[row2 + x2];
                int gx = -topLeft + topRight - 2 * left + 2 * right - bottomLeft + bottomRight;
                int gy = -topLeft - 2 * top - topRight + bottomLeft + 2 * bottom + bottomRight;
                output[row1 + x] = (byte)Mathf.Clamp(Mathf.Sqrt(gx * gx + gy * gy), 0f, 255f);
            }
        }
    }

    public static void Threshold(byte[] input, byte[] output, int threshold)
    {
        byte limit = (byte)Mathf.Clamp(threshold, 0, 255);

        for (int i = 0; i < input.Length; i++)
            output[i] = input[i] >= limit ? (byte)255 : (byte)0;
    }
}
