using UnityEngine;

/// <summary>
/// Builds a binary mask from one HSV range.
/// Hue wrapping is supported, which is required for red around 0/1.
/// </summary>
public static class HsvColorFilter
{
    public static int Apply(Color32[] input, byte[] output, HsvRange range)
    {
        int acceptedCount = 0;

        for (int i = 0; i < input.Length; i++)
        {
            Color.RGBToHSV(input[i], out float h, out float s, out float v);

            bool hueAccepted = range.minH <= range.maxH
                ? h >= range.minH && h <= range.maxH
                : h >= range.minH || h <= range.maxH;

            bool accepted = hueAccepted && s >= range.minS && v >= range.minV;

            output[i] = accepted ? (byte)255 : (byte)0;
            if (accepted) acceptedCount++;
        }

        return acceptedCount;
    }
}
