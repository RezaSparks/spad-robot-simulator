using UnityEngine;

public class SCARA_ReplayController : MonoBehaviour
{
    public Transform joint1;
    public Transform joint2;

    [Header("Manual Speed Range")]
    public float minManualMultiplier = 0.25f;
    public float maxManualMultiplier = 3f;

    private TrajectorySample[] samples;
    private float playbackElapsedTime;
    private float speedMultiplier = 1f;
    private bool isPlaying;
    private float totalDuration;
    private bool hasReachedEnd = false;

    public float CurrentTime => playbackElapsedTime;
    public float TotalDuration => totalDuration;
    public bool IsPlaying => isPlaying;

    public event System.Action OnPlaybackComplete;

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
        hasReachedEnd = false;
        isPlaying = samples.Length >= 2;
        if (isPlaying)
            Apply(0f);
    }

    public void Pause() { isPlaying = false; }
    public void Resume() { if (samples != null && samples.Length >= 2 && !hasReachedEnd) isPlaying = true; }
    public void ResetToStart() { playbackElapsedTime = 0f; hasReachedEnd = false; Apply(0f); }

    public void StepForward(float stepSize = 0.01f)
    {
        if (samples == null || samples.Length < 2) return;
        playbackElapsedTime = Mathf.Min(playbackElapsedTime + stepSize, totalDuration);
        Apply(playbackElapsedTime);
        if (playbackElapsedTime >= totalDuration)
        {
            isPlaying = false;
            hasReachedEnd = true;
            OnPlaybackComplete?.Invoke();
        }
    }

    public void StepBackward(float stepSize = 0.01f)
    {
        if (samples == null || samples.Length < 2) return;
        playbackElapsedTime = Mathf.Max(playbackElapsedTime - stepSize, 0f);
        hasReachedEnd = false;
        Apply(playbackElapsedTime);
    }

    void Update()
    {
        if (!isPlaying || samples == null || samples.Length < 2 || hasReachedEnd) return;

        playbackElapsedTime += Time.deltaTime * speedMultiplier;

        if (playbackElapsedTime >= totalDuration)
        {
            playbackElapsedTime = totalDuration;
            Apply(playbackElapsedTime);
            isPlaying = false;
            hasReachedEnd = true;
            OnPlaybackComplete?.Invoke();
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

        SCARA_IKSolver.ClampAngles(ref theta1, ref theta2);

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