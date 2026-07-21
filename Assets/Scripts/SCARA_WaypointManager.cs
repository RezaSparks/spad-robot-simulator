using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct Waypoint
{
    public Vector2 xz;
    public float theta1Deg;
    public float theta2Deg;
    public float speedPercent;
}

public class SCARA_WaypointManager : MonoBehaviour
{
    [Header("Core References")]
    public Camera targetCamera;
    public Collider planeCollider;
    public Transform robotBase;

    [Header("Arm & IK")]
    public float lengthArm1 = 2f;
    public float lengthArm2 = 2f;
    public bool elbowUp = true;
    public float defaultSpeedPercent = 50f;

    [Header("Grid Snapping")]
    public float gridSnapSize = 0.05f;

    [Header("Waypoint Markers")]
    public SCARA_WaypointMarker markerPrefab;
    public Transform markersRoot;

    [Header("Selection & Drag")]
    public LayerMask markerRaycastMask = ~0;
    public float raycastMaxDistance = 1000f;

    [Header("Collision Settings")]
    public bool enableCollisionChecks = false;
    public float armThicknessRadius = 0.15f;
    public float baseBodyRadius = 0.25f;
    public float minSafeTheta2Deg = 30f;
    public Vector2 boxCenter = Vector2.zero;
    public Vector2 boxSize = new Vector2(0.8f, 0.8f);

    public List<Waypoint> waypoints = new List<Waypoint>();
    public List<int> selectedIndices = new List<int>();

    private SCARA_WaypointMarker lastActiveMarker = null;
    private List<SCARA_WaypointMarker> spawnedMarkers = new List<SCARA_WaypointMarker>();
    private SCARA_WaypointMarker hoveredMarker = null;
    private SCARA_WaypointMarker draggingMarker = null;
    private int draggingIndex = -1;
    private bool isDragging = false;
    private int lastClickedIndex = -1;
    public bool IsEditMode { get; private set; } = false;

    private Texture2D grabCursorTexture;
    private Vector2 cursorHotspot = new Vector2(16f, 16f);
    private float planeY = 0f;

    public System.Action OnWaypointsChanged;
    public System.Action<int> OnWaypointMoved;
    public System.Action<List<int>> OnSelectedWaypointsChanged;
    public System.Action<int> OnWaypointDragStarted;
    public System.Action<int> OnWaypointDragEnded;
    public System.Action<string> OnStatusMessage;

