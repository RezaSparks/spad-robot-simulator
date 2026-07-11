using UnityEngine;

public class ProjectDiagnostics : MonoBehaviour
{
    [Header("Runtime Info")]
    [SerializeField] private string unityVersion;
    [SerializeField] private string platform;
    [SerializeField] private string persistentDataPath;
    [SerializeField] private string streamingAssetsPath;

    [Header("Performance Monitor")]
    [SerializeField] private bool showFps = true;
    [SerializeField] private float fpsUpdateInterval = 0.5f;
    [SerializeField] private float currentFps;
    [SerializeField] private float deltaTimeMs;

    [Header("Safety Limits")]
    [SerializeField] private int targetFrameRate = 60;
    [SerializeField] private bool disableVSync = true;

    private float fpsTimer;
    private int frameCount;

    private void Awake()
    {
        unityVersion = Application.unityVersion;
        platform = Application.platform.ToString();
        persistentDataPath = Application.persistentDataPath;
        streamingAssetsPath = Application.streamingAssetsPath;

        if (disableVSync)
        {
            QualitySettings.vSyncCount = 0;
        }

        Application.targetFrameRate = targetFrameRate;

        Debug.Log("Project Diagnostics Started");
        Debug.Log("Unity Version: " + unityVersion);
        Debug.Log("Platform: " + platform);
        Debug.Log("Persistent Data Path: " + persistentDataPath);
        Debug.Log("Streaming Assets Path: " + streamingAssetsPath);
    }

    private void Update()
    {
        if (!showFps)
        {
            return;
        }

        frameCount++;
        fpsTimer += Time.unscaledDeltaTime;

        if (fpsTimer >= fpsUpdateInterval)
        {
            currentFps = frameCount / fpsTimer;
            deltaTimeMs = Time.unscaledDeltaTime * 1000.0f;

            frameCount = 0;
            fpsTimer = 0.0f;
        }
    }

    private void OnGUI()
    {
        if (!showFps)
        {
            return;
        }

        GUI.Label(new Rect(10, 10, 250, 25), "FPS: " + currentFps.ToString("F1"));
        GUI.Label(new Rect(10, 30, 250, 25), "Frame ms: " + deltaTimeMs.ToString("F2"));
        GUI.Label(new Rect(10, 50, 500, 25), "Unity: " + unityVersion);
    }
}
