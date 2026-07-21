using System.Collections.Generic;
using UnityEngine;

public struct TrajectorySample
{
    public float time;
    public float x;
    public float z;
    public float theta1;
    public float theta2;
    public bool collision;
    public string collisionReason;
}

public class SCARA_TrajectoryPlanner : MonoBehaviour
{
    public float timeStep = 0.001f;
    public float maxDegreesPerSecond = 180f;
    public float lengthArm1 = 2f;
    public float lengthArm2 = 2f;

    [Header("Collision Settings")]
    public bool enableTrajectoryCollisionCheck = false;
    public bool continueOnCollision = true;
    public float armThicknessRadius = 0.15f;
    public float baseBodyRadius = 0.25f;
    public float minSafeTheta2Deg = 30f;
    public Vector2 boxCenter = Vector2.zero;
    public Vector2 boxSize = new Vector2(0.8f, 0.8f);

    [Header("Avoidance Settings")]
    [Tooltip("If a straight path collides, try solving the target with the opposite elbow configuration before attempting an arc reroute.")]
    public bool enableElbowFlipFallback = true;
    [Tooltip("If elbow-flip does not resolve the collision, insert intermediate waypoints that swing the arm around the base.")]
    public bool enableArcAvoidance = true;
    [Tooltip("Extra clearance (world units) beyond baseBodyRadius + armThicknessRadius that an avoidance arc will keep the end-effector away from the base.")]
    public float arcRadiusMargin = 0.4f;
    [Tooltip("Number of intermediate waypoints inserted per avoidance attempt. Escalates by this amount on each retry.")]
    public int arcResolution = 1;
    [Tooltip("Maximum number of escalating arc-avoidance attempts before giving up and shipping a best-effort (possibly colliding) path.")]
    public int maxAvoidanceAttempts = 3;

    [Header("Debug")]
    public bool verboseDebugLogs = true;

    private struct SegmentAvoidanceResult
    {
        public List<TrajectorySample> samples;
        public float duration;
        public float endTheta1;
        public float endTheta2;
        public bool stillColliding;
    }

    public TrajectorySample[] Generate(List<Waypoint> waypoints,
                                       float currentTheta1Deg,
                                       float currentTheta2Deg,
                                       out string errorMessage)
    {
        errorMessage = null;

        if (waypoints == null || waypoints.Count < 2)
        {
            errorMessage = "Need at least 2 waypoints.";
            return new TrajectorySample[0];
        }

        // Pass 1: resolve every waypoint to a concrete, validated joint configuration
        // (reachable, within joint limits, and collision-free where possible - trying
        // both elbow configurations before giving up on a single waypoint).
        bool elbowUp = currentTheta2Deg >= 0f;
        List<Waypoint> resolved = new List<Waypoint>(waypoints.Count);

        for (int i = 0; i < waypoints.Count; i++)
        {
            Waypoint wp = waypoints[i];

            bool ok = SCARA_CollisionChecker.SolveIKWithLimitsAndCollision(
                wp.xz, lengthArm1, lengthArm2, elbowUp, minSafeTheta2Deg,
                armThicknessRadius, baseBodyRadius, boxCenter, boxSize,
                out float t1, out float t2, out bool clamped, out bool hasCollision);

            if (!ok)
            {
                errorMessage = $"Waypoint #{i + 1} is unreachable (too far or too close).";
                return new TrajectorySample[0];
            }

            if (enableTrajectoryCollisionCheck && !continueOnCollision && hasCollision)
            {
                errorMessage = $"Waypoint #{i + 1} would cause a collision (self/base/obstacle) or exceeds joint limits, and no elbow configuration resolves it.";
                return new TrajectorySample[0];
            }

            wp.theta1Deg = t1;
            wp.theta2Deg = t2;
            resolved.Add(wp);

            // Keep elbow preference continuous into the next waypoint.
            elbowUp = t2 >= 0f;
        }

        // Pass 2: generate segment-by-segment, resolving forbidden-wedge crossings,
        // elbow collisions, and base-avoidance routing per segment.
        List<TrajectorySample> samplesList = new List<TrajectorySample>();
        float currentTime = 0f;
        float segStartTheta1 = currentTheta1Deg;
        float segStartTheta2 = currentTheta2Deg;

        for (int seg = 0; seg < resolved.Count; seg++)
        {
            SegmentAvoidanceResult result = GenerateSegmentWithAvoidance(
                segStartTheta1, segStartTheta2, resolved[seg], currentTime, seg);

            samplesList.AddRange(result.samples);
            currentTime += result.duration;
            segStartTheta1 = result.endTheta1;
            segStartTheta2 = result.endTheta2;
        }

        TrajectorySample[] finalResult = samplesList.ToArray();

        if (verboseDebugLogs)
            Debug.Log($"[TrajectoryPlanner] Generation complete. Total duration: {currentTime:F3}s, Total samples: {finalResult.Length}");

        return finalResult;
    }

