// File: WaypointRowUI.cs
/*using UnityEngine;
using UnityEngine.EventSystems;
using System;
using TMPro; // Use TextMeshPro

public class WaypointRowUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References (Display)")]
    public TMP_Text xText;
    public TMP_Text zText;
    public TMP_Text speedText;
    public TMP_Text theta1Text;
    public TMP_Text theta2Text;
    public TMP_Text timeText;

    [Header("UI References (Edit - Hidden by default)")]
    public TMP_InputField xInput;
    public TMP_InputField zInput;
    public TMP_InputField speedInput;

    private int waypointIndex;
    private Waypoint currentData; // Direct reference to the data model

    // Callbacks to the main controller
    private Action<int> OnSelectRow;
    private Action<int> OnWaypointUpdated; // Notifies controller that data has changed

    public void Initialize(int index, Waypoint data, Action<int> onSelect, Action<int> onUpdate)
    {
        waypointIndex = index;
        currentData = data;
        OnSelectRow = onSelect;
        OnWaypointUpdated = onUpdate;

        // Ensure inputs are hidden initially
        xInput.gameObject.SetActive(false);
        zInput.gameObject.SetActive(false);
        speedInput.gameObject.SetActive(false);

        // Setup InputField listeners for when editing is complete
        xInput.onEndEdit.AddListener(val => OnEndEditField(xInput, xText, val, FieldType.X));
        zInput.onEndEdit.AddListener(val => OnEndEditField(zInput, zText, val, FieldType.Z));
        speedInput.onEndEdit.AddListener(val => OnEndEditField(speedInput, speedText, val, FieldType.Speed));

        UpdateVisuals();
    }

    // Called to update all text fields from the currentData object
    public void UpdateVisuals()
    {
        if (currentData == null) return;
        xText.text = currentData.xz.x.ToString("F2");
        zText.text = currentData.xz.y.ToString("F2");
        speedText.text = currentData.speedPercent.ToString("F0");
        theta1Text.text = currentData.theta.x.ToString("F1");
        theta2Text.text = currentData.theta.y.ToString("F1");
        timeText.text = currentData.timeToReach.ToString("F2");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Single-click to select the row
        if (eventData.clickCount == 1)
        {
            OnSelectRow?.Invoke(waypointIndex);
        }
        // Double-click to edit a specific field
        else if (eventData.clickCount >= 2)
        {
            GameObject clickedObj = eventData.pointerPressRaycast.gameObject;
            if (clickedObj == xText.gameObject) EnableEdit(xText, xInput);
            else if (clickedObj == zText.gameObject) EnableEdit(zText, zInput);
            else if (clickedObj == speedText.gameObject) EnableEdit(speedText, speedInput);
        }
    }

    private void EnableEdit(TMP_Text textObj, TMP_InputField inputObj)
    {
        textObj.gameObject.SetActive(false);
        inputObj.gameObject.SetActive(true);
        inputObj.text = textObj.text; // Use current text value
        inputObj.Select();
        inputObj.ActivateInputField();
    }

    private enum FieldType { X, Z, Speed }

    private void OnEndEditField(TMP_InputField inputObj, TMP_Text textObj, string newValue, FieldType type)
    {
        // Always revert the UI state (hide input, show text)
        inputObj.gameObject.SetActive(false);
        textObj.gameObject.SetActive(true);

        if (float.TryParse(newValue, out float parsedValue))
        {
            bool hasChanged = false;
            switch (type)
            {
                case FieldType.X:
                    if (Mathf.Abs(currentData.xz.x - parsedValue) > 0.01f) { currentData.xz.x = parsedValue; hasChanged = true; }
                    break;
                case FieldType.Z:
                    if (Mathf.Abs(currentData.xz.y - parsedValue) > 0.01f) { currentData.xz.y = parsedValue; hasChanged = true; }
                    break;
                case FieldType.Speed:
                    parsedValue = Mathf.Clamp(parsedValue, 1f, 100f); // Validate speed
                    if (Mathf.Abs(currentData.speedPercent - parsedValue) > 0.1f) { currentData.speedPercent = parsedValue; hasChanged = true; }
                    break;
            }

            if (hasChanged)
            {
                // The data model (currentData) is already updated.
                // Tell the controller to recalculate derived values for this waypoint.
                OnWaypointUpdated?.Invoke(waypointIndex);
            }
        }
    }
}
*/