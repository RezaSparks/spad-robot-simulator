using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SCARA_WaypointRowUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Editable Position Fields")]
    public InputField xInput;
    public InputField zInput;

    [Header("Read-only Info Labels")]
    public Text indexText;
    public Text angleText;
    public Text speedTimeText;

    [Header("Row Selection")]
    public Button selectButton;
    public Image rowBackground;
    public Image selectedIndicator;

    [Header("Reorder Buttons (optional)")]
    public Button moveUpButton;
    public Button moveDownButton;

    // Drag events
    public System.Action<int, PointerEventData> OnBeginDragRow;
    public System.Action<int, PointerEventData> OnDragRow;
    public System.Action<int, PointerEventData> OnEndDragRow;

    [HideInInspector] public int rowIndex = -1;

    private static readonly Color NormalColor = new Color(0.95f, 0.95f, 0.94f);
    private static readonly Color SelectedColor = new Color(0.98f, 0.90f, 0.72f);

    private RectTransform rectTransform;
    private Canvas canvas;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void SetSelected(bool selected)
    {
        if (rowBackground != null)
            rowBackground.color = selected ? SelectedColor : NormalColor;
        if (selectedIndicator != null)
            selectedIndicator.gameObject.SetActive(selected);
    }

    // --- Drag Handlers ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsValidDragTarget(eventData)) return;
        OnBeginDragRow?.Invoke(rowIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsValidDragTarget(eventData)) return;
        OnDragRow?.Invoke(rowIndex, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsValidDragTarget(eventData)) return;
        OnEndDragRow?.Invoke(rowIndex, eventData);
    }

    private bool IsValidDragTarget(PointerEventData eventData)
    {
        if (eventData.pointerPress != null)
        {
            GameObject pressed = eventData.pointerPress;
            if (pressed == xInput.gameObject || pressed == zInput.gameObject)
                return false;
        }
        return true;
    }
}