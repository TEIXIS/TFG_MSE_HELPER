using TMPro;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

public class RoomLightIntensityControl : MonoBehaviour
{
    private const string CommandPrefix = "CMD_AMBIENT_LIGHT_LEVEL:";
    private const string ColorCommandPrefix = "CMD_AMBIENT_LIGHT_COLOR:";

    [Header("Controlador")]
    public AmbientLightIntensityController lightController;
    public Connection connectionServer;
    public bool tutorialPreviewMode;

    [Header("Mostrador")]
    public TMP_Text levelText;
    public Renderer[] levelIndicators;
    public Material indicatorOffMaterial;
    public Material indicatorOnMaterial;
    public Color indicatorOffColor = new Color(1f, 1f, 1f, 0.2f);
    public Color indicatorOnColor = Color.white;

    [Header("Colores")]
    public bool autoDetectColorButtons = true;
    public bool createColorButtonsFromTemplate = false;
    public Transform colorButtonTemplate;
    public string currentColor = "blanc";
    public float selectedColorEmission = 2.2f;
    public float idleColorEmission = 0.35f;

    [Header("Boton maestro")]
    [SerializeField] private Renderer botonMaestroRenderer;
    [SerializeField] private Color colorBaseBotonMaestro = Color.white;
    [SerializeField] private float emisionBotonMaestroEncendido = 1.5f;
    [SerializeField] private float emisionBotonMaestroApagado = 0.2f;

    private MaterialPropertyBlock propertyBlock;
    private MaterialPropertyBlock colorPropertyBlock;
    private Material materialBotonMaestro;
    private readonly List<ColorButtonVisual> colorButtonVisuals = new List<ColorButtonVisual>();
    private static readonly string[] PaletteColors = { "vermell", "groc", "verd", "blau", "lila", "blanc" };
    private int previewLevel = 2;
    private int previewTotalLevels = 4;
    private int lastActiveLevel = 2;
    private bool estaEncendido = true;
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

    private class ColorButtonVisual
    {
        public Renderer renderer;
        public string colorName;
    }

    private void Awake()
    {
        QuestCrashDiagnostics.Log("[RoomLightIntensityControl] Awake " + name);
        propertyBlock = new MaterialPropertyBlock();
        colorPropertyBlock = new MaterialPropertyBlock();
        PrepararBotonMaestro();
    }

    private void OnValidate()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (colorPropertyBlock == null)
            colorPropertyBlock = new MaterialPropertyBlock();

