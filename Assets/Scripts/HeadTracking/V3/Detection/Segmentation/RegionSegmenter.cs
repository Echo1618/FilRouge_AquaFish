using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Experimental gradient-based segmentation.
/// It computes a Sobel gradient, converts strong gradients to barriers,
/// then flood-fills the remaining image into region labels.
/// </summary>
public sealed class RegionSegmenter
{
    private readonly RegionSegmentationSettings settings;

    private byte[] grayscale;
    private byte[] temporaryEdges;
    private bool[] visited;
    private readonly Queue<int> queue = new Queue<int>();

    public RegionSegmenter(RegionSegmentationSettings settings)
    {
        this.settings = settings;
    }

    public int Segment(
        Color32[] input,
        int width,
        int height,
        DetectionDiagnostics diagnostics)
    {
        int pixelCount = width * height;
        EnsureSize(pixelCount);

        ImageFilters.Grayscale(input, grayscale);
        ImageFilters.SobelGradient(grayscale, diagnostics.Gradient, width, height);
        ImageFilters.Threshold(diagnostics.Gradient, diagnostics.EdgeMask, settings.gradientThreshold);

        if (settings.dilateEdges)
        {
            ImageFilters.Dilate3x3(diagnostics.EdgeMask, temporaryEdges, width, height);
            Array.Copy(temporaryEdges, diagnostics.EdgeMask, pixelCount);
        }

        int regionCount = BuildRegionLabels(
            diagnostics.EdgeMask,
            diagnostics.RegionLabels,
            width,
            height
        );

        diagnostics.HasSegmentation = true;
        diagnostics.RegionCount = regionCount;

        return regionCount;
    }

    private int BuildRegionLabels(byte[] edges, int[] labels, int width, int height)
    {
        for (int i = 0; i < labels.Length; i++)
            labels[i] = edges[i] != 0 ? -2 : -1; // -2 = barrier, -1 = unassigned.

        Array.Clear(visited, 0, visited.Length);

        int nextRegionId = 0;

        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != -1 || visited[i])
                continue;

            queue.Clear();
            queue.Enqueue(i);
            visited[i] = true;
            labels[i] = nextRegionId;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current % width;
                int y = current / width;

                // 4-connectivity is conservative here and reduces diagonal leaks through barriers.
                TryAddNeighbor(x - 1, y, nextRegionId, labels, width, height);
                TryAddNeighbor(x + 1, y, nextRegionId, labels, width, height);
                TryAddNeighbor(x, y - 1, nextRegionId, labels, width, height);
                TryAddNeighbor(x, y + 1, nextRegionId, labels, width, height);
            }

            nextRegionId++;
        }

        return nextRegionId;
    }

    private void TryAddNeighbor(
        int x,
        int y,
        int regionId,
        int[] labels,
        int width,
        int height)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        int index = y * width + x;

        if (visited[index] || labels[index] != -1)
            return;

        visited[index] = true;
        labels[index] = regionId;
        queue.Enqueue(index);
    }

    private void EnsureSize(int pixelCount)
    {
        if (grayscale != null && grayscale.Length == pixelCount)
            return;

        grayscale = new byte[pixelCount];
        temporaryEdges = new byte[pixelCount];
        visited = new bool[pixelCount];
    }
}
