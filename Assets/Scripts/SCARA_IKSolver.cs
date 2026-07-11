using UnityEngine;

/// <summary>
/// Pure analytical 2-DOF IK/FK math. No MonoBehaviour, no state.
/// Z-axis is the reference direction (matches the project's arm rest pose).
/// </summary>
public static class SCARA_IKSolver
{
    public static bool SolveIK(Vector2 targetXZ, float L1, float L2, bool elbowUp,
                                out float theta1Deg, out float theta2Deg, out bool wasClamped)
    {
        float dx = targetXZ.x;
        float dz = targetXZ.y; // Vector2.y stores world Z by convention in this project

        float d = Mathf.Sqrt(dx * dx + dz * dz);
        float maxReach = L1 + L2;
        float minReach = Mathf.Abs(L1 - L2);

        wasClamped = d > maxReach || d < minReach;
        d = Mathf.Clamp(d, minReach, maxReach);

        float cosTheta2 = (d * d - L1 * L1 - L2 * L2) / (2f * L1 * L2);
        cosTheta2 = Mathf.Clamp(cosTheta2, -1f, 1f);
        float theta2 = Mathf.Acos(cosTheta2) * (elbowUp ? 1f : -1f);

        float k1 = L1 + L2 * Mathf.Cos(theta2);
        float k2 = L2 * Mathf.Sin(theta2);
        float theta1 = Mathf.Atan2(dx, dz) - Mathf.Atan2(k2, k1);

        theta1Deg = theta1 * Mathf.Rad2Deg;
        theta2Deg = theta2 * Mathf.Rad2Deg;
        return !wasClamped;
    }

    /// <summary>Forward kinematics: joint angles -> (X,Z) position. Used to
    /// keep trajectory CSV's X/Z column consistent with its own angles.</summary>
    public static Vector2 SolveFK(float theta1Deg, float theta2Deg, float L1, float L2)
    {
        float t1 = theta1Deg * Mathf.Deg2Rad;
        float t2 = theta2Deg * Mathf.Deg2Rad;

        float x = L1 * Mathf.Sin(t1) + L2 * Mathf.Sin(t1 + t2);
        float z = L1 * Mathf.Cos(t1) + L2 * Mathf.Cos(t1 + t2);
        return new Vector2(x, z);
    }
}
