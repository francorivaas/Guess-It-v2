using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla un campo de partículas de fondo que aumenta su densidad,
/// velocidad y color según la racha del jugador.
/// UIManager llama a SetStreak cada vez que la racha cambia.
/// </summary>
public sealed class FeverBackgroundController : MonoBehaviour
{
    [Serializable]
    private sealed class FeverStage
    {
        public string label = "Calm";
        [Min(0)] public int minStreak;

        [Header("Density")]
        [Min(0f)] public float emissionRate = 2f;
        [Min(1)] public int maxParticles = 24;
        [Min(0)] public int enterBurst;

        [Header("Movement")]
        [Min(0f)] public float horizontalDrift = 0.12f;
        public float verticalSpeedMin = 0.08f;
        public float verticalSpeedMax = 0.22f;
        [Min(0f)] public float rotationSpeed = 12f;
        [Min(0f)] public float noiseStrength = 0.05f;
        [Min(0.01f)] public float simulationSpeed = 1f;

        [Header("Lifetime and size")]
        [Min(0.05f)] public float lifetimeMin = 3.5f;
        [Min(0.05f)] public float lifetimeMax = 5.5f;
        [Min(0.001f)] public float sizeMin = 0.08f;
        [Min(0.001f)] public float sizeMax = 0.16f;

        [Header("Colors")]
        public Color particleStartColor =
            new Color(0.35f, 0.72f, 1f, 0.35f);

        public Color particleEndColor =
            new Color(0.75f, 0.38f, 1f, 0f);

        public Color backgroundTintColor =
            new Color(0.18f, 0.38f, 1f, 1f);

        [Range(0f, 0.2f)] public float backgroundTintAlpha;
    }

    private struct RuntimeState
    {
        public float emissionRate;
        public float maxParticles;
        public float enterBurst;
        public float horizontalDrift;
        public float verticalSpeedMin;
        public float verticalSpeedMax;
        public float rotationSpeed;
        public float noiseStrength;
        public float simulationSpeed;
        public float lifetimeMin;
        public float lifetimeMax;
        public float sizeMin;
        public float sizeMax;
        public Color particleStartColor;
        public Color particleEndColor;
        public Color tintColor;
        public float tintAlpha;
    }

    [Header("References")]
    [SerializeField] private ParticleSystem primaryParticles;
    [SerializeField] private ParticleSystem accentParticles;
    [SerializeField] private Graphic backgroundTint;
    [SerializeField] private Camera targetCamera;

    [Header("Camera fitting")]
    [SerializeField] private bool fitEmitterToCamera = true;
    [SerializeField, Min(0.01f)] private float distanceFromCamera = 5f;
    [SerializeField, Min(0f)] private float emitterPadding = 0.5f;

    [Header("Transitions")]
    [SerializeField, Min(0.01f)] private float transitionDuration = 0.45f;
    [SerializeField] private bool burstOnStreakIncrease = true;
    [SerializeField] private bool clearParticlesOnStreakBreak;

    [Header("Accent multipliers")]
    [SerializeField, Min(0f)] private float accentEmissionMultiplier = 0.45f;
    [SerializeField, Min(0f)] private float accentMovementMultiplier = 0.8f;
    [SerializeField, Min(0f)] private float accentSizeMultiplier = 1.35f;
    [SerializeField, Min(0f)] private float accentMaxParticlesMultiplier = 0.5f;
    [SerializeField, Min(0f)] private float accentBurstMultiplier = 0.5f;

    [Header("Fever stages")]
    [Tooltip("La intensidad se interpola entre etapas, así que cada punto de racha produce progreso visual.")]
    [SerializeField] private List<FeverStage> stages = new List<FeverStage>();

    private Coroutine transitionRoutine;
    private RuntimeState currentState;
    private bool initialized;
    private int currentStreak;
    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Reset()
    {
        primaryParticles = GetComponent<ParticleSystem>();
        stages = CreateDefaultStages();
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        PlayIfNeeded(primaryParticles);
        PlayIfNeeded(accentParticles);

        if (fitEmitterToCamera)
        {
            FitEmittersToCamera();
        }
    }

    private void LateUpdate()
    {
        if (
            fitEmitterToCamera &&
            (Screen.width != lastScreenWidth ||
             Screen.height != lastScreenHeight)
        )
        {
            FitEmittersToCamera();
        }
    }

