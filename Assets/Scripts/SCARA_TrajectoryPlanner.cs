using System.Collections.Generic;
using UnityEngine;

public struct TrajectorySample
{
    public float time;
    public float x;
    public float z;
    public float theta1;
    public float theta2;
}

public class SCARA_TrajectoryPlanner : MonoBehaviour
{
    public float timeStep = 0.001f;

    // Raised from 90 -> 180: the arm now covers the same angular distance in
    // roughly half the time at any given speedPercent. This is only the
    // script's *default* value — see Reasoning & Debugging below if you
    // already have this component placed in the scene.
    public float maxDegreesPerSecond = 180f;

    public float lengthArm1 = 2f;
    public float lengthArm2 = 2f;

    public TrajectorySample[] Generate(List<Waypoint> waypoints,
                                       float currentTheta1Deg,
                                       float currentTheta2Deg)
    {
        if (waypoints == null || waypoints.Count < 2)
        {
            Debug.LogError("SCARA_TrajectoryPlanner: need at least 2 waypoints.");
            return new TrajectorySample[0];
        }

        List<Waypoint> allPoints;
        float[] segDuration;
        BuildSegmentPlan(waypoints, currentTheta1Deg, currentTheta2Deg, out allPoints, out segDuration);

        int segCount = segDuration.Length;
        float totalDuration = 0f;
        for (int i = 0; i < segCount; i++) totalDuration += segDuration[i];

        int totalSamples = Mathf.CeilToInt(totalDuration / timeStep) + 1;
        TrajectorySample[] result = new TrajectorySample[totalSamples];

        int segIndex = 0;
        float segStart = 0f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i * timeStep;

            while (segIndex < segCount - 1 && t >= segStart + segDuration[segIndex])
            {
                segStart += segDuration[segIndex];
                segIndex++;
            }

            float segElapsed = segDuration[segIndex] > 0.0001f
                ? Mathf.Clamp01((t - segStart) / segDuration[segIndex])
                : 1f;

            Waypoint a = allPoints[segIndex];
            Waypoint b = allPoints[segIndex + 1];

            float theta1 = Mathf.LerpAngle(a.theta1Deg, b.theta1Deg, segElapsed);
            float theta2 = Mathf.LerpAngle(a.theta2Deg, b.theta2Deg, segElapsed);
            Vector2 xz = SCARA_IKSolver.SolveFK(theta1, theta2, lengthArm1, lengthArm2);

            result[i] = new TrajectorySample
            {
                time = t,
                x = xz.x,
                z = xz.y,
                theta1 = theta1,
                theta2 = theta2
            };
        }

        return result;
    }

    /// <summary>
    /// Per-waypoint estimated travel time: index i = time to reach
    /// waypoints[i] from the previous point (or the arm's current pose, for
    /// i == 0). Same math as Generate() uses internally, without building a
    /// full sample array. Length == waypoints.Count. Safe to call with 0+
    /// waypoints (unlike Generate(), which requires at least 2).
    /// </summary>
    public float[] GetSegmentDurations(List<Waypoint> waypoints, float currentTheta1Deg, float currentTheta2Deg)
    {
        if (waypoints == null || waypoints.Count == 0) return new float[0];

        List<Waypoint> allPoints;
        float[] segDuration;
        BuildSegmentPlan(waypoints, currentTheta1Deg, currentTheta2Deg, out allPoints, out segDuration);
        return segDuration;
    }

    private void BuildSegmentPlan(List<Waypoint> waypoints, float currentTheta1Deg, float currentTheta2Deg,
                                   out List<Waypoint> allPoints, out float[] segDuration)
    {
        allPoints = new List<Waypoint>(waypoints.Count + 1);
        allPoints.Add(new Waypoint
        {
            xz = SCARA_IKSolver.SolveFK(currentTheta1Deg, currentTheta2Deg, lengthArm1, lengthArm2),
            theta1Deg = currentTheta1Deg,
            theta2Deg = currentTheta2Deg,
            speedPercent = waypoints.Count > 0 ? waypoints[0].speedPercent : 100f
        });
        allPoints.AddRange(waypoints);

        int segCount = allPoints.Count - 1;
        segDuration = new float[segCount];

        for (int i = 0; i < segCount; i++)
        {
            float dTheta1 = Mathf.Abs(Mathf.DeltaAngle(allPoints[i].theta1Deg, allPoints[i + 1].theta1Deg));
            float dTheta2 = Mathf.Abs(Mathf.DeltaAngle(allPoints[i].theta2Deg, allPoints[i + 1].theta2Deg));
            float angularDistance = Mathf.Max(dTheta1, dTheta2);

            float effectiveSpeed = maxDegreesPerSecond * (allPoints[i].speedPercent / 100f);
            if (effectiveSpeed < 0.001f) effectiveSpeed = 0.001f;

            segDuration[i] = angularDistance / effectiveSpeed;
        }
    }
}
