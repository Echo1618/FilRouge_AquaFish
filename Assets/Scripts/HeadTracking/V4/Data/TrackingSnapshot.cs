using UnityEngine;

public enum TrackingState { Searching, Tracking, ShortLost, Lost }
public enum MeasurementStatus { None, Accepted, Rejected, Missing }
public enum MappingStatus { NoDetection, Valid, InvalidCalibration, ResolutionMismatch, InvalidGeometry }

/// <summary>Value-only snapshot. SampleId changes only when a new measurement is processed.</summary>
public struct TrackingSnapshot
{
    public long sampleId;
    public int session;
    public int frameWidth, frameHeight;
    public DetectionResult rawDetection, acceptedDetection;
    public ViewerPose rawPose, targetPose, filteredPose;
    public TrackingState state;
    public MeasurementStatus measurement;
    public MappingStatus mappingStatus;
    public bool reacquired, smoothingEnabled, continuityEnabled;
    public float sampleTime;
}
