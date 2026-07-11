using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using SFB;

public class SCARA_UIController : MonoBehaviour
{
    [Header("Layer References")]
    public SCARA_WaypointManager waypointManager;
    public SCARA_TrajectoryPlanner trajectoryPlanner;
    public SCARA_ReplayController replayController;

    [Header("Waypoint List UI")]
    public RectTransform scrollViewContent;
    public GameObject waypointRowPrefab;

    [Header("Speed Control")]
    public Slider speedSlider;
    public Button applySpeedButton;
    public Text instructionText;
    public float defaultSpeed = 50f;

    [Header("Other Controls")]
    public Slider playbackSpeedSlider;
    public Button undoButton;
    public Button clearButton;
    public Button loadCsvButton;
    public InputField loadCsvPathField;
    public Text errorText;

    private string outputFolder;
    private float pendingSpeed = 50f;
    private bool isUpdatingUI = false;

    void Start()
    {
        outputFolder = Path.Combine(Application.dataPath, "Trajectories");

        waypointManager.OnWaypointsChanged += RefreshWaypointList;
        waypointManager.OnStatusMessage += SetStatus;
        waypointManager.OnSelectedWaypointChanged += OnWaypointSelected;

        undoButton.onClick.AddListener(() => waypointManager.UndoLast());
        clearButton.onClick.AddListener(() => waypointManager.ClearAll());
        loadCsvButton.onClick.AddListener(OnLoadCsvClicked);

        speedSlider.minValue = 1f;
        speedSlider.maxValue = 100f;
        speedSlider.wholeNumbers = true;
        speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);

        applySpeedButton.onClick.AddListener(ApplySpeedToSelected);

        if (instructionText != null)
            instructionText.text = "Select a waypoint from the list, adjust speed, then click Apply.";

        ResetSliderToDefault();

