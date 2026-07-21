using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds the full dark, industrial-style SCARA control screen and,
/// if a SCARA_UIController already exists in the scene, auto-assigns
/// every UI-facing public field it exposes (scrollViewContent,
/// waypointRowPrefab, speedSlider, applySpeedButton, instructionText,
/// playbackSpeedSlider, undoButton, clearButton, loadCsvButton,
/// loadCsvPathField, errorText). It does NOT touch waypointManager /
/// trajectoryPlanner / replayController - those are your scene logic
/// objects and should stay exactly as you already have them assigned.
///
/// Run "Build Waypoint Row Prefab" first (from SCARA_RowPrefabBuilder).
///
/// Usage: Tools -> SCARA HMI -> Build Full UI Layout
/// </summary>
public static class SCARA_UIBuilder
{
    private const string RowPrefabPath = "Assets/Prefabs/SCARA_WaypointRow.prefab";

    private static Font uiFont;
    private static readonly Color PanelDark = new Color(0.11f, 0.11f, 0.13f);
    private static readonly Color PanelMid = new Color(0.16f, 0.16f, 0.19f);
    private static readonly Color PanelLight = new Color(0.22f, 0.22f, 0.26f);
    private static readonly Color AccentYellow = new Color(0.95f, 0.75f, 0.15f);
    private static readonly Color TextWhite = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color TextDim = new Color(0.65f, 0.65f, 0.65f);

    [MenuItem("Tools/SCARA HMI/Build Full UI Layout")]
    public static void BuildUI()
    {
        GameObject rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath);
        if (rowPrefab == null)
        {
            Debug.LogError("SCARA_UIBuilder: run 'Tools -> SCARA HMI -> Build Waypoint Row Prefab' first.");
            return;
        }

        uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject canvasGO = new GameObject("SCARA_HMI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create SCARA HMI Canvas");
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // --- Top Bar: End-Effector / theta readouts + speed slider + Apply ---
        Text endEffectorText, theta1Text, theta2Text;
        Slider speedSlider;
        Button applyButton;
        BuildTopBar(canvasGO.transform, out endEffectorText, out theta1Text, out theta2Text, out speedSlider, out applyButton);

        // --- Right column: Undo / Clear / Playback speed ---
        Button undoButton, clearButton;
        Slider playbackSpeedSlider;
        BuildRightColumn(canvasGO.transform, out undoButton, out clearButton, out playbackSpeedSlider);

        // --- Left/Center: waypoint table ScrollView + Load button ---
        RectTransform content;
        Button loadButton;
        InputField loadPathField;
        BuildTablePanel(canvasGO.transform, rowPrefab, out content, out loadButton, out loadPathField);

        // --- Bottom status bar: instruction + error text ---
        Text instructionText, errorText;
        BuildStatusBar(canvasGO.transform, out instructionText, out errorText);

        Debug.Log("SCARA HMI UI built. EndEffector/theta1/theta2 readout Texts are NOT auto-wired " +
                  "(SCARA_EndEffectorDisplay.cs field names weren't provided) - assign those three manually.");

        AutoWireController(content, rowPrefab, speedSlider, applyButton, instructionText,
                            playbackSpeedSlider, undoButton, clearButton, loadButton, loadPathField, errorText);

