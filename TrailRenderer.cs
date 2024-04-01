// TrailRenderer.cs
using System;
using UnityEngine;

public class TrailRenderer : TrailRendererBase
{
    [SerializeField] private AudioClip trailSound;
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 1.2f;
    [SerializeField] private int fftSize = 256;
    [SerializeField] private float noiseThreshold = 0.1f;
    [SerializeField] private float trailDurationMultiplier = 1f;
    [SerializeField] private float trailWidthMultiplier = 1f;
    [SerializeField] private float fftSizeMultiplier = 1f;

    private AudioSource audioSource;
    private float[] fftSpectrum;

    protected override void Start()
    {
        try
        {
            base.Start();
            SetupAudio();
            SetupFFTSpectrum();
        }
        catch (Exception e)
        {
            LogError($"Error setting up trail renderer: {e.Message}");
            enabled = false;
        }
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = trailSound;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    private void SetupFFTSpectrum()
    {
        fftSpectrum = new float[fftSize];
    }

    protected override void UpdateTrail(LineRenderer lineRenderer, string boneName)
    {
        try
        {
            base.UpdateTrail(lineRenderer, boneName);

            if (lineRenderer == null) return;

            Vector3 bonePosition = GetBonePosition(boneName);
            int maxPoints = CalculateMaxTrailPoints();
            UpdateLineRendererPositions(lineRenderer, bonePosition, maxPoints);
        }
        catch (Exception e)
        {
            LogError($"Error updating trail for bone '{boneName}': {e.Message}");
        }
    }

    protected override void UpdateTrailParameters()
    {
        float trailWidth = BayesianOptimization(minTrailWidth, maxTrailWidth, Time.time * 0.1f) * trailWidthMultiplier;
        fftSize = Mathf.RoundToInt(fftSize * fftSizeMultiplier);

        foreach (var lineRenderer in lineRenderers)
        {
            SetLineRendererWidth(lineRenderer, trailWidth);
            SetLineRendererColorGradient(lineRenderer);
        }
    }

    private Vector3 GetBonePosition(string boneName)
    {
        var boneTransform = animator.GetBoneTransform(HumanBodyBones.Hips).Find(boneName);
        if (boneTransform == null)
        {
            throw new InvalidOperationException($"Bone '{boneName}' not found.");
        }
        return boneTransform.position;
    }

    private int CalculateMaxTrailPoints()
    {
        float trailDuration = BayesianOptimization(minTrailDuration, maxTrailDuration, Time.time * 0.1f) * trailDurationMultiplier;
        return Mathf.CeilToInt(trailDuration * 60f);
    }

    private void UpdateLineRendererPositions(LineRenderer lineRenderer, Vector3 bonePosition, int maxPoints)
    {
        lineRenderer.positionCount = Mathf.Min(lineRenderer.positionCount + 1, maxPoints);
        lineRenderer.SetPosition(lineRenderer.positionCount - 1, bonePosition);

        if (lineRenderer.positionCount >= maxPoints)
        {
            for (int i = 0; i < lineRenderer.positionCount - 1; i++)
            {
                lineRenderer.SetPosition(i, lineRenderer.GetPosition(i + 1));
            }
            lineRenderer.positionCount--;
        }
    }

    private void SetLineRendererWidth(LineRenderer lineRenderer, float width)
    {
        if (lineRenderer != null)
        {
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
        }
    }

    private void SetLineRendererColorGradient(LineRenderer lineRenderer)
    {
        if (lineRenderer != null)
        {
            lineRenderer.colorGradient = GetCurrentColorGradient();
        }
    }

    private Gradient GetCurrentColorGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(defaultTrailColorGradient.colorKeys, defaultTrailColorGradient.alphaKeys);

        float hue = (GetHueOffset() + UnityEngine.Random.Range(-GetColorVariation(), GetColorVariation())) % 1f;
        Color color = Color.HSVToRGB(hue, 1f, 1f);

        GradientColorKey[] colorKeys =
        {
            new GradientColorKey(color, 0f),
            new GradientColorKey(color, 1f)
        };
        gradient.SetKeys(colorKeys, gradient.alphaKeys);

        return gradient;
    }

    private void UpdateTrailSound()
    {
        try
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }

            float velocity = CalculateVelocity();
            float normalizedVelocity = NormalizeVelocity(velocity);
            float pitch = BayesianOptimization(minPitch, maxPitch, normalizedVelocity);
            audioSource.pitch = pitch;
        }
        catch (Exception e)
        {
            LogError($"Error updating trail sound: {e.Message}");
        }
    }

    private float CalculateVelocity()
    {
        Vector3 hipPosition = GetBonePosition("Hips");
        Vector3 chestPosition = GetBonePosition("Chest");
        return (chestPosition - hipPosition).magnitude;
    }

    private float NormalizeVelocity(float velocity)
    {
        return Mathf.Clamp01(velocity / 5f);
    }

    private void ProcessAudioSpectrum()
    {
        audioSource.GetSpectrumData(fftSpectrum, 0, FFTWindow.Blackman);
        float normalizedSpectrum = CalculateNormalizedSpectrum();

        if (normalizedSpectrum > noiseThreshold)
        {
            float hue = Mathf.Lerp(0f, 1f, normalizedSpectrum);
            SetHueOffset(hue);

            float colorVariation = BayesianOptimization(0f, 1f, normalizedSpectrum);
            SetColorVariation(colorVariation);
        }
    }

    private float CalculateNormalizedSpectrum()
    {
        float spectrumSum = 0f;
        foreach (float sample in fftSpectrum)
        {
            spectrumSum += sample;
        }
        return spectrumSum / fftSpectrum.Length;
    }

    protected virtual void UpdateExtraEffects()
    {
        UpdateTrailSound();
        ProcessAudioSpectrum();
    }

    protected override void Update()
    {
        base.Update();
        UpdateExtraEffects();
    }

    private float BayesianOptimization(float min, float max, float t)
    {
        return Mathf.Lerp(min, max, Mathf.PerlinNoise(t, 0f));
    }

    private void SetHueOffset(float value)
    {
        SetAvatarParameterValue("TrailRendererHueOffset", Mathf.Clamp01(value));
    }

    private float GetHueOffset()
    {
        return GetAvatarParameterValue("TrailRendererHueOffset");
    }

    private void SetColorVariation(float value)
    {
        SetAvatarParameterValue("TrailRendererColorVariation", Mathf.Clamp01(value));
    }

    private float GetColorVariation()
    {
        return GetAvatarParameterValue("TrailRendererColorVariation");
    }

    private void SetAvatarParameterValue(string parameterName, float value)
    {
        if (expressionParameters != null)
        {
            var parameter = expressionParameters.FindParameter(parameterName);
            if (parameter != null)
            {
                parameter.valueFloat = value;
            }
        }
    }

    private float GetAvatarParameterValue(string parameterName)
    {
        if (expressionParameters != null)
        {
            var parameter = expressionParameters.FindParameter(parameterName);
            if (parameter != null)
            {
                return parameter.valueFloat;
            }
        }
        return 0f;
    }

    protected override void LogError(string message)
    {
        Debug.LogError($"[TrailRenderer] {message}");
    }
}