    /// <summary>
    /// Picks the correct angular route for a single joint between two waypoints.
    /// UnwrapAngle always returns the shortest route. If that shortest route would
    /// require sweeping the joint outside its mechanical limits (i.e. through the
    /// forbidden wedge behind the base), this instead returns the other route around
    /// the circle, which is longer but stays within [minLimit, maxLimit] the whole way.
    /// </summary>
    private float ResolveWedgeSafeAngle(float startDeg, float rawTargetDeg, float minLimit, float maxLimit, out bool tookLongWay)
    {
        tookLongWay = false;
        float shortest = SCARA_IKSolver.UnwrapAngle(startDeg, rawTargetDeg);

        if (shortest >= minLimit && shortest <= maxLimit)
            return shortest;

        float longWay = (shortest > startDeg) ? shortest - 360f : shortest + 360f;

        if (longWay >= minLimit && longWay <= maxLimit)
        {
            tookLongWay = true;
            return longWay;
        }

        // Neither route is fully clean (should be rare if start/end are both valid).
        // Pick whichever violates the limit by the smaller amount so downstream
        // clamping does the least damage, and log it as a warning.
        float shortestViolation = Mathf.Max(minLimit - shortest, shortest - maxLimit, 0f);
        float longWayViolation = Mathf.Max(minLimit - longWay, longWay - maxLimit, 0f);

        if (verboseDebugLogs)
            Debug.LogWarning($"[TrajectoryPlanner] Neither angular route from {startDeg:F1} to {rawTargetDeg:F1} stays fully within [{minLimit},{maxLimit}]. Using the smaller violation.");

        return (longWayViolation < shortestViolation) ? longWay : shortest;
    }

    private float ComputeSegmentDuration(float startT1, float startT2, float endT1, float endT2, float speedPercent)
    {
        float dTheta1 = Mathf.Abs(endT1 - startT1);
        float dTheta2 = Mathf.Abs(endT2 - startT2);
        float angularDistance = Mathf.Max(dTheta1, dTheta2);
        float effectiveSpeed = maxDegreesPerSecond * (speedPercent / 100f);
        if (effectiveSpeed < 0.001f) effectiveSpeed = 0.001f;
        return angularDistance / effectiveSpeed;
    }

    /// <summary>
    /// Generates a single linear (in joint space) segment between two already-resolved
    /// angle pairs, running collision checks on every sample if enabled.
    /// Does NOT drop the last sample of the segment - callers may see a duplicated
    /// boundary sample between consecutive segments, which is intentional so each
    /// segment (including ones produced by avoidance routing) is self-contained.
    /// </summary>
    private SegmentAvoidanceResult BuildSegment(float startT1, float startT2, float rawTargetT1, float rawTargetT2, float speedPercent, float timeOffset)
    {
        bool tookLongWayT1;
        float safeT1 = ResolveWedgeSafeAngle(startT1, rawTargetT1, SCARA_IKSolver.THETA1_MIN, SCARA_IKSolver.THETA1_MAX, out tookLongWayT1);
        bool tookLongWayT2;
        float safeT2 = ResolveWedgeSafeAngle(startT2, rawTargetT2, SCARA_IKSolver.THETA2_MIN, SCARA_IKSolver.THETA2_MAX, out tookLongWayT2);

        if (tookLongWayT1 && verboseDebugLogs)
            Debug.Log($"[TrajectoryPlanner] Theta1 shortest path crosses the forbidden wedge behind the base. Routing the long way around instead ({startT1:F1} deg -> {safeT1:F1} deg).");

        float duration = ComputeSegmentDuration(startT1, startT2, safeT1, safeT2, speedPercent);
        int sampleCount = Mathf.CeilToInt(duration / timeStep) + 1;

        TrajectorySample[] samplesArr = GenerateSegmentSamples(startT1, startT2, safeT1, safeT2, duration, sampleCount, timeOffset);

        bool collides = false;
        if (enableTrajectoryCollisionCheck)
        {
            for (int i = 0; i < samplesArr.Length; i++)
            {
                var s = samplesArr[i];
                bool hit = SampleCollides(s.theta1, s.theta2);
                s.collision = hit;
                if (hit)
                {
                    collides = true;
                    var check = SCARA_CollisionChecker.CheckAll(
                        s.theta1, s.theta2, lengthArm1, lengthArm2,
                        armThicknessRadius, baseBodyRadius, boxCenter, boxSize, minSafeTheta2Deg);
                    s.collisionReason = string.Join(", ", check.reasons);
                }
                samplesArr[i] = s;
            }
        }

        return new SegmentAvoidanceResult
        {
            samples = new List<TrajectorySample>(samplesArr),
            duration = duration,
            endTheta1 = safeT1,
            endTheta2 = safeT2,
            stillColliding = collides
        };
    }