    private void OnDisable()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
    }

    /// <summary>
    /// Actualiza la intensidad del fondo según la racha.
    /// </summary>
    public void SetStreak(int streak, bool immediate = false)
    {
        EnsureInitialized();

        streak = Mathf.Max(0, streak);
        RuntimeState targetState = BuildStateForStreak(streak);

        bool streakIncreased = streak > currentStreak;
        bool streakWasBroken = streak == 0 && currentStreak > 0;

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (streakWasBroken && clearParticlesOnStreakBreak)
        {
            ClearParticles();
        }

        if (immediate || !isActiveAndEnabled)
        {
            currentState = targetState;
            ApplyState(currentState);
        }
        else
        {
            transitionRoutine = StartCoroutine(
                TransitionToState(targetState)
            );
        }

        if (streakIncreased && burstOnStreakIncrease)
        {
            EmitBurst(Mathf.RoundToInt(targetState.enterBurst));
        }

        currentStreak = streak;
    }

    [ContextMenu("Restore Default Stages")]
    private void RestoreDefaultStages()
    {
        stages = CreateDefaultStages();
        initialized = false;
        EnsureInitialized();
        SetStreak(currentStreak, true);
    }

    [ContextMenu("Preview Calm")]
    private void PreviewCalm()
    {
        SetStreak(0, true);
    }

    [ContextMenu("Preview Maximum Fever")]
    private void PreviewMaximumFever()
    {
        int streak = stages.Count > 0
            ? stages[stages.Count - 1].minStreak
            : 10;

        SetStreak(streak, true);
        EmitBurst(24);
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        if (primaryParticles == null)
        {
            primaryParticles = GetComponent<ParticleSystem>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (stages == null || stages.Count == 0)
        {
            stages = CreateDefaultStages();
        }

        PrepareParticleSystem(primaryParticles);
        PrepareParticleSystem(accentParticles);

        if (backgroundTint != null)
        {
            backgroundTint.raycastTarget = false;
        }

        currentState = BuildStateForStreak(0);
        ApplyState(currentState);
        currentStreak = 0;
        initialized = true;
    }

    private static void PrepareParticleSystem(ParticleSystem particles)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        ParticleSystem.VelocityOverLifetimeModule velocity =
            particles.velocityOverLifetime;

        // Configuramos los tres ejes con el mismo modo antes de habilitar
        // el módulo. Unity no permite mezclar Constant y TwoConstants.
        velocity.enabled = false;
        velocity.space = ParticleSystemSimulationSpace.Local;

        ParticleSystem.MinMaxCurve zeroVelocityCurve =
            new ParticleSystem.MinMaxCurve(0f, 0f);

        velocity.x = zeroVelocityCurve;
        velocity.y = zeroVelocityCurve;
        velocity.z = zeroVelocityCurve;
        velocity.enabled = true;

        ParticleSystem.ColorOverLifetimeModule colors =
            particles.colorOverLifetime;
        colors.enabled = true;

        ParticleSystem.RotationOverLifetimeModule rotation =
            particles.rotationOverLifetime;
        rotation.enabled = true;
    }

    private static void PlayIfNeeded(ParticleSystem particles)
    {
        if (particles != null && !particles.isPlaying)
        {
            particles.Play(true);
        }
    }

    private IEnumerator TransitionToState(RuntimeState targetState)
    {
        RuntimeState startState = currentState;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(
                elapsed / transitionDuration
            );

            currentState = LerpState(
                startState,
                targetState,
                Mathf.SmoothStep(0f, 1f, progress)
            );

            ApplyState(currentState);
            yield return null;
        }

        currentState = targetState;
        ApplyState(currentState);
        transitionRoutine = null;
    }

    private void ApplyState(RuntimeState state)
    {
        ApplyStateToParticles(
            primaryParticles,
            state,
            1f,
            1f,
            1f,
            1f
        );

        ApplyStateToParticles(
            accentParticles,
            state,
            accentEmissionMultiplier,
            accentMovementMultiplier,
            accentSizeMultiplier,
            accentMaxParticlesMultiplier
        );

        if (backgroundTint != null)
        {
            Color tint = state.tintColor;
            tint.a = state.tintAlpha;
            backgroundTint.color = tint;
        }

        PlayIfNeeded(primaryParticles);
        PlayIfNeeded(accentParticles);
    }

    private static void ApplyStateToParticles(
        ParticleSystem particles,
        RuntimeState state,
        float emissionMultiplier,
        float movementMultiplier,
        float sizeMultiplier,
        float maxParticlesMultiplier
    )
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = particles.main;
        main.maxParticles = Mathf.Max(
            1,
            Mathf.RoundToInt(
                state.maxParticles * maxParticlesMultiplier
            )
        );

        main.startSpeed = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.05f, state.lifetimeMin),
            Mathf.Max(state.lifetimeMin, state.lifetimeMax)
        );

        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.001f, state.sizeMin * sizeMultiplier),
            Mathf.Max(state.sizeMin, state.sizeMax) * sizeMultiplier
        );

        main.startColor = Color.white;
        main.simulationSpeed = Mathf.Max(
            0.01f,
            state.simulationSpeed
        );

        ParticleSystem.EmissionModule emission = particles.emission;
        float rate = Mathf.Max(
            0f,
            state.emissionRate * emissionMultiplier
        );

        emission.enabled = rate > 0.001f;
        emission.rateOverTime = rate;

        ParticleSystem.VelocityOverLifetimeModule velocity =
            particles.velocityOverLifetime;

        // Unity exige que las curvas X, Y y Z utilicen el mismo modo.
        // Deshabilitamos temporalmente el módulo para actualizar los tres
        // ejes como TwoConstants y lo habilitamos recién al terminar.
        velocity.enabled = false;
        velocity.space = ParticleSystemSimulationSpace.Local;

        ParticleSystem.MinMaxCurve horizontalVelocity =
            new ParticleSystem.MinMaxCurve(
                -state.horizontalDrift * movementMultiplier,
                state.horizontalDrift * movementMultiplier
            );

        ParticleSystem.MinMaxCurve verticalVelocity =
            new ParticleSystem.MinMaxCurve(
                state.verticalSpeedMin * movementMultiplier,
                state.verticalSpeedMax * movementMultiplier
            );

        ParticleSystem.MinMaxCurve depthVelocity =
            new ParticleSystem.MinMaxCurve(0f, 0f);

        velocity.x = horizontalVelocity;
        velocity.y = verticalVelocity;
        velocity.z = depthVelocity;
        velocity.enabled = true;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = state.noiseStrength > 0.001f;
        noise.strength = state.noiseStrength * movementMultiplier;

        ParticleSystem.RotationOverLifetimeModule rotation =
            particles.rotationOverLifetime;

        rotation.enabled = state.rotationSpeed > 0.001f;
        rotation.z = new ParticleSystem.MinMaxCurve(
            -state.rotationSpeed,
            state.rotationSpeed
        );

        ParticleSystem.ColorOverLifetimeModule colors =
            particles.colorOverLifetime;

        colors.enabled = true;
        colors.color = new ParticleSystem.MinMaxGradient(
            BuildParticleGradient(
                state.particleStartColor,
                state.particleEndColor
            )
        );
    }

    private void EmitBurst(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        primaryParticles?.Emit(amount);

        if (accentParticles != null)
        {
            accentParticles.Emit(
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        amount * accentBurstMultiplier
                    )
                )
            );
        }
    }

    private void ClearParticles()
    {
        ClearParticleSystem(primaryParticles);
        ClearParticleSystem(accentParticles);
    }

    private static void ClearParticleSystem(ParticleSystem particles)
    {
        if (particles == null)
        {
            return;
        }

        particles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        particles.Play(true);
    }

    private RuntimeState BuildStateForStreak(int streak)
    {
        FeverStage lower = null;
        FeverStage upper = null;
        int lowerMin = int.MinValue;
        int upperMin = int.MaxValue;

        foreach (FeverStage stage in stages)
        {
            if (stage == null)
            {
                continue;
            }

            if (
                stage.minStreak <= streak &&
                stage.minStreak >= lowerMin
            )
            {
                lower = stage;
                lowerMin = stage.minStreak;
            }

            if (
                stage.minStreak > streak &&
                stage.minStreak < upperMin
            )
            {
                upper = stage;
                upperMin = stage.minStreak;
            }
        }

        if (lower == null)
        {
            lower = stages[0];
        }

        if (upper == null)
        {
            upper = lower;
        }

        RuntimeState from = CreateState(lower);
        RuntimeState to = CreateState(upper);

        if (lower == upper)
        {
            return from;
        }

        float blend = Mathf.InverseLerp(
            lower.minStreak,
            upper.minStreak,
            streak
        );

        return LerpState(from, to, blend);
    }

    private static RuntimeState CreateState(FeverStage stage)
    {
        return new RuntimeState
        {
            emissionRate = stage.emissionRate,
            maxParticles = stage.maxParticles,
            enterBurst = stage.enterBurst,
            horizontalDrift = stage.horizontalDrift,
            verticalSpeedMin = stage.verticalSpeedMin,
            verticalSpeedMax = stage.verticalSpeedMax,
            rotationSpeed = stage.rotationSpeed,
            noiseStrength = stage.noiseStrength,
            simulationSpeed = stage.simulationSpeed,
            lifetimeMin = stage.lifetimeMin,
            lifetimeMax = stage.lifetimeMax,
            sizeMin = stage.sizeMin,
            sizeMax = stage.sizeMax,
            particleStartColor = stage.particleStartColor,
            particleEndColor = stage.particleEndColor,
            tintColor = stage.backgroundTintColor,
            tintAlpha = stage.backgroundTintAlpha
        };
    }

    private static RuntimeState LerpState(
        RuntimeState from,
        RuntimeState to,
        float t
    )
    {
        return new RuntimeState
        {
            emissionRate = Mathf.Lerp(from.emissionRate, to.emissionRate, t),
            maxParticles = Mathf.Lerp(from.maxParticles, to.maxParticles, t),
            enterBurst = Mathf.Lerp(from.enterBurst, to.enterBurst, t),
            horizontalDrift = Mathf.Lerp(from.horizontalDrift, to.horizontalDrift, t),
            verticalSpeedMin = Mathf.Lerp(from.verticalSpeedMin, to.verticalSpeedMin, t),
            verticalSpeedMax = Mathf.Lerp(from.verticalSpeedMax, to.verticalSpeedMax, t),
            rotationSpeed = Mathf.Lerp(from.rotationSpeed, to.rotationSpeed, t),
            noiseStrength = Mathf.Lerp(from.noiseStrength, to.noiseStrength, t),
            simulationSpeed = Mathf.Lerp(from.simulationSpeed, to.simulationSpeed, t),
            lifetimeMin = Mathf.Lerp(from.lifetimeMin, to.lifetimeMin, t),
            lifetimeMax = Mathf.Lerp(from.lifetimeMax, to.lifetimeMax, t),
            sizeMin = Mathf.Lerp(from.sizeMin, to.sizeMin, t),
            sizeMax = Mathf.Lerp(from.sizeMax, to.sizeMax, t),
            particleStartColor = Color.Lerp(from.particleStartColor, to.particleStartColor, t),
            particleEndColor = Color.Lerp(from.particleEndColor, to.particleEndColor, t),
            tintColor = Color.Lerp(from.tintColor, to.tintColor, t),
            tintAlpha = Mathf.Lerp(from.tintAlpha, to.tintAlpha, t)
        };
    }

    private static Gradient BuildParticleGradient(
        Color startColor,
        Color endColor
    )
    {
        Gradient gradient = new Gradient();
        Color middle = Color.Lerp(startColor, endColor, 0.45f);

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(middle, 0.55f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(startColor.a, 0.16f),
                new GradientAlphaKey(endColor.a, 0.78f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        return gradient;
    }

    private void FitEmittersToCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        float fullHeight;
        float fullWidth;

        if (targetCamera.orthographic)
        {
            fullHeight = targetCamera.orthographicSize * 2f;
            fullWidth = fullHeight * targetCamera.aspect;
        }
        else
        {
            fullHeight =
                2f *
                distanceFromCamera *
                Mathf.Tan(
                    targetCamera.fieldOfView *
                    0.5f *
                    Mathf.Deg2Rad
                );

            fullWidth = fullHeight * targetCamera.aspect;
        }

        Vector3 center = targetCamera.ViewportToWorldPoint(
            new Vector3(
                0.5f,
                0.5f,
                distanceFromCamera
            )
        );

        FitParticleSystem(
            primaryParticles,
            center,
            fullWidth,
            fullHeight
        );

        FitParticleSystem(
            accentParticles,
            center,
            fullWidth,
            fullHeight
        );

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    private void FitParticleSystem(
        ParticleSystem particles,
        Vector3 center,
        float fullWidth,
        float fullHeight
    )
    {
        if (particles == null)
        {
            return;
        }

        particles.transform.position = center;
        particles.transform.rotation = targetCamera.transform.rotation;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(
            fullWidth + emitterPadding * 2f,
            fullHeight + emitterPadding * 2f,
            0.1f
        );
    }

    private static List<FeverStage> CreateDefaultStages()
    {
        return new List<FeverStage>
        {
            new FeverStage
            {
                label = "Calm",
                minStreak = 0,
                emissionRate = 1.5f,
                maxParticles = 20,
                enterBurst = 0,
                horizontalDrift = 0.08f,
                verticalSpeedMin = 0.04f,
                verticalSpeedMax = 0.14f,
                rotationSpeed = 8f,
                noiseStrength = 0.02f,
                simulationSpeed = 0.75f,
                lifetimeMin = 4.5f,
                lifetimeMax = 6.5f,
                sizeMin = 0.06f,
                sizeMax = 0.12f,
                particleStartColor = new Color(0.32f, 0.67f, 1f, 0.22f),
                particleEndColor = new Color(0.54f, 0.35f, 1f, 0f),
                backgroundTintColor = new Color(0.2f, 0.38f, 1f, 1f),
                backgroundTintAlpha = 0f
            },
            new FeverStage
            {
                label = "Warm",
                minStreak = 2,
                emissionRate = 4f,
                maxParticles = 36,
                enterBurst = 5,
                horizontalDrift = 0.14f,
                verticalSpeedMin = 0.08f,
                verticalSpeedMax = 0.24f,
                rotationSpeed = 18f,
                noiseStrength = 0.08f,
                simulationSpeed = 0.9f,
                lifetimeMin = 3.8f,
                lifetimeMax = 5.8f,
                sizeMin = 0.07f,
                sizeMax = 0.15f,
                particleStartColor = new Color(0.18f, 0.9f, 1f, 0.42f),
                particleEndColor = new Color(0.7f, 0.32f, 1f, 0f),
                backgroundTintColor = new Color(0.18f, 0.56f, 1f, 1f),
                backgroundTintAlpha = 0.018f
            },
            new FeverStage
            {
                label = "Hot",
                minStreak = 4,
                emissionRate = 8f,
                maxParticles = 58,
                enterBurst = 10,
                horizontalDrift = 0.22f,
                verticalSpeedMin = 0.13f,
                verticalSpeedMax = 0.36f,
                rotationSpeed = 35f,
                noiseStrength = 0.18f,
                simulationSpeed = 1.05f,
                lifetimeMin = 3.2f,
                lifetimeMax = 5f,
                sizeMin = 0.08f,
                sizeMax = 0.19f,
                particleStartColor = new Color(1f, 0.22f, 0.78f, 0.5f),
                particleEndColor = new Color(0.2f, 0.8f, 1f, 0f),
                backgroundTintColor = new Color(0.72f, 0.18f, 1f, 1f),
                backgroundTintAlpha = 0.032f
            },
            new FeverStage
            {
                label = "Fever",
                minStreak = 6,
                emissionRate = 14f,
                maxParticles = 86,
                enterBurst = 17,
                horizontalDrift = 0.34f,
                verticalSpeedMin = 0.2f,
                verticalSpeedMax = 0.54f,
                rotationSpeed = 60f,
                noiseStrength = 0.32f,
                simulationSpeed = 1.2f,
                lifetimeMin = 2.8f,
                lifetimeMax = 4.4f,
                sizeMin = 0.1f,
                sizeMax = 0.24f,
                particleStartColor = new Color(1f, 0.78f, 0.12f, 0.62f),
                particleEndColor = new Color(1f, 0.16f, 0.62f, 0f),
                backgroundTintColor = new Color(1f, 0.14f, 0.62f, 1f),
                backgroundTintAlpha = 0.048f
            },
            new FeverStage
            {
                label = "Maximum Fever",
                minStreak = 10,
                emissionRate = 23f,
                maxParticles = 124,
                enterBurst = 28,
                horizontalDrift = 0.5f,
                verticalSpeedMin = 0.32f,
                verticalSpeedMax = 0.82f,
                rotationSpeed = 100f,
                noiseStrength = 0.5f,
                simulationSpeed = 1.4f,
                lifetimeMin = 2.2f,
                lifetimeMax = 3.8f,
                sizeMin = 0.12f,
                sizeMax = 0.3f,
                particleStartColor = new Color(1f, 0.92f, 0.18f, 0.72f),
                particleEndColor = new Color(0.2f, 0.95f, 1f, 0f),
                backgroundTintColor = new Color(1f, 0.28f, 0.58f, 1f),
                backgroundTintAlpha = 0.065f
            }
        };
    }
}
