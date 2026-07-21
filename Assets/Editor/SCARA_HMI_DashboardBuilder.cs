using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// PLACE THIS FILE INSIDE Assets/Editor/ - NOT Assets/Scripts/.
/// This is an Editor-only tool (uses UnityEditor APIs) and must not be
/// compiled into the runtime build; putting it next to your runtime
/// scripts causes compile errors.
///
/// Builds the dashboard matching the approved light-theme mockup:
/// top bar, 4 metric cards, speed row with amber Apply button, a
/// bordered waypoint table with a real selected-row highlight, an
/// action row (Undo/Clear/Load), and a status line.
///
/// Run the two menu items IN ORDER:
///   Tools -> SCARA HMI -> 1) Build Waypoint Row Prefab
///   Tools -> SCARA HMI -> 2) Build Full Dashboard
/// </summary>
public static class SCARA_HMI_DashboardBuilder
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string PrefabPath = "Assets/Prefabs/SCARA_WaypointRow.prefab";

    private static Font uiFont;

    private static readonly Color PageBg = new Color(0.98f, 0.98f, 0.97f);
    private static readonly Color CardBg = new Color(0.94f, 0.94f, 0.92f);
    private static readonly Color BorderColor = new Color(0.85f, 0.85f, 0.83f);
    private static readonly Color TextPrimary = new Color(0.14f, 0.14f, 0.13f);
    private static readonly Color TextSecondary = new Color(0.45f, 0.45f, 0.43f);
    private static readonly Color AmberFill = new Color(0.94f, 0.70f, 0.28f);
    private static readonly Color AmberText = new Color(0.35f, 0.20f, 0.02f);
    private static readonly Color DangerBg = new Color(0.98f, 0.90f, 0.90f);
    private static readonly Color DangerText = new Color(0.55f, 0.14f, 0.14f);
    private static readonly Color SecondaryBtnBg = new Color(0.90f, 0.90f, 0.88f);

    // ------------------------------------------------------------
    // 1) Row prefab
    // ------------------------------------------------------------

    [MenuItem("Tools/SCARA HMI/1) Build Waypoint Row Prefab")]
    public static void BuildRowPrefab()
    {
        uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        GameObject root = new GameObject("SCARA_WaypointRow", typeof(RectTransform));
        RectTransform rootRT = root.GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(0, 34);

        Image bg = root.AddComponent<Image>();
        bg.color = CardBg;

        Button selectButton = root.AddComponent<Button>();
        ColorBlock colors = selectButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.99f, 0.95f, 0.85f);
        colors.pressedColor = AmberFill;
        selectButton.colors = colors;

        HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(14, 10, 4, 4);
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        SCARA_WaypointRowUI rowUI = root.AddComponent<SCARA_WaypointRowUI>();

        GameObject indicatorGO = new GameObject("SelectedIndicator", typeof(RectTransform));
        indicatorGO.transform.SetParent(root.transform, false);
        RectTransform indicatorRT = indicatorGO.GetComponent<RectTransform>();
        indicatorRT.anchorMin = new Vector2(0, 0);
        indicatorRT.anchorMax = new Vector2(0, 1);
        indicatorRT.pivot = new Vector2(0, 0.5f);
        indicatorRT.sizeDelta = new Vector2(3, 0);
        indicatorRT.anchoredPosition = Vector2.zero;
        Image indicatorImg = indicatorGO.AddComponent<Image>();
        indicatorImg.color = AmberFill;
        indicatorGO.SetActive(false);

        Text indexText = CreateColumnText(root.transform, "IndexText", "#0", 40, TextPrimary, TextAnchor.MiddleCenter, false, 0);
        Text speedTimeText = CreateColumnText(root.transform, "SpeedTimeText", "Speed: 0%  Time: 0.00s", 0, TextSecondary, TextAnchor.MiddleLeft, true, 1);
        Text angleText = CreateColumnText(root.transform, "AngleText", "\u03B81: 0.0\u00B0  \u03B82: 0.0\u00B0", 0, TextPrimary, TextAnchor.MiddleLeft, true, 1);
        InputField xInput = CreateColumnInputField(root.transform, "XInput", 70);
        InputField zInput = CreateColumnInputField(root.transform, "ZInput", 70);

        rowUI.indexText = indexText;
        rowUI.angleText = angleText;
        rowUI.speedTimeText = speedTimeText;
        rowUI.xInput = xInput;
        rowUI.zInput = zInput;
        rowUI.selectButton = selectButton;
        rowUI.rowBackground = bg;
        rowUI.selectedIndicator = indicatorImg;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("SCARA waypoint row prefab saved at: " + PrefabPath);
    }

    private static Text CreateColumnText(Transform parent, string name, string sample, float width, Color color, TextAnchor anchor, bool flexible, float flexWeight)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Text txt = go.AddComponent<Text>();
        txt.font = uiFont;
        txt.text = sample;
        txt.fontSize = 13;
        txt.color = color;
        txt.alignment = anchor;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.flexibleWidth = flexible ? flexWeight : 0;

        return txt;
    }

    private static InputField CreateColumnInputField(Transform parent, string name, float width)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image bg = go.AddComponent<Image>();
        bg.color = Color.white;

        InputField input = go.AddComponent<InputField>();
        input.contentType = InputField.ContentType.DecimalNumber;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.flexibleWidth = 0;

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(6, 2);
        textRT.offsetMax = new Vector2(-6, -2);

        Text txt = textGO.AddComponent<Text>();
        txt.font = uiFont;
        txt.fontSize = 13;
        txt.color = TextPrimary;
        txt.alignment = TextAnchor.MiddleLeft;

        input.textComponent = txt;
        return input;
    }

    // ------------------------------------------------------------
    // 2) Full dashboard
    // ------------------------------------------------------------

    [MenuItem("Tools/SCARA HMI/2) Build Full Dashboard")]
    public static void BuildDashboard()
    {
        GameObject rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (rowPrefab == null)
        {
            Debug.LogError("SCARA_HMI_DashboardBuilder: run 'Tools -> SCARA HMI -> 1) Build Waypoint Row Prefab' first.");
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

        Image bg = AddImage(canvasGO.transform, "PageBackground", PageBg);
        StretchFull(bg.rectTransform);

        GameObject root = CreateUIObject("DashboardRoot", canvasGO.transform);
        RectTransform rootRT = root.GetComponent<RectTransform>();
        StretchFull(rootRT);
        VerticalLayoutGroup rootLayout = root.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(40, 40, 30, 30);
        rootLayout.spacing = 16;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childControlHeight = true;
        rootLayout.childControlWidth = true;

        BuildTopBar(root.transform);
        BuildMetricsRow(root.transform);
        Slider speedSlider;
        Button applyButton;
        BuildSpeedRow(root.transform, out speedSlider, out applyButton);

        RectTransform content;
        BuildWaypointsTable(root.transform, rowPrefab, out content);

        Button undoButton, clearButton, loadButton;
        BuildActionRow(root.transform, out undoButton, out clearButton, out loadButton);

        Text instructionText, errorText;
        BuildStatusText(root.transform, out instructionText, out errorText);

        // Hidden field required by SCARA_UIController (kept inactive, per your Start() logic)
        InputField loadPathField = CreateHiddenInputField(root.transform);

        AutoWireController(content, rowPrefab, speedSlider, applyButton, instructionText,
                            null, undoButton, clearButton, loadButton, loadPathField, errorText);

        Selection.activeGameObject = canvasGO;
        Debug.Log("SCARA HMI dashboard built. Playback speed slider and End-Effector/theta readout texts were not part of the approved mockup - add them separately if needed.");
    }

    private static void BuildTopBar(Transform parent)
    {
        GameObject bar = CreateUIObject("TopBar", parent);
        HorizontalLayoutGroup layout = bar.AddComponent<HorizontalLayoutGroup>();
        layout.childForceExpandWidth = false;
        layout.childControlWidth = true;
        LayoutElement barLE = bar.AddComponent<LayoutElement>();
        barLE.preferredHeight = 30;

        Text title = CreateText(bar.transform, "TitleText", "Scara robot control", 20, TextAnchor.MiddleLeft, TextPrimary);
        title.GetComponent<LayoutElement>().flexibleWidth = 1;

        GameObject pill = CreateUIObject("StatusPill", bar.transform);
        Image pillBg = pill.AddComponent<Image>();
        pillBg.color = DangerBg;
        LayoutElement pillLE = pill.AddComponent<LayoutElement>();
        pillLE.preferredWidth = 90;
        pillLE.preferredHeight = 26;

        Text pillText = CreateText(pill.transform, "Text", "Offline", 12, TextAnchor.MiddleCenter, DangerText);
        StretchFull(pillText.rectTransform);
    }

    private static void BuildMetricsRow(Transform parent)
    {
        GameObject row = CreateUIObject("MetricsRow", parent);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 80;

        BuildMetricCard(row.transform, "End-effector x", "0.00");
        BuildMetricCard(row.transform, "End-effector z", "7.00");
        BuildMetricCard(row.transform, "\u03B81", "0.0\u00B0");
        BuildMetricCard(row.transform, "\u03B82", "0.0\u00B0");
    }

    private static void BuildMetricCard(Transform parent, string label, string value)
    {
        GameObject card = CreateUIObject(label + "Card", parent);
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = CardBg;
        VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 12, 12);
        vlg.childAlignment = TextAnchor.MiddleLeft;

        Text labelText = CreateText(card.transform, "Label", label, 13, TextAnchor.MiddleLeft, TextSecondary);
        labelText.GetComponent<LayoutElement>().preferredHeight = 20;

        Text valueText = CreateText(card.transform, "Value", value, 24, TextAnchor.MiddleLeft, TextPrimary);
        valueText.GetComponent<LayoutElement>().preferredHeight = 32;
    }

    private static void BuildSpeedRow(Transform parent, out Slider speedSlider, out Button applyButton)
    {
        GameObject row = CreateUIObject("SpeedRow", parent);
        Image rowBg = row.AddComponent<Image>();
        rowBg.color = CardBg;
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 8, 8);
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.MiddleLeft;
        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 46;

        Text label = CreateText(row.transform, "Label", "Speed", 13, TextAnchor.MiddleLeft, TextSecondary);
        label.GetComponent<LayoutElement>().preferredWidth = 50;

        speedSlider = CreateSlider(row.transform);
        speedSlider.GetComponent<LayoutElement>().flexibleWidth = 1;

        applyButton = CreateButton(row.transform, "ApplySpeedButton", "Apply", AmberFill, AmberText);
        applyButton.GetComponent<LayoutElement>().preferredWidth = 90;
    }

    private static void BuildWaypointsTable(Transform parent, GameObject rowPrefab, out RectTransform content)
    {
        Text tableLabel = CreateText(parent, "WaypointsLabel", "Waypoints", 13, TextAnchor.MiddleLeft, TextSecondary);
        tableLabel.GetComponent<LayoutElement>().preferredHeight = 20;

        GameObject panel = CreateUIObject("TablePanel", parent);
        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = Color.white;
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(1, -1);
        VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandHeight = false;
        LayoutElement panelLE = panel.AddComponent<LayoutElement>();
        panelLE.flexibleHeight = 1;
        panelLE.minHeight = 220;

        GameObject header = CreateUIObject("HeaderRow", panel.transform);
        Image headerBg = header.AddComponent<Image>();
        headerBg.color = CardBg;
        HorizontalLayoutGroup headerLayout = header.AddComponent<HorizontalLayoutGroup>();
        headerLayout.padding = new RectOffset(14, 10, 6, 6);
        headerLayout.spacing = 10;
        LayoutElement headerLE = header.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 28;

        AddHeaderLabel(header.transform, "#", 40, 0);
        AddHeaderLabel(header.transform, "Speed / time", 0, 1);
        AddHeaderLabel(header.transform, "\u03B81 / \u03B82", 0, 1);
        AddHeaderLabel(header.transform, "X", 70, 0);
        AddHeaderLabel(header.transform, "Z", 70, 0);

        GameObject scrollGO = CreateUIObject("WaypointScrollView", panel.transform);
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

        GameObject sample = Object.Instantiate(rowPrefab, content);
        sample.name = "SampleRow_PreviewOnly";
    }

    private static void AddHeaderLabel(Transform parent, string label, float width, float flexWeight)
    {
        Text t = CreateText(parent, "Header_" + label, label, 12, TextAnchor.MiddleLeft, TextSecondary);
        LayoutElement le = t.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.flexibleWidth = flexWeight;
    }

    private static void BuildActionRow(Transform parent, out Button undoButton, out Button clearButton, out Button loadButton)
    {
        GameObject row = CreateUIObject("ActionRow", parent);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childForceExpandWidth = false;
        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 34;

        undoButton = CreateButton(row.transform, "UndoButton", "Undo", SecondaryBtnBg, TextPrimary);
        undoButton.GetComponent<LayoutElement>().preferredWidth = 90;

        clearButton = CreateButton(row.transform, "ClearButton", "Clear", SecondaryBtnBg, TextPrimary);
        clearButton.GetComponent<LayoutElement>().preferredWidth = 90;

        loadButton = CreateButton(row.transform, "LoadCsvButton", "Load", SecondaryBtnBg, TextPrimary);
        loadButton.GetComponent<LayoutElement>().preferredWidth = 90;
    }

    private static void BuildStatusText(Transform parent, out Text instructionText, out Text errorText)
    {
        instructionText = CreateText(parent, "InstructionText", "Select a waypoint from the list, adjust speed, then click Apply.", 13, TextAnchor.MiddleLeft, TextSecondary);
        instructionText.GetComponent<LayoutElement>().preferredHeight = 20;

        errorText = CreateText(parent, "ErrorText", "", 12, TextAnchor.MiddleLeft, DangerText);
        errorText.GetComponent<LayoutElement>().preferredHeight = 18;
    }

    private static InputField CreateHiddenInputField(Transform parent)
    {
        GameObject go = CreateUIObject("LoadCsvPathField", parent);
        Image img = go.AddComponent<Image>();
        img.color = Color.white;
        InputField input = go.AddComponent<InputField>();
        Text txt = CreateText(go.transform, "Text", "", 12, TextAnchor.MiddleLeft, TextPrimary);
        StretchFull(txt.rectTransform);
        input.textComponent = txt;
        go.SetActive(false);
        return input;
    }

    // ------------------------------------------------------------
    // Wiring + generic helpers
    // ------------------------------------------------------------

    private static void AutoWireController(RectTransform content, GameObject rowPrefab, Slider speedSlider,
        Button applyButton, Text instructionText, Slider playbackSpeedSlider, Button undoButton,
        Button clearButton, Button loadButton, InputField loadPathField, Text errorText)
    {
        SCARA_UIController controller = Object.FindObjectOfType<SCARA_UIController>();
        if (controller == null)
        {
            Debug.LogWarning("SCARA_HMI_DashboardBuilder: no SCARA_UIController found in the scene - assign the new UI elements manually.");
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
        Debug.Log("SCARA_UIController auto-wired (waypointManager / trajectoryPlanner / replayController left untouched).");
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

    private static Button CreateButton(Transform parent, string name, string label, Color bgColor, Color textColor)
    {
        GameObject go = CreateUIObject(name, parent);
        Image img = go.AddComponent<Image>();
        img.color = bgColor;
        Button btn = go.AddComponent<Button>();
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 30;
        le.preferredWidth = 90;

        Text txt = CreateText(go.transform, "Text", label, 13, TextAnchor.MiddleCenter, textColor);
        StretchFull(txt.rectTransform);

        return btn;
    }

    private static Slider CreateSlider(Transform parent)
    {
        GameObject go = CreateUIObject("SpeedSlider", parent);
        Image trackBg = go.AddComponent<Image>();
        trackBg.color = BorderColor;
        Slider slider = go.AddComponent<Slider>();
        go.AddComponent<LayoutElement>();

        GameObject fillArea = CreateUIObject("Fill Area", go.transform);
        RectTransform fillAreaRT = fillArea.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0, 0.3f);
        fillAreaRT.anchorMax = new Vector2(1, 0.7f);
        fillAreaRT.offsetMin = new Vector2(5, 0);
        fillAreaRT.offsetMax = new Vector2(-15, 0);

        GameObject fill = CreateUIObject("Fill", fillArea.transform);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = AmberFill;
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
        handleImg.color = TextPrimary;
        RectTransform handleRT = handle.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(14, 0);

        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 1;
        slider.maxValue = 100;
        slider.value = 50;

        return slider;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}