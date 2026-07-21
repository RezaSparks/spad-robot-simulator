using System.Collections.Generic;
using UnityEngine;

public static class SCARA_CollisionChecker
{
    private const float COLLISION_TOLERANCE = 0.6f;

    // How far (as a fraction of the collision threshold) we push a segment endpoint
    // away from the base pivot before measuring base collision. This exempts the
    // portion of a link that is physically mounted on the base housing, without
    // ignoring real collisions further along the link.
    private const float BASE_MOUNT_OFFSET_FACTOR = 0.4f;

    // Distance (world units) under which a point is considered "at the base pivot"
    // for the purpose of the mount-offset fix above.
    private const float BASE_MOUNT_EPSILON = 0.01f;

    public static bool CheckSelfCollision(
        float theta1Deg, float theta2Deg,
        float L1, float L2,
        float armRadius,
        float baseRadius)
    {
        Vector2 elbow = SCARA_IKSolver.SolveFK(theta1Deg, 0f, L1, 0f);
        Vector2 endEffector = SCARA_IKSolver.SolveFK(theta1Deg, theta2Deg, L1, L2);

        float thresholdBase = (baseRadius + armRadius) * COLLISION_TOLERANCE;
        if (Vector2.Distance(endEffector, Vector2.zero) < thresholdBase)
            return true;

        float distToArm1 = DistancePointToSegment(endEffector, Vector2.zero, elbow);
        float thresholdArm = armRadius * 1.2f;
        if (distToArm1 < thresholdArm)
            return true;

        return false;
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        float abSqr = ab.sqrMagnitude;
        if (abSqr < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / abSqr);
        Vector2 closest = a + t * ab;
        return Vector2.Distance(p, closest);
    }

    /// <summary>
    /// Tests whether a link segment (p1 -> p2) collides with the physical base column.
    /// FIX: link 1 always starts exactly at the base pivot (Vector2.zero). Measuring
    /// distance-to-origin on a segment that itself starts at the origin always returns 0,
    /// which previously made this function report "colliding" for every single configuration
    /// of link 1, regardless of angle. That silently defeated any elbow-flip fallback logic,
    /// because there was never a collision-free candidate to fall back to.
    /// The fix: if either endpoint of the segment sits at (or very near) the base pivot,
    /// nudge that endpoint outward along the link direction by a small offset before testing.
    /// This represents the fact that the link is physically mounted on the base housing and
    /// is expected to be close to it there - we only want to flag genuine intrusions further
    /// along the link, not the mount point itself.
    /// </summary>
    public static bool IsBaseColliding(Vector2 p1, Vector2 p2, float baseRadius, float armRadius)
    {
        float threshold = (baseRadius + armRadius) * COLLISION_TOLERANCE;

        Vector2 segStart = p1;
        Vector2 segEnd = p2;
        float startOffsetAmount = threshold * BASE_MOUNT_OFFSET_FACTOR;

        if (p1.magnitude < BASE_MOUNT_EPSILON)
        {
            Vector2 dir = p2 - p1;
            float len = dir.magnitude;
            if (len > 0.0001f)
            {
                dir /= len;
                float offset = Mathf.Min(startOffsetAmount, len);
                segStart = p1 + dir * offset;
            }
        }
        else if (p2.magnitude < BASE_MOUNT_EPSILON)
        {
            Vector2 dir = p1 - p2;
            float len = dir.magnitude;
            if (len > 0.0001f)
            {
                dir /= len;
                float offset = Mathf.Min(startOffsetAmount, len);
                segEnd = p2 + dir * offset;
            }
        }

        float dist = DistancePointToSegment(Vector2.zero, segStart, segEnd);
        return dist < threshold;
    }

    public static bool IsObstacleColliding(
        float theta1Deg, float theta2Deg,
        float L1, float L2,
        float armRadius,
        Vector2 boxCenter, Vector2 boxSize)
    {
        Vector2 elbow = SCARA_IKSolver.SolveFK(theta1Deg, 0f, L1, 0f);
        Vector2 endEffector = SCARA_IKSolver.SolveFK(theta1Deg, theta2Deg, L1, L2);

        Vector2 halfSize = boxSize * 0.5f + Vector2.one * armRadius * 0.5f;
        Vector2 min = boxCenter - halfSize;
        Vector2 max = boxCenter + halfSize;

        if (SegmentIntersectsAABB(Vector2.zero, elbow, min, max)) return true;
        if (SegmentIntersectsAABB(elbow, endEffector, min, max)) return true;
        return false;
    }

