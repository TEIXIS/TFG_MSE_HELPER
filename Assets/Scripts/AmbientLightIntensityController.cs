using System;
using System.Collections;
using UnityEngine;

public class AmbientLightIntensityController : MonoBehaviour
{
    [Header("Niveles")]
    [Range(1, 4)] public int currentLevel = 2;
    public float[] intensityLevels = { 0.35f, 0.55f, 0.75f, 0.95f };

    [Header("Luces de la sala")]
    [Tooltip("Arrastra aqui la luz o luces ambiente de la sala. Si se deja vacio solo se modifica RenderSettings.")]
    public Light[] roomLights;
    public bool updateRenderSettings = true;
    public bool updateAmbientLightColor = true;
    public bool updateDynamicGI = false;
    public bool updateRoomLights = true;
    public bool autoFindRoomLights = true;

    [Header("Transicion")]
    [Min(0f)] public float transitionDuration = 0.6f;

    public event Action<int, int> LevelChanged;

    private Coroutine transitionCoroutine;
    private Color baseAmbientLight;
    private Color currentAmbientColor = Color.white;

    public int TotalLevels
    {
        get
        {
            if (intensityLevels == null || intensityLevels.Length == 0)
                return 1;

            return intensityLevels.Length;
        }
    }

    private void Start()
    {
        QuestCrashDiagnostics.Log("[AmbientLightIntensityController] Start " + name);
        baseAmbientLight = RenderSettings.ambientLight.maxColorComponent > 0f
            ? RenderSettings.ambientLight
            : Color.white;
        currentAmbientColor = baseAmbientLight;

        FindRoomLightsIfNeeded();
        SetLevel(currentLevel);
    }

    public void IncreaseLevel()
    {
        SetLevel(currentLevel + 1);
    }

    public void DecreaseLevel()
    {
        SetLevel(currentLevel - 1);
    }

    public void SetLevel(int level)
    {
        QuestCrashDiagnostics.Log("[AmbientLightIntensityController] SetLevel begin requested=" + level + " current=" + currentLevel);
        int totalLevels = TotalLevels;
        currentLevel = Mathf.Clamp(level, 1, totalLevels);
        float intensity = intensityLevels != null && intensityLevels.Length > 0
            ? intensityLevels[currentLevel - 1]
            : 1f;

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        if (transitionDuration <= 0f)
        {
            ApplyIntensity(intensity);
        }
        else
        {
            transitionCoroutine = StartCoroutine(TransitionIntensity(intensity));
        }

        LevelChanged?.Invoke(currentLevel, totalLevels);
        QuestCrashDiagnostics.Log("[AmbientLightIntensityController] SetLevel end current=" + currentLevel + " total=" + totalLevels);
    }

    public bool SetColorByName(string colorName)
    {
        if (!TryGetColorByName(colorName, out Color color))
            return false;

        SetAmbientColor(color);
        return true;
    }

    public void SetAmbientColor(Color color)
    {
        currentAmbientColor = color.maxColorComponent > 0f ? color : Color.white;
        SetLevel(currentLevel);
    }

