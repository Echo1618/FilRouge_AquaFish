using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(HeadDetector))]
[RequireComponent(typeof(AnaglyphDisplay))]
public class WebCamController : MonoBehaviour
{
    // =========================================================
    // INPUT MODE
    // =========================================================

    [Header("Input Source")]
    [SerializeField] private bool useTestImages = true;

    [Tooltip("Path relative to an Assets/Resources folder.")]
    [SerializeField] private string testImageFolder = "TestImages";

    [Tooltip("Continuously reprocess the current test image. Useful while tuning filters.")]
    [SerializeField] private bool processTestImageContinuously = true;


    // =========================================================
    // WEBCAM SETTINGS
    // =========================================================

    [Header("Webcam")]
    [SerializeField] private int requestedWidth = 640;
    [SerializeField] private int requestedHeight = 480;
    [SerializeField] private int requestedFPS = 30;


    // =========================================================
    // COMPONENTS
    // =========================================================

    private HeadDetectorV2 headDetector;
    private AnaglyphDisplay anaglyphDisplay;


    // =========================================================
    // INPUT DATA
    // =========================================================

    private WebCamTexture webcamTexture;

    private Texture2D[] testImages;
    private int currentTestImageIndex;

    private Color32[] inputPixels;
    private Color32[] debugPixels;

    private int width;
    private int height;

    private bool initialized;


    // =========================================================
    // UNITY
    // =========================================================

    private void Start()
    {
        headDetector = GetComponent<HeadDetectorV2>();
        anaglyphDisplay = GetComponent<AnaglyphDisplay>();

        if (useTestImages)
            InitializeTestImages();
        else
            InitializeWebcam();
    }


    private void Update()
    {
        if (useTestImages)
        {
            UpdateTestImages();
            return;
        }

        UpdateWebcam();
    }


    // =========================================================
    // TEST IMAGE MODE
    // =========================================================

    private void InitializeTestImages()
    {
        testImages = Resources.LoadAll<Texture2D>(testImageFolder);

        if (testImages == null || testImages.Length == 0)
        {
            Debug.LogError(
                $"No test images found in Ressources/{testImageFolder}"
            );

            return;
        }

        // Sort images alphabetically by filename.
        Array.Sort(
            testImages,
            (a, b) => string.Compare(
                a.name,
                b.name,
                StringComparison.OrdinalIgnoreCase
            )
        );

        currentTestImageIndex = 0;

        LoadTestImage(currentTestImageIndex);

        Debug.Log(
            $"{testImages.Length} test images loaded."
        );
    }


    private void UpdateTestImages()
    {
        if (!initialized)
            return;


        bool imageChanged = false;


        // Previous image.
        if (Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            currentTestImageIndex--;

            if (currentTestImageIndex < 0)
                currentTestImageIndex = testImages.Length - 1;

            imageChanged = true;
        }


        // Next image.
        if (Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            currentTestImageIndex++;

            if (currentTestImageIndex >= testImages.Length)
                currentTestImageIndex = 0;

            imageChanged = true;
        }


        if (imageChanged)
        {
            LoadTestImage(currentTestImageIndex);
            return;
        }


        // Allows manual refresh if continuous processing is disabled.
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            ProcessFrame();
            return;
        }


        // Useful while changing HSV values in the Inspector.
        if (processTestImageContinuously)
            ProcessFrame();
    }


    private void LoadTestImage(int index)
    {
        Texture2D image = testImages[index];

        if (image == null)
            return;


        width = image.width;
        height = image.height;


        try
        {
            inputPixels = image.GetPixels32();
        }
        catch (UnityException)
        {
            Debug.LogError(
                $"Image '{image.name}' is not readable. " +
                "Enable Read/Write in its import settings."
            );

            return;
        }


        debugPixels = new Color32[width * height];


        // Reinitialize processing because test images may have
        // different resolutions.
        headDetector.Initialize(
            width,
            height
        );

        anaglyphDisplay.Initialize(
            width,
            height
        );


        anaglyphDisplay.SetImageName(
            $"{index + 1}/{testImages.Length}  -  {image.name}"
        );


        initialized = true;

        ProcessFrame();


        Debug.Log(
            $"Test image: {image.name} ({width} x {height})"
        );
    }


    // =========================================================
    // WEBCAM MODE
    // =========================================================

    private void InitializeWebcam()
    {
        webcamTexture = new WebCamTexture(
            requestedWidth,
            requestedHeight,
            requestedFPS
        );

        webcamTexture.Play();

        anaglyphDisplay.SetImageName("");
    }


    private void UpdateWebcam()
    {
        if (webcamTexture == null || !webcamTexture.isPlaying)
            return;


        // Unity may temporarily report a 16x16 texture at startup.
        if (webcamTexture.width <= 16 || webcamTexture.height <= 16)
            return;


        if (!initialized)
            InitializeWebcamProcessing();


        if (!webcamTexture.didUpdateThisFrame)
            return;


        webcamTexture.GetPixels32(inputPixels);

        ProcessFrame();
    }


    private void InitializeWebcamProcessing()
    {
        width = webcamTexture.width;
        height = webcamTexture.height;

        int pixelCount = width * height;

        inputPixels = new Color32[pixelCount];
        debugPixels = new Color32[pixelCount];


        headDetector.Initialize(
            width,
            height
        );

        anaglyphDisplay.Initialize(
            width,
            height
        );


        initialized = true;


        Debug.Log(
            $"Webcam initialized: {width} x {height}"
        );
    }


    // =========================================================
    // PROCESSING
    // =========================================================

    private void ProcessFrame()
    {
        if (inputPixels == null || debugPixels == null)
            return;


        HeadDetectorV2.DetectionResult detection =
            headDetector.FilteringProcess(
                inputPixels,
                debugPixels
            );


        anaglyphDisplay.DisplayDetection(
            inputPixels,
            debugPixels,
            detection
        );
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (webcamTexture != null &&
            webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
    }
}