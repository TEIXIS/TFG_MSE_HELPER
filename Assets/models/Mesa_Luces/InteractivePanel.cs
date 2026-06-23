using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class InteractivePanel : MonoBehaviour
{
    [Header("Referencias del Sistema")]
    [SerializeField] private MasterSwitch _masterSwitch;

    [Header("Configuración Visual")]
    [SerializeField] private Renderer _renderer;
    [SerializeField] private int _materialIndex = 0;

    [Header("Colores de Reposo (HDR)")]
    [ColorUsage(true, true)][SerializeField] private Color _colorBlancoReposo = new Color(0.2f, 0.2f, 0.2f);
    [ColorUsage(true, true)][SerializeField] private Color _colorFantasmaReposo = new Color(0.1f, 0.2f, 0.2f);

    [Header("Color Activo (HDR)")]
    [ColorUsage(true, true)][SerializeField] private Color _colorPulsadoEmisivo = Color.cyan;

    [Header("Configuración del Desvanecimiento (Fade)")]
    [SerializeField] private float _tiempoColorMaximo = 0.5f;
    [SerializeField] private float _duracionDesvanecimiento = 1.5f;

    [Header("Audio")]
    [SerializeField, Range(0f, 1f)] private float _volumenPanel = 1f;
    [SerializeField] private float _distanciaMinimaAudio = 1.5f;
    [SerializeField] private float _distanciaMaximaAudio = 14f;

    private Material _material;
    private AudioSource _audioSource;
    private bool _isSystemActive = false;
    private bool _isCooldown = false;
    private Coroutine _pulseCoroutine;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.volume = _volumenPanel;
        _audioSource.spatialBlend = 1f;
        _audioSource.spatialize = true;
        _audioSource.minDistance = _distanciaMinimaAudio;
        _audioSource.maxDistance = _distanciaMaximaAudio;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;

        if (_renderer != null) _material = _renderer.materials[_materialIndex];
    }

    private void OnEnable()
    {
        if (_masterSwitch != null)
        {
            _masterSwitch.OnSystemStateChanged += HandleSystemStateChanged;
            _masterSwitch.OnModeChanged += HandleModeChanged;
            HandleSystemStateChanged(_masterSwitch.IsSystemOn);
        }
    }

    private void OnDisable()
    {
        if (_masterSwitch != null)
        {
            _masterSwitch.OnSystemStateChanged -= HandleSystemStateChanged;
            _masterSwitch.OnModeChanged -= HandleModeChanged;
        }
    }

    private void HandleSystemStateChanged(bool isOn)
    {
        _isSystemActive = isOn;

        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        _isCooldown = false;

        if (_isSystemActive)
        {
            UpdateCurrentReposoColor();
        }
        else
        {
            SetColorState(Color.white, Color.black);
            if (_audioSource.isPlaying) _audioSource.Stop();
        }
    }

    private void HandleModeChanged(bool isPredictiveMode)
    {
        if (_isCooldown || !_isSystemActive) return;
        UpdateCurrentReposoColor();
    }

    private void UpdateCurrentReposoColor()
    {
        // AHORA: Si el modo predictivo está activo muestra color fantasma, si no, se queda blanco
        Color colorDestino = _masterSwitch.IsPredictiveModeActive ? _colorFantasmaReposo : _colorBlancoReposo;
        SetColorState(Color.white, colorDestino);
    }

    public void OnPanelPoked()
    {
        if (!_isSystemActive || _isCooldown) return;
        _pulseCoroutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        _isCooldown = true;

        SetColorState(Color.white, _colorPulsadoEmisivo);

        if (_audioSource != null && _audioSource.clip != null)
        {
            _audioSource.volume = _volumenPanel;
            _audioSource.Play();
        }

        yield return new WaitForSeconds(_tiempoColorMaximo);

        float elapsedTime = 0f;
        Color colorActualEmision = _colorPulsadoEmisivo;
        Color colorTargetReposo = _masterSwitch.IsPredictiveModeActive ? _colorFantasmaReposo : _colorBlancoReposo;

        while (elapsedTime < _duracionDesvanecimiento)
        {
            if (!_isSystemActive) yield break;

            elapsedTime += Time.deltaTime;
            float porcentaje = elapsedTime / _duracionDesvanecimiento;

            colorTargetReposo = _masterSwitch.IsPredictiveModeActive ? _colorFantasmaReposo : _colorBlancoReposo;

            Color colorIntermedio = Color.Lerp(colorActualEmision, colorTargetReposo, porcentaje);
            SetColorState(Color.white, colorIntermedio);

            yield return null;
        }

        if (_isSystemActive)
        {
            UpdateCurrentReposoColor();
        }

        _isCooldown = false;
    }

    private void SetColorState(Color baseColor, Color emissionColor)
    {
        if (_material == null) return;
        _material.SetColor("_BaseColor", baseColor);
        _material.SetColor("_EmissionColor", emissionColor);
        DynamicGI.SetEmissive(_renderer, emissionColor);
    }
}