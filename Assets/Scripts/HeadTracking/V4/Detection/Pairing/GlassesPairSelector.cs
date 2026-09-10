using System.Collections.Generic;
using UnityEngine;

/// <summary>Instantaneous geometric gates and final pair cost. Lower cost is better.</summary>
public sealed class GlassesPairSelector
{
    public bool TrySelect(IReadOnlyList<Region> reds, IReadOnlyList<Region> blues,
        GlassesPairSettings settings, out Region bestRed, out Region bestBlue, out float bestScore)
    {
        bestRed = bestBlue = default;
        bestScore = float.PositiveInfinity;
        float weight = settings.verticalWeight + settings.shapeWeight + settings.areaWeight;
        if (!TrackingMath.IsFinite(weight) || weight <= 0f ||
            settings.verticalWeight < 0f || settings.shapeWeight < 0f || settings.areaWeight < 0f)
            return false;

        for (int r = 0; r < reds.Count; r++)
        {
            Region red = reds[r];
            if (red.Area <= 0 || !TrackingMath.IsFinite(red.Center)) continue;
            for (int b = 0; b < blues.Count; b++)
            {
                Region blue = blues[b];
                if (blue.Area <= 0 || !TrackingMath.IsFinite(blue.Center)) continue;
                if (settings.expectedColorOrder == ColorOrder.RedOnImageLeft && red.Center.x >= blue.Center.x) continue;
                if (settings.expectedColorOrder == ColorOrder.RedOnImageRight && red.Center.x <= blue.Center.x) continue;
                float deltaY = Mathf.Abs(red.Center.y - blue.Center.y);
                float distance = Vector2.Distance(red.Center, blue.Center);
                if (deltaY > Mathf.Max(0f, settings.maxVerticalDifference) ||
                    distance < Mathf.Max(1f, settings.minEyeDistance) || distance > settings.maxEyeDistance) continue;
                float areaRatio = (float)Mathf.Max(red.Area, blue.Area) / Mathf.Min(red.Area, blue.Area);
                if (areaRatio > Mathf.Max(1f, settings.maxAreaRatio)) continue;

                float vertical = deltaY / Mathf.Max(0.001f, settings.maxVerticalDifference);
                float shape = (RelativeDifference(red.Bounds.width, blue.Bounds.width) +
                    RelativeDifference(red.Bounds.height, blue.Bounds.height)) * 0.5f;
                float area = RelativeDifference(red.Area, blue.Area);
                float score = (vertical * settings.verticalWeight + shape * settings.shapeWeight +
                    area * settings.areaWeight) / weight;
                if (!TrackingMath.IsFinite(score) || score >= bestScore) continue;
                bestScore = score;
                bestRed = red;
                bestBlue = blue;
            }
        }
        // Outputs keep the best geometric candidate even when its quality is insufficient.
        return TrackingMath.IsFinite(bestScore) && TrackingMath.IsFinite(settings.maxAcceptedScore) &&
            bestScore <= Mathf.Max(0f, settings.maxAcceptedScore);
    }

    private static float RelativeDifference(float a, float b) =>
        Mathf.Max(a, b) <= 0f ? 1f : Mathf.Abs(a - b) / Mathf.Max(a, b);
}
