using UnityEngine;

[ExecuteInEditMode]
public class SCARA_CollisionAutoSetup : MonoBehaviour
{
    [Header("Assign Visuals")]
    public Transform robotBase;
    public Transform baseVisual;
    public Transform joint1Visual;
    public Transform joint2Visual;

    [Header("Target Scripts")]
    public SCARA_WaypointManager waypointManager;
    public SCARA_TrajectoryPlanner trajectoryPlanner;

    [Header("Computed Values (Read‑Only)")]
    [SerializeField] private float computedArmRadius;
    [SerializeField] private float computedBaseRadius;
    [SerializeField] private Vector2 computedBoxCenter;
    [SerializeField] private Vector2 computedBoxSize;

    [ContextMenu("Compute Collision Parameters")]
    public void ComputeFromMeshes()
    {
        if (robotBase == null) robotBase = transform;
        if (waypointManager == null) waypointManager = GetComponent<SCARA_WaypointManager>();
        if (trajectoryPlanner == null) trajectoryPlanner = GetComponent<SCARA_TrajectoryPlanner>();

        // Base radius
        if (baseVisual != null)
        {
            Bounds b = GetBoundsInLocalXZ(baseVisual);
            computedBaseRadius = (b.extents.x + b.extents.z) * 0.5f;
        }
        else computedBaseRadius = 0.25f;

        // Arm thickness (average of both links)
        float r1 = GetLinkRadius(joint1Visual);
        float r2 = GetLinkRadius(joint2Visual);
        computedArmRadius = (r1 + r2) * 0.5f;

        // Box (use base visual bounds as conservative estimate)
        if (baseVisual != null)
        {
            Bounds boxBounds = GetBoundsInLocalXZ(baseVisual);
            computedBoxCenter = new Vector2(boxBounds.center.x, boxBounds.center.z);
            computedBoxSize = new Vector2(boxBounds.size.x, boxBounds.size.z) + Vector2.one * 0.1f;
        }
        else
        {
            computedBoxCenter = Vector2.zero;
            computedBoxSize = new Vector2(0.8f, 0.8f);
        }

        // Apply
        if (waypointManager != null)
        {
            waypointManager.armThicknessRadius = computedArmRadius;
            waypointManager.baseBodyRadius = computedBaseRadius;
            waypointManager.boxCenter = computedBoxCenter;
            waypointManager.boxSize = computedBoxSize;
        }
        if (trajectoryPlanner != null)
        {
            trajectoryPlanner.armThicknessRadius = computedArmRadius;
            trajectoryPlanner.baseBodyRadius = computedBaseRadius;
            trajectoryPlanner.boxCenter = computedBoxCenter;
            trajectoryPlanner.boxSize = computedBoxSize;
        }

        Debug.Log($"Auto‑computed: Arm={computedArmRadius:F3}, Base={computedBaseRadius:F3}, Box={computedBoxSize}");
    }

    private float GetLinkRadius(Transform linkVisual)
    {
        if (linkVisual == null) return 0.1f;
        Bounds b = GetBoundsInLocalXZ(linkVisual);
        return Mathf.Min(b.extents.x, b.extents.z);
    }

    private Bounds GetBoundsInLocalXZ(Transform obj)
    {
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            Bounds worldBounds = rend.bounds;
            Vector3 localCenter = robotBase.InverseTransformPoint(worldBounds.center);
            Vector3 localExtents = robotBase.InverseTransformVector(worldBounds.extents);
            return new Bounds(localCenter, localExtents * 2f);
        }
        MeshFilter mf = obj.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.bounds;
        return new Bounds(Vector3.zero, Vector3.one * 0.1f);
    }

    void Start()
    {
        if (Application.isPlaying) ComputeFromMeshes();
    }
}