        if (loadCsvPathField != null)
            loadCsvPathField.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            OnGenerateTrajectory();
    }

    void GetCurrentJointAngles(out float theta1, out float theta2)
    {
        theta1 = 0f;
        theta2 = 0f;
        if (replayController == null) return;
        if (replayController.joint1 != null)
            theta1 = Mathf.DeltaAngle(0f, replayController.joint1.localEulerAngles.y);
        if (replayController.joint2 != null)
            theta2 = Mathf.DeltaAngle(0f, replayController.joint2.localEulerAngles.y);
    }

    void ResetSliderToDefault()
    {
        isUpdatingUI = true;
        speedSlider.value = defaultSpeed;
        pendingSpeed = defaultSpeed;
        isUpdatingUI = false;
        if (instructionText != null)
            instructionText.text = "Select a waypoint, adjust speed, then click Apply.";
    }

    void OnWaypointSelected(int index)
    {
        if (index < 0 || index >= waypointManager.waypoints.Count)
        {
            ResetSliderToDefault();
            return;
        }

        isUpdatingUI = true;
        float currentSpeed = waypointManager.waypoints[index].speedPercent;
        speedSlider.value = currentSpeed;
        pendingSpeed = currentSpeed;
        isUpdatingUI = false;

        if (instructionText != null)
            instructionText.text = string.Format("Waypoint #{0} selected. Adjust speed and click Apply.", index + 1);
    }

    void OnSpeedSliderChanged(float value)
    {
        if (isUpdatingUI) return;
        pendingSpeed = value;
        if (instructionText != null)
            instructionText.text = string.Format("Speed set to {0:F0}%. Click Apply to save for selected waypoint.", value);
    }

    void ApplySpeedToSelected()
    {
        if (waypointManager.selectedIndex < 0 || waypointManager.selectedIndex >= waypointManager.waypoints.Count)
        {
            SetStatus("No waypoint selected. Select one from the list first.");
            if (instructionText != null)
                instructionText.text = "Error: No waypoint selected.";
            return;
        }

        int idx = waypointManager.selectedIndex;
        waypointManager.SetSpeedForSelected(idx, pendingSpeed);
        SetStatus(string.Format("Speed for Waypoint #{0} set to {1:F0}%.", idx + 1, pendingSpeed));

        ResetSliderToDefault();
        RefreshWaypointList();
    }

    void RefreshWaypointList()
    {
        for (int i = scrollViewContent.childCount - 1; i >= 0; i--)
            Destroy(scrollViewContent.GetChild(i).gameObject);

        List<Waypoint> wps = waypointManager.waypoints;

        float currentTheta1, currentTheta2;
        GetCurrentJointAngles(out currentTheta1, out currentTheta2);

        float[] segDurations = trajectoryPlanner != null
            ? trajectoryPlanner.GetSegmentDurations(wps, currentTheta1, currentTheta2)
            : new float[0];

        float rowHeight = 65f;

        RectTransform contentRect = scrollViewContent;
        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight * wps.Count);

        for (int i = 0; i < wps.Count; i++)
        {
            int index = i;
            GameObject row = Instantiate(waypointRowPrefab);
            row.transform.SetParent(scrollViewContent, false);

            RectTransform rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0, -i * rowHeight);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight);
                rt.localScale = Vector3.one;
            }

            Text label = row.GetComponentInChildren<Text>();
            if (label != null)
            {
                float segTime = (i < segDurations.Length) ? segDurations[i] : 0f;
                label.text = string.Format(
                    "#{0}\nX: {1:F2}    Z: {2:F2}\nθ1: {3:F1}°   θ2: {4:F1}°\nSpeed: {5:F0}%   Time: {6:F2}s",
                    i + 1,
                    wps[i].xz.x,
                    wps[i].xz.y,
                    wps[i].theta1Deg,
                    wps[i].theta2Deg,
                    wps[i].speedPercent,
                    segTime);
            }

            Button selectBtn = row.GetComponentInChildren<Button>();
            if (selectBtn != null)
                selectBtn.onClick.AddListener(() => waypointManager.SelectWaypoint(index));
        }

        Canvas.ForceUpdateCanvases();
    }

    void OnGenerateTrajectory()
    {
        SetStatus("");

        if (trajectoryPlanner == null)
        {
            SetStatus("Trajectory Planner is not assigned.");
            return;
        }
        if (replayController == null)
        {
            SetStatus("Replay Controller is not assigned.");
            return;
        }

        if (waypointManager.waypoints.Count < 2)
        {
            SetStatus("Need at least 2 waypoints before generating a trajectory.");
            return;
        }

        float currentTheta1, currentTheta2;
        GetCurrentJointAngles(out currentTheta1, out currentTheta2);

        TrajectorySample[] data = trajectoryPlanner.Generate(waypointManager.waypoints,
                                                             currentTheta1, currentTheta2);

        if (data.Length == 0)
        {
            SetStatus("Trajectory generation failed.");
            return;
        }

        string path = SCARA_CSVSerializer.Save(data, outputFolder);
        Debug.Log("Trajectory saved -> " + path);

        float playbackSpeed = playbackSpeedSlider != null ? playbackSpeedSlider.value : 50f;
        replayController.LoadTrajectory(data, playbackSpeed, true);

        float duration = data[data.Length - 1].time;
        SetStatus(string.Format("Trajectory generated: {0} samples, {1:F2}s total. Saved to {2}.",
                                 data.Length, duration, path));
    }

    void OnLoadCsvClicked()
    {
        SetStatus("");

        var extensions = new[] { new ExtensionFilter("CSV Files", "csv") };

        StandaloneFileBrowser.OpenFilePanelAsync("Select CSV Trajectory", "", extensions, false, (string[] paths) =>
        {
            if (paths.Length == 0)
            {
                SetStatus("No file selected.");
                return;
            }

            string path = paths[0];

            TrajectorySample[] data;
            string error;
            if (!SCARA_CSVSerializer.TryLoad(path, out data, out error))
            {
                SetStatus("Could not load trajectory file: " + error);
                return;
            }

            float playbackSpeed = playbackSpeedSlider != null ? playbackSpeedSlider.value : 50f;
            replayController.LoadTrajectory(data, playbackSpeed, false);
            SetStatus(string.Format("Loaded {0} samples from {1}.", data.Length, path));
        });
    }

    void SetStatus(string msg)
    {
        if (errorText != null) errorText.text = msg;
        if (!string.IsNullOrEmpty(msg)) Debug.Log("SCARA_UIController: " + msg);
    }
}