using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Dependency-free regression tests. Also callable with Unity -executeMethod.</summary>
public static class AnaglyphV3SelfTests
{
    private static int checks;

    [MenuItem("Tools/Anaglyph/Run V3 self tests")]
    public static void Run()
    {
        checks = 0;
        TestDetection();
        TestSegmentation();
        TestTemporalTracking();
        TestPipeline();
        TestMappingAndSmoothing();
        TestOrientation();
        TestProjection();
        TestStatistics();
        Debug.Log("Anaglyph V3: " + checks + " checks passed.");
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Anaglyph V3 regression: " + message);
    }
    private static void Near(float actual, float expected, string message, float tolerance = 0.0001f) =>
        Check(Mathf.Abs(actual - expected) <= tolerance, message + ": " + actual + " vs " + expected);
    private static DetectionResult D(float x = 320f, float y = 240f, float e = 120f) =>
        new DetectionResult { detected = true, hasCandidate = true, headCenter = new Vector2(x, y), eyeDistance = e };

    private static void TestDetection()
    {
        var settings = new DetectionSettings { useClosing = false, minRegionArea = 10 };
        var pixels = new Color32[640 * 480];
        for (int y = 230; y < 250; y++)
            for (int x = 250; x < 270; x++)
            {
                pixels[y * 640 + x] = new Color32(255, 0, 0, 255);
                pixels[y * 640 + x + 120] = new Color32(0, 0, 255, 255);
            }
        var detector = new HeadDetector(settings);
        var diagnostics = new DetectionDiagnostics();
        DetectionResult result = detector.Detect(new ImageFrame(pixels, 640, 480, 1, "synthetic"), diagnostics);
        Check(result.detected && result.hasCandidate, "painted-frame synthetic pair");
        Near(result.eyeDistance, 120f, "marker separation");
        Near(result.headCenter.x, 320f, "box-center convention");
        Near(result.pairScore, 0f, "perfect pair cost");
        settings.pairing.expectedColorOrder = ColorOrder.RedOnImageRight;
        Check(!detector.Detect(new ImageFrame(pixels, 640, 480, 2, "wrong order"), diagnostics).detected, "color order gate");
        settings.pairing = new GlassesPairSettings();
        Check(detector.Detect(new ImageFrame(pixels, 640, 480, 3, "live settings"), diagnostics).detected, "replaced settings object is live");
        Array.Clear(pixels, 0, pixels.Length);
        Check(!detector.Detect(new ImageFrame(pixels, 640, 480, 4, "empty"), diagnostics).detected, "no mask history leak");

        var reds = new List<Region> { new Region { Area = 100, Center = new Vector2(100, 100), Bounds = new RectInt(95, 95, 10, 10) } };
        var blues = new List<Region> { new Region { Area = 100, Center = new Vector2(200, 145), Bounds = new RectInt(195, 140, 10, 10) } };
        var selector = new GlassesPairSelector();
        var pairSettings = new GlassesPairSettings { maxAcceptedScore = 0.2f };
        Check(!selector.TrySelect(reds, blues, pairSettings, out Region red, out Region blue, out float score), "bad score rejected");
        Check(TrackingMath.IsFinite(score) && score > 0.2f && red.Area == 100, "rejected candidate score retained");
        pairSettings.maxAcceptedScore = 1f;
        blues[0] = new Region { Area = 1000, Center = new Vector2(200, 100), Bounds = new RectInt(190, 90, 20, 20) };
        Check(!selector.TrySelect(reds, blues, pairSettings, out red, out blue, out score), "strict area ratio");
        var mask = new byte[] { 0, 0, 0, 0, 255, 0, 0, 0, 0 };
        var output = new byte[9];
        ImageFilters.Dilate3x3(mask, output, 3, 3);
        Array.Clear(mask, 0, mask.Length);
        ImageFilters.Dilate3x3(mask, output, 3, 3);
        for (int i = 0; i < output.Length; i++) Check(output[i] == 0, "reused dilation buffer cleared");
    }

