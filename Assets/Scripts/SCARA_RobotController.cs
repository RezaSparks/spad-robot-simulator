using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class SCARA_RobotController : MonoBehaviour
{
    [Header("Links (Assign in this order: Base→Joint1→Joint2)")]
    public Transform joint1;
    public Transform joint2;
    public Transform endEffector;

    [Header("Geometry")]
    public float armL1 = 2f;
    public float armL2 = 2f;

    [Header("Collision Visuals")]
    public bool showCollisionState = true;
    public Color colorNormal = new Color(0.35f, 0.65f, 1f, 1f);
    public Color colorCollision = new Color(0.97f, 0.32f, 0.29f, 1f);

    [Header("Gizmos")]
    public bool drawJointLimits = true;
    public bool drawReachableArea = true;
    public float gizmoLineWidth = 8f;
    public float planeY = 0f;

    [Header("Debug Gizmos")]
    public bool showBaseRadius = true;
    public bool showArmRadius = true;
    public bool showBox = true;
    public Color baseRadiusColor = new Color(1f, 0f, 0f, 0.25f);
    public Color armRadiusColor = new Color(0f, 1f, 0f, 0.25f);
    public Color boxColor = new Color(0f, 0.5f, 1f, 0.25f);

    [HideInInspector] public TrajectorySample[] cachedTrajectory;

    [SerializeField] private float currentTheta1;
    [SerializeField] private float currentTheta2;
    private bool isColliding;

    private SCARA_WaypointManager wpManager;
    private SCARA_TrajectoryPlanner planner;

    private void OnEnable()
    {
        wpManager = FindObjectOfType<SCARA_WaypointManager>();
        planner = FindObjectOfType<SCARA_TrajectoryPlanner>();
    }

    public void SetJointAngles(float theta1Deg, float theta2Deg, bool checkCollision = true)
    {
        SCARA_IKSolver.ClampAngles(ref theta1Deg, ref theta2Deg);

        if (checkCollision && planner != null)
        {
            var check = SCARA_CollisionChecker.CheckAll(
                theta1Deg, theta2Deg,
                armL1, armL2,
                planner.armThicknessRadius,
                planner.baseBodyRadius,
                planner.boxCenter, planner.boxSize,
                planner.minSafeTheta2Deg);
            isColliding = check.hit;
        }
        else
        {
            isColliding = false;
        }

        currentTheta1 = theta1Deg;
        currentTheta2 = theta2Deg;

        if (joint1 != null)
            joint1.localRotation = Quaternion.Euler(0f, currentTheta1, 0f);
        if (joint2 != null)
            joint2.localRotation = Quaternion.Euler(0f, currentTheta2, 0f);
    }

    public void GetJointAngles(out float t1, out float t2)
    {
        t1 = currentTheta1;
        t2 = currentTheta2;
    }

    private void OnDrawGizmos()
    {
        if (joint1 == null || joint2 == null) return;

        Vector3 basePos = transform.position;
        Vector3 p1World = joint1.position;
        Vector3 p2World = joint2.position;

        Color segColor = (showCollisionState && isColliding) ? colorCollision : colorNormal;
        Color seg2Color = (showCollisionState && isColliding) ? colorCollision : new Color(0.64f, 0.44f, 0.97f, 1f);

        Gizmos.color = segColor;
        DrawThickLine(basePos, p1World, gizmoLineWidth * 0.0015f);
        Gizmos.color = seg2Color;
        DrawThickLine(p1World, p2World, gizmoLineWidth * 0.0013f);

        Gizmos.color = Color.white;
        Gizmos.DrawSphere(basePos, 0.08f);
        Gizmos.DrawSphere(p1World, 0.06f);
        Gizmos.DrawSphere(p2World, 0.05f);

#if UNITY_EDITOR
        Handles.color = Color.white;
        Handles.Label(basePos + Vector3.up * 0.15f, "Base");
        Handles.Label(p1World + Vector3.up * 0.15f, "Joint1");
        Handles.Label(p2World + Vector3.up * 0.15f, "Joint2");
        if (endEffector != null)
            Handles.Label(endEffector.position + Vector3.up * 0.15f, "End-Effector");
#endif

        if (drawJointLimits)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            DrawLimitArc(basePos, 0.6f, SCARA_IKSolver.THETA1_MIN, SCARA_IKSolver.THETA1_MAX);
        }

        if (drawReachableArea)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.04f);
            int seg = 64;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= seg; i++)
            {
                float ang = (float)i / seg * Mathf.PI * 2f;
                Vector3 p = basePos + new Vector3(Mathf.Cos(ang) * (armL1 + armL2), 0f, Mathf.Sin(ang) * (armL1 + armL2));
                if (i > 0) Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }

        // --- نمایش Gizmos برای تنظیمات برخورد ---
        DrawCollisionGizmos();

        DrawTrajectoryPath(planeY);
    }

    private void DrawCollisionGizmos()
    {
        if (planner == null) return;

        Vector3 basePos = transform.position + Vector3.up * 0.05f;

        // شعاع پایه
        if (showBaseRadius && planner.baseBodyRadius > 0f)
        {
            Gizmos.color = baseRadiusColor;
            DrawCircle(basePos, planner.baseBodyRadius);
        }

        // شعاع بازوها (در دو موقعیت برای نمایش بهتر)
        if (showArmRadius && planner.armThicknessRadius > 0f)
        {
            Gizmos.color = armRadiusColor;
            Vector2 elbow = SCARA_IKSolver.SolveFK(currentTheta1, 0f, armL1, 0f);
            Vector3 elbowPos = basePos + new Vector3(elbow.x, 0f, elbow.y);
            DrawCircle(elbowPos, planner.armThicknessRadius);

            Vector2 endEff = SCARA_IKSolver.SolveFK(currentTheta1, currentTheta2, armL1, armL2);
            Vector3 endPos = basePos + new Vector3(endEff.x, 0f, endEff.y);
            DrawCircle(endPos, planner.armThicknessRadius);
        }

        // جعبه کنترل
        if (showBox && planner.boxSize.magnitude > 0.01f)
        {
            Gizmos.color = boxColor;
            Vector3 boxCenter3 = basePos + new Vector3(planner.boxCenter.x, 0f, planner.boxCenter.y);
            Vector3 boxSize3 = new Vector3(planner.boxSize.x, 0.02f, planner.boxSize.y);
            Gizmos.DrawWireCube(boxCenter3, boxSize3);
            Gizmos.DrawCube(boxCenter3, boxSize3 * 0.3f);
        }
    }

    private void DrawCircle(Vector3 center, float radius)
    {
        const int segments = 32;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * 2f * Mathf.PI;
            Vector3 point = center + new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }

    private void DrawThickLine(Vector3 a, Vector3 b, float thickness)
    {
        Vector3 mid = (a + b) * 0.5f;
        float len = Vector3.Distance(a, b);
        if (len < 1e-4f) return;
        Quaternion rot = Quaternion.LookRotation(b - a);
        Gizmos.matrix = Matrix4x4.TRS(mid, rot, new Vector3(thickness, thickness, len));
        Gizmos.DrawCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawLimitArc(Vector3 center, float radius, float degMin, float degMax)
    {
        int steps = 32;
        Vector3 prev = center;
        bool first = true;
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float ang = Mathf.Lerp(degMin, degMax, t) * Mathf.Deg2Rad;
            Vector3 p = center + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
            if (!first) Gizmos.DrawLine(prev, p);
            prev = p;
            first = false;
        }
        Gizmos.DrawLine(center, center + new Vector3(Mathf.Cos(degMin * Mathf.Deg2Rad) * radius * 1.3f, 0f, Mathf.Sin(degMin * Mathf.Deg2Rad) * radius * 1.3f));
        Gizmos.DrawLine(center, center + new Vector3(Mathf.Cos(degMax * Mathf.Deg2Rad) * radius * 1.3f, 0f, Mathf.Sin(degMax * Mathf.Deg2Rad) * radius * 1.3f));
    }

    private void DrawTrajectoryPath(float yHeight)
    {
        if (cachedTrajectory != null && cachedTrajectory.Length > 1)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.9f);
            Vector3 prevPos = new Vector3(cachedTrajectory[0].x, yHeight, cachedTrajectory[0].z);
            for (int i = 1; i < cachedTrajectory.Length; i++)
            {
                Vector3 curPos = new Vector3(cachedTrajectory[i].x, yHeight, cachedTrajectory[i].z);
                Gizmos.DrawLine(prevPos, curPos);
                prevPos = curPos;
            }

            Vector3 last = new Vector3(cachedTrajectory[cachedTrajectory.Length - 1].x, yHeight, cachedTrajectory[cachedTrajectory.Length - 1].z);
            Vector3 secondLast = new Vector3(cachedTrajectory[cachedTrajectory.Length - 2].x, yHeight, cachedTrajectory[cachedTrajectory.Length - 2].z);
            Vector3 dir = (last - secondLast).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            Gizmos.DrawLine(last, last - dir * 0.2f + right * 0.08f);
            Gizmos.DrawLine(last, last - dir * 0.2f - right * 0.08f);
        }
        else
        {
            if (wpManager == null) return;
            var wps = wpManager.waypoints;
            if (wps.Count < 2) return;

            Gizmos.color = new Color(0.82f, 0.6f, 0.13f, 0.7f);
            for (int i = 0; i < wps.Count - 1; i++)
            {
                Vector3 a = new Vector3(wps[i].xz.x, yHeight, wps[i].xz.y);
                Vector3 b = new Vector3(wps[i + 1].xz.x, yHeight, wps[i + 1].xz.y);
                Gizmos.DrawLine(a, b);

                Vector3 dir = (b - a).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                Vector3 tip = b;
                Gizmos.DrawLine(tip, tip - dir * 0.2f + right * 0.08f);
                Gizmos.DrawLine(tip, tip - dir * 0.2f - right * 0.08f);
            }
        }

#if UNITY_EDITOR
        if (wpManager != null)
        {
            var wps = wpManager.waypoints;
            for (int i = 0; i < wps.Count; i++)
            {
                Vector3 pos = new Vector3(wps[i].xz.x, yHeight + 0.25f, wps[i].xz.y);
                Handles.color = Color.white;
                Handles.Label(pos, (i + 1).ToString(), new GUIStyle()
                {
                    normal = new GUIStyleState() { textColor = Color.white },
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
            }
        }
#endif
    }
}