    void Awake()
    {
        grabCursorTexture = GenerateGrabCursor();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E)) ToggleEditMode();
        if (IsEditMode && Input.GetKeyDown(KeyCode.Escape)) SetEditMode(false);
        if (Input.GetMouseButtonDown(1)) TryAddWaypointFromClick();
        HandleWaypointSelectionAndDrag();
    }

    public void ToggleEditMode() => SetEditMode(!IsEditMode);

    public void SetEditMode(bool enabled)
    {
        if (IsEditMode == enabled) return;
        IsEditMode = enabled;
        if (!IsEditMode)
        {
            if (isDragging) EndDrag();
            ClearHover();
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
        UpdateMarkersEditMode(IsEditMode);
        RaiseStatus(IsEditMode ? "Edit Mode ON – drag waypoints to move them." : "Edit Mode OFF.");
    }

    private void UpdateMarkersEditMode(bool editMode)
    {
        foreach (var marker in spawnedMarkers)
            if (marker != null) marker.SetEditMode(editMode);
    }

    private void ClearHover()
    {
        if (hoveredMarker != null)
        {
            hoveredMarker.SetHovered(false);
            hoveredMarker = null;
        }
    }

    private bool ComputeSafeIK(Vector2 xz, out float theta1, out float theta2,
                                out Vector2 safeXZ, out bool wasClamped, out string failReason)
    {
        theta1 = 0f; theta2 = 0f; safeXZ = xz; wasClamped = false; failReason = null;

        if (!enableCollisionChecks)
        {
            bool ikSuccess = SCARA_IKSolver.SolveIK(xz, lengthArm1, lengthArm2, elbowUp,
                                                    out theta1, out theta2, out wasClamped);
            safeXZ = SCARA_IKSolver.ClampAnglesAndUpdateFK(ref theta1, ref theta2, lengthArm1, lengthArm2);
            return ikSuccess;
        }

        bool success = SCARA_CollisionChecker.SolveIKWithLimitsAndCollision(
            xz,
            lengthArm1, lengthArm2,
            elbowUp,
            minSafeTheta2Deg,
            armThicknessRadius,
            baseBodyRadius,
            boxCenter, boxSize,
            out theta1, out theta2,
            out wasClamped,
            out bool hasCollision
        );

        if (!success)
        {
            failReason = "Target is completely unreachable (IK failed).";
            return false;
        }

        safeXZ = SCARA_IKSolver.SolveFK(theta1, theta2, lengthArm1, lengthArm2);

        if (hasCollision)
        {
            failReason = "Waypoint placed with collision! (check the red path)";
            Debug.LogWarning(failReason);
        }

        return true;
    }

    void TryAddWaypointFromClick()
    {
        if (targetCamera == null || planeCollider == null || robotBase == null)
        {
            RaiseStatus("Setup error: Camera, Plane Collider, or Robot Base is not assigned.");
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!planeCollider.Raycast(ray, out hit, raycastMaxDistance))
        {
            RaiseStatus("Click was outside the work plane.");
            return;
        }

        planeY = hit.point.y;

        Vector3 offset = hit.point - robotBase.position;
        Vector2 xz = new Vector2(offset.x, offset.z);
        if (gridSnapSize > 0.001f)
        {
            xz.x = Mathf.Round(xz.x / gridSnapSize) * gridSnapSize;
            xz.y = Mathf.Round(xz.y / gridSnapSize) * gridSnapSize;
        }

        float theta1, theta2;
        Vector2 safeXZ;
        bool wasClamped;
        string failReason;

        if (!ComputeSafeIK(xz, out theta1, out theta2, out safeXZ, out wasClamped, out failReason))
        {
            RaiseStatus($"Cannot place waypoint: {failReason}");
            return;
        }

        Waypoint newWaypoint = new Waypoint
        {
            xz = safeXZ,
            theta1Deg = theta1,
            theta2Deg = theta2,
            speedPercent = defaultSpeedPercent
        };

        waypoints.Add(newWaypoint);
        Vector3 safeWorldPos = new Vector3(robotBase.position.x + safeXZ.x, planeY, robotBase.position.z + safeXZ.y);
        SpawnMarkerFor(waypoints.Count - 1, newWaypoint, safeWorldPos);
        RaiseStatus($"Waypoint #{waypoints.Count - 1} added at X:{safeXZ.x:F2} Z:{safeXZ.y:F2}.");
        SelectWaypoint(waypoints.Count - 1);
        OnWaypointsChanged?.Invoke();
    }

    private void HandleWaypointSelectionAndDrag()
    {
        if (targetCamera == null) return;

        if (isDragging)
        {
            if (Input.GetMouseButtonUp(0)) EndDrag();
            else DoDrag();
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        SCARA_WaypointMarker hitMarker = null;
        if (Physics.Raycast(ray, out hit, raycastMaxDistance, markerRaycastMask))
            hitMarker = hit.collider.GetComponentInParent<SCARA_WaypointMarker>();

        if (IsEditMode)
        {
            if (hitMarker != hoveredMarker)
            {
                if (hoveredMarker != null) hoveredMarker.SetHovered(false);
                hoveredMarker = hitMarker;
                if (hoveredMarker != null) hoveredMarker.SetHovered(true);
            }
        }
        else
        {
            if (hoveredMarker != null)
            {
                hoveredMarker.SetHovered(false);
                hoveredMarker = null;
            }
        }

        if (hitMarker != null && Input.GetMouseButtonDown(0))
        {
            if (IsEditMode) StartDrag(hitMarker);
            else HandleSelectionClick(hitMarker.waypointIndex);
        }
        else if (Input.GetMouseButtonDown(0) && hitMarker == null && !IsEditMode)
        {
            DeselectAll();
        }
    }

    private void HandleSelectionClick(int index)
    {
        if (index < 0 || index >= waypoints.Count) return;
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (ctrl) ToggleWaypointSelection(index);
        else if (shift && lastClickedIndex >= 0 && lastClickedIndex < waypoints.Count)
            SelectRange(lastClickedIndex, index);
        else SelectWaypoint(index);
        lastClickedIndex = index;
    }

    public void DeselectAll()
    {
        selectedIndices.Clear();
        UpdateMarkerSelection();
        OnSelectedWaypointsChanged?.Invoke(selectedIndices);
    }

    private void StartDrag(SCARA_WaypointMarker marker)
    {
        if (!IsEditMode) return;
        if (marker.waypointIndex < 0 || marker.waypointIndex >= waypoints.Count)
        {
            RaiseStatus("Selected marker's waypoint index is out of sync. Ignoring drag.");
            return;
        }
        if (hoveredMarker != null && hoveredMarker != marker)
        {
            hoveredMarker.SetHovered(false);
            hoveredMarker = null;
        }
        draggingMarker = marker;
        draggingIndex = marker.waypointIndex;
        isDragging = true;
        SelectWaypoint(draggingIndex);
        if (grabCursorTexture != null)
            Cursor.SetCursor(grabCursorTexture, cursorHotspot, CursorMode.Auto);
        OnWaypointDragStarted?.Invoke(draggingIndex);
    }

    private void DoDrag()
    {
        if (draggingIndex < 0 || draggingIndex >= waypoints.Count || planeCollider == null)
        {
            EndDrag();
            return;
        }
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!planeCollider.Raycast(ray, out hit, raycastMaxDistance)) return;

        planeY = hit.point.y;

        Vector3 offset = hit.point - robotBase.position;
        Vector2 xz = new Vector2(offset.x, offset.z);
        if (gridSnapSize > 0.001f)
        {
            xz.x = Mathf.Round(xz.x / gridSnapSize) * gridSnapSize;
            xz.y = Mathf.Round(xz.y / gridSnapSize) * gridSnapSize;
        }

        bool success = UpdateWaypointXZ(draggingIndex, xz, true);
        if (success) OnWaypointMoved?.Invoke(draggingIndex);
    }

    private void EndDrag()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        if (draggingMarker != null && draggingIndex >= 0 && draggingIndex < waypoints.Count)
        {
            Waypoint wp = waypoints[draggingIndex];
            RaiseStatus($"Waypoint #{draggingIndex + 1} moved to X:{wp.xz.x:F2} Z:{wp.xz.y:F2}.");
            OnWaypointDragEnded?.Invoke(draggingIndex);
        }
        isDragging = false;
        draggingMarker = null;
        draggingIndex = -1;
    }

    private Texture2D GenerateGrabCursor()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        Color32 transparent = new Color32(0, 0, 0, 0);
        Color32 white = new Color32(255, 255, 255, 255);
        Color32 black = new Color32(30, 30, 30, 255);
        Color32 gray = new Color32(200, 200, 200, 255);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - size / 2f;
                float dy = y - size / 2f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > 9f && dist < 12f)
                    tex.SetPixel(x, y, black);
                else if (dist <= 9f)
                {
                    bool isCross = (Mathf.Abs(dx) < 2.5f && Mathf.Abs(dy) > 3.5f) ||
                                   (Mathf.Abs(dy) < 2.5f && Mathf.Abs(dx) > 3.5f);
                    if (isCross && dist > 4f)
                        tex.SetPixel(x, y, gray);
                    else
                        tex.SetPixel(x, y, white);
                }
                else
                    tex.SetPixel(x, y, transparent);
            }
        }
        tex.Apply();
        return tex;
    }

    void SpawnMarkerFor(int index, Waypoint data, Vector3 worldPos)
    {
        if (markerPrefab == null) return;
        Transform parent = markersRoot != null ? markersRoot : transform;
        SCARA_WaypointMarker marker = Instantiate(markerPrefab);
        marker.transform.SetParent(parent, false);
        marker.transform.position = worldPos;
        marker.SetInfo(index, data);
        marker.SetEditMode(IsEditMode);
        if (lastActiveMarker != null)
        {
            lastActiveMarker.isActiveMarker = false;
            if (lastActiveMarker.markerRenderer != null)
                lastActiveMarker.markerRenderer.enabled = true;
        }
        marker.isActiveMarker = true;
        lastActiveMarker = marker;
        spawnedMarkers.Add(marker);
    }

    private void UpdateMarkerAt(int index, Vector2 xz, Waypoint data)
    {
        if (index < 0 || index >= spawnedMarkers.Count) return;
        SCARA_WaypointMarker marker = spawnedMarkers[index];
        if (marker == null) return;
        Vector3 newPos = new Vector3(robotBase.position.x + xz.x, planeY, robotBase.position.z + xz.y);
        marker.transform.position = newPos;
        marker.SetInfo(index, data);
    }

    public void SelectWaypoint(int index)
    {
        selectedIndices.Clear();
        if (index >= 0 && index < waypoints.Count) selectedIndices.Add(index);
        UpdateMarkerSelection();
        OnSelectedWaypointsChanged?.Invoke(selectedIndices);
    }

    public void ToggleWaypointSelection(int index)
    {
        if (index < 0 || index >= waypoints.Count) return;
        if (selectedIndices.Contains(index)) selectedIndices.Remove(index);
        else selectedIndices.Add(index);
        UpdateMarkerSelection();
        OnSelectedWaypointsChanged?.Invoke(selectedIndices);
    }

    public void SelectRange(int from, int to)
    {
        selectedIndices.Clear();
        int min = Mathf.Min(from, to), max = Mathf.Max(from, to);
        for (int i = min; i <= max; i++)
            if (i >= 0 && i < waypoints.Count) selectedIndices.Add(i);
        UpdateMarkerSelection();
        OnSelectedWaypointsChanged?.Invoke(selectedIndices);
    }

    private void UpdateMarkerSelection()
    {
        for (int i = 0; i < spawnedMarkers.Count; i++)
            if (spawnedMarkers[i] != null)
                spawnedMarkers[i].SetSelected(selectedIndices.Contains(i));
    }

    public void MoveSelectedWaypoints(int direction)
    {
        if (selectedIndices.Count == 0) return;
        List<int> sorted = new List<int>(selectedIndices);
        sorted.Sort();
        if (direction < 0 && sorted[0] == 0) return;
        if (direction > 0 && sorted[sorted.Count - 1] == waypoints.Count - 1) return;

        List<Waypoint> wps = new List<Waypoint>();
        List<SCARA_WaypointMarker> markers = new List<SCARA_WaypointMarker>();
        foreach (int idx in sorted)
        {
            wps.Add(waypoints[idx]);
            markers.Add(spawnedMarkers[idx]);
        }

        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            waypoints.RemoveAt(sorted[i]);
            spawnedMarkers.RemoveAt(sorted[i]);
        }

        int insertIndex = direction < 0 ? sorted[0] - 1 : sorted[sorted.Count - 1] + 1 - sorted.Count;
        for (int i = 0; i < wps.Count; i++)
        {
            waypoints.Insert(insertIndex + i, wps[i]);
            spawnedMarkers.Insert(insertIndex + i, markers[i]);
        }

        for (int i = 0; i < spawnedMarkers.Count; i++)
            if (spawnedMarkers[i] != null)
                spawnedMarkers[i].waypointIndex = i;

        selectedIndices.Clear();
        for (int i = insertIndex; i < insertIndex + wps.Count; i++)
            selectedIndices.Add(i);

        UpdateMarkerSelection();
        OnWaypointsChanged?.Invoke();
        OnSelectedWaypointsChanged?.Invoke(selectedIndices);
    }

    public void SetSpeedForSelected(int index, float percent)
    {
        if (index < 0 || index >= waypoints.Count) return;
        percent = Mathf.Clamp(percent, 1f, 100f);
        Waypoint wp = waypoints[index];
        wp.speedPercent = percent;
        waypoints[index] = wp;
        OnWaypointsChanged?.Invoke();
    }

    public void UndoLast()
    {
        if (waypoints.Count == 0)
        {
            RaiseStatus("Nothing to undo — there are no waypoints yet.");
            return;
        }

        waypoints.RemoveAt(waypoints.Count - 1);

        if (spawnedMarkers.Count > 0)
        {
            int lastIndex = spawnedMarkers.Count - 1;
            if (draggingIndex == lastIndex)
            {
                isDragging = false;
                draggingMarker = null;
                draggingIndex = -1;
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
            if (hoveredMarker == spawnedMarkers[lastIndex])
                hoveredMarker = null;
            if (spawnedMarkers[lastIndex] != null)
                Destroy(spawnedMarkers[lastIndex].gameObject);
            spawnedMarkers.RemoveAt(lastIndex);
        }

        if (spawnedMarkers.Count > 0)
        {
            SCARA_WaypointMarker newLast = spawnedMarkers[spawnedMarkers.Count - 1];
            newLast.isActiveMarker = true;
            lastActiveMarker = newLast;
            if (newLast.markerRenderer != null)
                newLast.markerRenderer.enabled = true;
        }
        else
        {
            lastActiveMarker = null;
        }

        selectedIndices.Clear();
        UpdateMarkerSelection();
        RaiseStatus(waypoints.Count > 0 ? $"Removed last waypoint. {waypoints.Count} remaining." : "All waypoints cleared.");
        OnWaypointsChanged?.Invoke();
        OnSelectedWaypointsChanged?.Invoke(selectedIndices);
    }

    public void ClearAll()
    {
        for (int i = 0; i < spawnedMarkers.Count; i++)
            if (spawnedMarkers[i] != null) Destroy(spawnedMarkers[i].gameObject);
        spawnedMarkers.Clear();
        lastActiveMarker = null;
        isDragging = false;
        draggingMarker = null;
        draggingIndex = -1;
        hoveredMarker = null;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        waypoints.Clear();
        selectedIndices.Clear();
        UpdateMarkerSelection();
        RaiseStatus("All waypoints cleared.");
        OnWaypointsChanged?.Invoke();
        OnSelectedWaypointsChanged?.Invoke(selectedIndices);
    }

    public bool UpdateWaypointXZ(int index, Vector2 newXZ, bool silent = false)
    {
        if (index < 0 || index >= waypoints.Count)
        {
            if (!silent) RaiseStatus("Invalid waypoint index.");
            return false;
        }

        float theta1, theta2;
        Vector2 safeXZ;
        bool wasClamped;
        string failReason;

        if (!ComputeSafeIK(newXZ, out theta1, out theta2, out safeXZ, out wasClamped, out failReason))
        {
            if (!silent) RaiseStatus($"Cannot move waypoint: {failReason}");
            return false;
        }

        Waypoint wp = waypoints[index];
        wp.xz = safeXZ;
        wp.theta1Deg = theta1;
        wp.theta2Deg = theta2;
        waypoints[index] = wp;

        UpdateMarkerAt(index, safeXZ, wp);
        if (!silent) RaiseStatus($"Waypoint #{index + 1} updated: X:{safeXZ.x:F2} Z:{safeXZ.y:F2}.");
        OnWaypointsChanged?.Invoke();
        return true;
    }

    public void AddWaypoint(Waypoint wp, bool silent = false)
    {
        float theta1 = wp.theta1Deg;
        float theta2 = wp.theta2Deg;
        Vector2 safeXZ = SCARA_IKSolver.ClampAnglesAndUpdateFK(ref theta1, ref theta2, lengthArm1, lengthArm2);

        if (Mathf.Abs(theta1 - wp.theta1Deg) > 0.01f || Mathf.Abs(theta2 - wp.theta2Deg) > 0.01f)
            RaiseStatus($"CSV waypoint adjusted: angles clamped to limits. New XZ: ({safeXZ.x:F2}, {safeXZ.y:F2})");

        Waypoint clampedWp = wp;
        clampedWp.theta1Deg = theta1;
        clampedWp.theta2Deg = theta2;
        clampedWp.xz = safeXZ;

        waypoints.Add(clampedWp);
        float yPos = Mathf.Approximately(planeY, 0f) ? robotBase.position.y : planeY;
        Vector3 worldPos = new Vector3(robotBase.position.x + safeXZ.x, yPos, robotBase.position.z + safeXZ.y);
        SpawnMarkerFor(waypoints.Count - 1, clampedWp, worldPos);
        if (!silent) RaiseStatus($"Waypoint #{waypoints.Count - 1} loaded from CSV.");
        OnWaypointsChanged?.Invoke();
    }

    private void RaiseStatus(string message)
    {
        Debug.Log("SCARA_WaypointManager: " + message);
        OnStatusMessage?.Invoke(message);
    }

    private void OnDrawGizmosSelected()
    {
        if (!enableCollisionChecks) return;
        if (robotBase == null) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Vector3 center = robotBase.position + Vector3.up * 0.1f;
        DrawCircle(center, baseBodyRadius);

        if (boxSize.magnitude > 0.01f)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            Vector3 boxCenter3 = robotBase.position + new Vector3(boxCenter.x, 0.1f, boxCenter.y);
            Vector3 boxSize3 = new Vector3(boxSize.x, 0.01f, boxSize.y);
            Gizmos.DrawWireCube(boxCenter3, boxSize3);
        }

        Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
        foreach (var wp in waypoints)
        {
            Vector2 elbow = SCARA_IKSolver.SolveFK(wp.theta1Deg, 0f, lengthArm1, 0f);
            Vector2 end = SCARA_IKSolver.SolveFK(wp.theta1Deg, wp.theta2Deg, lengthArm1, lengthArm2);
            Vector3 elbowWorld = robotBase.position + new Vector3(elbow.x, 0.1f, elbow.y);
            Vector3 endWorld = robotBase.position + new Vector3(end.x, 0.1f, end.y);
            Gizmos.DrawLine(robotBase.position + Vector3.up * 0.1f, elbowWorld);
            Gizmos.DrawLine(elbowWorld, endWorld);
        }

        if (selectedIndices.Count > 0)
        {
            int idx = selectedIndices[0];
            if (idx >= 0 && idx < waypoints.Count)
            {
                Waypoint wp = waypoints[idx];
                Vector2 elbow = SCARA_IKSolver.SolveFK(wp.theta1Deg, 0f, lengthArm1, 0f);
                Vector2 end = SCARA_IKSolver.SolveFK(wp.theta1Deg, wp.theta2Deg, lengthArm1, lengthArm2);
                Vector3 elbowWorld = robotBase.position + new Vector3(elbow.x, 0.1f, elbow.y);
                Vector3 endWorld = robotBase.position + new Vector3(end.x, 0.1f, end.y);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(robotBase.position + Vector3.up * 0.1f, elbowWorld);
                Gizmos.DrawLine(elbowWorld, endWorld);
            }
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
}