        Selection.activeGameObject = canvasGO;
    }

    // ----------------------------------------------------------------
    private static void BuildTopBar(Transform parent, out Text endEffectorText, out Text theta1Text,
                                     out Text theta2Text, out Slider speedSlider, out Button applyButton)
    {
        Image bar = AddImage(parent, "TopBar", PanelMid);
        RectTransform rt = bar.rectTransform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, 60);
        rt.anchoredPosition = Vector2.zero;

        endEffectorText = CreateText(bar.transform, "EndEffectorPositionText", "End-Effector: (0.00, 0.00)", 16, TextAnchor.MiddleLeft, TextWhite);
        AnchorLeft(endEffectorText.rectTransform, 20, 280);

        theta1Text = CreateText(bar.transform, "Theta1ReadoutText", "\u03B81: 0.0\u00B0", 16, TextAnchor.MiddleCenter, TextWhite);
        AnchorCenter(theta1Text.rectTransform, -120, 160);

        theta2Text = CreateText(bar.transform, "Theta2ReadoutText", "\u03B82: 0.0\u00B0", 16, TextAnchor.MiddleCenter, TextWhite);
        AnchorCenter(theta2Text.rectTransform, 120, 160);

        speedSlider = CreateSlider(bar.transform, "SpeedSlider", 1, 100, 50);
        RectTransform sliderRT = speedSlider.GetComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(1, 0.5f);
        sliderRT.anchorMax = new Vector2(1, 0.5f);
        sliderRT.pivot = new Vector2(1, 0.5f);
        sliderRT.sizeDelta = new Vector2(180, 20);
        sliderRT.anchoredPosition = new Vector2(-220, 12);

        applyButton = CreateButton(bar.transform, "ApplySpeedButton", "Apply");
        RectTransform applyRT = applyButton.GetComponent<RectTransform>();
        applyRT.anchorMin = new Vector2(1, 0.5f);
        applyRT.anchorMax = new Vector2(1, 0.5f);
        applyRT.pivot = new Vector2(1, 0.5f);
        applyRT.sizeDelta = new Vector2(100, 26);
        applyRT.anchoredPosition = new Vector2(-20, -14);
    }

    private static void BuildRightColumn(Transform parent, out Button undoButton, out Button clearButton, out Slider playbackSpeedSlider)
    {
        GameObject col = CreateUIObject("RightColumn", parent);
        RectTransform rt = col.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(160, 0);
        rt.offsetMax = new Vector2(0, -60);
        rt.offsetMin = new Vector2(-160, 60);

        VerticalLayoutGroup vlg = col.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 20, 10);
        vlg.spacing = 12;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;

        undoButton = CreateButton(col.transform, "UndoButton", "Undo");
        undoButton.GetComponent<LayoutElement>().preferredHeight = 32;

        clearButton = CreateButton(col.transform, "ClearButton", "Clear");
        clearButton.GetComponent<LayoutElement>().preferredHeight = 32;

        CreateText(col.transform, "PlaybackLabel", "Playback Speed", 12, TextAnchor.MiddleCenter, TextDim);
        playbackSpeedSlider = CreateSlider(col.transform, "PlaybackSpeedSlider", 1, 100, 50);
        playbackSpeedSlider.GetComponent<LayoutElement>().preferredHeight = 20;
    }

    private static void BuildTablePanel(Transform parent, GameObject rowPrefab, out RectTransform content, out Button loadButton, out InputField loadPathField)
    {
        GameObject panel = CreateUIObject("TablePanel", parent);
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0, 0);
        panelRT.anchorMax = new Vector2(1, 1);
        panelRT.offsetMin = new Vector2(0, 100); // room for bottom status bar
        panelRT.offsetMax = new Vector2(-160, -60); // room for top bar + right column

        VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 6;
        vlg.childForceExpandHeight = false;

        // Header row (static labels, not a data row)
        GameObject header = CreateUIObject("HeaderRow", panel.transform);
        Image headerBg = header.AddComponent<Image>();
        headerBg.color = PanelLight;
        HorizontalLayoutGroup headerLayout = header.AddComponent<HorizontalLayoutGroup>();
        headerLayout.padding = new RectOffset(8, 8, 4, 4);
        headerLayout.spacing = 10;
        LayoutElement headerLE = header.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 28;

        AddHeaderLabel(header.transform, "#", 45);
        AddHeaderLabel(header.transform, "Speed / Time", 190);
        AddHeaderLabel(header.transform, "\u03B81 / \u03B82", 170);
        AddHeaderLabel(header.transform, "X", 80);
        AddHeaderLabel(header.transform, "Z", 80);

        // ScrollView (Viewport + Content) - Content is left as a PLAIN RectTransform
        // with no layout group, because SCARA_UIController positions/sizes each row
        // manually (anchoredPosition / SetSizeWithCurrentAnchors) in RefreshWaypointList().
        GameObject scrollGO = CreateUIObject("WaypointScrollView", panel.transform);
        Image scrollBg = scrollGO.AddComponent<Image>();
        scrollBg.color = PanelDark;
        ScrollRect scrollRect = scrollGO.AddComponent<ScrollRect>();
        LayoutElement scrollLE = scrollGO.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1;

        GameObject viewport = CreateUIObject("Viewport", scrollGO.transform);
        RectTransform viewportRT = viewport.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        GameObject contentGO = CreateUIObject("Content", viewport.transform);
        content = contentGO.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0, 0);

        scrollRect.viewport = viewportRT;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // Optional: instantiate one sample row purely for a visual preview in the Editor
        GameObject sample = Object.Instantiate(rowPrefab, content);
        sample.name = "SampleRow (preview only - destroy or leave; runtime clears it)";

        // Load button (bottom-left of the table panel, matches your screenshot)
        loadButton = CreateButton(panel.transform, "LoadCsvButton", "Load");
        loadButton.GetComponent<LayoutElement>().preferredHeight = 30;
        loadButton.GetComponent<LayoutElement>().preferredWidth = 100;

        // Hidden path field required by SCARA_UIController (SetActive(false) in Start())
        GameObject pathFieldGO = CreateUIObject("LoadCsvPathField", panel.transform);
        Image pathBg = pathFieldGO.AddComponent<Image>();
        pathBg.color = PanelLight;
        loadPathField = pathFieldGO.AddComponent<InputField>();
        Text pathText = CreateText(pathFieldGO.transform, "Text", "", 12, TextAnchor.MiddleLeft, TextWhite);
        pathText.rectTransform.anchorMin = Vector2.zero;
        pathText.rectTransform.anchorMax = Vector2.one;
        pathText.rectTransform.offsetMin = new Vector2(6, 2);
        pathText.rectTransform.offsetMax = new Vector2(-6, -2);
        loadPathField.textComponent = pathText;
        pathFieldGO.SetActive(false);
    }

    private static void BuildStatusBar(Transform parent, out Text instructionText, out Text errorText)
    {
        Image bar = AddImage(parent, "StatusBar", PanelMid);
        RectTransform rt = bar.rectTransform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = new Vector2(0, 40);
        rt.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vlg = bar.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 4, 4);
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandHeight = true;

        instructionText = CreateText(bar.transform, "InstructionText", "Select a waypoint from the list, adjust speed, then click Apply.", 14, TextAnchor.MiddleCenter, TextWhite);
        errorText = CreateText(bar.transform, "ErrorText", "", 12, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.5f));
    }

    // ----------------------------------------------------------------
    private static void AutoWireController(RectTransform content, GameObject rowPrefab, Slider speedSlider,
        Button applyButton, Text instructionText, Slider playbackSpeedSlider, Button undoButton,
        Button clearButton, Button loadButton, InputField loadPathField, Text errorText)
    {
        SCARA_UIController controller = Object.FindObjectOfType<SCARA_UIController>();
        if (controller == null)
        {
            Debug.LogWarning("SCARA_UIBuilder: no SCARA_UIController found in the scene - assign the new UI elements manually.");
            return;
        }

        controller.scrollViewContent = content;
        controller.waypointRowPrefab = rowPrefab;
        controller.speedSlider = speedSlider;
        controller.instructionText = instructionText;
        controller.playbackSpeedSlider = playbackSpeedSlider;
        controller.undoButton = undoButton;
        controller.clearButton = clearButton;
        controller.loadCsvButton = loadButton;
        controller.loadCsvPathField = loadPathField;
        controller.errorText = errorText;

        EditorUtility.SetDirty(controller);
        Debug.Log("SCARA_UIController auto-wired to the new UI (waypointManager / trajectoryPlanner / replayController left untouched).");
    }

    // ----------------------------------------------------------------
    private static void AddHeaderLabel(Transform parent, string label, float width)
    {
        Text t = CreateText(parent, "Header_" + label, label, 12, TextAnchor.MiddleCenter, AccentYellow);
        LayoutElement le = t.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.flexibleWidth = 0;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image AddImage(Transform parent, string name, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor anchor, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        Text txt = go.AddComponent<Text>();
        txt.font = uiFont;
        txt.text = content;
        txt.fontSize = fontSize;
        txt.alignment = anchor;
        txt.color = color;
        go.AddComponent<LayoutElement>();
        return txt;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject go = CreateUIObject(name, parent);
        Image img = go.AddComponent<Image>();
        img.color = PanelLight;
        Button btn = go.AddComponent<Button>();
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 28;
        le.preferredWidth = 100;

        Text txt = CreateText(go.transform, "Text", label, 13, TextAnchor.MiddleCenter, TextWhite);
        RectTransform txtRT = txt.rectTransform;
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        return btn;
    }

    private static Slider CreateSlider(Transform parent, string name, float min, float max, float value)
    {
        GameObject go = CreateUIObject(name, parent);
        Image bg = go.AddComponent<Image>();
        bg.color = PanelDark;
        Slider slider = go.AddComponent<Slider>();
        go.AddComponent<LayoutElement>();

        GameObject fillArea = CreateUIObject("Fill Area", go.transform);
        RectTransform fillAreaRT = fillArea.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0, 0.25f);
        fillAreaRT.anchorMax = new Vector2(1, 0.75f);
        fillAreaRT.offsetMin = new Vector2(5, 0);
        fillAreaRT.offsetMax = new Vector2(-15, 0);

        GameObject fill = CreateUIObject("Fill", fillArea.transform);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = AccentYellow;
        RectTransform fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0.5f, 1);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        GameObject handleArea = CreateUIObject("Handle Slide Area", go.transform);
        RectTransform handleAreaRT = handleArea.GetComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(5, 0);
        handleAreaRT.offsetMax = new Vector2(-5, 0);

        GameObject handle = CreateUIObject("Handle", handleArea.transform);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = TextWhite;
        RectTransform handleRT = handle.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(14, 0);

        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;

        return slider;
    }

    // Anchoring helpers for the top bar's fixed-position elements
    private static void AnchorLeft(RectTransform rt, float left, float width)
    {
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 0.5f);
        rt.sizeDelta = new Vector2(width, 0);
        rt.anchoredPosition = new Vector2(left, 0);
    }

    private static void AnchorCenter(RectTransform rt, float xOffset, float width)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, 30);
        rt.anchoredPosition = new Vector2(xOffset, 5);
    }
}