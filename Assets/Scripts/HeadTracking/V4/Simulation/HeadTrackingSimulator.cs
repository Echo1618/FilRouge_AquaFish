using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Produces synthetic measurements only when requested by TrackingController.</summary>
public sealed class HeadTrackingSimulator : MonoBehaviour
{
    [Header("Simulated camera")]
    [Min(32)] [SerializeField] private int frameWidth = 640;
    [Min(32)] [SerializeField] private int frameHeight = 480;
    [Min(1f)] [SerializeField] private float sampleRate = 30f;
    [Header("Head")]
    [SerializeField] private Vector2 headCenter = new Vector2(320f, 240f);
    [SerializeField] private float eyeDistance = 120f;
    [SerializeField] private float headAngle;
    [SerializeField] private bool redEyeOnLeft = true;
    [SerializeField] private Vector2Int eyeBoxSize = new Vector2Int(70, 45);
    [Header("Controls")]
    [SerializeField] private float movementSpeed = 150f;
    [SerializeField] private float eyeDistanceSpeed = 60f;
    [SerializeField] private float rotationSpeed = 50f;
    [SerializeField] private float minEyeDistance = 40f;
    [SerializeField] private float maxEyeDistance = 250f;
    [Header("Test conditions")]
    public bool simulateLoss;
    public bool simulateStall;
    [Min(0f)] public float pairScore;
    [Min(0f)] public float centerNoisePixels;
    [Min(0f)] public float eyeDistanceNoisePixels;
    private float nextSampleTime;
    private readonly System.Random random = new System.Random(7321);
    public int FrameWidth => frameWidth;
    public int FrameHeight => frameHeight;
    public int Revision { get; private set; }

    public void BeginSession() { nextSampleTime = 0f; Revision++; }
    public bool TryGetDetection(float now, float dt, out DetectionResult result)
    {
        result = default;
        if (!isActiveAndEnabled) return false;
        HandleControls(dt);
        if (simulateStall || now < nextSampleTime) return false;
        nextSampleTime = now + 1f / Mathf.Max(1f, sampleRate);
        if (!simulateLoss) result = GetDetectionResult();
        return true;
    }

    public DetectionResult GetDetectionResult()
    {
        Vector2 center = headCenter + new Vector2(Noise(), Noise()) * centerNoisePixels;
        float separation = Mathf.Max(1f, eyeDistance + Noise() * eyeDistanceNoisePixels);
        float angle = headAngle * Mathf.Deg2Rad;
        Vector2 half = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * separation * 0.5f;
        Vector2 red = redEyeOnLeft ? center - half : center + half;
        Vector2 blue = redEyeOnLeft ? center + half : center - half;
        return new DetectionResult
        {
            detected = true, hasCandidate = true, pairScore = pairScore,
            headCenter = center, eyeDistance = separation,
            headAngle = Mathf.Repeat(headAngle + 90f, 180f) - 90f,
            redEye = red, blueEye = blue, redBox = Box(red), blueBox = Box(blue)
        };
    }

    private float Noise() => (float)(random.NextDouble() * 2.0 - 1.0);
    private RectInt Box(Vector2 center) => new RectInt(
        Mathf.RoundToInt(center.x - eyeBoxSize.x * 0.5f), Mathf.RoundToInt(center.y - eyeBoxSize.y * 0.5f),
        eyeBoxSize.x, eyeBoxSize.y);

    private void HandleControls(float dt)
    {
        Vector2 move = Vector2.zero;
        float depth = 0f, roll = 0f;
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k != null)
        {
            move.x = (k.rightArrowKey.isPressed ? 1 : 0) - (k.leftArrowKey.isPressed ? 1 : 0);
            move.y = (k.upArrowKey.isPressed ? 1 : 0) - (k.downArrowKey.isPressed ? 1 : 0);
            depth = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
            roll = (k.qKey.isPressed ? 1 : 0) - (k.eKey.isPressed ? 1 : 0);
            if (k.spaceKey.wasPressedThisFrame) ResetSimulation();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        move.x = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
        move.y = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
        depth = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
        roll = (Input.GetKey(KeyCode.Q) ? 1 : 0) - (Input.GetKey(KeyCode.E) ? 1 : 0);
        if (Input.GetKeyDown(KeyCode.Space)) ResetSimulation();
#endif
        headCenter += move * movementSpeed * dt;
        headCenter.x = Mathf.Clamp(headCenter.x, 0f, frameWidth);
        headCenter.y = Mathf.Clamp(headCenter.y, 0f, frameHeight);
        eyeDistance = Mathf.Clamp(eyeDistance + depth * eyeDistanceSpeed * dt,
            Mathf.Max(1f, minEyeDistance), Mathf.Max(minEyeDistance, maxEyeDistance));
        headAngle = Mathf.Clamp(headAngle + roll * rotationSpeed * dt, -80f, 80f);
    }

    [ContextMenu("Reset simulation")]
    public void ResetSimulation()
    {
        headCenter = new Vector2(frameWidth, frameHeight) * 0.5f;
        eyeDistance = 120f;
        headAngle = 0f;
        simulateLoss = simulateStall = false;
        Revision++;
    }
}
