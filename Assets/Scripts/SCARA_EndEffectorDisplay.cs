using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Live, read-only readout of the end-effector's current X/Z and joint
/// angles, derived every frame from whatever pose joint1/joint2 are
/// actually in via SCARA_IKSolver.SolveFK. Holds no waypoint/trajectory
/// state and performs no IK — display only.
/// </summary>
public class SCARA_EndEffectorDisplay : MonoBehaviour
{
    [Header("Source (reuses SCARA_ReplayController's joint references)")]
    public SCARA_ReplayController replayController;

    [Header("Arm Lengths (must match SCARA_WaypointManager / SCARA_TrajectoryPlanner)")]
    public float lengthArm1 = 2f;
    public float lengthArm2 = 2f;

    [Header("UI Text Targets (top-of-screen fields)")]
    public Text positionText;      // single text for (X, Z)
    public Text theta1Text;
    public Text theta2Text;

    [Header("Formatting")]
    public string positionFormat = "End-Effector: ({0:F2}, {1:F2})";
    public string theta1Format = "\u03B81: {0:F1}\u00B0";
    public string theta2Format = "\u03B82: {0:F1}\u00B0";

    void Update()
    {
        if (replayController == null || replayController.joint1 == null || replayController.joint2 == null)
            return;

        float theta1 = Mathf.DeltaAngle(0f, replayController.joint1.localEulerAngles.y);
        float theta2 = Mathf.DeltaAngle(0f, replayController.joint2.localEulerAngles.y);

        Vector2 xz = SCARA_IKSolver.SolveFK(theta1, theta2, lengthArm1, lengthArm2);

        if (positionText != null)
            positionText.text = string.Format(positionFormat, xz.x, xz.y);
        if (theta1Text != null)
            theta1Text.text = string.Format(theta1Format, theta1);
        if (theta2Text != null)
            theta2Text.text = string.Format(theta2Format, theta2);
    }
}
