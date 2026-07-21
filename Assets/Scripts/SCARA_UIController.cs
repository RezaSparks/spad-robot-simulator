using System;
using System.Collections.Generic;
using System.IO;
using SFB;
using UnityEngine;
using UnityEngine.UI;

public class SCARA_UIController : MonoBehaviour
{
    [Header("Layer References")]
    public SCARA_WaypointManager waypointManager;
    public SCARA_TrajectoryPlanner trajectoryPlanner;
    public SCARA_ReplayController replayController;
    public SCARA_RobotController robotController;

    [Header("Waypoint List UI")]
    public RectTransform scrollViewContent;
    public GameObject waypointRowPrefab;
    public float rowHeight = 34f;

    [Header("Speed Control")]
    public Slider speedSlider;
    public Text instructionText;
    public float defaultSpeed = 50f;

    [Header("Edit Mode UI")]
    public Button editModeButton;
    public Color editModeActiveColor = Color.green;
    public Color editModeInactiveColor = Color.white;
    public string editModeActiveText = "Exit Edit Mode";
    public string editModeInactiveText = "Enter Edit Mode";

    [Header("Trajectory Controls")]
    public Button exportCsvButton;
    public Button openFolderButton;
    public Button loadCsvButton;

    [Header("Playback Controls")]
    public Button playPauseButton;
    public Button stepForwardButton;
    public Button stepBackwardButton;
    public Text timeDisplayText;

    [Header("Other Controls")]
    public Slider playbackSpeedSlider;
    public Button undoButton;
    public Button clearButton;
    public InputField loadCsvPathField;
    public Text errorText;

    private string outputFolder;
    private bool isUpdatingUI = false;
    private List<SCARA_WaypointRowUI> activeRows = new List<SCARA_WaypointRowUI>();

    private TrajectorySample[] lastGeneratedTrajectory;
    private List<Waypoint> lastGeneratedWaypoints;

    private bool isPlaying = false;

    void Start()
    {
        outputFolder = Path.Combine(Application.dataPath, "Trajectories");
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        waypointManager.OnWaypointsChanged += RefreshWaypointList;
        waypointManager.OnStatusMessage += SetStatus;
        waypointManager.OnSelectedWaypointsChanged += OnWaypointsSelected;
        waypointManager.OnWaypointMoved += UpdateSingleRowPosition;

        undoButton.onClick.AddListener(() => waypointManager.UndoLast());
        clearButton.onClick.AddListener(() => waypointManager.ClearAll());
        loadCsvButton.onClick.AddListener(OnLoadCsvClicked);

        speedSlider.minValue = 1f;
        speedSlider.maxValue = 100f;
        speedSlider.wholeNumbers = true;
        speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);

        if (editModeButton != null)
        {
            editModeButton.onClick.AddListener(ToggleEditModeUI);
            UpdateEditModeButtonVisuals();
        }

        if (exportCsvButton != null)
            exportCsvButton.onClick.AddListener(OnExportCsvAsClicked);
        if (openFolderButton != null)
            openFolderButton.onClick.AddListener(OnOpenFolderClicked);

        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(TogglePlayPause);
        if (stepForwardButton != null)
            stepForwardButton.onClick.AddListener(() => replayController.StepForward());
        if (stepBackwardButton != null)
            stepBackwardButton.onClick.AddListener(() => replayController.StepBackward());

        if (instructionText != null)
            instructionText.text = "Select a waypoint. Drag the slider to adjust speed.";

        ResetSliderToDefault();

        if (loadCsvPathField != null)
            loadCsvPathField.gameObject.SetActive(false);

        UpdatePlayPauseButtonText();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            OnGenerateTrajectory();

        if (timeDisplayText != null && replayController != null)
        {
            float current = replayController.CurrentTime;
            float total = replayController.TotalDuration;
            timeDisplayText.text = string.Format("{0:F2} / {1:F2} s", current, total);
        }

