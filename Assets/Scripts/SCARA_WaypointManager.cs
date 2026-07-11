using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct Waypoint
{
    public Vector2 xz;
    public float theta1Deg;
    public float theta2Deg;
    public float speedPercent; // 1-100
}

public class SCARA_WaypointManager : MonoBehaviour
{
    public Camera targetCamera;
    public Collider planeCollider;
    public Transform robotBase;

    public float lengthArm1 = 2f;
    public float lengthArm2 = 2f;
    public bool elbowUp = true;
    public float defaultSpeedPercent = 50f;

    [Header("Waypoint Marker Visualization")]
    public SCARA_WaypointMarker markerPrefab;   // prefab built in Step 1
    public Transform markersRoot;                // optional; defaults to this.transform if empty

    public List<Waypoint> waypoints = new List<Waypoint>();
    public int selectedIndex = -1;

    private SCARA_WaypointMarker lastActiveMarker = null;

    private List<SCARA_WaypointMarker> spawnedMarkers = new List<SCARA_WaypointMarker>();

    public System.Action OnWaypointsChanged;
    public System.Action<int> OnSelectedWaypointChanged;

    /// <summary>Fired with a human-readable message any time something the
    /// user should know about happens (success, invalid click, unreachable
    /// point, invalid speed, etc). SCARA_UIController subscribes in Step 3.</summary>
    public System.Action<string> OnStatusMessage;

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
            TryAddWaypointFromClick();
    }

    void TryAddWaypointFromClick()
    {
        if (targetCamera == null || planeCollider == null || robotBase == null)
        {
            RaiseStatus("Setup error: Camera, Plane Collider, or Robot Base is not assigned on SCARA_WaypointManager. Fix this in the Inspector before placing waypoints.");
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!planeCollider.Raycast(ray, out hit, 1000f))
        {
            RaiseStatus("Click was outside the work plane. Aim inside the white circular area to place a waypoint.");
            return;
        }

        Vector3 offset = hit.point - robotBase.position;
        Vector2 xz = new Vector2(offset.x, offset.z);

        // Same reach formula SolveIK uses internally — computed here only to
        // classify *why* a click is unreachable, without touching SCARA_IKSolver.
        float d = xz.magnitude;
        float maxReach = lengthArm1 + lengthArm2;
        float minReach = Mathf.Abs(lengthArm1 - lengthArm2);

        float theta1, theta2;
        bool wasClamped;
        SCARA_IKSolver.SolveIK(xz, lengthArm1, lengthArm2, elbowUp,
                               out theta1, out theta2, out wasClamped);

        if (wasClamped)
        {
            if (d > maxReach)
            {
                RaiseStatus(string.Format(
                    "Unreachable: point is {0:F2} units from the base, but the arm's maximum reach is {1:F2}. Click closer to the base.",
                    d, maxReach));
            }
            else
            {
                RaiseStatus(string.Format(
                    "Unreachable: point is {0:F2} units from the base, inside the arm's minimum reach of {1:F2} (a dead zone the elbow can't fold into). Click farther from the base.",
                    d, minReach));
            }
            return;
        }

        Waypoint newWaypoint = new Waypoint
        {
            xz = xz,
            theta1Deg = theta1,
            theta2Deg = theta2,
            speedPercent = defaultSpeedPercent
        };

        waypoints.Add(newWaypoint);
        SpawnMarkerFor(waypoints.Count - 1, newWaypoint, hit.point);

        RaiseStatus(string.Format("Waypoint #{0} added at X:{1:F2} Z:{2:F2} (\u03B81:{3:F1}\u00B0 \u03B82:{4:F1}\u00B0).",
                                   waypoints.Count - 1, xz.x, xz.y, theta1, theta2));

        SelectWaypoint(waypoints.Count - 1);
        if (OnWaypointsChanged != null) OnWaypointsChanged();
    }



    void SpawnMarkerFor(int index, Waypoint data, Vector3 worldPos)

    {

        if (markerPrefab == null) return;



        Transform parent = markersRoot != null ? markersRoot : transform;



        SCARA_WaypointMarker marker = Instantiate(markerPrefab);

        marker.transform.SetParent(parent, false);

        marker.transform.position = worldPos;

        marker.SetInfo(index, data);



        // Deactivate the previous active marker

        if (lastActiveMarker != null)

        {

            lastActiveMarker.isActiveMarker = false;

            if (lastActiveMarker.markerRenderer != null)

                lastActiveMarker.markerRenderer.enabled = true;

        }



        // Activate the new marker

        marker.isActiveMarker = true;

        lastActiveMarker = marker;



        spawnedMarkers.Add(marker);

    }

    public void SelectWaypoint(int index)
    {
        if (index < 0 || index >= waypoints.Count)
            selectedIndex = -1;
        else
            selectedIndex = index;

        if (OnSelectedWaypointChanged != null)
            OnSelectedWaypointChanged(selectedIndex);
    }



    public void SetSpeedForSelected(int index, float percent)

    {

        if (index < 0 || index >= waypoints.Count)

        {

            RaiseStatus("Invalid waypoint index.");

            return;

        }



        percent = Mathf.Clamp(percent, 1f, 100f);

        Waypoint wp = waypoints[index];

        wp.speedPercent = percent;

        waypoints[index] = wp;



        selectedIndex = index;



        if (OnSelectedWaypointChanged != null)

            OnSelectedWaypointChanged(index);

    }



    public void UndoLast()

    {

        if (waypoints.Count == 0)

        {

            RaiseStatus("Nothing to undo \u2014 there are no waypoints yet.");

            return;

        }



        waypoints.RemoveAt(waypoints.Count - 1);



        if (spawnedMarkers.Count > 0)

        {

            int lastIndex = spawnedMarkers.Count - 1;

            if (spawnedMarkers[lastIndex] != null)

                Destroy(spawnedMarkers[lastIndex].gameObject);

            spawnedMarkers.RemoveAt(lastIndex);

        }



        // If markers remain, set the new last one as active

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



        if (selectedIndex >= waypoints.Count) SelectWaypoint(-1);



        RaiseStatus(waypoints.Count > 0

            ? string.Format("Removed last waypoint. {0} waypoint(s) remaining.", waypoints.Count)

            : "Removed last waypoint. No waypoints remain \u2014 add at least 2 before generating a trajectory.");



        if (OnWaypointsChanged != null) OnWaypointsChanged();

    }



    public void ClearAll()

    {

        for (int i = 0; i < spawnedMarkers.Count; i++)

            if (spawnedMarkers[i] != null) Destroy(spawnedMarkers[i].gameObject);

        spawnedMarkers.Clear();

        lastActiveMarker = null;  // reset



        waypoints.Clear();

        SelectWaypoint(-1);



        RaiseStatus("All waypoints cleared.");

        if (OnWaypointsChanged != null) OnWaypointsChanged();

    }

    private void RaiseStatus(string message)
    {
        Debug.Log("SCARA_WaypointManager: " + message);
        if (OnStatusMessage != null) OnStatusMessage(message);
    }
}
