using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AmbientLightTabletControl : MonoBehaviour
{
    private const string CommandPrefix = "CMD_AMBIENT_LIGHT_LEVEL:";

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

    private bool pendingSend;

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
    }

    private void Start()
    {
        UpdateInterface();
        SendCurrentLevel();
    }

    private void Update()
    {
        if (pendingSend && connectionScript != null && connectionScript.connected)
            SendCurrentLevel();
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
}
