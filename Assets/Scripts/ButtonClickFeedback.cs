using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

public class ButtonClickFeedback : MonoBehaviour
{
    private static readonly HashSet<int> ConfiguredWrappers = new HashSet<int>();
    private static AudioClip generatedClickClip;
    public static AudioClip DefaultClickClip { get; set; }

    [SerializeField] private AudioClip clickClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.28f;
    [SerializeField] private float spatialBlend = 1f;
    [SerializeField] private float minInterval = 0.08f;

    private AudioSource audioSource;
    private float lastPlayTime = -999f;

    public static void EnsureInHierarchy(GameObject root)
    {
        if (root == null)
            return;

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour.GetType().Name != "InteractableUnityEventWrapper")
                continue;

            GameObject buttonObject = behaviour.gameObject;
            if (ShouldSkipGlobalClick(behaviour))
                continue;

            ButtonClickFeedback feedback = buttonObject.GetComponent<ButtonClickFeedback>();
            if (feedback == null)
                feedback = buttonObject.AddComponent<ButtonClickFeedback>();

            feedback.Configure(behaviour);
        }
    }

    private static bool ShouldSkipGlobalClick(MonoBehaviour wrapper)
    {
        FieldInfo whenSelectField = wrapper.GetType().GetField("_whenSelect", BindingFlags.Instance | BindingFlags.NonPublic);
        UnityEvent whenSelect = whenSelectField != null ? whenSelectField.GetValue(wrapper) as UnityEvent : null;
        if (whenSelect == null)
            return false;

        int count = whenSelect.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (whenSelect.GetPersistentMethodName(i) == "OnPanelPoked")
                return true;
        }

        return false;
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = spatialBlend;
        audioSource.volume = volume;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 0.05f;
        audioSource.maxDistance = 8f;
    }

    private void Configure(MonoBehaviour wrapper)
    {
        if (wrapper == null)
            return;

        int id = wrapper.GetInstanceID();
        if (ConfiguredWrappers.Contains(id))
            return;

        FieldInfo whenSelectField = wrapper.GetType().GetField("_whenSelect", BindingFlags.Instance | BindingFlags.NonPublic);
        UnityEvent whenSelect = whenSelectField != null ? whenSelectField.GetValue(wrapper) as UnityEvent : null;
        if (whenSelect == null)
            return;

        whenSelect.AddListener(PlayClick);
        ConfiguredWrappers.Add(id);
    }

    public void PlayClick()
    {
        if (SceneManager.EstaAplicandoSeleccionAutomatica)
            return;

        if (Time.unscaledTime - lastPlayTime < minInterval)
            return;

        lastPlayTime = Time.unscaledTime;

        if (audioSource == null)
            Awake();

        AudioClip clip = clickClip != null ? clickClip : (DefaultClickClip != null ? DefaultClickClip : GetGeneratedClickClip());
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, volume);
    }

    private static AudioClip GetGeneratedClickClip()
    {
        if (generatedClickClip != null)
            return generatedClickClip;

        const int sampleRate = 24000;
        const float duration = 0.045f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-70f * t);
            float body = Mathf.Sin(2f * Mathf.PI * 1350f * t);
            float snap = Mathf.Sin(2f * Mathf.PI * 2600f * t) * 0.35f;
            data[i] = (body + snap) * envelope * 0.55f;
        }

        generatedClickClip = AudioClip.Create("GeneratedButtonClick", sampleCount, 1, sampleRate, false);
        generatedClickClip.SetData(data, 0);
        return generatedClickClip;
    }
}
