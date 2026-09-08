using UnityEngine;

[RequireComponent(typeof(HeadDetector))]
[RequireComponent(typeof(AnaglyphDisplay))]
public class WebCamController : MonoBehaviour
{
    private HeadDetector headDetector;
    private AnaglyphDisplay anaglyphDisplay;

    private WebCamTexture webcamTexture;

    private Color32[] webCamPixels;
    private Color32[] buffer;

    private int width, height;
    private bool initialized;


    private void Start()
    {
        headDetector = GetComponent<HeadDetector>();
        anaglyphDisplay = GetComponent<AnaglyphDisplay>();

        webcamTexture = new WebCamTexture();
        webcamTexture.Play();
    }


    private void Update()
    {
        if (webcamTexture == null || !webcamTexture.isPlaying) return;
        if (webcamTexture.width <= 16 || webcamTexture.height <= 16) return;

        if (!initialized) Initialize();

        webcamTexture.GetPixels32(webCamPixels);

        HeadDetector.DetectionResult detection =
            headDetector.FilteringProcess(
                webCamPixels,
                buffer
            );

        anaglyphDisplay.DisplayDetection(
            buffer,
            detection
        );
    }


    private void Initialize()
    {
        width = webcamTexture.width;
        height = webcamTexture.height;

        int pixelCount = width * height;

        webCamPixels = new Color32[pixelCount];
        buffer = new Color32[pixelCount];

        headDetector.Initialize(width, height);
        anaglyphDisplay.Initialize(width, height);

        initialized = true;

        Debug.Log($"Webcam initialized : {width} x {height}");
    }


    private void OnDestroy()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
            webcamTexture.Stop();
    }
}