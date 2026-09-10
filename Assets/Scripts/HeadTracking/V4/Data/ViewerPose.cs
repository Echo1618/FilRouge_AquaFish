using UnityEngine;

/// <summary>Screen-axis meters, ignoring Transform scale. The viewer is on negative Z.</summary>
public struct ViewerPose
{
    public bool valid;
    public Vector3 position;
    public float rollDegrees;

    public ViewerPose(bool valid, Vector3 position, float rollDegrees = 0f)
    {
        this.valid = valid;
        this.position = position;
        this.rollDegrees = rollDegrees;
    }
}
