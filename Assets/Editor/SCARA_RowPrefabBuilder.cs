using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds Assets/Prefabs/SCARA_WaypointRow.prefab: a single horizontal
/// table row (dark theme) wired to the EXACT public fields already
/// declared on SCARA_WaypointRowUI - no invented fields.
///
/// Columns: # | Speed/Time | Angles | X | Z
/// The whole row background is the select button (rowUI.selectButton).
///
/// Usage: Tools -> SCARA HMI -> Build Waypoint Row Prefab
/// Run this BEFORE "Build Full UI Layout".
/// </summary>
public static class SCARA_RowPrefabBuilder
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string PrefabPath = "Assets/Prefabs/SCARA_WaypointRow.prefab";

    private static readonly Color RowBg = new Color(0.16f, 0.16f, 0.19f);
    private static readonly Color RowBgHover = new Color(0.22f, 0.22f, 0.26f);
    private static readonly Color TextWhite = new Color(0.9f, 0.9f, 0.9f);
    private static readonly Color AccentYellow = new Color(0.95f, 0.75f, 0.15f);

    [MenuItem("Tools/SCARA HMI/Build Waypoint Row Prefab")]
    public static void BuildRowPrefab()
    {
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        // --- Root: background + button + layout + the row's data component ---
        GameObject root = new GameObject("SCARA_WaypointRow", typeof(RectTransform));
        RectTransform rootRT = root.GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(0, 34); // matches SCARA_UIController.rowHeight default

        Image bg = root.AddComponent<Image>();
        bg.color = RowBg;

        Button selectButton = root.AddComponent<Button>();
        ColorBlock colors = selectButton.colors;
        colors.normalColor = RowBg;
        colors.highlightedColor = RowBgHover;
        colors.pressedColor = AccentYellow;
        colors.selectedColor = RowBgHover;
        selectButton.colors = colors;

        HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        SCARA_WaypointRowUI rowUI = root.AddComponent<SCARA_WaypointRowUI>();

        // --- Columns ---
        Text indexText = CreateColumnText(root.transform, "IndexText", "#0", 45, AccentYellow, font, TextAnchor.MiddleCenter);
        Text speedTimeText = CreateColumnText(root.transform, "SpeedTimeText", "Speed: 0%  Time: 0.00s", 190, TextWhite, font, TextAnchor.MiddleLeft);
        Text angleText = CreateColumnText(root.transform, "AngleText", "\u03B81: 0.0\u00B0  \u03B82: 0.0\u00B0", 170, TextWhite, font, TextAnchor.MiddleLeft);
        InputField xInput = CreateColumnInputField(root.transform, "XInput", 80, font);
        InputField zInput = CreateColumnInputField(root.transform, "ZInput", 80, font);

        // --- Wire the EXACT fields declared in SCARA_WaypointRowUI.cs ---
        rowUI.indexText = indexText;
        rowUI.angleText = angleText;
        rowUI.speedTimeText = speedTimeText;
        rowUI.xInput = xInput;
        rowUI.zInput = zInput;
        rowUI.selectButton = selectButton;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("SCARA waypoint row prefab saved at: " + PrefabPath);
    }

    private static Text CreateColumnText(Transform parent, string name, string sample, float width, Color color, Font font, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Text txt = go.AddComponent<Text>();
        txt.font = font;
        txt.text = sample;
        txt.fontSize = 13;
        txt.color = color;
        txt.alignment = anchor;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.flexibleWidth = 0;

        return txt;
    }

    private static InputField CreateColumnInputField(Transform parent, string name, float width, Font font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.07f);

        InputField input = go.AddComponent<InputField>();

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
        txt.font = font;
        txt.fontSize = 13;
        txt.color = TextWhite;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.supportRichText = false;

        input.textComponent = txt;
        input.contentType = InputField.ContentType.DecimalNumber;

        return input;
    }
}