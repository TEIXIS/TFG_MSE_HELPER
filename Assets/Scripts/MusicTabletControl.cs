using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MusicTabletControl : MonoBehaviour
{
    private const string SongCommandPrefix = "CMD_MUSIC_SONG:";
    private const string VolumeCommandPrefix = "CMD_MUSIC_VOLUME:";

    [Header("Red")]
    public Connection connectionScript;

    [Header("Canciones")]
    public Transform songButtonsRoot;
    public bool autoWireSongButtons = true;
    public string currentSong = "INSTRUMENTAL";
    [Range(1f, 1.4f)] public float selectedSongScale = 1.12f;
    [Range(0.1f, 1f)] public float unselectedSongAlpha = 0.55f;

    [Header("Volumen")]
    [Range(1, 6)] public int currentVolumeLevel = 4;
    public int totalVolumeLevels = 6;
    public TMP_Text volumeText;
    public Image[] volumeIndicators;
    public Sprite indicatorOffSprite;
    public Sprite indicatorOnSprite;
    public Color indicatorOffColor = new Color(1f, 1f, 1f, 0.25f);
    public Color indicatorOnColor = Color.white;
    public Button decreaseButton;
    public Button increaseButton;
    public Slider legacySlider;

    private bool pendingSongSend;
    private bool pendingVolumeSend;
    private readonly List<SongButtonBinding> songButtonBindings = new List<SongButtonBinding>();

    private class SongButtonBinding
    {
        public Button button;
        public Graphic graphic;
        public string songName;
        public Color normalColor;
        public Vector3 normalScale;
    }

    private void Awake()
    {
        if (connectionScript == null)
            connectionScript = FindFirstObjectByType<Connection>();

        totalVolumeLevels = Mathf.Max(1, totalVolumeLevels);
        currentVolumeLevel = Mathf.Clamp(currentVolumeLevel, 1, totalVolumeLevels);
        currentSong = NormalizeSongName(currentSong);

        if (decreaseButton != null)
            decreaseButton.onClick.AddListener(DecreaseVolume);

        if (increaseButton != null)
            increaseButton.onClick.AddListener(IncreaseVolume);

        if (legacySlider == null)
            legacySlider = FindLegacySlider();

        if (legacySlider != null)
        {
            legacySlider.wholeNumbers = true;
            legacySlider.minValue = 1;
            legacySlider.maxValue = totalVolumeLevels;
            legacySlider.interactable = false;
        }

        if (autoWireSongButtons)
            WireSongButtons();
    }

    private void Start()
    {
        UpdateSongSelection();
        UpdateVolumeInterface();
        SendCurrentSong();
        SendCurrentVolume();
    }

    private void Update()
    {
        if (pendingSongSend && connectionScript != null && connectionScript.connected)
            SendCurrentSong();

        if (pendingVolumeSend && connectionScript != null && connectionScript.connected)
            SendCurrentVolume();
    }

    public void SetSong(string songName)
    {
        string normalizedSong = NormalizeSongName(songName);
        if (string.IsNullOrEmpty(normalizedSong))
            return;

        currentSong = normalizedSong;
        UpdateSongSelection();
        SendCurrentSong();
    }

    public void SetSongFromRemote(string songName)
    {
        string normalizedSong = NormalizeSongName(songName);
        if (string.IsNullOrEmpty(normalizedSong))
            return;

        currentSong = normalizedSong;
        UpdateSongSelection();
    }

    public void IncreaseVolume()
    {
        SetVolumeLevel(currentVolumeLevel + 1, true);
    }

    public void DecreaseVolume()
    {
        SetVolumeLevel(currentVolumeLevel - 1, true);
    }

    public void SetVolumeLevel(int level)
    {
        SetVolumeLevel(level, true);
    }

    public void SetVolumeLevelFromRemote(int level)
    {
        SetVolumeLevel(level, false);
    }

    private void SetVolumeLevel(int level, bool sendToHeadset)
    {
        int clampedLevel = Mathf.Clamp(level, 1, totalVolumeLevels);
        if (clampedLevel == currentVolumeLevel)
        {
            UpdateVolumeInterface();
            return;
        }

        currentVolumeLevel = clampedLevel;
        UpdateVolumeInterface();

        if (sendToHeadset)
            SendCurrentVolume();
    }

    public void SendCurrentSong()
    {
        if (connectionScript == null || !connectionScript.connected)
        {
            pendingSongSend = true;
            return;
        }

        pendingSongSend = false;
        connectionScript.Send(SongCommandPrefix + currentSong);
        Debug.Log("[MUSICA] Enviada cancion a VR: " + currentSong);
    }

    public void SendCurrentVolume()
    {
        if (connectionScript == null || !connectionScript.connected)
        {
            pendingVolumeSend = true;
            return;
        }

        pendingVolumeSend = false;
        connectionScript.Send(VolumeCommandPrefix + currentVolumeLevel);
        Debug.Log("[MUSICA] Enviado volumen a VR: " + currentVolumeLevel);
    }

    private void WireSongButtons()
    {
        Button[] buttons = songButtonsRoot != null
            ? songButtonsRoot.GetComponentsInChildren<Button>(true)
            : FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        songButtonBindings.Clear();

        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            string songName = NormalizeSongName(button.gameObject.name);
            if (string.IsNullOrEmpty(songName))
                continue;

            Graphic graphic = button.targetGraphic != null ? button.targetGraphic : button.GetComponent<Graphic>();
            songButtonBindings.Add(new SongButtonBinding
            {
                button = button,
                graphic = graphic,
                songName = songName,
                normalColor = graphic != null ? graphic.color : Color.white,
                normalScale = button.transform.localScale
            });

            string capturedSong = songName;
            button.onClick.AddListener(() => SetSong(capturedSong));
        }

        UpdateSongSelection();
    }

    private Slider FindLegacySlider()
    {
        Slider[] sliders = FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Slider slider in sliders)
        {
            if (slider != null && slider.gameObject.name == "Slider_audio")
                return slider;
        }

        return null;
    }

    private string NormalizeSongName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        string name = rawName.Trim();
        if (name.StartsWith("c_"))
            name = name.Substring(2);

        name = name.Replace("à", "a").Replace("À", "A").ToUpperInvariant();

        switch (name)
        {
            case "ZEN":
                return "ZEN";
            case "SMR":
            case "ASMR":
                return "SMR";
            case "INSTRUMENTAL":
            case "CLASSICA":
            case "CLASICA":
                return "INSTRUMENTAL";
            case "AIGUA":
            case "NATURA":
            case "AGUA":
                return "AIGUA";
            case "RITMICA":
            case "RITME":
            case "RITM":
                return "RITMICA";
            default:
                return string.Empty;
        }
    }

    private void UpdateSongSelection()
    {
        foreach (SongButtonBinding binding in songButtonBindings)
        {
            if (binding == null || binding.button == null)
                continue;

            bool selected = binding.songName == currentSong;
            binding.button.transform.localScale = binding.normalScale * (selected ? selectedSongScale : 1f);

            if (binding.graphic == null)
                continue;

            Color color = binding.normalColor;
            color.a = selected ? 1f : binding.normalColor.a * unselectedSongAlpha;
            binding.graphic.color = color;
        }
    }

    private void UpdateVolumeInterface()
    {
        if (volumeText != null)
            volumeText.text = currentVolumeLevel + " / " + totalVolumeLevels;

        if (decreaseButton != null)
            decreaseButton.interactable = currentVolumeLevel > 1;

        if (increaseButton != null)
            increaseButton.interactable = currentVolumeLevel < totalVolumeLevels;

        if (legacySlider != null)
            legacySlider.value = currentVolumeLevel;

        if (volumeIndicators == null)
            return;

        for (int i = 0; i < volumeIndicators.Length; i++)
        {
            Image indicator = volumeIndicators[i];
            if (indicator == null)
                continue;

            bool active = i < currentVolumeLevel;
            if (indicatorOnSprite != null && indicatorOffSprite != null)
                indicator.sprite = active ? indicatorOnSprite : indicatorOffSprite;

            indicator.color = active ? indicatorOnColor : indicatorOffColor;
        }
    }
}