    private static void TestSegmentation()
    {
        var extractor = new RegionExtractor();
        var candidates = new List<Region>();
        byte[] mask = { 255, 255, 255, 255, 255, 255 };
        int[] labels = { 0, 0, 1, 0, 0, 1 };
        extractor.Extract(mask, 3, 2, 1, candidates, labels);
        Check(candidates.Count == 2, "components cannot cross a segment boundary");
        extractor.Extract(mask, 3, 2, 1, candidates);
        Check(candidates.Count == 1 && candidates[0].Area == 6, "extractor reuse and optional segmentation");
        var image = new Color32[8 * 6];
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 4; x++) image[y * 8 + x] = new Color32(255, 255, 255, 255);
        var diagnostics = new DetectionDiagnostics(); diagnostics.EnsureSize(image.Length);
        var segmenter = new RegionSegmenter();
        var settings = new RegionSegmentationSettings { gradientThreshold = 50, dilateEdges = false };
        Check(segmenter.Segment(image, 8, 6, diagnostics, settings) == 2, "image-wide edge remains a barrier at image borders");
        Array.Clear(image, 0, image.Length);
        Check(segmenter.Segment(image, 8, 6, diagnostics, settings) == 1, "segmentation clears old edges and labels");
    }

    private static void TestTemporalTracking()
    {
        var s = new ContinuitySettings();
        var f = new DetectionContinuityFilter();
        f.Process(D(), 640, 480, 0f, s);
        Check(f.MeasurementAccepted && f.ResetSmoothing, "initial acquisition");
        f.Process(D(323), 640, 480, 0.033f, s);
        Check(f.MeasurementAccepted && !f.ResetSmoothing, "small fluctuation is accepted without reset");
        f.Process(D(550), 640, 480, 0.066f, s);
        Check(!f.MeasurementAccepted && f.State == TrackingState.ShortLost, "large jump rejected");
        Near(f.Accepted.headCenter.x, 323f, "hold previous accepted geometry");
        f.Process(D(551), 640, 480, 0.099f, s);
        Check(!f.MeasurementAccepted, "two confirmation frames are insufficient");
        f.Process(D(552), 640, 480, 0.132f, s);
        Check(f.MeasurementAccepted && f.ResetSmoothing, "distant stable reacquisition");
        f.Process(D(552, 240, 200), 640, 480, 0.165f, s);
        Check(!f.MeasurementAccepted, "eye distance discontinuity rejected");
        f.Tick(0.4f, true, s);
        Check(f.State == TrackingState.Lost, "stalled stream becomes Lost");
        f.Process(D(552), 640, 480, 0.41f, s);
        Check(!f.MeasurementAccepted, "nearby return after Lost requires confirmation");
        f.Process(D(552), 640, 480, 0.44f, s);
        f.Process(D(552), 640, 480, 0.47f, s);
        Check(f.MeasurementAccepted && f.ResetSmoothing, "nearby reacquisition resets smoothing");
        f.Reset();
        f.Process(D(15, 15), 640, 480, 1f, s);
        f.Process(D(16, 15), 640, 480, 1.03f, s);
        f.Process(D(15, 16), 640, 480, 1.06f, s);
        Check(f.MeasurementAccepted, "initial off-center target is not permanently blocked");
        f.Reset();
        f.Process(D(), 640, 480, 2f, s);
        f.Process(D(550), 640, 480, 2.03f, s);
        f.Process(default, 640, 480, 2.06f, s);
        f.Process(D(550), 640, 480, 2.09f, s);
        f.Process(D(550), 640, 480, 2.12f, s);
        Check(!f.MeasurementAccepted, "missing frame breaks confirmation");
        DetectionResult bad = D(); bad.eyeDistance = float.NaN;
        f.Process(bad, 640, 480, 2.15f, s);
        Check(!f.MeasurementAccepted, "NaN detection rejected");
        f.Reset();
        f.Process(D(), 640, 480, 3f, s);
        f.Process(D(550), 640, 480, 3.03f, s);
        f.Process(D(550), 640, 480, 3.30f, s);
        f.Process(D(550), 640, 480, 3.33f, s);
        Check(!f.MeasurementAccepted, "long gap restarts pending confirmation");
    }

    private static void TestPipeline()
    {
        var p = new TrackingPipeline();
        var temporal = new ContinuitySettings();
        var mapping = new PoseMappingSettings();
        var smoothing = new SmoothingSettings();
        p.Reset(); p.Configure(temporal, smoothing);
        p.Process(D(), 640, 480, 0f, false, temporal);
        TrackingSnapshot snap = p.Tick(0f, 0.01f, mapping, temporal, smoothing);
        Check(snap.filteredPose.valid, "pipeline first pose valid");
        long id = snap.sampleId;
        p.Process(D(340), 640, 480, 0.03f, false, temporal);
        snap = p.Tick(0.03f, 0.01f, mapping, temporal, smoothing);
        Vector3 before = snap.filteredPose.position;
        p.Process(default, 640, 480, 0.06f, false, temporal);
        snap = p.Tick(0.06f, 0.03f, mapping, temporal, smoothing);
        Check(snap.state == TrackingState.ShortLost && snap.filteredPose.position == before, "short loss freezes displayed pose exactly");
        snap = p.Tick(0.3f, 0.24f, mapping, temporal, smoothing);
        Check(snap.state == TrackingState.Lost && !snap.filteredPose.valid, "Lost invalidates output pose");
        Check(snap.sampleId == id + 2, "render ticks do not create detection samples");
        p.Reset();
        p.Process(D(), 640, 480, 0f, true, temporal);
        p.Tick(0f, 0.01f, mapping, temporal, smoothing);
        snap = p.Tick(10f, 0.01f, mapping, temporal, smoothing);
        Check(snap.state == TrackingState.Tracking, "one-shot static image is not a stalled webcam");
        int session = snap.session;
        p.Reset();
        Check(p.Snapshot.session != session && p.Snapshot.sampleId == 0, "session reset clears samples");
    }

    private static void TestMappingAndSmoothing()
    {
        var mapper = new HeadPoseMapper();
        var s = new PoseMappingSettings { xyMode = XYMappingMode.Metric, focalX = 600f, focalY = 600f, invertTrackingX = false };
        ViewerPose pose = mapper.Map(D(380, 270, 112), 640, 480, s, out MappingStatus status);
        Near(pose.position.z, -67f / 112f, "inverse Z calibration");
        Near(pose.position.x, 60f * (67f / 112f) / 600f, "metric X");
        Near(pose.position.y, 30f * (67f / 112f) / 600f, "metric Y");
        s.invertTrackingX = true;
        s.webcamOffsetFromScreen = new Vector3(0.02f, 0.1f, -0.01f);
        ViewerPose flipped = mapper.Map(D(380, 270, 112), 640, 480, s, out status);
        Near(flipped.position.x, 0.02f - pose.position.x, "inversion before physical offset");
        Near(flipped.position.y, 0.1f + pose.position.y, "webcam vertical offset");
        s.focalX = 0f;
        Check(!mapper.Map(D(), 640, 480, s, out status).valid && status == MappingStatus.InvalidCalibration, "missing focal is explicit");
        s.focalX = 600f;
        Check(!mapper.Map(D(), 1280, 720, s, out status).valid && status == MappingStatus.ResolutionMismatch, "resolution mismatch cannot silently reuse calibration");
        s.useHeadRoll = true;
        DetectionResult rolled = D(); rolled.headAngle = 20f;
        Near(mapper.Map(rolled, 640, 480, s, out status).rollDegrees, -20f, "roll sign follows tracking-axis inversion");
        var a = new ViewerPoseSmoother(); var b = new ViewerPoseSmoother();
        var smooth = new SmoothingSettings();
        ViewerPose start = new ViewerPose(true, new Vector3(0f, 0f, -0.5f));
        ViewerPose target = new ViewerPose(true, new Vector3(0.1f, 0.2f, -0.8f));
        a.Filter(start, 0f, smooth); b.Filter(start, 0f, smooth);
        ViewerPose pa = default, pb = default;
        for (int i = 0; i < 100; i++) pa = a.Filter(target, 0.01f, smooth);
        for (int i = 0; i < 20; i++) pb = b.Filter(target, 0.05f, smooth);
        Near(pa.position.x, pb.position.x, "XY independent of timestep partition");
        Near(pa.position.z, pb.position.z, "Z independent of timestep partition");
        a.Reset();
        Near(a.Filter(target, 0.01f, smooth).position.x, target.position.x, "smoother reset snaps to new pose");
        Near(ViewerPoseSmoother.Alpha(7f, 0f), 0f, "zero dt");
    }

    private static void TestOrientation()
    {
        Color32[] pixels = new Color32[6];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)(i + 1), 0, 0, 255);
        int[][] expected = { new[] { 1, 2, 3, 4, 5, 6 }, new[] { 3, 6, 2, 5, 1, 4 },
            new[] { 6, 5, 4, 3, 2, 1 }, new[] { 4, 1, 5, 2, 6, 3 } };
        int[][] mirrored = { new[] { 4, 5, 6, 1, 2, 3 }, new[] { 6, 3, 5, 2, 4, 1 },
            new[] { 3, 2, 1, 6, 5, 4 }, new[] { 1, 4, 2, 5, 3, 6 } };
        var normalizer = new FrameOrientation();
        for (int flip = 0; flip < 2; flip++)
            for (int turn = 0; turn < 4; turn++)
            {
                Color32[] result = normalizer.Apply(pixels, 3, 2, turn * 90, flip != 0, out int w, out int h);
                Check(w == (turn % 2 == 0 ? 3 : 2) && h == (turn % 2 == 0 ? 2 : 3), "upright dimensions");
                for (int i = 0; i < 6; i++) Near(result[i].r, (flip == 0 ? expected : mirrored)[turn][i], "orientation mapping");
            }
        for (int i = 0; i < pixels.Length; i++) Near(pixels[i].r, i + 1, "input pixels never mutated");
    }

    private static void TestProjection()
    {
        const float width = 1.2f, height = 0.675f;
        Vector3 viewer = new Vector3(0.12f, -0.05f, -0.6f);
        Vector3 baseline = new Vector3(0.03f, 0.01f, 0f);
        Vector3[] corners = { new Vector3(-width / 2f, -height / 2f), new Vector3(width / 2f, height / 2f), Vector3.zero };
        foreach (Vector3 point in corners)
        {
            Vector3 left = Project(point, viewer - baseline, width, height);
            Vector3 right = Project(point, viewer + baseline, width, height);
            Near(left.x, right.x, "screen plane zero horizontal disparity with rolled baseline");
            Near(left.y, right.y, "screen plane zero vertical disparity");
            Near(left.x, 2f * point.x / width, "screen X maps to expected NDC");
            Near(left.y, 2f * point.y / height, "screen Y maps to expected NDC");
        }
        Vector3 behind = new Vector3(0f, 0f, 1f);
        Check(Mathf.Abs(Project(behind, viewer - baseline, width, height).x -
            Project(behind, viewer + baseline, width, height).x) > 0.001f, "behind-screen object has disparity");
        GameObject go = new GameObject("Screen test");
        try
        {
            go.transform.SetPositionAndRotation(new Vector3(2f, 3f, 4f), Quaternion.Euler(10f, 30f, 20f));
            go.transform.localScale = new Vector3(2f, 3f, 4f);
            Vector3 world = StereoProjection.ToWorld(viewer, go.transform);
            Vector3 recovered = Quaternion.Inverse(go.transform.rotation) * (world - go.transform.position);
            Near((recovered - viewer).magnitude, 0f, "screen translation rotation and scale-independent units");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    private static Vector3 Project(Vector3 point, Vector3 eye, float width, float height)
    {
        Vector3 relative = point - eye;
        Vector4 clip = StereoProjection.Build(eye, width, height, 0.1f, 100f) *
            new Vector4(relative.x, relative.y, -relative.z, 1f);
        return new Vector3(clip.x, clip.y, clip.z) / clip.w;
    }

    private static void TestStatistics()
    {
        var stats = new CalibrationStatistics(); stats.Configure(5);
        for (int i = 0; i < 10; i++) stats.Add(D(320 + i, 240, 100 + i));
        Check(stats.Count == 5, "rolling capacity");
        Near(stats.MeanCenter.x, 327f, "rolling mean center");
        Near(stats.MeanEyeDistance, 107f, "rolling mean marker distance");
        Near(stats.EyeDistanceStdDev, Mathf.Sqrt(2f), "rolling standard deviation");
        stats.Reset();
        Check(stats.Count == 0 && stats.MeanEyeDistance == 0f, "statistics reset");
    }
}
