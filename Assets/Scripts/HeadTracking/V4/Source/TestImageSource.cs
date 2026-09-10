using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class TestImageSource : FrameSource
{
    [SerializeField] private string resourceFolder = "TestImages";
    [Tooltip("An optional explicit list takes precedence over Resources.")]
    [SerializeField] private Texture2D[] testImages;
    [SerializeField] private bool processContinuously = true;
    private Texture2D[] images;
    private Color32[] currentPixels;
    private int currentIndex;
    private bool running, dirty;
    private long frameId;
    private string frameName;
    public override bool IsStatic => true;

    public override void StartSource()
    {
        StopSource();
        if (!isActiveAndEnabled) return;
        running = true;
        images = testImages != null && testImages.Length > 0 ? testImages : Resources.LoadAll<Texture2D>(resourceFolder);
        if (images == null || images.Length == 0)
        {
            Status = "No test images";
            Debug.LogError("TestImageSource: assign testImages or add images to Resources/" + resourceFolder, this);
            return;
        }
        if (images != testImages)
            Array.Sort(images, (a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        currentIndex = 0;
        LoadCurrentImage();
    }

    public override bool TryGetFrame(out ImageFrame frame)
    {
        frame = default;
        if (!running || !isActiveAndEnabled || images == null || images.Length == 0) return false;
        // Navigation still works when the current image is missing or unreadable.
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame) PreviousImage();
            if (keyboard.rightArrowKey.wasPressedThisFrame) NextImage();
            if (keyboard.rKey.wasPressedThisFrame) RefreshImage();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.LeftArrow)) PreviousImage();
        if (Input.GetKeyDown(KeyCode.RightArrow)) NextImage();
        if (Input.GetKeyDown(KeyCode.R)) RefreshImage();
#endif
        if (currentPixels == null || (!dirty && !processContinuously)) return false;
        dirty = false;
        Texture2D image = images[currentIndex];
        frame = new ImageFrame(currentPixels, image.width, image.height, ++frameId, frameName);
        return true;
    }

    public void NextImage() => MoveImage(1);
    public void PreviousImage() => MoveImage(-1);
    public void RefreshImage() { dirty = true; }
    private void MoveImage(int direction)
    {
        if (images == null || images.Length == 0) return;
        currentIndex = (currentIndex + direction + images.Length) % images.Length;
        LoadCurrentImage();
    }

    private void LoadCurrentImage()
    {
        Revision++;
        currentPixels = null;
        Texture2D image = images[currentIndex];
        if (image == null) { Status = "Missing image"; return; }
        if (!image.isReadable)
        {
            Status = "Image is not readable";
            Debug.LogError("TestImageSource: enable Read/Write for " + image.name, this);
            return;
        }
        currentPixels = image.GetPixels32();
        frameName = (currentIndex + 1) + "/" + images.Length + " - " + image.name;
        dirty = true;
        Status = "Static test image";
    }

    public override void StopSource()
    {
        if (running) Revision++;
        running = dirty = false;
        images = null;
        currentPixels = null;
        Status = "Stopped";
    }
}
