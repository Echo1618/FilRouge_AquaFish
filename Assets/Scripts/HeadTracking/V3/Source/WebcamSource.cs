using UnityEngine;

public sealed class WebcamSource : FrameSource
{
    [Header("Webcam")]
    [SerializeField] private int requestedWidth = 640;
    [SerializeField] private int requestedHeight = 480;
    [SerializeField] private int requestedFPS = 30;

    private WebCamTexture webcamTexture;
    private Color32[] pixels;
    private int width;
    private int height;
    private long frameId;

    public override void StartSource()
    {
        StopSource();

        webcamTexture = new WebCamTexture(
            requestedWidth,
            requestedHeight,
            requestedFPS
        );

        webcamTexture.Play();
    }

    public override bool TryGetFrame(out ImageFrame frame)
    {
        frame = default;

        if (webcamTexture == null || !webcamTexture.isPlaying)
            return false;

        // Unity can temporarily report a tiny placeholder texture while starting.
        if (webcamTexture.width <= 16 || webcamTexture.height <= 16)
            return false;

        if (!webcamTexture.didUpdateThisFrame)
            return false;

        EnsureBuffer(webcamTexture.width, webcamTexture.height);

        webcamTexture.GetPixels32(pixels);
        frameId++;

        frame = new ImageFrame(
            pixels,
            width,
            height,
            frameId,
            "Webcam"
        );

        return true;
    }

    public override void StopSource()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
            webcamTexture.Stop();

        webcamTexture = null;
        pixels = null;
        width = 0;
        height = 0;
    }

    private void EnsureBuffer(int newWidth, int newHeight)
    {
        if (pixels != null && width == newWidth && height == newHeight)
            return;

        width = newWidth;
        height = newHeight;
        pixels = new Color32[width * height];
    }
}
