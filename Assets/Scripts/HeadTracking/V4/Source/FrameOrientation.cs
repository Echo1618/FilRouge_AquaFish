using UnityEngine;

/// <summary>Corrects acquisition orientation, never the user's horizontal preview mirror.</summary>
public sealed class FrameOrientation
{
    private Color32[] output;

    public Color32[] Apply(Color32[] input, int width, int height, int clockwiseDegrees,
        bool verticallyMirrored, out int outputWidth, out int outputHeight)
    {
        int rotation = ((clockwiseDegrees % 360) + 360) % 360;
        bool quarterTurn = rotation == 90 || rotation == 270;
        outputWidth = quarterTurn ? height : width;
        outputHeight = quarterTurn ? width : height;
        if (rotation == 0 && !verticallyMirrored) return input;
        if (output == null || output.Length != input.Length) output = new Color32[input.Length];

        for (int y = 0; y < height; y++)
        {
            int correctedY = verticallyMirrored ? height - 1 - y : y;
            for (int x = 0; x < width; x++)
            {
                int dx, dy;
                switch (rotation)
                {
                    case 90: dx = correctedY; dy = width - 1 - x; break;
                    case 180: dx = width - 1 - x; dy = height - 1 - correctedY; break;
                    case 270: dx = height - 1 - correctedY; dy = x; break;
                    default: dx = x; dy = correctedY; break;
                }
                output[dy * outputWidth + dx] = input[y * width + x];
            }
        }
        return output;
    }
    public void Reset() => output = null;
}