        if (playPauseButton != null && replayController != null)
        {
            bool nowPlaying = replayController.IsPlaying;
            if (nowPlaying != isPlaying)
            {
                isPlaying = nowPlaying;
                UpdatePlayPauseButtonText();
            }
        }
    }

    public void ToggleEditModeUI()
    {
        waypointManager.ToggleEditMode();
        UpdateEditModeButtonVisuals();
    }

    private void UpdateEditModeButtonVisuals()
    {
        if (editModeButton == null) return;
        bool isActive = waypointManager.IsEditMode;

        Text buttonText = editModeButton.GetComponentInChildren<Text>();
        if (buttonText != null)
            buttonText.text = isActive ? editModeActiveText : editModeInactiveText;

        ColorBlock cb = editModeButton.colors;
        cb.normalColor = isActive ? editModeActiveColor : editModeInactiveColor;
        cb.highlightedColor = isActive ? editModeActiveColor : editModeInactiveColor;
        cb.pressedColor = isActive ? editModeActiveColor : editModeInactiveColor;
        editModeButton.colors = cb;
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
        isUpdatingUI = false;
        if (instructionText != null)
            instructionText.text = "Select a waypoint, then drag the slider to adjust speed.";
    }

    void OnWaypointsSelected(List<int> selectedIndices)
    {
        if (selectedIndices.Count == 0)
        {
            ResetSliderToDefault();
            return;
        }
        int first = selectedIndices[0];
        if (first >= 0 && first < waypointManager.waypoints.Count)
        {
            isUpdatingUI = true;
            speedSlider.value = waypointManager.waypoints[first].speedPercent;
            isUpdatingUI = false;
            if (instructionText != null)
                instructionText.text = string.Format("{0} waypoints selected. Adjusting speed for first.", selectedIndices.Count);
        }
        else
            ResetSliderToDefault();
    }

    void OnSpeedSliderChanged(float value)
    {
        if (isUpdatingUI) return;

        List<int> selected = waypointManager.selectedIndices;
        if (selected.Count == 0)
        {
            SetStatus("No waypoint selected to apply speed.");
            return;
        }

        foreach (int idx in selected)
        {
            waypointManager.SetSpeedForSelected(idx, value);
        }
        foreach (int idx in selected)
            UpdateSingleRowTime(idx);

        if (instructionText != null)
            instructionText.text = string.Format("Speed set to {0:F0}% for {1} waypoints.", value, selected.Count);
    }

    private void UpdateSingleRowPosition(int index)
    {
        if (activeRows == null || index >= activeRows.Count) return;
        SCARA_WaypointRowUI rowUI = activeRows[index];
        if (rowUI == null) return;

        List<Waypoint> wps = waypointManager.waypoints;
        if (index >= wps.Count) return;
        Waypoint wp = wps[index];

        if (rowUI.xInput != null)
            rowUI.xInput.text = wp.xz.x.ToString("F2");
        if (rowUI.zInput != null)
            rowUI.zInput.text = wp.xz.y.ToString("F2");
        if (rowUI.angleText != null)
            rowUI.angleText.text = string.Format("\u03B81: {0:F1}\u00B0\n\u03B82: {1:F1}\u00B0", wp.theta1Deg, wp.theta2Deg);

        UpdateSingleRowTime(index);
    }

    private void UpdateSingleRowTime(int index)
    {
        if (activeRows == null || index >= activeRows.Count) return;
        SCARA_WaypointRowUI rowUI = activeRows[index];
        if (rowUI == null || rowUI.speedTimeText == null) return;

        List<Waypoint> wps = waypointManager.waypoints;
        if (index >= wps.Count) return;

        float currentTheta1, currentTheta2;
        GetCurrentJointAngles(out currentTheta1, out currentTheta2);

        float[] segDurations = trajectoryPlanner != null
            ? trajectoryPlanner.GetSegmentDurations(wps, currentTheta1, currentTheta2)
            : new float[0];

        float segTime = (index < segDurations.Length) ? segDurations[index] : 0f;
        Waypoint wp = wps[index];
        rowUI.speedTimeText.text = string.Format("Speed: {0:F0}%\nTime: {1:F2}s", wp.speedPercent, segTime);
    }

    void RefreshWaypointList()
    {
        for (int i = scrollViewContent.childCount - 1; i >= 0; i--)
            Destroy(scrollViewContent.GetChild(i).gameObject);
        activeRows.Clear();

        List<Waypoint> wps = waypointManager.waypoints;
        float currentTheta1, currentTheta2;
        GetCurrentJointAngles(out currentTheta1, out currentTheta2);
        float[] segDurations = trajectoryPlanner != null
            ? trajectoryPlanner.GetSegmentDurations(wps, currentTheta1, currentTheta2)
            : new float[0];

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
                Vector2 offsetMin = rt.offsetMin;
                Vector2 offsetMax = rt.offsetMax;
                offsetMin.x = 0f;
                offsetMax.x = 0f;
                rt.offsetMin = offsetMin;
                rt.offsetMax = offsetMax;
                rt.localScale = Vector3.one;

                RectTransform[] childRects = row.GetComponentsInChildren<RectTransform>(true);
                for (int c = 0; c < childRects.Length; c++)
                    childRects[c].localScale = Vector3.one;
            }

            SCARA_WaypointRowUI rowUI = row.GetComponent<SCARA_WaypointRowUI>();
            if (rowUI == null) continue;
            activeRows.Add(rowUI);

            Waypoint wp = wps[i];
            float segTime = (i < segDurations.Length) ? segDurations[i] : 0f;

            if (rowUI.indexText != null)
                rowUI.indexText.text = string.Format("#{0}", i + 1);
            if (rowUI.xInput != null)
                rowUI.xInput.text = wp.xz.x.ToString("F2");
            if (rowUI.zInput != null)
                rowUI.zInput.text = wp.xz.y.ToString("F2");
            if (rowUI.angleText != null)
                rowUI.angleText.text = string.Format("\u03B81: {0:F1}\u00B0\n\u03B82: {1:F1}\u00B0", wp.theta1Deg, wp.theta2Deg);
            if (rowUI.speedTimeText != null)
                rowUI.speedTimeText.text = string.Format("Speed: {0:F0}%\nTime: {1:F2}s", wp.speedPercent, segTime);

            rowUI.SetSelected(waypointManager.selectedIndices.Contains(i));

            if (rowUI.selectButton != null)
            {
                rowUI.selectButton.onClick.RemoveAllListeners();
                rowUI.selectButton.onClick.AddListener(() => OnRowClicked(index));
            }

            if (rowUI.xInput != null)
            {
                rowUI.xInput.onEndEdit.RemoveAllListeners();
                InputField zRef = rowUI.zInput;
                rowUI.xInput.onEndEdit.AddListener((string newValue) => OnWaypointFieldEdited(index, newValue, zRef, true));
            }
            if (rowUI.zInput != null)
            {
                rowUI.zInput.onEndEdit.RemoveAllListeners();
                InputField xRef = rowUI.xInput;
                rowUI.zInput.onEndEdit.AddListener((string newValue) => OnWaypointFieldEdited(index, newValue, xRef, false));
            }

            Button upBtn = rowUI.moveUpButton;
            Button downBtn = rowUI.moveDownButton;
            if (upBtn != null)
            {
                upBtn.onClick.RemoveAllListeners();
                upBtn.onClick.AddListener(() => waypointManager.MoveSelectedWaypoints(-1));
            }
            if (downBtn != null)
            {
                downBtn.onClick.RemoveAllListeners();
                downBtn.onClick.AddListener(() => waypointManager.MoveSelectedWaypoints(1));
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private void OnRowClicked(int index)
    {
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (ctrl)
            waypointManager.ToggleWaypointSelection(index);
        else if (shift && waypointManager.selectedIndices.Count > 0)
        {
            int last = waypointManager.selectedIndices[waypointManager.selectedIndices.Count - 1];
            waypointManager.SelectRange(last, index);
        }
        else
            waypointManager.SelectWaypoint(index);
    }

    void OnWaypointFieldEdited(int index, string editedValue, InputField otherField, bool editedIsX)
    {
        if (index < 0 || index >= waypointManager.waypoints.Count) return;

        float editedNumber;
        if (!float.TryParse(editedValue, out editedNumber))
        {
            SetStatus(string.Format("Waypoint #{0}: \"{1}\" is not a valid number.", index + 1, editedValue));
            RefreshWaypointList();
            return;
        }

        float otherNumber = 0f;
        bool otherParsed = otherField != null && float.TryParse(otherField.text, out otherNumber);
        if (!otherParsed)
        {
            Waypoint current = waypointManager.waypoints[index];
            otherNumber = editedIsX ? current.xz.y : current.xz.x;
        }

        Vector2 newXZ = editedIsX
            ? new Vector2(editedNumber, otherNumber)
            : new Vector2(otherNumber, editedNumber);

        bool success = waypointManager.UpdateWaypointXZ(index, newXZ);
        if (!success)
            RefreshWaypointList();
        else
            UpdateSingleRowPosition(index);
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

        string errorMsg;
        TrajectorySample[] data = trajectoryPlanner.Generate(waypointManager.waypoints,
                                                             currentTheta1, currentTheta2,
                                                             out errorMsg);
        if (data.Length == 0)
        {
            SetStatus("Trajectory generation failed: " + errorMsg);
            return;
        }

        lastGeneratedTrajectory = data;
        lastGeneratedWaypoints = new List<Waypoint>(waypointManager.waypoints);

        if (robotController != null)
            robotController.cachedTrajectory = data;

        float playbackSpeed = playbackSpeedSlider != null ? playbackSpeedSlider.value : 50f;
        replayController.LoadTrajectory(data, playbackSpeed, true);
        isPlaying = true;
        UpdatePlayPauseButtonText();

        float duration = data[data.Length - 1].time;
        SetStatus(string.Format("Trajectory generated: {0} samples, {1:F2}s total. Use 'Export CSV As…' to save.",
                                 data.Length, duration));
    }

    public void OnExportCsvAsClicked()
    {
        if (lastGeneratedTrajectory == null || lastGeneratedTrajectory.Length == 0)
        {
            SetStatus("No trajectory generated yet. Press Space to generate one first.");
            return;
        }

        if (trajectoryPlanner == null)
        {
            SetStatus("Trajectory Planner is not assigned.");
            return;
        }

        string defaultName = $"Trajectory_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
        var extensions = new[] { new ExtensionFilter("CSV Files", "csv") };
        StandaloneFileBrowser.SaveFilePanelAsync(
            title: "Export Trajectory CSV",
            directory: outputFolder,
            defaultName: defaultName,
            extensions: extensions,
            (string path) =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    SetStatus("Export cancelled.");
                    return;
                }

                string error;
                bool success = SCARA_CSVSerializer.Save(
                    lastGeneratedTrajectory,
                    path,
                    trajectoryPlanner.lengthArm1,
                    trajectoryPlanner.lengthArm2,
                    trajectoryPlanner.maxDegreesPerSecond,
                    lastGeneratedWaypoints,
                    out error
                );

                if (success)
                    SetStatus($"Trajectory exported successfully to: {path}");
                else
                    SetStatus($"Export failed: {error}");
            }
        );
    }

    public void OnOpenFolderClicked()
    {
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);
        Application.OpenURL("file://" + outputFolder);
        SetStatus($"Opened folder: {outputFolder}");
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
            List<Waypoint> waypoints;
            string error;
            if (!SCARA_CSVSerializer.TryLoad(path, out data, out waypoints, out error))
            {
                SetStatus("Could not load trajectory file: " + error);
                return;
            }

            if (waypoints != null && waypoints.Count > 0)
            {
                waypointManager.ClearAll();
                foreach (var wp in waypoints)
                    waypointManager.AddWaypoint(wp, true);
                waypointManager.DeselectAll();
                waypointManager.OnWaypointsChanged?.Invoke();
            }

            float playbackSpeed = playbackSpeedSlider != null ? playbackSpeedSlider.value : 50f;
            replayController.LoadTrajectory(data, playbackSpeed, false);
            isPlaying = false;
            UpdatePlayPauseButtonText();
            SetStatus(string.Format("Loaded {0} samples and {1} waypoints from {2}.", data.Length, waypoints?.Count ?? 0, path));
        });
    }

    void TogglePlayPause()
    {
        if (replayController == null) return;
        if (replayController.IsPlaying)
        {
            replayController.Pause();
            isPlaying = false;
        }
        else
        {
            replayController.Resume();
            isPlaying = true;
        }
        UpdatePlayPauseButtonText();
    }

    void UpdatePlayPauseButtonText()
    {
        if (playPauseButton == null) return;
        Text btnText = playPauseButton.GetComponentInChildren<Text>();
        if (btnText != null)
            btnText.text = isPlaying ? "Pause" : "Play";
    }

    void SetStatus(string msg)
    {
        if (errorText != null) errorText.text = msg;
        if (!string.IsNullOrEmpty(msg)) Debug.Log("SCARA_UIController: " + msg);
    }
}