using UnityEngine;

public struct ViewerPose
{
    public bool valid;

    public Vector3 position;

    public ViewerPose(bool valid, Vector3 position)
    {
        this.valid = valid;
        this.position = position;
    }
}