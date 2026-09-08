using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TestImageSource : FrameSource
{
    [Header("Test images")]
    [Tooltip("Path relative to an Assets/Resources folder.")]
    [SerializeField] private string resourceFolder = "TestImages";

    [Tooltip("Reprocess the current image every frame while tuning detector settings.")]
    [SerializeField] private bool processContinuously = true;

    private Texture2D[] images;
    private Color32[] currentPixels;
    private int currentIndex;
    private bool dirty;
    private long frameId;

    public override void StartSource()
    {
        images = Resources.LoadAll<Texture2D>(resourceFolder);

        if (images == null || images.Length == 0)
        {
            Debug.LogError($"TestImageSource: no images found in Resources/{resourceFolder}.");
            return;
        }

        Array.Sort(
            images,
            (a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase)
        );

        currentIndex = 0;
        LoadCurrentImage();
    }

    public override bool TryGetFrame(out ImageFrame frame)
    {
        frame = default;

        if (images == null || images.Length == 0 || currentPixels == null)
            return false;

        HandleKeyboard();

        if (!dirty && !processContinuously)
            return false;

        Texture2D image = images[currentIndex];

        dirty = false;
        frameId++;

        frame = new ImageFrame(
            currentPixels,
            image.width,
            image.height,
            frameId,
            $"{currentIndex + 1}/{images.Length} - {image.name}"
        );

        return true;
    }

    public override void StopSource()
    {
        images = null;
        currentPixels = null;
        currentIndex = 0;
        dirty = false;
    }

    private void HandleKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.leftArrowKey.wasPressedThisFrame)
        {
            currentIndex = (currentIndex - 1 + images.Length) % images.Length;
            LoadCurrentImage();
        }
        else if (keyboard.rightArrowKey.wasPressedThisFrame)
        {
            currentIndex = (currentIndex + 1) % images.Length;
            LoadCurrentImage();
        }
        else if (keyboard.rKey.wasPressedThisFrame)
        {
            dirty = true;
        }
    }

    private void LoadCurrentImage()
    {
        Texture2D image = images[currentIndex];

        try
        {
            currentPixels = image.GetPixels32();
            dirty = true;
        }
        catch (UnityException)
        {
            currentPixels = null;
            Debug.LogError(
                $"TestImageSource: '{image.name}' is not readable. Enable Read/Write in its import settings."
            );
        }
    }
}
