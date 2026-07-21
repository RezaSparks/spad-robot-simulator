using UnityEngine;

public static class SCARA_IKSolver
{
    public const float THETA1_MIN = -160f;
    public const float THETA1_MAX = 160f;
    public const float THETA2_MIN = -170f;
    public const float THETA2_MAX = 170f;

    public static bool SolveIK(Vector2 targetXZ, float L1, float L2, bool elbowUp,
                                out float theta1Deg, out float theta2Deg, out bool wasClamped)
    {
        float dx = targetXZ.x;
        float dz = targetXZ.y;
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

    public static Vector2 SolveFK(float theta1Deg, float theta2Deg, float L1, float L2)
    {
        float t1 = theta1Deg * Mathf.Deg2Rad;
        float t2 = theta2Deg * Mathf.Deg2Rad;
        float x = L1 * Mathf.Sin(t1) + L2 * Mathf.Sin(t1 + t2);
        float z = L1 * Mathf.Cos(t1) + L2 * Mathf.Cos(t1 + t2);
        return new Vector2(x, z);
    }

    public static bool ClampAngles(ref float theta1Deg, ref float theta2Deg)
    {
        bool clamped = false;
        if (theta1Deg < THETA1_MIN) { theta1Deg = THETA1_MIN; clamped = true; }
        if (theta1Deg > THETA1_MAX) { theta1Deg = THETA1_MAX; clamped = true; }
        if (theta2Deg < THETA2_MIN) { theta2Deg = THETA2_MIN; clamped = true; }
        if (theta2Deg > THETA2_MAX) { theta2Deg = THETA2_MAX; clamped = true; }
        return clamped;
    }

    public static Vector2 ClampAnglesAndUpdateFK(ref float theta1Deg, ref float theta2Deg, float L1, float L2)
    {
        ClampAngles(ref theta1Deg, ref theta2Deg);
        return SolveFK(theta1Deg, theta2Deg, L1, L2);
    }

    public static bool IsWithinLimits(float t1, float t2)
    {
        return t1 >= THETA1_MIN && t1 <= THETA1_MAX &&
               t2 >= THETA2_MIN && t2 <= THETA2_MAX;
    }

    public static float UnwrapAngle(float currentDeg, float targetDeg)
    {
        float delta = targetDeg - currentDeg;
        while (delta > 180f) delta -= 360f;
        while (delta < -180f) delta += 360f;
        return currentDeg + delta;
    }
}