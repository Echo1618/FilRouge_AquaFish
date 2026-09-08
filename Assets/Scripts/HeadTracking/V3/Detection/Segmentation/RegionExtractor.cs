using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extracts connected components from a binary mask using 8-connectivity.
/// An optional segmentation map can prevent a component from crossing region boundaries.
/// </summary>
public sealed class RegionExtractor
{
    private bool[] visited;
    private readonly Queue<int> queue = new Queue<int>();

    public void Extract(
        byte[] mask,
        int width,
        int height,
        int minArea,
        List<Region> output,
        int[] segmentLabels = null)
    {
        EnsureSize(mask.Length);

        Array.Clear(visited, 0, visited.Length);
        output.Clear();

        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] == 0 || visited[i])
                continue;

            int segmentId = segmentLabels != null ? segmentLabels[i] : -1;

            // Segmentation edge pixels are not allowed to seed a component.
            if (segmentLabels != null && segmentId < 0)
                continue;

            queue.Clear();

            int area = 0;
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            long sumX = 0;
            long sumY = 0;

            visited[i] = true;
            queue.Enqueue(i);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current % width;
                int y = current / width;

                area++;
                sumX += x;
                sumY += y;

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);

                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0)
                            continue;

                        int nx = x + ox;
                        int ny = y + oy;

                        if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                            continue;

                        int neighbor = ny * width + nx;

                        if (mask[neighbor] == 0 || visited[neighbor])
                            continue;

                        if (segmentLabels != null && segmentLabels[neighbor] != segmentId)
                            continue;

                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (area < Mathf.Max(1, minArea))
                continue;

            RectInt bounds = new RectInt(
                minX,
                minY,
                maxX - minX + 1,
                maxY - minY + 1
            );

            output.Add(new Region
            {
                Id = output.Count,
                SegmentId = segmentId,
                Area = area,
                Bounds = bounds,
                Centroid = new Vector2((float)sumX / area, (float)sumY / area),
                Center = new Vector2(
                    bounds.x + bounds.width * 0.5f,
                    bounds.y + bounds.height * 0.5f
                )
            });
        }
    }

    private void EnsureSize(int pixelCount)
    {
        if (visited == null || visited.Length != pixelCount)
            visited = new bool[pixelCount];
    }
}