    private IEnumerator TransitionIntensity(float targetIntensity)
    {
        QuestCrashDiagnostics.Log("[AmbientLightIntensityController] TransitionIntensity begin target=" + targetIntensity);
        FindRoomLightsIfNeeded();

        float startAmbientIntensity = RenderSettings.ambientIntensity;
        Color startAmbientColor = RenderSettings.ambientLight;
        Color targetAmbientColor = currentAmbientColor * targetIntensity;
        float[] startLightIntensities = null;
        Color[] startLightColors = null;

        if (updateRoomLights && roomLights != null)
        {
            startLightIntensities = new float[roomLights.Length];
            startLightColors = new Color[roomLights.Length];
            for (int i = 0; i < roomLights.Length; i++)
            {
                Light roomLight = roomLights[i];
                startLightIntensities[i] = roomLight != null ? roomLight.intensity : targetIntensity;
                startLightColors[i] = roomLight != null ? roomLight.color : currentAmbientColor;
            }
        }

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            if (updateRenderSettings)
                RenderSettings.ambientIntensity = Mathf.Lerp(startAmbientIntensity, targetIntensity, easedT);

            if (updateAmbientLightColor)
                RenderSettings.ambientLight = Color.Lerp(startAmbientColor, targetAmbientColor, easedT);

            if (updateRoomLights && roomLights != null && startLightIntensities != null)
            {
                for (int i = 0; i < roomLights.Length; i++)
                {
                    Light roomLight = roomLights[i];
                    if (roomLight != null)
                    {
                        if (startLightColors != null)
                            roomLight.color = Color.Lerp(startLightColors[i], currentAmbientColor, easedT);

                        roomLight.intensity = Mathf.Lerp(startLightIntensities[i], targetIntensity, easedT);
                    }
                }
            }

            yield return null;
        }

        ApplyIntensity(targetIntensity);
        transitionCoroutine = null;
        QuestCrashDiagnostics.Log("[AmbientLightIntensityController] TransitionIntensity end target=" + targetIntensity);
    }

    private void ApplyIntensity(float intensity)
    {
        FindRoomLightsIfNeeded();

        if (updateRenderSettings)
            RenderSettings.ambientIntensity = intensity;

        if (updateAmbientLightColor)
            RenderSettings.ambientLight = currentAmbientColor * intensity;

        if (updateDynamicGI)
            DynamicGI.UpdateEnvironment();

        if (updateRoomLights && roomLights != null)
        {
            foreach (Light roomLight in roomLights)
            {
                if (roomLight != null)
                {
                    roomLight.color = currentAmbientColor;
                    roomLight.intensity = intensity;
                }
            }
        }

        Debug.Log("[LUZ] Intensidad ambiente aplicada: " + intensity
            + " | RenderSettings.ambientIntensity=" + RenderSettings.ambientIntensity
            + " | RenderSettings.ambientLight=" + RenderSettings.ambientLight);
        QuestCrashDiagnostics.Log("[LUZ] Intensidad ambiente aplicada: " + intensity
            + " roomLights=" + (roomLights != null ? roomLights.Length : 0));
    }

    private void FindRoomLightsIfNeeded()
    {
        if (!autoFindRoomLights || !updateRoomLights || (roomLights != null && roomLights.Length > 0))
            return;

        roomLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private bool TryGetColorByName(string rawName, out Color color)
    {
        color = Color.white;
        if (string.IsNullOrWhiteSpace(rawName))
            return false;

        string name = rawName.Trim().ToLowerInvariant();
        if (name.StartsWith("c_"))
            name = name.Substring(2);

        switch (name)
        {
            case "vermell":
            case "rojo":
            case "red":
                color = new Color(1f, 0.08f, 0.06f);
                return true;
            case "groc":
            case "amarillo":
            case "yellow":
                color = new Color(1f, 0.82f, 0.12f);
                return true;
            case "verd":
            case "verde":
            case "green":
                color = new Color(0.18f, 0.9f, 0.22f);
                return true;
            case "blau":
            case "azul":
            case "blue":
                color = new Color(0.14f, 0.45f, 1f);
                return true;
            case "blau_fosc":
            case "blaufosc":
                color = new Color(0.04f, 0.09f, 0.9f);
                return true;
            case "lila":
            case "violeta":
            case "purple":
                color = new Color(0.58f, 0.18f, 1f);
                return true;
            case "rosa":
            case "pink":
                color = new Color(1f, 0.22f, 0.62f);
                return true;
            case "taronja":
            case "naranja":
            case "orange":
                color = new Color(1f, 0.45f, 0.08f);
                return true;
            case "blanc":
            case "blanco":
            case "white":
                color = Color.white;
                return true;
            default:
                return false;
        }
    }
}