    /// <summary>
    /// Top-level per-segment resolution pipeline:
    /// 1. Try the direct (wedge-safe) path with the target's already-resolved elbow.
    /// 2. If that collides, try the opposite elbow configuration for the same XZ target.
    /// 3. If that also collides, escalate through arc-avoidance waypoints that swing
    ///    the arm around the base at a safe radius, increasing resolution each attempt.
    /// 4. If nothing resolves it within maxAvoidanceAttempts, ship the best-effort
    ///    (still-flagged) path rather than breaking playback.
    /// </summary>
    private SegmentAvoidanceResult GenerateSegmentWithAvoidance(
        float startT1, float startT2, Waypoint target, float timeOffset, int segmentIndex)
    {
        SegmentAvoidanceResult result = BuildSegment(startT1, startT2, target.theta1Deg, target.theta2Deg, target.speedPercent, timeOffset);

        if (!enableTrajectoryCollisionCheck || !result.stillColliding)
            return result;

        if (verboseDebugLogs)
            Debug.Log($"[TrajectoryPlanner] Segment {segmentIndex + 1}: direct path collides, trying opposite elbow configuration...");

        if (enableElbowFlipFallback)
        {
            bool currentElbowUp = target.theta2Deg >= 0f;
            if (SCARA_IKSolver.SolveIK(target.xz, lengthArm1, lengthArm2, !currentElbowUp, out float altT1, out float altT2, out bool altClamped) && !altClamped)
            {
                SegmentAvoidanceResult altResult = BuildSegment(startT1, startT2, altT1, altT2, target.speedPercent, timeOffset);
                if (!altResult.stillColliding)
                {
                    if (verboseDebugLogs)
                        Debug.Log($"[TrajectoryPlanner] Segment {segmentIndex + 1}: switched elbow configuration, path is now clean.");
                    return altResult;
                }

                if (verboseDebugLogs)
                    Debug.Log($"[TrajectoryPlanner] Segment {segmentIndex + 1}: opposite elbow configuration still collides.");
            }
            else if (verboseDebugLogs)
            {
                Debug.Log($"[TrajectoryPlanner] Segment {segmentIndex + 1}: opposite elbow configuration is unreachable for this target, skipping.");
            }
        }

        if (!enableArcAvoidance)
        {
            if (verboseDebugLogs)
                Debug.LogWarning($"[TrajectoryPlanner] Segment {segmentIndex + 1}: collision could not be resolved (arc avoidance disabled).");
            return result;
        }

        if (verboseDebugLogs)
            Debug.Log($"[TrajectoryPlanner] Segment {segmentIndex + 1}: both elbow configurations collide, attempting arc avoidance around the base...");

        for (int attempt = 1; attempt <= maxAvoidanceAttempts; attempt++)
        {
            int pointCount = arcResolution * attempt;
            SegmentAvoidanceResult arcResult = BuildArcSegment(startT1, startT2, target, timeOffset, pointCount);

            if (!arcResult.stillColliding)
            {
                if (verboseDebugLogs)
                    Debug.Log($"[TrajectoryPlanner] Segment {segmentIndex + 1}: arc avoidance succeeded with {pointCount} intermediate point(s) (attempt {attempt}/{maxAvoidanceAttempts}).");
                return arcResult;
            }

            if (verboseDebugLogs)
                Debug.Log($"[TrajectoryPlanner] Segment {segmentIndex + 1}: arc avoidance attempt {attempt}/{maxAvoidanceAttempts} with {pointCount} point(s) still collides, escalating...");
        }

        Debug.LogWarning($"[TrajectoryPlanner] Segment {segmentIndex + 1}: unable to find a collision-free path after {maxAvoidanceAttempts} arc avoidance attempts. Shipping best-effort path; colliding samples remain flagged.");
        return result;
    }