    private static bool SegmentIntersectsAABB(Vector2 p1, Vector2 p2, Vector2 min, Vector2 max)
    {
        Vector2 dir = p2 - p1;
        float tMin = 0f, tMax = 1f;

        if (Mathf.Abs(dir.x) > 0.0001f)
        {
            float t1 = (min.x - p1.x) / dir.x;
            float t2 = (max.x - p1.x) / dir.x;
            if (t1 > t2) { float temp = t1; t1 = t2; t2 = temp; }
            tMin = Mathf.Max(tMin, t1);
            tMax = Mathf.Min(tMax, t2);
            if (tMin > tMax) return false;
        }
        else if (p1.x < min.x || p1.x > max.x) return false;

        if (Mathf.Abs(dir.y) > 0.0001f)
        {
            float t1 = (min.y - p1.y) / dir.y;
            float t2 = (max.y - p1.y) / dir.y;
            if (t1 > t2) { float temp = t1; t1 = t2; t2 = temp; }
            tMin = Mathf.Max(tMin, t1);
            tMax = Mathf.Min(tMax, t2);
            if (tMin > tMax) return false;
        }
        else if (p1.y < min.y || p1.y > max.y) return false;

        return true;
    }

    /// <summary>
    /// Convenience helper: is a given theta1 angle within the mechanical shoulder limits?
    /// Used by the trajectory planner's forbidden-wedge routing logic.
    /// </summary>
    public static bool IsTheta1WithinLimits(float theta1Deg)
    {
        return theta1Deg >= SCARA_IKSolver.THETA1_MIN && theta1Deg <= SCARA_IKSolver.THETA1_MAX;
    }

    /// <summary>
    /// Resolves a target position to a valid, collision-free joint configuration.
    /// NOTE (behavior change): this now also rejects configurations that are outside
    /// the mechanical joint limits (theta1/theta2), not just physically colliding ones.
    /// Previously this method's name implied limit-checking ("WithLimits") but it never
    /// actually called SCARA_IKSolver.IsWithinLimits - a config sitting past +-160/+-170
    /// degrees could be silently returned as "valid". Both elbow configurations are still
    /// tried in the caller's preferred order; if the first fails (collision OR out-of-limits),
    /// the other elbow is tried before giving up.
    /// hasCollision now means "not a usable, in-limits, collision-free configuration" -
    /// its name is preserved for signature compatibility.
    /// </summary>
    public static bool SolveIKWithLimitsAndCollision(
        Vector2 targetXZ,
        float L1, float L2,
        bool preferElbowUp,
        float minSafeTheta2Deg,
        float armRadius,
        float baseBodyRadius,
        Vector2 boxCenter, Vector2 boxSize,
        out float theta1Deg, out float theta2Deg,
        out bool wasClamped,
        out bool hasCollision)
    {
        theta1Deg = 0f;
        theta2Deg = 0f;
        wasClamped = false;
        hasCollision = false;

        bool[] configs = preferElbowUp ? new bool[] { true, false } : new bool[] { false, true };

        float lastT1 = 0f, lastT2 = 0f;
        bool lastWasClamped = false;
        bool anyValid = false;

        foreach (bool elbowUp in configs)
        {
            if (!SCARA_IKSolver.SolveIK(targetXZ, L1, L2, elbowUp, out float t1, out float t2, out bool clamped))
                continue;

            wasClamped = clamped;
            anyValid = true;

            bool outOfLimits = !SCARA_IKSolver.IsWithinLimits(t1, t2);
            bool selfCol = CheckSelfCollision(t1, t2, L1, L2, armRadius, baseBodyRadius);
            Vector2 elbow = SCARA_IKSolver.SolveFK(t1, 0f, L1, 0f);
            Vector2 endEff = SCARA_IKSolver.SolveFK(t1, t2, L1, L2);
            bool baseCol = IsBaseColliding(elbow, endEff, baseBodyRadius, armRadius);
            bool boxCol = IsObstacleColliding(t1, t2, L1, L2, armRadius, boxCenter, boxSize);

            bool invalid = outOfLimits || selfCol || baseCol || boxCol;

            if (!invalid)
            {
                theta1Deg = t1;
                theta2Deg = t2;
                hasCollision = false;
                return true;
            }

            lastT1 = t1;
            lastT2 = t2;
            lastWasClamped = clamped;
        }

        if (!anyValid)
        {
            hasCollision = true;
            return false;
        }

        theta1Deg = lastT1;
        theta2Deg = lastT2;
        wasClamped = lastWasClamped;
        hasCollision = true;
        return true;
    }

    public static (bool hit, List<string> reasons) CheckAll(
        float theta1Deg, float theta2Deg, float L1, float L2,
        float armRadius, float baseRadius, Vector2 boxCenter, Vector2 boxSize, float minSafeTheta2Deg)
    {
        var reasons = new List<string>();

        if (!SCARA_IKSolver.IsWithinLimits(theta1Deg, theta2Deg))
            reasons.Add("joint limit exceeded");

        if (CheckSelfCollision(theta1Deg, theta2Deg, L1, L2, armRadius, baseRadius))
            reasons.Add("self-collision");

        Vector2 p1 = SCARA_IKSolver.SolveFK(theta1Deg, 0f, L1, 0f);
        Vector2 p2 = SCARA_IKSolver.SolveFK(theta1Deg, theta2Deg, L1, L2);

        if (IsBaseColliding(p1, p2, baseRadius, armRadius))
            reasons.Add("base collision");

        if (IsObstacleColliding(theta1Deg, theta2Deg, L1, L2, armRadius, boxCenter, boxSize))
            reasons.Add("obstacle collision");

        return (reasons.Count > 0, reasons);
    }
}