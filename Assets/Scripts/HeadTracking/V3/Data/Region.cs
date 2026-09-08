using UnityEngine;

/// <summary>
/// Connected image region extracted from a binary mask.
/// </summary>
public struct Region
{
    public int Id;
    public int SegmentId;
    public int Area;
    public RectInt Bounds;
    public Vector2 Centroid;
    public Vector2 Center;
}
