using UnityEngine;

/// <summary>
/// Visual marker for one placed waypoint. Purely presentational — holds no
/// IK/trajectory logic. SCARA_WaypointManager instantiates one of these per
/// waypoint (Step 2), positions it at the exact plane-click point, and calls
/// SetInfo() whenever that waypoint's data is created or changed.
/// </summary>
public class SCARA_WaypointMarker : MonoBehaviour
{
    [Header("Wiring (assign in the prefab, not at runtime)")]
    public TextMesh infoText;       // child "3D Text" object
    public Renderer markerRenderer; // child sphere's Mesh Renderer (the part that blinks)

    [Header("Blink Settings")]
    [Tooltip("Seconds spent in each on/off phase.")]
    public float blinkInterval = 0.5f;

    [Header("Display Formatting")]

    public string labelFormat = "#{0}";

    private float blinkTimer;
    private bool blinkOn = true;



    public bool isActiveMarker = false;



    // Auto-fills references if you forget to wire them by hand in the Inspector.

    void Reset()
    {
        if (markerRenderer == null) markerRenderer = GetComponentInChildren<Renderer>();
        if (infoText == null) infoText = GetComponentInChildren<TextMesh>();
    }

    void Update()

    {

        if (markerRenderer == null) return;



        if (isActiveMarker)

        {

            blinkTimer += Time.deltaTime;

            if (blinkTimer >= blinkInterval)

            {

                blinkTimer = 0f;

                blinkOn = !blinkOn;

                markerRenderer.enabled = blinkOn;

            }

        }

        else

        {

            // Always visible, not blinking

            markerRenderer.enabled = true;

            blinkTimer = 0f;

            blinkOn = true;

        }

    }

    /// <summary>
    /// Call right after Instantiate(), and again any time this waypoint's
    /// own data changes (kept as a public entry point for that, even though
    /// nothing in the current codebase edits X/Z/theta after creation yet).
    /// </summary>
    public void SetInfo(int displayIndex, Waypoint data)
    {
        if (infoText == null) return;
        infoText.text = (displayIndex + 1).ToString();
    }
}
