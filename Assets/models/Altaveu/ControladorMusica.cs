using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ControladorMultisensorial : MonoBehaviour
{
    public enum TipusEspacialitzacio { Mode2D_Envoltant, Mode3D_DirecteAltaveu, Mode3D_AmbDispersioAmplia }

    [System.Serializable]
    public struct BarraVolumen
    {
        public Renderer rendererBarra;
        [ColorUsage(true, true)] public Color colorEncendido;
        [ColorUsage(true, true)] public Color colorApagado;
    }

    [System.Serializable]
    public class BotonMusica
    {
        public Renderer rendererBoton;
        [Tooltip("Intensidad de emisión cuando el sistema está ON y la canción está SELECCIONADA")]
        public float intensidadActivo = 2.5f;
        [Tooltip("Intensidad de emisión cuando el sistema está ON y la canción está EN REPOSO")]
        public float intensidadApagado = 0.2f;
        public TipusEspacialitzacio comportamentEspacial;

        // CORRECCIÓN APK: Color manual por si falla la lectura automática de la GPU en Android
        [Header("Respaldo Android")]
        [SerializeField] public Color colorBaseAsignado = Color.white;
        [HideInInspector] public Color colorBaseOriginal;
    }

    [System.Serializable]
    public class BotonEncendidoMaestro
    {
        public Renderer rendererBoton;
        public float intensidadOn = 3.0f;
        public float intensidadOff = 0.3f;

        // CORRECCIÓN APK: Respaldo para el botón de encendido
        [Header("Respaldo Android")]
        [SerializeField] public Color colorBaseAsignado = Color.white;
        [HideInInspector] public Color colorBaseOriginal;
    }

    [System.Serializable]
    public class BotonesControlVolumen
    {
        public Renderer rendererSubirVolumen;
        public Renderer rendererBajarVolumen;
        [Tooltip("Intensidad de emisión de los botones de volumen en ON (Fijada a 1.5f por código)")]
        public float intensidadOn = 1.5f;

        // CORRECCIÓN APK: Respaldo para los botones de volumen
        [Header("Respaldo Android")]
        [SerializeField] public Color colorBaseSubirAsignado = Color.white;
        [SerializeField] public Color colorBaseBajarAsignado = Color.white;

        [HideInInspector] public Color colorBaseSubirOriginal;
        [HideInInspector] public Color colorBaseBajarOriginal;
    }

    [Header("Configuración de Audio (Música)")]
    [SerializeField] private AudioClip[] canciones;
    [SerializeField] private float tiempoTransicionAudio = 1.5f;
    [SerializeField] private float velocidadCambioVolumen = 2f;

    [SerializeField] [Range(1f, 2f)] private float limiteSuperiorVolumen = 2.0f;
    [SerializeField] [Range(0f, 1f)] private float volumenInicialNormalizado = 0.65f;

    // MEJORA RELAJACIÓN: Ajusta este valor para bajar/subir la potencia final sin editar los MP3
    [SerializeField] [Range(0f, 1f)] private float atenuacionVolumenGlobal = 1.0f;

    [Header("Feedback de Pulsacion (SFX)")]
    [SerializeField] private AudioClip sonidoClicBoton;
    [SerializeField] [Range(0f, 1f)] private float volumenClic = 0.25f;
    private AudioSource audioSourceEfectos;

    [Header("Elementos de la Interfaz (Renderers)")]
    [SerializeField] private BotonMusica[] botonesCanciones;
    [SerializeField] private BarraVolumen[] barrasVolumen;

    [Header("Botón Maestro de Encendido")]
    [SerializeField] private BotonEncendidoMaestro botonMaestro;
    private bool estaEncendido = true;

    [Header("Botones Físicos de Ajuste de Volumen")]
    [SerializeField] private BotonesControlVolumen botonesVolumenFisicos;

    // Variables de control y memoria
    private AudioSource audioSourceMusica;
    private float volumenObjetivoGlobal = 0.1f;
    private int indiceCancionActiva = 0;
    private int indiceCancionMemoria = 0;
    private float volumenMemoria = 0.1f;
    private bool estaEnPausaPorVolumenCero = false;
    private Coroutine coroutineTransicion;
    private Connection connectionCache;

    private MaterialPropertyBlock bloquePropiedades;

    void Awake()
    {
        audioSourceMusica = GetComponent<AudioSource>();
        bloquePropiedades = new MaterialPropertyBlock();

        audioSourceEfectos = gameObject.AddComponent<AudioSource>();
        audioSourceEfectos.spatialBlend = 1f;
        audioSourceEfectos.volume = 1f;
        audioSourceEfectos.minDistance = 2f;
        audioSourceEfectos.maxDistance = 12f;
        audioSourceEfectos.rolloffMode = AudioRolloffMode.Linear;
    }

    void Start()
    {
        // 1. GUARDAR COLORES BASE ORIGINALES CON FILTRO DE SEGURIDAD PARA QUEST
        foreach (var boton in botonesCanciones)
        {
            if (boton.rendererBoton != null)
            {
                if (boton.rendererBoton.sharedMaterial != null)
                    boton.colorBaseOriginal = ObtenerColorBaseMaterial(boton.rendererBoton.sharedMaterial, boton.colorBaseAsignado);
                else
                    boton.colorBaseOriginal = boton.colorBaseAsignado;
            }
        }

        if (botonMaestro.rendererBoton != null)
        {
            if (botonMaestro.rendererBoton.sharedMaterial != null)
                botonMaestro.colorBaseOriginal = ObtenerColorBaseMaterial(botonMaestro.rendererBoton.sharedMaterial, botonMaestro.colorBaseAsignado);
            else
                botonMaestro.colorBaseOriginal = botonMaestro.colorBaseAsignado;
        }

        if (botonesVolumenFisicos.rendererSubirVolumen != null)
        {
            if (botonesVolumenFisicos.rendererSubirVolumen.sharedMaterial != null)
                botonesVolumenFisicos.colorBaseSubirOriginal = ObtenerColorBaseMaterial(botonesVolumenFisicos.rendererSubirVolumen.sharedMaterial, botonesVolumenFisicos.colorBaseSubirAsignado);
            else
                botonesVolumenFisicos.colorBaseSubirOriginal = botonesVolumenFisicos.colorBaseSubirAsignado;
        }

        if (botonesVolumenFisicos.rendererBajarVolumen != null)
        {
            if (botonesVolumenFisicos.rendererBajarVolumen.sharedMaterial != null)
                botonesVolumenFisicos.colorBaseBajarOriginal = ObtenerColorBaseMaterial(botonesVolumenFisicos.rendererBajarVolumen.sharedMaterial, botonesVolumenFisicos.colorBaseBajarAsignado);
            else
                botonesVolumenFisicos.colorBaseBajarOriginal = botonesVolumenFisicos.colorBaseBajarAsignado;
        }

        // 2. CONFIGURACIÓN INICIAL (Play)
        estaEncendido = true;

        if (barrasVolumen != null && barrasVolumen.Length > 0)
        {
            volumenObjetivoGlobal = ObtenerVolumenInicial();
        }
        else
        {
            volumenObjetivoGlobal = ObtenerVolumenInicial();
        }

        indiceCancionActiva = 0;
        indiceCancionMemoria = indiceCancionActiva;
        volumenMemoria = volumenObjetivoGlobal;

        audioSourceMusica.loop = true;

        // Aplicamos la atenuación desde el primer frame
        audioSourceMusica.volume = volumenObjetivoGlobal * atenuacionVolumenGlobal;

        if (canciones != null && canciones.Length > indiceCancionActiva && canciones[indiceCancionActiva] != null)
        {
            audioSourceMusica.clip = canciones[indiceCancionActiva];
            ConfigurarEspacializacion(botonesCanciones[indiceCancionActiva].comportamentEspacial);
            audioSourceMusica.Play();
        }

        ActualizarVisualBotonMaestro();
        ActualizarVisualBotonesMusica();
        ActualizarVisualBotonesVolumenFisicos();
        ActualizarBarrasVolumen(volumenObjetivoGlobal);
        EnviarEstadoMusica();
    }

    // Método optimizado: Si Android devuelve nulo o negro absoluto, rescata el color manual asignado
    private Color ObtenerColorBaseMaterial(Material mat, Color colorRespaldo)
    {
        Color c = Color.black;
        if (mat.HasProperty("_BaseColor")) c = mat.GetColor("_BaseColor");
        else if (mat.HasProperty("_Color")) c = mat.GetColor("_Color");

        if (c.r == 0f && c.g == 0f && c.b == 0f) return colorRespaldo;
        return c;
    }

    public void AlternarEstadoSistema()
    {
        if (!SceneManager.PermitirCambioActivacionElemento(gameObject, estaEncendido))
            return;

        estaEncendido = !estaEncendido;
        ReproducirSonidoFeedback();

        if (!estaEncendido)
        {
            volumenMemoria = volumenObjetivoGlobal;
            indiceCancionMemoria = indiceCancionActiva;

            if (audioSourceMusica.isPlaying) audioSourceMusica.Pause();
            ApagarEmisionTotalVisual();
        }
        else
        {
            volumenObjetivoGlobal = volumenMemoria;
            indiceCancionActiva = indiceCancionMemoria;

            if (indiceCancionActiva != -1 && canciones != null && canciones.Length > indiceCancionActiva)
            {
                audioSourceMusica.clip = canciones[indiceCancionActiva];
                ConfigurarEspacializacion(botonesCanciones[indiceCancionActiva].comportamentEspacial);

                if (volumenObjetivoGlobal > 0f)
                {
                    audioSourceMusica.Play();
                    estaEnPausaPorVolumenCero = false;
                }
            }

            ActualizarVisualBotonesMusica();
            ActualizarVisualBotonesVolumenFisicos();
            ActualizarBarrasVolumen(volumenObjetivoGlobal);
        }

        ActualizarVisualBotonMaestro();
        EnviarEstadoMusica();
    }

    public void SeleccionarCancion(int indice)
    {
        if (!estaEncendido) return;
        if (canciones == null || indice < 0 || indice >= canciones.Length) return;

        ReproducirSonidoFeedback();

        if (indiceCancionActiva == indice && audioSourceMusica.isPlaying) return;

        indiceCancionActiva = indice;
        indiceCancionMemoria = indice;

        ActualizarVisualBotonesMusica();

        if (coroutineTransicion != null) StopCoroutine(coroutineTransicion);
        coroutineTransicion = StartCoroutine(TransicionCambioCancion(canciones[indice], botonesCanciones[indice].comportamentEspacial));
        EnviarEstadoMusica();
    }

    public void SubirVolumen()
    {
        if (!estaEncendido) return;
        ReproducirSonidoFeedback();
        EstablecerNivelVolumen(ObtenerNivelVolumenActual() + 1, true);
    }

    public void BajarVolumen()
    {
        if (!estaEncendido) return;
        ReproducirSonidoFeedback();
        EstablecerNivelVolumen(ObtenerNivelVolumenActual() - 1, true);
    }

    public void SeleccionarCancionPorNombre(string nombreCancion)
    {
        int indice = ObtenerIndiceCancion(nombreCancion);
        if (indice >= 0)
            SeleccionarCancion(indice);
    }

    public void EstablecerNivelVolumen(int nivel)
    {
        EstablecerNivelVolumen(nivel, true);
    }

    private void EstablecerNivelVolumen(int nivel, bool notificar)
    {
        int totalNiveles = ObtenerTotalNivelesVolumen();
        int nivelNormalizado = Mathf.Clamp(nivel, 1, totalNiveles);
        volumenObjetivoGlobal = ObtenerLimiteSuperiorVolumen() * (nivelNormalizado / (float)totalNiveles);
        volumenMemoria = volumenObjetivoGlobal;

        ActualizarBarrasVolumen(volumenObjetivoGlobal);
        ActualizarVisualBotonesVolumenFisicos();

        if (estaEnPausaPorVolumenCero && volumenObjetivoGlobal > 0f && indiceCancionActiva != -1)
        {
            estaEnPausaPorVolumenCero = false;
            if (!audioSourceMusica.isPlaying) audioSourceMusica.Play();
        }
        else if (volumenObjetivoGlobal <= 0f && audioSourceMusica.isPlaying)
        {
            audioSourceMusica.Pause();
            estaEnPausaPorVolumenCero = true;
        }

        if (notificar)
            EnviarEstadoMusica();
    }
    public void SeleccionarSiguienteCancion()
    {
        if (canciones == null || canciones.Length == 0)
            return;

        int siguiente = indiceCancionActiva < 0 ? 0 : (indiceCancionActiva + 1) % canciones.Length;
        SeleccionarCancion(siguiente);
    }

    public void SeleccionarCancionAnterior()
    {
        if (canciones == null || canciones.Length == 0)
            return;

        int anterior = indiceCancionActiva <= 0 ? canciones.Length - 1 : indiceCancionActiva - 1;
        SeleccionarCancion(anterior);
    }

    private void ActualizarVisualBotonesMusica()
    {
        for (int i = 0; i < botonesCanciones.Length; i++)
        {
            if (botonesCanciones[i].rendererBoton != null)
            {
                botonesCanciones[i].rendererBoton.GetPropertyBlock(bloquePropiedades);
                float intensidad = (i == indiceCancionActiva) ? botonesCanciones[i].intensidadActivo : botonesCanciones[i].intensidadApagado;
                Color colorFinal = botonesCanciones[i].colorBaseOriginal * intensidad;
                colorFinal.a = 1f;
                bloquePropiedades.SetColor("_EmissionColor", colorFinal);
                botonesCanciones[i].rendererBoton.SetPropertyBlock(bloquePropiedades);
            }
        }
    }

    private void ActualizarBarrasVolumen(float volumenReferencia)
    {
        if (barrasVolumen == null || barrasVolumen.Length == 0) return;

        float proporcion = Mathf.Clamp01(volumenReferencia / ObtenerLimiteSuperiorVolumen());
        int barrasAEncender = Mathf.RoundToInt(proporcion * barrasVolumen.Length);

        for (int i = 0; i < barrasVolumen.Length; i++)
        {
            if (barrasVolumen[i].rendererBarra != null)
            {
                barrasVolumen[i].rendererBarra.GetPropertyBlock(bloquePropiedades);
                Color colorFinal = (i < barrasAEncender) ? barrasVolumen[i].colorEncendido : barrasVolumen[i].colorApagado;
                bloquePropiedades.SetColor("_EmissionColor", colorFinal);
                barrasVolumen[i].rendererBarra.SetPropertyBlock(bloquePropiedades);
            }
        }
    }

    private float ObtenerLimiteSuperiorVolumen()
    {
        return Mathf.Max(1f, limiteSuperiorVolumen);
    }

    private float ObtenerPasoVolumen()
    {
        int cantidadBarras = barrasVolumen != null ? barrasVolumen.Length : 0;
        return cantidadBarras > 0 ? ObtenerLimiteSuperiorVolumen() / cantidadBarras : 0.1f;
    }

    public int ObtenerNivelVolumenActual()
    {
        int totalNiveles = ObtenerTotalNivelesVolumen();
        float proporcion = Mathf.Clamp01(volumenObjetivoGlobal / ObtenerLimiteSuperiorVolumen());
        return Mathf.Clamp(Mathf.RoundToInt(proporcion * totalNiveles), 1, totalNiveles);
    }

    private int ObtenerTotalNivelesVolumen()
    {
        return barrasVolumen != null && barrasVolumen.Length > 0 ? barrasVolumen.Length : 6;
    }
    private float ObtenerVolumenInicial()
    {
        float limite = ObtenerLimiteSuperiorVolumen();
        float minimoAudible = Mathf.Min(limite, ObtenerPasoVolumen() * 2f);
        return Mathf.Clamp(limite * volumenInicialNormalizado, minimoAudible, limite);
    }

    private void ActualizarVisualBotonMaestro()
    {
        if (botonMaestro.rendererBoton != null)
        {
            botonMaestro.rendererBoton.GetPropertyBlock(bloquePropiedades);
            float intensidadActual = estaEncendido ? botonMaestro.intensidadOn : botonMaestro.intensidadOff;
            Color colorFinal = botonMaestro.colorBaseOriginal * intensidadActual;
            colorFinal.a = 1f;
            bloquePropiedades.SetColor("_EmissionColor", colorFinal);
            botonMaestro.rendererBoton.SetPropertyBlock(bloquePropiedades);
        }
    }

    private void ActualizarVisualBotonesVolumenFisicos()
    {
        float intensidadAjuste = estaEncendido ? botonesVolumenFisicos.intensidadOn : 0f;

        if (botonesVolumenFisicos.rendererSubirVolumen != null)
        {
            botonesVolumenFisicos.rendererSubirVolumen.GetPropertyBlock(bloquePropiedades);
            Color col = botonesVolumenFisicos.colorBaseSubirOriginal * intensidadAjuste;
            col.a = 1f;
            bloquePropiedades.SetColor("_EmissionColor", col);
            botonesVolumenFisicos.rendererSubirVolumen.SetPropertyBlock(bloquePropiedades);
        }
        if (botonesVolumenFisicos.rendererBajarVolumen != null)
        {
            botonesVolumenFisicos.rendererBajarVolumen.GetPropertyBlock(bloquePropiedades);
            Color col = botonesVolumenFisicos.colorBaseBajarOriginal * intensidadAjuste;
            col.a = 1f;
            bloquePropiedades.SetColor("_EmissionColor", col);
            botonesVolumenFisicos.rendererBajarVolumen.SetPropertyBlock(bloquePropiedades);
        }
    }

    private void ApagarEmisionTotalVisual()
    {
        if (bloquePropiedades == null) bloquePropiedades = new MaterialPropertyBlock();

        for (int i = 0; i < botonesCanciones.Length; i++)
        {
            if (botonesCanciones[i].rendererBoton != null)
            {
                botonesCanciones[i].rendererBoton.GetPropertyBlock(bloquePropiedades);
                bloquePropiedades.SetColor("_EmissionColor", Color.black);
                botonesCanciones[i].rendererBoton.SetPropertyBlock(bloquePropiedades);
            }
        }

        for (int i = 0; i < barrasVolumen.Length; i++)
        {
            if (barrasVolumen[i].rendererBarra != null)
            {
                barrasVolumen[i].rendererBarra.GetPropertyBlock(bloquePropiedades);
                bloquePropiedades.SetColor("_EmissionColor", Color.black);
                barrasVolumen[i].rendererBarra.SetPropertyBlock(bloquePropiedades);
            }
        }

        ActualizarVisualBotonesVolumenFisicos();
    }

    private void ReproducirSonidoFeedback()
    {
        if (!SceneManager.EstaAplicandoSeleccionAutomatica && audioSourceEfectos != null && sonidoClicBoton != null)
        {
            audioSourceEfectos.PlayOneShot(sonidoClicBoton, Mathf.Min(volumenClic, 0.35f));
        }
    }

    private int ObtenerIndiceCancion(string nombreCancion)
    {
        switch (NormalizarNombreCancion(nombreCancion))
        {
            case "INSTRUMENTAL": return 0;
            case "ZEN": return 1;
            case "SMR": return 2;
            case "RITMICA": return 3;
            case "AIGUA": return 4;
            default: return -1;
        }
    }

    private string ObtenerNombreCancionActual()
    {
        switch (indiceCancionActiva)
        {
            case 0: return "INSTRUMENTAL";
            case 1: return "ZEN";
            case 2: return "SMR";
            case 3: return "RITMICA";
            case 4: return "AIGUA";
            default: return string.Empty;
        }
    }

    private string NormalizarNombreCancion(string nombreCancion)
    {
        if (string.IsNullOrWhiteSpace(nombreCancion))
            return string.Empty;

        return nombreCancion.Trim()
            .Replace("c_", string.Empty)
            .Replace("C_", string.Empty)
            .Replace("à", "a")
            .Replace("À", "A")
            .ToUpperInvariant();
    }

    private void EnviarEstadoMusica()
    {
        if (SceneManager.EstaAplicandoSeleccionAutomatica)
            return;

        if (connectionCache == null)
            connectionCache = FindFirstObjectByType<Connection>();

        if (connectionCache == null || !connectionCache.connected)
            return;

        string nombreCancion = ObtenerNombreCancionActual();
        if (!string.IsNullOrEmpty(nombreCancion))
            connectionCache.Send("CMD_MUSIC_SONG:" + nombreCancion);

        connectionCache.Send("CMD_MUSIC_VOLUME:" + ObtenerNivelVolumenActual());
    }
    private void ConfigurarEspacializacion(TipusEspacialitzacio modo)
    {
        audioSourceMusica.minDistance = 2f;
        audioSourceMusica.maxDistance = 12f;
        audioSourceMusica.rolloffMode = AudioRolloffMode.Linear;

        switch (modo)
        {
            case TipusEspacialitzacio.Mode2D_Envoltant:
                audioSourceMusica.spatialBlend = 0f;
                audioSourceMusica.spatialize = false;
                break;
            case TipusEspacialitzacio.Mode3D_DirecteAltaveu:
                audioSourceMusica.spatialBlend = 1f;
                audioSourceMusica.spatialize = true;
                audioSourceMusica.spread = 0f;
                break;
            case TipusEspacialitzacio.Mode3D_AmbDispersioAmplia:
                audioSourceMusica.spatialBlend = 1f;
                audioSourceMusica.spatialize = true;
                audioSourceMusica.spread = 60f;
                break;
        }
    }

    private IEnumerator TransicionCambioCancion(AudioClip nuevaCancion, TipusEspacialitzacio nuevoModo)
    {
        float volumenInicial = audioSourceMusica.volume;
        float tiempo = 0f;

        while (tiempo < tiempoTransicionAudio)
        {
            tiempo += Time.deltaTime;
            // Corregido también con la atenuación global
            audioSourceMusica.volume = Mathf.Lerp(volumenInicial, 0f, tiempo / tiempoTransicionAudio);
            yield return null;
        }

        audioSourceMusica.Stop();
        audioSourceMusica.clip = nuevaCancion;
        ConfigurarEspacializacion(nuevoModo);

        if (nuevaCancion != null && !estaEnPausaPorVolumenCero && estaEncendido)
        {
            audioSourceMusica.Play();
        }

        tiempo = 0f;
        while (tiempo < tiempoTransicionAudio)
        {
            tiempo += Time.deltaTime;
            float maxVolumen = estaEnPausaPorVolumenCero ? 0f : (volumenObjetivoGlobal * atenuacionVolumenGlobal);
            audioSourceMusica.volume = Mathf.Lerp(0f, maxVolumen, tiempo / tiempoTransicionAudio);
            yield return null;
        }
    }

    void Update()
    {
        if (!estaEncendido) return;

        if (!estaEnPausaPorVolumenCero)
        {
            // Aplicamos la atenuación global de forma continua en el suavizado
            float volumenDestinoSaturado = volumenObjetivoGlobal * atenuacionVolumenGlobal;
            audioSourceMusica.volume = Mathf.MoveTowards(audioSourceMusica.volume, volumenDestinoSaturado, velocidadCambioVolumen * Time.deltaTime);
        }
    }
}

