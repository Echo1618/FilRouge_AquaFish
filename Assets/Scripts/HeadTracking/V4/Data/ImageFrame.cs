using UnityEngine;

/// <summary>
/// Immutable description of one input image.
/// The pixel buffer is owned by the FrameSource and is valid until the next frame request.
/// </summary>
public struct ImageFrame
{
    public Color32[] Pixels { get; }
    public int Width { get; }
    public int Height { get; }
    public long Id { get; }
    public string Name { get; }

    public int PixelCount => Width * Height;
    public bool IsValid => Pixels != null && Width > 0 && Height > 0 && Pixels.Length == PixelCount;

    public ImageFrame(Color32[] pixels, int width, int height, long id, string name)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
        Id = id;
        Name = name ?? string.Empty;
    }
}