    /// <summary>
    /// Builds a segment that swings the arm around the base via 'pointCount' intermediate
    /// waypoints. Theta1 is interpolated along the wedge-safe route. Theta2 is tapered
    /// toward a "safe extended" angle at the midpoint of the arc (an angle that places the
    /// end-effector at baseBodyRadius + armThicknessRadius + arcRadiusMargin from the base,
    /// computed analytically via the law of cosines), then eased back to the target's
    /// theta2 as the arm approaches start and end.
    /// </summary>
    private SegmentAvoidanceResult BuildArcSegment(float startT1, float startT2, Waypoint target, float timeOffset, int pointCount)
    {
        bool tookLongWay;
        float safeTargetT1 = ResolveWedgeSafeAngle(startT1, target.theta1Deg, SCARA_IKSolver.THETA1_MIN, SCARA_IKSolver.THETA1_MAX, out tookLongWay);

        float safeT2Magnitude = ComputeSafeExtendedTheta2();
        float elbowSign = Mathf.Abs(target.theta2Deg) > 0.01f
            ? Mathf.Sign(target.theta2Deg)
            : (Mathf.Abs(startT2) > 0.01f ? Mathf.Sign(startT2) : 1f);
        float retractedPoleT2 = elbowSign * safeT2Magnitude;

        List<TrajectorySample> combinedSamples = new List<TrajectorySample>();
        float cumulativeTime = timeOffset;
        float curT1 = startT1;
        float curT2 = startT2;
        bool anyCollision = false;

        int totalLegs = pointCount + 1;
        for (int i = 1; i <= totalLegs; i++)
        {
            float frac = (float)i / totalLegs;
            float legT1;
            float legT2;

            if (i < totalLegs)
            {
                legT1 = Mathf.Lerp(startT1, safeTargetT1, frac);
                float blendTowardTarget = Mathf.Lerp(startT2, target.theta2Deg, frac);
                float taper = 1f - Mathf.Sin(frac * Mathf.PI); // 1 at both ends, 0 at the midpoint
                legT2 = Mathf.Lerp(retractedPoleT2, blendTowardTarget, taper);
            }
            else
            {
                // Final leg always lands exactly on the resolved target angles.
                legT1 = target.theta1Deg;
                legT2 = target.theta2Deg;
            }

            SegmentAvoidanceResult legResult = BuildSegment(curT1, curT2, legT1, legT2, target.speedPercent, cumulativeTime);
            combinedSamples.AddRange(legResult.samples);
            cumulativeTime += legResult.duration;
            curT1 = legResult.endTheta1;
            curT2 = legResult.endTheta2;
            if (legResult.stillColliding) anyCollision = true;
        }

        return new SegmentAvoidanceResult
        {
            samples = combinedSamples,
            duration = cumulativeTime - timeOffset,
            endTheta1 = curT1,
            endTheta2 = curT2,
            stillColliding = anyCollision
        };
    }

    /// <summary>
    /// Solves for the theta2 magnitude that places the end-effector at a target
    /// radial distance from the base pivot (baseBodyRadius + armThicknessRadius +
    /// arcRadiusMargin), using the same law-of-cosines relationship the IK solver
    /// uses in reverse. This gives a physically meaningful "safe extended" elbow
    /// angle rather than an arbitrary constant.
    /// </summary>
    private float ComputeSafeExtendedTheta2()
    {
        float desiredReach = baseBodyRadius + armThicknessRadius + arcRadiusMargin;
        float minReach = Mathf.Abs(lengthArm1 - lengthArm2) + 0.01f;
        float maxReach = lengthArm1 + lengthArm2 - 0.01f;
        desiredReach = Mathf.Clamp(desiredReach, minReach, maxReach);

        float cosT2 = (desiredReach * desiredReach - lengthArm1 * lengthArm1 - lengthArm2 * lengthArm2) / (2f * lengthArm1 * lengthArm2);
        cosT2 = Mathf.Clamp(cosT2, -1f, 1f);
        return Mathf.Acos(cosT2) * Mathf.Rad2Deg;
    }