        if (autoDetectColorButtons)
        {
            CacheColorButtons();
            UpdateColorButtonVisuals();
        }
    }

    private void OnEnable()
    {
        QuestCrashDiagnostics.Log("[RoomLightIntensityControl] OnEnable " + name);
        if (!tutorialPreviewMode && lightController == null)
            lightController = FindFirstObjectByType<AmbientLightIntensityController>();

        if (!tutorialPreviewMode && connectionServer == null)
            connectionServer = FindFirstObjectByType<Connection>();

        if (lightController != null)
            lightController.LevelChanged += UpdateDisplay;

        if (autoDetectColorButtons)
            CacheColorButtons();
    }

    private void Start()
    {
        QuestCrashDiagnostics.Log("[RoomLightIntensityControl] Start " + name);
        SetupColorButtonsFromTemplate();

        if (lightController != null)
            UpdateDisplay(lightController.currentLevel, lightController.TotalLevels);
        else if (tutorialPreviewMode)
            UpdateDisplay(previewLevel, previewTotalLevels);

        UpdateColorButtonVisuals();
        ActualizarBotonMaestro();
        ButtonClickFeedback.EnsureInHierarchy(gameObject);
    }

    private void OnDisable()
    {
        QuestCrashDiagnostics.Log("[RoomLightIntensityControl] OnDisable " + name);
        if (lightController != null)
            lightController.LevelChanged -= UpdateDisplay;
    }

    private void OnDestroy()
    {
        QuestCrashDiagnostics.Log("[RoomLightIntensityControl] OnDestroy " + name);
    }

    public void IncreaseLevel()
    {
        QuestCrashDiagnostics.Log("[RoomLightIntensityControl] IncreaseLevel");
        if (!estaEncendido)
            return;

        if (tutorialPreviewMode && lightController == null)
        {
            SetPreviewLevel(previewLevel + 1);
            return;
        }

        if (lightController == null)
            return;

        lightController.IncreaseLevel();
        SendLevelToTablet();
    }

    public void DecreaseLevel()
    {
        QuestCrashDiagnostics.Log("[RoomLightIntensityControl] DecreaseLevel");
        if (!estaEncendido)
            return;

        if (tutorialPreviewMode && lightController == null)
        {
            SetPreviewLevel(previewLevel - 1);
            return;
        }

        if (lightController == null)
            return;

        lightController.DecreaseLevel();
        SendLevelToTablet();
    }

    public void AlternarEstadoSistema()
    {
        if (!SceneManager.PermitirCambioActivacionElemento(gameObject, estaEncendido))
            return;

        estaEncendido = !estaEncendido;
        QuestCrashDiagnostics.Log("[RoomLightIntensityControl] AlternarEstadoSistema encendido=" + estaEncendido);

        if (estaEncendido)
        {
            EncenderSistema();
        }
        else
        {
            ApagarSistema();
        }

        ActualizarBotonMaestro();
    }

    // Prepara el material del boton principal para marcar visualmente el estado de la mesa de luces.
    private void PrepararBotonMaestro()
    {
        if (botonMaestroRenderer == null)
            return;

        materialBotonMaestro = botonMaestroRenderer.material;
        materialBotonMaestro.EnableKeyword("_EMISSION");
        ActualizarBotonMaestro();
    }

    // Cambia solo el brillo del boton principal; el objeto sigue visible aunque no este interactuable.
    private void ActualizarBotonMaestro()
    {
        if (materialBotonMaestro == null)
            return;

        float intensidad = estaEncendido ? emisionBotonMaestroEncendido : emisionBotonMaestroApagado;
        materialBotonMaestro.SetColor(EmissionColorProperty, colorBaseBotonMaestro * intensidad);
    }

    public void SetTutorialPreviewMode(bool active)
    {
        if (lightController != null)
            lightController.LevelChanged -= UpdateDisplay;

        tutorialPreviewMode = active;
        connectionServer = active ? null : connectionServer;

        if (active && lightController != null)
        {
            previewLevel = lightController.currentLevel;
            previewTotalLevels = lightController.TotalLevels;
        }

        if (active)
            lightController = null;

        UpdateDisplay(previewLevel, previewTotalLevels);
        UpdateColorButtonVisuals();
    }

    public void SeleccionarColor(string colorName)
    {
        if (!estaEncendido)
            return;

        AplicarColorSeleccionado(colorName, true);
    }

    public void SetColorFromRemote(string colorName)
    {
        AplicarColorSeleccionado(colorName, false);
    }

    private void AplicarColorSeleccionado(string colorName, bool applyToLight)
    {
        string normalizedColor = NormalizeColorName(colorName);
        if (string.IsNullOrEmpty(normalizedColor))
            return;

        currentColor = normalizedColor;
        QuestCrashDiagnostics.Log("[RoomLightIntensityControl] SeleccionarColor " + currentColor + " tutorial=" + tutorialPreviewMode + " applyToLight=" + applyToLight);
        UpdateColorButtonVisuals();

        if (tutorialPreviewMode || !applyToLight)
            return;

        if (lightController == null)
            lightController = FindFirstObjectByType<AmbientLightIntensityController>();

        if (lightController != null)
            lightController.SetColorByName(currentColor);

        SendColorToTablet();
    }

    private void SetupColorButtonsFromTemplate()
    {
        if (!createColorButtonsFromTemplate)
            return;

        Transform template = colorButtonTemplate != null ? colorButtonTemplate : FindColorButtonTemplate();
        if (template == null)
            return;

        Vector3 basePosition = template.localPosition;
        Quaternion baseRotation = template.localRotation;
        Vector3 baseScale = template.localScale;
        float spacing = 0.34f;
        int middleIndex = PaletteColors.Length / 2;

        for (int i = 0; i < PaletteColors.Length; i++)
        {
            string colorName = PaletteColors[i];
            Transform button = FindDirectChild("Color_" + colorName);

            if (button == null)
            {
                button = colorName == "verd" && template.name == "buto_verd"
                    ? template
                    : Instantiate(template, template.parent);
            }

            button.name = "Color_" + colorName;
            button.localPosition = basePosition + new Vector3((i - middleIndex) * spacing, 0f, 0f);
            button.localRotation = baseRotation;
            button.localScale = baseScale;

            ConfigureColorButtonEvent(button, colorName);
        }

        CacheColorButtons();
        UpdateColorButtonVisuals();
    }

    private Transform FindColorButtonTemplate()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && (candidate.name == "buto_verd" || candidate.name == "Color_verd"))
                return candidate;
        }

        return null;
    }

    private Transform FindDirectChild(string childName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.name == childName)
                return candidate;
        }

        return null;
    }

    private void ConfigureColorButtonEvent(Transform button, string colorName)
    {
        MonoBehaviour[] behaviours = button.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour.GetType().Name != "InteractableUnityEventWrapper")
                continue;

            FieldInfo whenSelectField = behaviour.GetType().GetField("_whenSelect", BindingFlags.Instance | BindingFlags.NonPublic);
            UnityEvent whenSelect = whenSelectField != null ? whenSelectField.GetValue(behaviour) as UnityEvent : null;
            if (whenSelect == null)
                continue;

            string capturedColor = colorName;
            whenSelect.RemoveAllListeners();
            whenSelect.AddListener(() => SeleccionarColor(capturedColor));
        }
    }

    private void SetPreviewLevel(int level)
    {
        previewLevel = Mathf.Clamp(level, 1, Mathf.Max(1, previewTotalLevels));
        lastActiveLevel = previewLevel;
        UpdateDisplay(previewLevel, previewTotalLevels);
    }

    private void EncenderSistema()
    {
        if (tutorialPreviewMode && lightController == null)
        {
            UpdateDisplay(previewLevel, previewTotalLevels);
            UpdateColorButtonVisuals();
            return;
        }

        if (lightController != null)
        {
            UpdateDisplay(lightController.currentLevel, lightController.TotalLevels);
            UpdateColorButtonVisuals();
        }
        else
        {
            UpdateDisplay(Mathf.Max(1, lastActiveLevel), previewTotalLevels);
            UpdateColorButtonVisuals();
        }
    }

    private void ApagarSistema()
    {
        if (lightController != null)
            lastActiveLevel = Mathf.Max(1, lightController.currentLevel);
        else
            lastActiveLevel = Mathf.Max(1, previewLevel);

        UpdateDisplay(0, lightController != null ? lightController.TotalLevels : previewTotalLevels);
        UpdateColorButtonVisuals();
    }

    private void SendLevelToTablet()
    {
        if (tutorialPreviewMode)
            return;

        QuestCrashDiagnostics.Log("[RoomLightIntensityControl] SendLevelToTablet connected=" + (connectionServer != null && connectionServer.connected)
            + " level=" + (lightController != null ? lightController.currentLevel : -1));
        if (connectionServer != null && connectionServer.connected && lightController != null)
            connectionServer.Send(CommandPrefix + lightController.currentLevel);
    }

    private void SendColorToTablet()
    {
        if (tutorialPreviewMode)
            return;

        if (connectionServer == null)
            connectionServer = FindFirstObjectByType<Connection>();

        QuestCrashDiagnostics.Log("[RoomLightIntensityControl] SendColorToTablet connected=" + (connectionServer != null && connectionServer.connected)
            + " color=" + currentColor);
        if (connectionServer != null && connectionServer.connected)
            connectionServer.Send(ColorCommandPrefix + currentColor);
    }

    private void UpdateDisplay(int level, int totalLevels)
    {
        if (level > 0)
            lastActiveLevel = level;

        if (!estaEncendido)
            level = 0;

        if (levelText != null)
            levelText.text = level + " / " + totalLevels;

        if (levelIndicators == null)
            return;

        for (int i = 0; i < levelIndicators.Length; i++)
        {
            Renderer indicator = levelIndicators[i];
            if (indicator == null)
                continue;

            bool active = i < level;
            if (indicatorOnMaterial != null && indicatorOffMaterial != null)
            {
                indicator.sharedMaterial = active ? indicatorOnMaterial : indicatorOffMaterial;
                continue;
            }

            indicator.GetPropertyBlock(propertyBlock);
            Material sharedMaterial = indicator.sharedMaterial;
            int colorProperty = sharedMaterial != null && sharedMaterial.HasProperty(BaseColorProperty)
                ? BaseColorProperty
                : ColorProperty;

            propertyBlock.SetColor(colorProperty, active ? indicatorOnColor : indicatorOffColor);
            indicator.SetPropertyBlock(propertyBlock);
        }
    }

    private void CacheColorButtons()
    {
        colorButtonVisuals.Clear();
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer candidate in renderers)
        {
            if (candidate == null)
                continue;

            string colorName = FindColorName(candidate.transform);
            if (string.IsNullOrEmpty(colorName) || HasColorButtonVisual(candidate))
                continue;

            colorButtonVisuals.Add(new ColorButtonVisual
            {
                renderer = candidate,
                colorName = colorName
            });
        }
    }

    private bool HasColorButtonVisual(Renderer renderer)
    {
        foreach (ColorButtonVisual visual in colorButtonVisuals)
        {
            if (visual != null && visual.renderer == renderer)
                return true;
        }

        return false;
    }

    private string FindColorName(Transform candidate)
    {
        Transform current = candidate;
        while (current != null && current != transform)
        {
            string normalizedColor = NormalizeColorName(current.name);
            if (!string.IsNullOrEmpty(normalizedColor))
                return normalizedColor;

            current = current.parent;
        }

        return string.Empty;
    }

    private void UpdateColorButtonVisuals()
    {
        if (autoDetectColorButtons && colorButtonVisuals.Count == 0)
            CacheColorButtons();

        foreach (ColorButtonVisual visual in colorButtonVisuals)
        {
            if (visual == null || visual.renderer == null)
                continue;

            Color baseColor = GetColorValue(visual.colorName);
            bool selected = visual.colorName == currentColor;
            float emission = estaEncendido
                ? (selected ? selectedColorEmission : idleColorEmission)
                : (selected ? idleColorEmission : 0f);
            Color finalEmission = baseColor * emission;
            finalEmission.a = 1f;
            Color displayColor = estaEncendido
                ? baseColor * (selected ? 1f : 0.45f)
                : baseColor * (selected ? 0.35f : 0.08f);
            displayColor.a = 1f;

            visual.renderer.GetPropertyBlock(colorPropertyBlock);
            colorPropertyBlock.SetColor(BaseColorProperty, displayColor);
            colorPropertyBlock.SetColor(ColorProperty, displayColor);
            colorPropertyBlock.SetColor(EmissionColorProperty, finalEmission);
            visual.renderer.SetPropertyBlock(colorPropertyBlock);
        }
    }

    private string NormalizeColorName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        string name = rawName.Trim().ToLowerInvariant();
        if (name.StartsWith("c_"))
            name = name.Substring(2);
        else if (name.StartsWith("color_"))
            name = name.Substring(6);
        else if (name.StartsWith("buto_"))
            name = name.Substring(5);

        switch (name)
        {
            case "vermell":
            case "rojo":
            case "red":
                return "vermell";
            case "groc":
            case "amarillo":
            case "yellow":
                return "groc";
            case "verd":
            case "verde":
            case "green":
                return "verd";
            case "blau":
            case "azul":
            case "blue":
                return "blau";
            case "blau_fosc":
            case "blaufosc":
            case "azuloscuro":
            case "darkblue":
                return "blau_fosc";
            case "lila":
            case "violeta":
            case "purple":
                return "lila";
            case "rosa":
            case "pink":
                return "rosa";
            case "taronja":
            case "naranja":
            case "orange":
                return "taronja";
            case "blanc":
            case "blanco":
            case "white":
                return "blanc";
            default:
                return string.Empty;
        }
    }

    private Color GetColorValue(string colorName)
    {
        switch (NormalizeColorName(colorName))
        {
            case "vermell":
                return new Color(1f, 0.12f, 0.08f);
            case "groc":
                return new Color(1f, 0.78f, 0.12f);
            case "verd":
                return new Color(0.2f, 1f, 0.35f);
            case "blau":
                return new Color(0.18f, 0.45f, 1f);
            case "blau_fosc":
                return new Color(0.04f, 0.12f, 0.55f);
            case "lila":
                return new Color(0.58f, 0.22f, 1f);
            case "rosa":
                return new Color(1f, 0.22f, 0.72f);
            case "taronja":
                return new Color(1f, 0.38f, 0.08f);
            default:
                return Color.white;
        }
    }
}
