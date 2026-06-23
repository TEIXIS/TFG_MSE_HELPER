using UnityEngine;

public class MasterSwitch : MonoBehaviour
{
    [Header("BOTÓN MAESTRO (Encendido/Apagado)")]
    [SerializeField] private Renderer _masterRenderer;
    [SerializeField] private int _masterMaterialIndex = 0;
    [ColorUsage(true, true)][SerializeField] private Color _masterApagado = Color.gray;
    [ColorUsage(true, true)][SerializeField] private Color _masterEncendidoEmisivo = Color.white;

    [Header("BOTÓN DE MODO (Memoria/Predictivo)")]
    [SerializeField] private Renderer _modeRenderer;
    [SerializeField] private int _modeMaterialIndex = 0;
    [ColorUsage(true, true)][SerializeField] private Color _modeDesactivadoEmisivo = new Color(0.2f, 0.2f, 0.2f);
    [ColorUsage(true, true)][SerializeField] private Color _modeActivoEmisivo = new Color(2.5f, 2.5f, 2.5f);

    [Header("Audio de Feedback Global Spatial")]
    [SerializeField] private AudioClip _clickSound; // El clip único común para ambos botones
    [SerializeField, Range(0f, 1f)] private float _clickVolume = 0.25f;

    public bool IsSystemOn { get; private set; } = false;
    public bool IsPredictiveModeActive { get; private set; } = false;

    public System.Action<bool> OnSystemStateChanged;
    public System.Action<bool> OnModeChanged;

    private Material _masterMaterial;
    private Material _modeMaterial;

    private void Awake()
    {
        if (_masterRenderer != null) _masterMaterial = _masterRenderer.materials[_masterMaterialIndex];
        if (_modeRenderer != null) _modeMaterial = _modeRenderer.materials[_modeMaterialIndex];

        UpdateVisuals();
    }

    // AHORA RECIBE EL AUDIOSOURCE DEL PROPIO BOTÓN MAESTRO (3D)
    public void ToggleMasterSystem(AudioSource localAudioSource)
    {
        if (!SceneManager.PermitirCambioActivacionElemento(gameObject, IsSystemOn))
            return;

        IsSystemOn = !IsSystemOn;

        if (!IsSystemOn)
        {
            IsPredictiveModeActive = false;
        }

        // Reproducir el clic usando el AudioSource del botón físico pulsado
        PlaySpatialFeedback(localAudioSource);

        UpdateVisuals();
        OnSystemStateChanged?.Invoke(IsSystemOn);
    }

    // AHORA RECIBE EL AUDIOSOURCE DEL PROPIO BOTÓN DE MODO (3D)
    public void ToggleGameMode(AudioSource localAudioSource)
    {
        if (!IsSystemOn) return;

        IsPredictiveModeActive = !IsPredictiveModeActive;

        // Reproducir el clic usando el AudioSource del botón físico pulsado
        PlaySpatialFeedback(localAudioSource);

        UpdateVisuals();
        OnModeChanged?.Invoke(IsPredictiveModeActive);
    }

    private void PlaySpatialFeedback(AudioSource source)
    {
        if (source != null && _clickSound != null)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.spatialize = true;
            source.minDistance = 1.5f;
            source.maxDistance = 10f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.PlayOneShot(_clickSound, _clickVolume);
        }
    }

    private void UpdateVisuals()
    {
        if (_masterMaterial != null)
        {
            if (IsSystemOn)
            {
                _masterMaterial.SetColor("_BaseColor", Color.white);
                _masterMaterial.SetColor("_EmissionColor", _masterEncendidoEmisivo);
                DynamicGI.SetEmissive(_masterRenderer, _masterEncendidoEmisivo);
            }
            else
            {
                _masterMaterial.SetColor("_BaseColor", _masterApagado);
                _masterMaterial.SetColor("_EmissionColor", Color.black);
                DynamicGI.SetEmissive(_masterRenderer, Color.black);
            }
        }

        if (_modeMaterial != null)
        {
            if (IsSystemOn)
            {
                _modeMaterial.SetColor("_BaseColor", Color.white);
                Color colorModoActual = IsPredictiveModeActive ? _modeActivoEmisivo : _modeDesactivadoEmisivo;
                _modeMaterial.SetColor("_EmissionColor", colorModoActual);
                DynamicGI.SetEmissive(_modeRenderer, colorModoActual);
            }
            else
            {
                _modeMaterial.SetColor("_BaseColor", _masterApagado);
                _modeMaterial.SetColor("_EmissionColor", Color.black);
                DynamicGI.SetEmissive(_modeRenderer, Color.black);
            }
        }
    }
}