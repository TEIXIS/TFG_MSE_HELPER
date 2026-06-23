using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AmbientLightTabletControl : MonoBehaviour
{
    private const string CommandPrefix = "CMD_AMBIENT_LIGHT_LEVEL:";
    private const string ColorCommandPrefix = "CMD_AMBIENT_LIGHT_COLOR:";

    [Header("Red")]
    public Connection connectionScript;

    [Header("Nivel")]
    [Range(1, 4)] public int currentLevel = 2;
    public int totalLevels = 4;

    [Header("UI")]
    public TMP_Text levelText;
    public Image[] levelIndicators;
    public Sprite indicatorOffSprite;
    public Sprite indicatorOnSprite;
    public Color indicatorOffColor = new Color(1f, 1f, 1f, 0.25f);
    public Color indicatorOnColor = Color.white;
    public Button decreaseButton;
    public Button increaseButton;

    [Header("Colores")]
    public Transform colorButtonsRoot;
    public bool autoWireColorButtons = true;
    public string currentColor = "blanc";
    [Range(1f, 1.4f)] public float selectedColorScale = 1.12f;
    [Range(0.1f, 1f)] public float unselectedColorAlpha = 0.55f;

    private bool pendingSend;
    private bool pendingColorSend;
    private readonly List<ColorButtonBinding> colorButtonBindings = new List<ColorButtonBinding>();

    private class ColorButtonBinding
    {
        public Button button;
        public Graphic graphic;
        public string colorName;
        public Color normalColor;
        public Vector3 normalScale;
    }

    private void Awake()
    {
        if (connectionScript == null)
            connectionScript = FindFirstObjectByType<Connection>();

        totalLevels = Mathf.Max(1, totalLevels);
        currentLevel = Mathf.Clamp(currentLevel, 1, totalLevels);

        if (decreaseButton != null)
            decreaseButton.onClick.AddListener(DecreaseLevel);

        if (increaseButton != null)
            increaseButton.onClick.AddListener(IncreaseLevel);

        if (autoWireColorButtons)
            WireColorButtons();
    }

    private void Start()
    {
        UpdateInterface();
        UpdateColorSelection();
        SendCurrentLevel();
        SendCurrentColor();
    }

    private void Update()
    {
        if (pendingSend && connectionScript != null && connectionScript.connected)
            SendCurrentLevel();

        if (pendingColorSend && connectionScript != null && connectionScript.connected)
            SendCurrentColor();
    }

    public void IncreaseLevel()
    {
        SetLevel(currentLevel + 1, true);
    }

    public void DecreaseLevel()
    {
        SetLevel(currentLevel - 1, true);
    }

    public void SetLevel(int level)
    {
        SetLevel(level, true);
    }

    public void SetLevelFromRemote(int level)
    {
        SetLevel(level, false);
    }

    private void SetLevel(int level, bool sendToHeadset)
    {
        int clampedLevel = Mathf.Clamp(level, 1, totalLevels);
        if (clampedLevel == currentLevel)
            return;

        currentLevel = clampedLevel;
        UpdateInterface();

        if (sendToHeadset)
            SendCurrentLevel();
    }

    public void SendCurrentLevel()
    {
        if (connectionScript == null || !connectionScript.connected)
        {
            pendingSend = true;
            return;
        }

        pendingSend = false;
        connectionScript.Send(CommandPrefix + currentLevel);
        Debug.Log("[LUZ] Enviado nivel de luz a VR: " + currentLevel);
    }

    public void SetColor(string colorName)
    {
        string normalizedColor = NormalizeColorName(colorName);
        if (string.IsNullOrEmpty(normalizedColor))
            return;

        if (currentColor == normalizedColor)
        {
            UpdateColorSelection();
            return;
        }

        currentColor = normalizedColor;
        UpdateColorSelection();
        SendCurrentColor();
    }

    public void SetColorFromRemote(string colorName)
    {
        string normalizedColor = NormalizeColorName(colorName);
        if (!string.IsNullOrEmpty(normalizedColor))
        {
            currentColor = normalizedColor;
            UpdateColorSelection();
        }
    }

    public void SendCurrentColor()
    {
        if (connectionScript == null || !connectionScript.connected)
        {
            pendingColorSend = true;
            return;
        }

        pendingColorSend = false;
        connectionScript.Send(ColorCommandPrefix + currentColor);
        Debug.Log("[LUZ] Enviado color de luz a VR: " + currentColor);
    }

    private void WireColorButtons()
    {
        Transform root = colorButtonsRoot != null ? colorButtonsRoot : FindColorButtonsRoot();
        if (root == null)
            return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        colorButtonBindings.Clear();
        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            string colorName = NormalizeColorName(button.gameObject.name);
            if (string.IsNullOrEmpty(colorName))
                continue;

            Graphic graphic = button.targetGraphic != null ? button.targetGraphic : button.GetComponent<Graphic>();
            colorButtonBindings.Add(new ColorButtonBinding
            {
                button = button,
                graphic = graphic,
                colorName = colorName,
                normalColor = graphic != null ? graphic.color : Color.white,
                normalScale = button.transform.localScale
            });

            string capturedColor = colorName;
            button.onClick.AddListener(() => SetColor(capturedColor));
        }

        UpdateColorSelection();
    }

    private Transform FindColorButtonsRoot()
    {
        Transform[] transforms = transform.root.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.name == "Esquerra")
                return candidate;
        }

        return null;
    }

    private string NormalizeColorName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        string name = rawName.Trim().ToLowerInvariant();
        if (name.StartsWith("c_"))
            name = name.Substring(2);

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

    private void UpdateInterface()
    {
        if (levelText != null)
            levelText.text = currentLevel + " / " + totalLevels;

        if (decreaseButton != null)
            decreaseButton.interactable = currentLevel > 1;

        if (increaseButton != null)
            increaseButton.interactable = currentLevel < totalLevels;

        if (levelIndicators == null)
            return;

        for (int i = 0; i < levelIndicators.Length; i++)
        {
            Image indicator = levelIndicators[i];
            if (indicator == null)
                continue;

            bool active = i < currentLevel;
            if (indicatorOnSprite != null && indicatorOffSprite != null)
                indicator.sprite = active ? indicatorOnSprite : indicatorOffSprite;

            indicator.color = active ? indicatorOnColor : indicatorOffColor;
        }
    }

    private void UpdateColorSelection()
    {
        foreach (ColorButtonBinding binding in colorButtonBindings)
        {
            if (binding == null || binding.button == null)
                continue;

            bool selected = binding.colorName == currentColor;
            binding.button.transform.localScale = binding.normalScale * (selected ? selectedColorScale : 1f);

            if (binding.graphic == null)
                continue;

            Color color = binding.normalColor;
            color.a = selected ? 1f : binding.normalColor.a * unselectedColorAlpha;
            binding.graphic.color = color;
        }
    }
}
