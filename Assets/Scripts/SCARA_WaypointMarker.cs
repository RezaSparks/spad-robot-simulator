using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SCARA_WaypointMarker : MonoBehaviour
{
    [Header("Wiring (assign in the prefab, not at runtime)")]
    public TextMesh infoText;
    public Renderer markerRenderer;

    [Header("Blink Settings")]
    public float blinkInterval = 0.5f;

    [Header("Display Formatting")]
    public string labelFormat = "#{0}";

    [Header("Selection Visualization")]
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(1f, 0.85f, 0.2f);   // amber
    public Color selectedColor = new Color(0.2f, 0.9f, 1f); // cyan
    // NEW: blue glow for hover in Edit Mode
    public Color editHoverColor = Color.blue;

    [Tooltip("Shader color property to drive via MaterialPropertyBlock.")]
    public string colorPropertyName = "_Color";

    [HideInInspector] public int waypointIndex = -1;

    private float blinkTimer;
    private bool blinkOn = true;

    public bool isActiveMarker = false;

    private bool isHovered = false;
    private bool isSelectedMarker = false;
    private bool editModeActive = false;  // NEW

    private MaterialPropertyBlock mpb;
    private int colorPropertyId;

    void Reset()
    {
        if (markerRenderer == null) markerRenderer = GetComponentInChildren<Renderer>();
        if (infoText == null) infoText = GetComponentInChildren<TextMesh>();
    }

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        colorPropertyId = Shader.PropertyToID(colorPropertyName);
    }

    void Start()
    {
        ApplyColor(normalColor);
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
            markerRenderer.enabled = true;
            blinkTimer = 0f;
            blinkOn = true;
        }
    }

    public void SetInfo(int displayIndex, Waypoint data)
    {
        waypointIndex = displayIndex;
        if (infoText == null) return;
        infoText.text = (displayIndex + 1).ToString();
    }

    // NEW: called by the manager when Edit Mode toggles
    public void SetEditMode(bool active)
    {
        editModeActive = active;
        RefreshColor();
    }

    public void SetHovered(bool hovered)
    {
        isHovered = hovered;
        RefreshColor();
    }

    public void SetSelected(bool selected)
    {
        isSelectedMarker = selected;
        RefreshColor();
    }

    private void RefreshColor()
    {
        if (isSelectedMarker)
            ApplyColor(selectedColor);
        else if (isHovered && editModeActive)   // blue glow only in Edit Mode
            ApplyColor(editHoverColor);
        else if (isHovered)                     // amber hover (kept for consistency, though not used outside Edit Mode)
            ApplyColor(hoverColor);
        else
            ApplyColor(normalColor);
    }

    private void ApplyColor(Color c)
    {
        if (markerRenderer == null) return;
        markerRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(colorPropertyId, c);
        markerRenderer.SetPropertyBlock(mpb);
    }
}