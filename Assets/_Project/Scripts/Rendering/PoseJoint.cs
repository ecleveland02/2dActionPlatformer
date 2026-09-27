namespace Margin.Rendering
{
    /// <summary>
    /// The 10 rotatable joints of the stick figure. "Front" is the limb nearer the camera, "Back" the one
    /// behind the body (drawn lighter). For a right-facing figure, a positive angle always swings the bone
    /// toward the facing direction (forward). Knees bend with negative angles, elbows with positive.
    /// </summary>
    public enum PoseJoint
    {
        Spine,          // at the hips, tilts the torso
        Neck,           // at the chest, tilts the head
        ShoulderFront,
        ElbowFront,
        ShoulderBack,
        ElbowBack,
        HipFront,
        KneeFront,
        HipBack,
        KneeBack,
    }
}
