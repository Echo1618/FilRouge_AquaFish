using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chooses the most geometrically plausible red/blue glasses pair.
/// </summary>
public sealed class GlassesPairSelector
{
    private readonly GlassesPairSettings settings;

    public GlassesPairSelector(GlassesPairSettings settings)
    {
        this.settings = settings;
    }

    public bool TrySelect(
        IReadOnlyList<Region> redRegions,
        IReadOnlyList<Region> blueRegions,
        out Region bestRed,
        out Region bestBlue)
    {
        bestRed = default;
        bestBlue = default;

        bool found = false;
        float bestScore = float.MaxValue;

        for (int r = 0; r < redRegions.Count; r++)
        {
            Region red = redRegions[r];

            for (int b = 0; b < blueRegions.Count; b++)
            {
                Region blue = blueRegions[b];

                if (!HasValidOrder(red, blue))
                    continue;

                float deltaY = Mathf.Abs(red.Center.y - blue.Center.y);
                float distance = Vector2.Distance(red.Center, blue.Center);

                if (deltaY > settings.maxVerticalDifference)
                    continue;

                if (distance < Mathf.Max(1f, settings.minEyeDistance) ||
                    distance > settings.maxEyeDistance)
                    continue;

                float verticalScore =
                    deltaY / Mathf.Max(0.001f, settings.maxVerticalDifference);

                float widthScore = RelativeDifference(red.Bounds.width, blue.Bounds.width);
                float heightScore = RelativeDifference(red.Bounds.height, blue.Bounds.height);
                float shapeScore = (widthScore + heightScore) * 0.5f;

                // Area is deliberately a weak criterion because one painted frame
                // can appear more filled than the other.
                float areaScore = RelativeDifference(red.Area, blue.Area);

                float weightSum = Mathf.Max(
                    0.001f,
                    settings.verticalWeight + settings.shapeWeight + settings.areaWeight
                );

                float score = (
                    verticalScore * settings.verticalWeight +
                    shapeScore * settings.shapeWeight +
                    areaScore * settings.areaWeight
                ) / weightSum;

                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestRed = red;
                bestBlue = blue;
                found = true;
            }
        }

        return found;
    }

    private bool HasValidOrder(Region red, Region blue)
    {
        switch (settings.expectedColorOrder)
        {
            case ColorOrder.RedOnImageLeft:
                return red.Center.x < blue.Center.x;

            case ColorOrder.RedOnImageRight:
                return red.Center.x > blue.Center.x;

            default:
                return true;
        }
    }

    private static float RelativeDifference(float a, float b)
    {
        float max = Mathf.Max(a, b);
        return max <= 0f ? 1f : Mathf.Abs(a - b) / max;
    }
}
