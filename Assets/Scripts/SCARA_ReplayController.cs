using UnityEngine;

/// <summary>
/// REPLAY LAYER. Holds an in-memory TrajectorySample[] and only ever
/// performs: binary search -> LerpAngle -> apply rotation. Never solves
/// IK, never allocates per frame.
/// </summary>
public class SCARA_ReplayController : MonoBehaviour
{
    public Transform joint1;
    public Transform joint2;

    [Header("Manual Speed Range (Generate/preview path only)")]
    [Tooltip("Playback multiplier applied when the slider is at 0%.")]
    public float minManualMultiplier = 0.25f;
    [Tooltip("Playback multiplier applied when the slider is at 100%.")]
    public float maxManualMultiplier = 3f;

    private TrajectorySample[] samples;
    private float playbackElapsedTime;
    private float speedMultiplier = 1f; // read ONCE at playback start, per spec
    private bool isPlaying;
    private float totalDuration;

    /// <param name="useManualSpeedOverride">
    /// True (Generate path): slider maps to [minManualMultiplier, maxManualMultiplier],
    /// letting the user slow down OR speed up preview playback, and never
    /// freezes at 0%. False (CSV Load path): slider is ignored entirely and
    /// playback always runs at exactly 1x — the CSV's own Time column is
    /// authoritative, so external data always plays back correctly regardless
    /// of slider position.
    /// </param>
    public void LoadTrajectory(TrajectorySample[] data, float speedPercent0to100, bool useManualSpeedOverride = true)
    {
        samples = data;
        totalDuration = samples.Length > 0 ? samples[samples.Length - 1].time : 0f;

        if (useManualSpeedOverride)
        {
            float t = Mathf.Clamp01(speedPercent0to100 / 100f);
            speedMultiplier = Mathf.Lerp(minManualMultiplier, maxManualMultiplier, t);
        }
        else
        {
            speedMultiplier = 1f;
        }

        playbackElapsedTime = 0f;
        isPlaying = samples.Length >= 2;
    }

    public void Pause() { isPlaying = false; }
    public void Resume() { if (samples != null && samples.Length >= 2) isPlaying = true; }
    public void ResetToStart() { playbackElapsedTime = 0f; }

    void Update()
    {
        if (!isPlaying || samples == null || samples.Length < 2) return;

        playbackElapsedTime += Time.deltaTime * speedMultiplier;

        if (playbackElapsedTime >= totalDuration)
        {
            playbackElapsedTime = totalDuration;
            Apply(playbackElapsedTime);
            isPlaying = false;
            return;
        }

        Apply(playbackElapsedTime);
    }

    void Apply(float t)
    {
        int lo = BinarySearchFloor(t);
        int hi = Mathf.Min(lo + 1, samples.Length - 1);

        float segT = 0f;
        float loT = samples[lo].time, hiT = samples[hi].time;
        if (hi != lo && hiT > loT)
            segT = Mathf.Clamp01((t - loT) / (hiT - loT));

        float theta1 = Mathf.LerpAngle(samples[lo].theta1, samples[hi].theta1, segT);
        float theta2 = Mathf.LerpAngle(samples[lo].theta2, samples[hi].theta2, segT);

        joint1.localRotation = Quaternion.Euler(0f, theta1, 0f);
        joint2.localRotation = Quaternion.Euler(0f, theta2, 0f);
    }

    int BinarySearchFloor(float t)
    {
        int low = 0, high = samples.Length - 1;
        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (samples[mid].time <= t) low = mid;
            else high = mid - 1;
        }
        return Mathf.Clamp(low, 0, samples.Length - 1);
    }
}
