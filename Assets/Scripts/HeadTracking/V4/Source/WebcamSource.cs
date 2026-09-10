using System.Collections;
using UnityEngine;

public sealed class WebcamSource : FrameSource
{
    [Header("Device")]
    [Tooltip("Exact device name. Empty selects deviceIndex. Use List devices in the component menu.")]
    [SerializeField] private string deviceName = "";
    [Min(0)] [SerializeField] private int deviceIndex;
    [Header("Requested capture mode")]
    [Min(32)] [SerializeField] private int requestedWidth = 640;
    [Min(32)] [SerializeField] private int requestedHeight = 480;
    [Min(1)] [SerializeField] private int requestedFPS = 30;
    [Header("Acquisition orientation")]
    [SerializeField] private bool correctVideoOrientation = true;
    [Tooltip("Additional clockwise quarter turns after the device rotation.")]
    [Range(0, 3)] [SerializeField] private int additionalQuarterTurns;
    [SerializeField] private bool additionalVerticalFlip;

    private WebCamTexture webcamTexture;
    private Color32[] pixels;
    private readonly FrameOrientation orientation = new FrameOrientation();
    private Coroutine permissionRoutine;
    private bool running, restartRequested, hasGeometry;
    private int width, height, lastRotation;
    private bool lastMirror;
    private long frameId;
    public int VideoRotationAngle { get; private set; }
    public bool VideoVerticallyMirrored { get; private set; }

    public override void StartSource()
    {
        StopSource();
        if (!isActiveAndEnabled) return;
        running = true;
        Status = "Starting";
        permissionRoutine = StartCoroutine(StartAuthorized());
    }

    private IEnumerator StartAuthorized()
    {
#if UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Status = "Webcam permission denied";
            yield break;
        }
#else
        // Keep this an iterator on desktop; no artificial startup delay is introduced.
        if (!running) yield break;
#endif
        if (!running) yield break;
        WebCamDevice[] devices = WebCamTexture.devices;
        int selected = -1;
        if (string.IsNullOrEmpty(deviceName))
        {
            if (deviceIndex >= 0 && deviceIndex < devices.Length) selected = deviceIndex;
        }
        else
        {
            for (int i = 0; i < devices.Length; i++)
                if (devices[i].name == deviceName) { selected = i; break; }
        }
        if (selected < 0)
        {
            Status = "Requested webcam not found";
            Debug.LogError("WebcamSource: requested device not found. Use List devices.", this);
            yield break;
        }
        webcamTexture = new WebCamTexture(devices[selected].name,
            Mathf.Max(32, requestedWidth), Mathf.Max(32, requestedHeight), Mathf.Max(1, requestedFPS));
        webcamTexture.Play();
        Status = "Waiting for webcam frames";
    }

    public override bool TryGetFrame(out ImageFrame frame)
    {
        frame = default;
        if (!running || !isActiveAndEnabled) return false;
        if (restartRequested) { StartSource(); return false; }
        if (webcamTexture == null || !webcamTexture.isPlaying || !webcamTexture.didUpdateThisFrame ||
            webcamTexture.width <= 16 || webcamTexture.height <= 16) return false;

        VideoRotationAngle = webcamTexture.videoRotationAngle;
        VideoVerticallyMirrored = webcamTexture.videoVerticallyMirrored;
        int rotation = ((correctVideoOrientation ? VideoRotationAngle : 0) + additionalQuarterTurns * 90) % 360;
        bool mirror = (correctVideoOrientation && VideoVerticallyMirrored) ^ additionalVerticalFlip;
        int newWidth = webcamTexture.width, newHeight = webcamTexture.height;
        if (!hasGeometry || newWidth != width || newHeight != height || rotation != lastRotation || mirror != lastMirror)
        {
            Revision++;
            hasGeometry = true;
            width = newWidth;
            height = newHeight;
            lastRotation = rotation;
            lastMirror = mirror;
            if (pixels == null || pixels.Length != width * height) pixels = new Color32[width * height];
        }
        webcamTexture.GetPixels32(pixels);
        Color32[] upright = orientation.Apply(pixels, width, height, rotation, mirror, out int w, out int h);
        frame = new ImageFrame(upright, w, h, ++frameId, webcamTexture.deviceName);
        Status = "Streaming";
        return true;
    }

    public override void StopSource()
    {
        if (permissionRoutine != null) StopCoroutine(permissionRoutine);
        permissionRoutine = null;
        if (webcamTexture != null)
        {
            if (webcamTexture.isPlaying) webcamTexture.Stop();
            Destroy(webcamTexture);
        }
        if (running) Revision++;
        running = false;
        restartRequested = hasGeometry = false;
        webcamTexture = null;
        pixels = null;
        orientation.Reset();
        Status = "Stopped";
    }

    // OnValidate may run outside the main thread. Actual restart happens on the next request.
    private void OnValidate() { restartRequested = true; }

    [ContextMenu("Restart source")]
    public void RestartSource() { if (Application.isPlaying && running) StartSource(); }

    [ContextMenu("List devices")]
    private void ListDevices()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        for (int i = 0; i < devices.Length; i++) Debug.Log(i + ": " + devices[i].name, this);
        if (devices.Length == 0) Debug.LogWarning("WebcamSource: no webcam device found.", this);
    }
}