/*using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ControladorMultisensorial : MonoBehaviour
{
    public enum TipusEspacialitzacio { Mode2D_Envoltant, Mode3D_DirecteAltaveu, Mode3D_AmbDispersioAmplia }

    [System.Serializable]
    public struct BarraVolumen
    {
        public Renderer rendererBarra;
        [ColorUsage(true, true)] public Color colorEncendido;
        [ColorUsage(true, true)] public Color colorApagado;
    }

    [System.Serializable]
    public class BotonMusica
    {
        public Renderer rendererBoton;
        [Tooltip("Intensidad de emisión cuando el sistema está ON y la canción está SELECCIONADA")]
        public float intensidadActivo = 2.5f;
        [Tooltip("Intensidad de emisión cuando el sistema está ON y la canción está EN REPOSO")]
        public float intensidadApagado = 0.2f;
        public TipusEspacialitzacio comportamentEspacial;
        [HideInInspector] public Color colorBaseOriginal;
    }

    [System.Serializable]
    public class BotonEncendidoMaestro
    {
        public Renderer rendererBoton;
        public float intensidadOn = 3.0f;
        public float intensidadOff = 0.3f;
        [HideInInspector] public Color colorBaseOriginal;
    }

    [System.Serializable]
    public class BotonesControlVolumen
    {
        public Renderer rendererSubirVolumen;
        public Renderer rendererBajarVolumen;
        [Tooltip("Intensidad de emisión fija de estos botones cuando el sistema está ON")]
        public float intensidadOn = 2.0f;
        [HideInInspector] public Color colorBaseSubirOriginal;
        [HideInInspector] public Color colorBaseBajarOriginal;
    }

    [Header("Configuración de Audio (Música)")]
    [SerializeField] private AudioClip[] canciones;
    [SerializeField] private float tiempoTransicionAudio = 1.5f;
    [SerializeField] private float velocidadCambioVolumen = 2f;

    [Header("Feedback de Pulsación (SFX)")]
    [SerializeField] private AudioClip sonidoClicBoton;
    [SerializeField][Range(0f, 1f)] private float volumenClic = 0.6f;
    private AudioSource audioSourceEfectos;

    [Header("Elementos de la Interfaz (Renderers)")]
    [SerializeField] private BotonMusica[] botonesCanciones;
    [SerializeField] private BarraVolumen[] barrasVolumen;

    [Header("Botón Maestro de Encendido")]
    [SerializeField] private BotonEncendidoMaestro botonMaestro;
    private bool estaEncendido = true;

    [Header("Botones Físicos de Ajuste de Volumen")]
    [SerializeField] private BotonesControlVolumen botonesVolumenFisicos;

    // Variables de control y memoria
    private AudioSource audioSourceMusica;
    private float volumenObjetivoGlobal = 0.1f;
    private int indiceCancionActiva = 0;         // CAMBIADO: Por defecto arranca en la posición 0 (Clásica)
    private int indiceCancionMemoria = 0;
    private float volumenMemoria = 0.1f;
    private bool estaEnPausaPorVolumenCero = false;
    private Coroutine coroutineTransicion;

    private MaterialPropertyBlock bloquePropiedades;

    void Awake()
    {
        audioSourceMusica = GetComponent<AudioSource>();
        bloquePropiedades = new MaterialPropertyBlock();

        audioSourceEfectos = gameObject.AddComponent<AudioSource>();
        audioSourceEfectos.spatialBlend = 1f;
    }

    void Start()
    {
        // 1. GUARDAR COLORES BASE ORIGINALES PARA EL SISTEMA EMISIVO
        // Canciones
        foreach (var boton in botonesCanciones)
        {
            if (boton.rendererBoton != null && boton.rendererBoton.sharedMaterial != null)
            {
                boton.colorBaseOriginal = ObtenerColorBaseMaterial(boton.rendererBoton.sharedMaterial);
            }
        }
        // Botón Maestro
        if (botonMaestro.rendererBoton != null && botonMaestro.rendererBoton.sharedMaterial != null)
        {
            botonMaestro.colorBaseOriginal = ObtenerColorBaseMaterial(botonMaestro.rendererBoton.sharedMaterial);
        }
        // Botones de Volumen
        if (botonesVolumenFisicos.rendererSubirVolumen != null && botonesVolumenFisicos.rendererSubirVolumen.sharedMaterial != null)
        {
            botonesVolumenFisicos.colorBaseSubirOriginal = ObtenerColorBaseMaterial(botonesVolumenFisicos.rendererSubirVolumen.sharedMaterial);
        }
        if (botonesVolumenFisicos.rendererBajarVolumen != null && botonesVolumenFisicos.rendererBajarVolumen.sharedMaterial != null)
        {
            botonesVolumenFisicos.colorBaseBajarOriginal = ObtenerColorBaseMaterial(botonesVolumenFisicos.rendererBajarVolumen.sharedMaterial);
        }

        // 2. CONFIGURACIÓN INICIAL DE REQUISITOS (Play)
        estaEncendido = true;

        // Calcular volumen al mínimo (1 barra iluminada)
        if (barrasVolumen != null && barrasVolumen.Length > 0)
        {
            volumenObjetivoGlobal = ObtenerVolumenInicial();
        }
        else
        {
            volumenObjetivoGlobal = ObtenerVolumenInicial();
        }

        indiceCancionActiva = 0; // Posición 0 = "Clasica"
        indiceCancionMemoria = indiceCancionActiva;
        volumenMemoria = volumenObjetivoGlobal;

        // 3. Preparación y arranque del AudioSource
        audioSourceMusica.loop = true;
        audioSourceMusica.volume = volumenObjetivoGlobal;

        if (canciones != null && canciones.Length > indiceCancionActiva && canciones[indiceCancionActiva] != null)
        {
            audioSourceMusica.clip = canciones[indiceCancionActiva];
            ConfigurarEspacializacion(botonesCanciones[indiceCancionActiva].comportamentEspacial);
            audioSourceMusica.Play();
        }

        // 4. Refresco de la iluminación HDR de toda la botonera
        ActualizarVisualBotonMaestro();
        ActualizarVisualBotonesMusica();
        ActualizarVisualBotonesVolumenFisicos();
        ActualizarBarrasVolumen(volumenObjetivoGlobal);
    }

    private Color ObtenerColorBaseMaterial(Material mat)
    {
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
        return Color.white;
    }

    public void AlternarEstadoSistema()
    {
        if (!SceneManager.PermitirCambioActivacionElemento(gameObject, estaEncendido))
            return;

        estaEncendido = !estaEncendido;
        ReproducirSonidoFeedback(reproducirClickBotonMaestro);

        if (!estaEncendido)
        {
            // AL APAGAR: Memorizar y pausar
            volumenMemoria = volumenObjetivoGlobal;
            indiceCancionMemoria = indiceCancionActiva;

            if (audioSourceMusica.isPlaying) audioSourceMusica.Pause();

            // Apagamos absolutamente todo a negro de forma inmediata
            ApagarEmisionTotalVisual();
        }
        else
        {
            // AL ENCENDER: Recuperar memoria
            volumenObjetivoGlobal = volumenMemoria;
            indiceCancionActiva = indiceCancionMemoria;

            if (indiceCancionActiva != -1 && canciones != null && canciones.Length > indiceCancionActiva)
            {
                audioSourceMusica.clip = canciones[indiceCancionActiva];
                ConfigurarEspacializacion(botonesCanciones[indiceCancionActiva].comportamentEspacial);

                if (volumenObjetivoGlobal > 0f)
                {
                    audioSourceMusica.Play();
                    estaEnPausaPorVolumenCero = false;
                }
            }

            // Restaurar visuales HDR
            ActualizarVisualBotonesMusica();
            ActualizarVisualBotonesVolumenFisicos();
            ActualizarBarrasVolumen(volumenObjetivoGlobal);
        }

        ActualizarVisualBotonMaestro();
    }

    public void SeleccionarCancion(int indice)
    {
        if (!estaEncendido) return;
        if (canciones == null || indice < 0 || indice >= canciones.Length) return;

        ReproducirSonidoFeedback(reproducirClickBotonesCancion);

        if (indiceCancionActiva == indice && audioSourceMusica.isPlaying) return;

        indiceCancionActiva = indice;
        indiceCancionMemoria = indice;

        ActualizarVisualBotonesMusica();

        if (coroutineTransicion != null) StopCoroutine(coroutineTransicion);
        coroutineTransicion = StartCoroutine(TransicionCambioCancion(canciones[indice], botonesCanciones[indice].comportamentEspacial));
    }

    public void SubirVolumen()
    {
        if (!estaEncendido) return;
        ReproducirSonidoFeedback(reproducirClickBotonesVolumen);

        float paso = ObtenerPasoVolumen();
        volumenObjetivoGlobal = Mathf.Clamp(volumenObjetivoGlobal + paso, 0f, ObtenerLimiteSuperiorVolumen());
        volumenMemoria = volumenObjetivoGlobal;

        ActualizarBarrasVolumen(volumenObjetivoGlobal);

        if (estaEnPausaPorVolumenCero && volumenObjetivoGlobal > 0f && indiceCancionActiva != -1)
        {
            estaEnPausaPorVolumenCero = false;
            if (!audioSourceMusica.isPlaying) audioSourceMusica.Play();
        }
    }

    public void BajarVolumen()
    {
        if (!estaEncendido) return;
        ReproducirSonidoFeedback(reproducirClickBotonesVolumen);

        float paso = ObtenerPasoVolumen();
        volumenObjetivoGlobal = Mathf.Clamp(volumenObjetivoGlobal - paso, 0f, ObtenerLimiteSuperiorVolumen());
        volumenMemoria = volumenObjetivoGlobal;

        ActualizarBarrasVolumen(volumenObjetivoGlobal);

        if (volumenObjetivoGlobal <= 0f && audioSourceMusica.isPlaying)
        {
            audioSourceMusica.Pause();
            estaEnPausaPorVolumenCero = true;
        }
    }

    public void SeleccionarSiguienteCancion()
    {
        if (canciones == null || canciones.Length == 0)
            return;

        int siguiente = indiceCancionActiva < 0 ? 0 : (indiceCancionActiva + 1) % canciones.Length;
        SeleccionarCancion(siguiente);
    }

    public void SeleccionarCancionAnterior()
    {
        if (canciones == null || canciones.Length == 0)
            return;

        int anterior = indiceCancionActiva <= 0 ? canciones.Length - 1 : indiceCancionActiva - 1;
        SeleccionarCancion(anterior);
    }

    public void EjecutarComandoTablet(string comando)
    {
        if (string.IsNullOrWhiteSpace(comando))
            return;

        string comandoNormalizado = comando.Trim().ToUpperInvariant();
        if (comandoNormalizado == "TOGGLE")
            AlternarEstadoSistema();
        else if (comandoNormalizado == "NEXT")
            SeleccionarSiguienteCancion();
        else if (comandoNormalizado == "PREV")
            SeleccionarCancionAnterior();
        else if (comandoNormalizado == "VOL_UP")
            SubirVolumen();
        else if (comandoNormalizado == "VOL_DOWN")
            BajarVolumen();
        else if (comandoNormalizado.StartsWith("SONG:"))
        {
            string rawIndex = comandoNormalizado.Substring("SONG:".Length);
            if (int.TryParse(rawIndex, out int indice))
                SeleccionarCancion(indice);
        }
    }

    public void SeleccionarSiguienteCancion()
    {
        if (canciones == null || canciones.Length == 0)
            return;

        int siguiente = indiceCancionActiva < 0 ? 0 : (indiceCancionActiva + 1) % canciones.Length;
        SeleccionarCancion(siguiente);
    }

    public void SeleccionarCancionAnterior()
    {
        if (canciones == null || canciones.Length == 0)
            return;

        int anterior = indiceCancionActiva <= 0 ? canciones.Length - 1 : indiceCancionActiva - 1;
        SeleccionarCancion(anterior);
    }

    public void EjecutarComandoTablet(string comando)
    {
        if (string.IsNullOrWhiteSpace(comando))
            return;

        string comandoNormalizado = comando.Trim().ToUpperInvariant();
        if (comandoNormalizado == "TOGGLE")
            AlternarEstadoSistema();
        else if (comandoNormalizado == "NEXT")
            SeleccionarSiguienteCancion();
        else if (comandoNormalizado == "PREV")
            SeleccionarCancionAnterior();
        else if (comandoNormalizado == "VOL_UP")
            SubirVolumen();
        else if (comandoNormalizado == "VOL_DOWN")
            BajarVolumen();
        else if (comandoNormalizado.StartsWith("SONG:"))
        {
            string rawIndex = comandoNormalizado.Substring("SONG:".Length);
            if (int.TryParse(rawIndex, out int indice))
                SeleccionarCancion(indice);
        }
    }

    private void ActualizarVisualBotonesMusica()
    {
        for (int i = 0; i < botonesCanciones.Length; i++)
        {
            if (botonesCanciones[i].rendererBoton != null)
            {
                botonesCanciones[i].rendererBoton.GetPropertyBlock(bloquePropiedades);
                float intensidad = (i == indiceCancionActiva) ? botonesCanciones[i].intensidadActivo : botonesCanciones[i].intensidadApagado;
                Color colorFinal = botonesCanciones[i].colorBaseOriginal * intensidad;
                colorFinal.a = 1f;
                bloquePropiedades.SetColor("_EmissionColor", colorFinal);
                botonesCanciones[i].rendererBoton.SetPropertyBlock(bloquePropiedades);
            }
        }
    }

    private void ActualizarBarrasVolumen(float volumenReferencia)
    {
        if (barrasVolumen == null || barrasVolumen.Length == 0) return;

        float proporcion = Mathf.Clamp01(volumenReferencia / ObtenerLimiteSuperiorVolumen());
        int barrasAEncender = Mathf.RoundToInt(proporcion * barrasVolumen.Length);

        for (int i = 0; i < barrasVolumen.Length; i++)
        {
            if (barrasVolumen[i].rendererBarra != null)
            {
                barrasVolumen[i].rendererBarra.GetPropertyBlock(bloquePropiedades);
                Color colorFinal = (i < barrasAEncender) ? barrasVolumen[i].colorEncendido : barrasVolumen[i].colorApagado;
                bloquePropiedades.SetColor("_EmissionColor", colorFinal);
                barrasVolumen[i].rendererBarra.SetPropertyBlock(bloquePropiedades);
            }
        }
    }

    private float ObtenerLimiteSuperiorVolumen()
    {
        return Mathf.Max(1f, limiteSuperiorVolumen);
    }

    private float ObtenerPasoVolumen()
    {
        int cantidadBarras = barrasVolumen != null ? barrasVolumen.Length : 0;
        return cantidadBarras > 0 ? ObtenerLimiteSuperiorVolumen() / cantidadBarras : 0.1f;
    }

    private float ObtenerVolumenInicial()
    {
        float limite = ObtenerLimiteSuperiorVolumen();
        float minimoAudible = Mathf.Min(limite, ObtenerPasoVolumen() * 2f);
        return Mathf.Clamp(limite * volumenInicialNormalizado, minimoAudible, limite);
    }

    private void ActualizarVisualBotonMaestro()
    {
        if (botonMaestro.rendererBoton != null)
        {
            botonMaestro.rendererBoton.GetPropertyBlock(bloquePropiedades);
            float intensidadActual = estaEncendido ? botonMaestro.intensidadOn : botonMaestro.intensidadOff;
            Color colorFinal = botonMaestro.colorBaseOriginal * intensidadActual;
            colorFinal.a = 1f;
            bloquePropiedades.SetColor("_EmissionColor", colorFinal);
            botonMaestro.rendererBoton.SetPropertyBlock(bloquePropiedades);
        }
    }

    private void ActualizarVisualBotonesVolumenFisicos()
    {
        if (botonesVolumenFisicos.rendererSubirVolumen != null)
        {
            botonesVolumenFisicos.rendererSubirVolumen.GetPropertyBlock(bloquePropiedades);
            Color col = botonesVolumenFisicos.colorBaseSubirOriginal * (estaEncendido ? botonesVolumenFisicos.intensidadOn : 0f);
            col.a = 1f;
            bloquePropiedades.SetColor("_EmissionColor", col);
            botonesVolumenFisicos.rendererSubirVolumen.SetPropertyBlock(bloquePropiedades);
        }
        if (botonesVolumenFisicos.rendererBajarVolumen != null)
        {
            botonesVolumenFisicos.rendererBajarVolumen.GetPropertyBlock(bloquePropiedades);
            Color col = botonesVolumenFisicos.colorBaseBajarOriginal * (estaEncendido ? botonesVolumenFisicos.intensidadOn : 0f);
            col.a = 1f;
            bloquePropiedades.SetColor("_EmissionColor", col);
            botonesVolumenFisicos.rendererBajarVolumen.SetPropertyBlock(bloquePropiedades);
        }
    }

    private void ApagarEmisionTotalVisual()
    {
        if (bloquePropiedades == null) bloquePropiedades = new MaterialPropertyBlock();

        // 1. Apagar botones de música (Negro absoluto)
        for (int i = 0; i < botonesCanciones.Length; i++)
        {
            if (botonesCanciones[i].rendererBoton != null)
            {
                botonesCanciones[i].rendererBoton.GetPropertyBlock(bloquePropiedades);
                bloquePropiedades.SetColor("_EmissionColor", Color.black);
                botonesCanciones[i].rendererBoton.SetPropertyBlock(bloquePropiedades);
            }
        }

        // 2. Apagar indicadores de la pared (barras de volumen)
        for (int i = 0; i < barrasVolumen.Length; i++)
        {
            if (barrasVolumen[i].rendererBarra != null)
            {
                barrasVolumen[i].rendererBarra.GetPropertyBlock(bloquePropiedades);
                bloquePropiedades.SetColor("_EmissionColor", Color.black);
                barrasVolumen[i].rendererBarra.SetPropertyBlock(bloquePropiedades);
            }
        }

        // 3. Apagar físicamente los botones de subir y bajar volumen
        if (botonesVolumenFisicos.rendererSubirVolumen != null)
        {
            botonesVolumenFisicos.rendererSubirVolumen.GetPropertyBlock(bloquePropiedades);
            bloquePropiedades.SetColor("_EmissionColor", Color.black);
            botonesVolumenFisicos.rendererSubirVolumen.SetPropertyBlock(bloquePropiedades);
        }
        if (botonesVolumenFisicos.rendererBajarVolumen != null)
        {
            botonesVolumenFisicos.rendererBajarVolumen.GetPropertyBlock(bloquePropiedades);
            bloquePropiedades.SetColor("_EmissionColor", Color.black);
            botonesVolumenFisicos.rendererBajarVolumen.SetPropertyBlock(bloquePropiedades);
        }
    }

    private void ReproducirSonidoFeedback(bool permitido)
    {
        if (!permitido)
            return;

        if (!SceneManager.EstaAplicandoSeleccionAutomatica && audioSourceEfectos != null && sonidoClicBoton != null)
        {
            audioSourceEfectos.PlayOneShot(sonidoClicBoton, Mathf.Min(volumenClic, 0.35f));
        }
    }

    private void ConfigurarEspacializacion(TipusEspacialitzacio modo)
    {

        switch (modo)
        {
            case TipusEspacialitzacio.Mode2D_Envoltant:
                audioSourceMusica.spatialBlend = 0f;
                audioSourceMusica.spatialize = false;
                break;
            case TipusEspacialitzacio.Mode3D_DirecteAltaveu:
                audioSourceMusica.spatialBlend = 1f;
                audioSourceMusica.spatialize = true;
                audioSourceMusica.spread = 0f;
                break;
            case TipusEspacialitzacio.Mode3D_AmbDispersioAmplia:
                audioSourceMusica.spatialBlend = 1f;
                audioSourceMusica.spatialize = true;
                audioSourceMusica.spread = 60f;
                break;
        }
    }

    private IEnumerator TransicionCambioCancion(AudioClip nuevaCancion, TipusEspacialitzacio nuevoModo)
    {
        float volumenInicial = audioSourceMusica.volume;
        float tiempo = 0f;

        while (tiempo < tiempoTransicionAudio)
        {
            tiempo += Time.deltaTime;
            audioSourceMusica.volume = Mathf.Lerp(volumenInicial, 0f, tiempo / tiempoTransicionAudio);
            yield return null;
        }

        audioSourceMusica.Stop();
        audioSourceMusica.clip = nuevaCancion;
        ConfigurarEspacializacion(nuevoModo);

        if (nuevaCancion != null && !estaEnPausaPorVolumenCero && estaEncendido)
        {
            audioSourceMusica.Play();
        }

        tiempo = 0f;
        while (tiempo < tiempoTransicionAudio)
        {
            tiempo += Time.deltaTime;
            float maxVolumen = estaEnPausaPorVolumenCero ? 0f : volumenObjetivoGlobal;
            audioSourceMusica.volume = Mathf.Lerp(0f, maxVolumen, tiempo / tiempoTransicionAudio);
            yield return null;
        }
    }

    void Update()
    {
        // SOLUCCIÓN CRÍTICA: Si el sistema está apagado, salimos inmediatamente del Update 
        // para que no se recalculen volúmenes ni se reactiven luces por error
        if (!estaEncendido) return;

        if (!estaEnPausaPorVolumenCero)
        {
            audioSourceMusica.volume = Mathf.MoveTowards(audioSourceMusica.volume, volumenObjetivoGlobal, velocidadCambioVolumen * Time.deltaTime);
        }
    }
}*/