    private TrajectorySample[] GenerateSegmentSamples(
        float startTheta1, float startTheta2,
        float endTheta1, float endTheta2,
        float duration, int sampleCount,
        float timeOffset)
    {
        TrajectorySample[] samples = new TrajectorySample[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i * timeStep;
            if (t > duration) t = duration;
            float frac = duration > 0.0001f ? t / duration : 1f;

            float theta1 = Mathf.Lerp(startTheta1, endTheta1, frac);
            float theta2 = Mathf.Lerp(startTheta2, endTheta2, frac);

            theta1 = Mathf.Clamp(theta1, SCARA_IKSolver.THETA1_MIN, SCARA_IKSolver.THETA1_MAX);
            theta2 = Mathf.Clamp(theta2, SCARA_IKSolver.THETA2_MIN, SCARA_IKSolver.THETA2_MAX);

            Vector2 xz = SCARA_IKSolver.SolveFK(theta1, theta2, lengthArm1, lengthArm2);

            samples[i] = new TrajectorySample
            {
                time = timeOffset + t,
                x = xz.x,
                z = xz.y,
                theta1 = theta1,
                theta2 = theta2,
                collision = false,
                collisionReason = null
            };
        }
        return samples;
    }

    private bool SampleCollides(float theta1, float theta2)
    {
        if (SCARA_CollisionChecker.CheckSelfCollision(theta1, theta2, lengthArm1, lengthArm2, armThicknessRadius, baseBodyRadius))
            return true;

        Vector2 elbow = SCARA_IKSolver.SolveFK(theta1, 0f, lengthArm1, 0f);
        Vector2 endEffector = SCARA_IKSolver.SolveFK(theta1, theta2, lengthArm1, lengthArm2);
        if (SCARA_CollisionChecker.IsBaseColliding(elbow, endEffector, baseBodyRadius, armThicknessRadius))
            return true;

        if (SCARA_CollisionChecker.IsObstacleColliding(theta1, theta2, lengthArm1, lengthArm2,
                                                         armThicknessRadius, boxCenter, boxSize))
            return true;

        return false;
    }

    /// <summary>
    /// Rough duration estimate per segment, ignoring any avoidance detours (elbow-flip
    /// or arc insertion change actual travel time/distance). Useful for UI progress
    /// display purposes; the authoritative durations come from Generate()'s output samples.
    /// </summary>
    public float[] GetSegmentDurations(List<Waypoint> waypoints, float currentTheta1Deg, float currentTheta2Deg)
    {
        if (waypoints == null || waypoints.Count == 0) return new float[0];
        List<Waypoint> allPoints = new List<Waypoint>(waypoints.Count + 1);
        allPoints.Add(new Waypoint
        {
            xz = SCARA_IKSolver.SolveFK(currentTheta1Deg, currentTheta2Deg, lengthArm1, lengthArm2),
            theta1Deg = currentTheta1Deg,
            theta2Deg = currentTheta2Deg,
            speedPercent = 100f
        });
        allPoints.AddRange(waypoints);

        int segCount = allPoints.Count - 1;
        float[] segDuration = new float[segCount];
        for (int i = 0; i < segCount; i++)
        {
            float dTheta1 = Mathf.Abs(Mathf.DeltaAngle(allPoints[i].theta1Deg, allPoints[i + 1].theta1Deg));
            float dTheta2 = Mathf.Abs(Mathf.DeltaAngle(allPoints[i].theta2Deg, allPoints[i + 1].theta2Deg));
            float angularDistance = Mathf.Max(dTheta1, dTheta2);
            float effectiveSpeed = maxDegreesPerSecond * (allPoints[i + 1].speedPercent / 100f);
            if (effectiveSpeed < 0.001f) effectiveSpeed = 0.001f;
            segDuration[i] = angularDistance / effectiveSpeed;
        }
        return segDuration;
    }
}