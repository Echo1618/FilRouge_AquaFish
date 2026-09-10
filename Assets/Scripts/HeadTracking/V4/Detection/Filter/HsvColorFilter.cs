using UnityEngine;

/// <summary>One RGB-to-HSV conversion per pixel for both color ranges.</summary>
public static class HsvColorFilter
{
    public static void Apply(Color32[] input, byte[] red, byte[] blue, HsvRange redRange,
        HsvRange blueRange, out int redCount, out int blueCount)
    {
        redCount = blueCount = 0;
        for (int i = 0; i < input.Length; i++)
        {
            Color.RGBToHSV(input[i], out float h, out float s, out float v);
            bool r = Contains(h, s, v, redRange);
            bool b = Contains(h, s, v, blueRange);
            red[i] = r ? (byte)255 : (byte)0;
            blue[i] = b ? (byte)255 : (byte)0;
            if (r) redCount++;
            if (b) blueCount++;
        }
    }

    private static bool Contains(float h, float s, float v, HsvRange range)
    {
        bool hue = range.minH <= range.maxH ? h >= range.minH && h <= range.maxH :
            h >= range.minH || h <= range.maxH;
        return hue && s >= range.minS && v >= range.minV;
    }
}
