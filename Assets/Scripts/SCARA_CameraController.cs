using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SCARA_CameraController : MonoBehaviour
{
    [Header("Target (for initial pivot only)")]
    public Transform target;

    [Header("Orbit")]
    public float distance = 10f;
    public float minDistance = 2f;
    public float maxDistance = 30f;

    [Header("Speeds")]
    public float orbitSpeed = 100f;
    public float panSpeed = 1f;
    public float zoomSpeed = 2f;

    [Header("Speed Multiplier (Ctrl+Scroll)")]
    public float speedMultiplierStep = 0.1f;
    public float minSpeedMultiplier = 0.1f;
    public float maxSpeedMultiplier = 5f;

    [Header("OSD")]
    public GameObject osdPanel;          // the entire panel (set inactive when hidden)
    public Text speedDisplayText;        // the Text inside the panel (shows speed & zoom)

    [Header("OSD Behaviour")]
    public float osdDisplayDuration = 2f; // seconds before hiding

    private float currentX = 0f;
    private float currentY = 20f;
    private float currentSpeedMultiplier = 1f;
    private Vector3 pivotPosition;

    private Coroutine hideCoroutine;

    void Start()
    {
        if (target == null) target = transform.parent;
        pivotPosition = target != null ? target.position : Vector3.zero;
        UpdateCameraPosition();

        // Initially hide the OSD panel
        if (osdPanel != null) osdPanel.SetActive(false);
    }

    void Update()
    {
        // --- Zoom ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                // Speed multiplier change – show OSD
                currentSpeedMultiplier += scroll * speedMultiplierStep;
                currentSpeedMultiplier = Mathf.Clamp(currentSpeedMultiplier, minSpeedMultiplier, maxSpeedMultiplier);
                ShowOSD(); // triggers display and resets timer
            }
            else
            {
                // Normal zoom – no OSD trigger, but if OSD is already visible, update zoom value
                distance -= scroll * zoomSpeed * currentSpeedMultiplier;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
                // If OSD is visible, update its text (but don't trigger show)
                if (osdPanel != null && osdPanel.activeSelf)
                    UpdateOSDText();
            }
        }

        // --- Orbit: Alt + Left Mouse ---
        if (Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftAlt))
        {
            currentX += Input.GetAxis("Mouse X") * orbitSpeed * currentSpeedMultiplier * Time.deltaTime;
            currentY -= Input.GetAxis("Mouse Y") * orbitSpeed * currentSpeedMultiplier * Time.deltaTime;
            currentY = Mathf.Clamp(currentY, -80f, 80f);
        }

        // --- Pan: Middle Mouse (press scroll wheel) ---
        if (Input.GetMouseButton(2))
        {
            float deltaX = Input.GetAxis("Mouse X") * panSpeed * currentSpeedMultiplier * Time.deltaTime;
            float deltaY = Input.GetAxis("Mouse Y") * panSpeed * currentSpeedMultiplier * Time.deltaTime;
            pivotPosition += transform.right * deltaX + transform.up * deltaY;
        }

        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        transform.position = pivotPosition + rotation * new Vector3(0, 0, -distance);
        transform.LookAt(pivotPosition);
    }

    // Show the OSD and reset the hide timer
    void ShowOSD()
    {
        if (osdPanel == null) return;

        // Update text with current values
        UpdateOSDText();

        // Show the panel
        osdPanel.SetActive(true);

        // Reset the hide timer
        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideOSDAfterDelay());
    }

    IEnumerator HideOSDAfterDelay()
    {
        yield return new WaitForSeconds(osdDisplayDuration);
        if (osdPanel != null)
            osdPanel.SetActive(false);
        hideCoroutine = null;
    }

    void UpdateOSDText()
    {
        if (speedDisplayText != null)
            speedDisplayText.text = $"Speed: {currentSpeedMultiplier:F1}x";
    }
}