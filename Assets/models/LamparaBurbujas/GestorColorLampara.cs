
using UnityEngine;
using System.Collections.Generic;

public class GestorColorLampara : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip sonidoClick;
    [Tooltip("Arrastra aquí el objeto vacío que tiene el AudioSource con el sonido de burbujas en bucle")]
    public AudioSource audioBurbujasLoop; // <--- ¡NUEVA VARIABLE!

    [System.Serializable]
    public class ConfiguracionBoton
    {
        [Tooltip("Escribe el nombre EXACTO que pasa el Poke por parámetro (ej: CyanGaseoso, AmbarLava, RosaNeon)")]
        public string nombreIdentificador;
        public Renderer rendererBoton;

        [Tooltip("Intensidad de emisión cuando la lámpara está ON y el botón está SELECCIONADO")]
        public float intensidadActivo = 2.5f;
        [Tooltip("Intensidad de emisión cuando la lámpara está ON y el botón está EN REPOSO")]
        public float intensidadApagado = 0.2f;
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

    public enum ColorPredefinido
    {
        CyanGaseoso, RosaNeon, VerdeRadioactivo, AmbarLava, VioletaProfundo, Blanco
    }

    [Header("Configuración de Nombres de Shaders")]
    public string referenciaShaderBoton = "_EmissionColor";
    public string referenciaShader = "_ColorBombolles";
    public string referenciaShaderEmission = "_EmissionColor";

    [Header("Configuración de Botones")]
    public List<ConfiguracionBoton> listaBotones;

    [Header("Botón Maestro de Encendido")]
    public BotonEncendidoMaestro botonMaestro;
    private bool estaEncendida = true;

    [Header("Selector de Color Lámpara")]
    [Tooltip("Color inicial por defecto al arrancar el juego")]
    public ColorPredefinido colorActivo = ColorPredefinido.CyanGaseoso;

    [Header("Ajustes de la Paleta (HDR)")]
    [ColorUsage(true, true)] public Color color1_Cyan = new Color(0, 1, 1, 2f);
    [ColorUsage(true, true)] public Color color2_Rosa = new Color(1, 0, 1, 2f);
    [ColorUsage(true, true)] public Color color3_Verde = new Color(0, 1, 0, 2f);
    [ColorUsage(true, true)] public Color color4_Ambar = new Color(1, 0.5f, 0, 2f);
    [ColorUsage(true, true)] public Color color5_Violeta = new Color(0.5f, 0, 1, 2f);
    [ColorUsage(true, true)] public Color color6_Blanco = new Color(1, 1, 1, 2f);

    [Header("Elementos de la Escena (Lámpara)")]
    public GameObject tuboInterior;
    public GameObject quadInterior;
    public GameObject tapaSuperficie;

    [Header("Luz")]
    public Light luzBase;

    private MaterialPropertyBlock bloquePropiedadesLampara;
    private MaterialPropertyBlock bloquePropiedadesBotones;

    void Awake()
    {
        AsegurarBloquesPropiedades();
    }

    void Start()
    {
        AsegurarBloquesPropiedades();

        // Guardar colores originales de los botones
        foreach (var boton in listaBotones)
        {
            if (boton.rendererBoton != null && boton.rendererBoton.sharedMaterial != null)
            {
                if (boton.rendererBoton.sharedMaterial.HasProperty("_BaseColor"))
                    boton.colorBaseOriginal = boton.rendererBoton.sharedMaterial.GetColor("_BaseColor");
                else if (boton.rendererBoton.sharedMaterial.HasProperty("_Color"))
                    boton.colorBaseOriginal = boton.rendererBoton.sharedMaterial.GetColor("_Color");
                else
                    boton.colorBaseOriginal = Color.white;
            }
        }

        // Guardar color original del botón maestro
        if (botonMaestro.rendererBoton != null && botonMaestro.rendererBoton.sharedMaterial != null)
        {
            if (botonMaestro.rendererBoton.sharedMaterial.HasProperty("_BaseColor"))
                botonMaestro.colorBaseOriginal = botonMaestro.rendererBoton.sharedMaterial.GetColor("_BaseColor");
            else if (botonMaestro.rendererBoton.sharedMaterial.HasProperty("_Color"))
                botonMaestro.colorBaseOriginal = botonMaestro.rendererBoton.sharedMaterial.GetColor("_Color");
            else
                botonMaestro.colorBaseOriginal = Color.white;
        }

        // ESTADO INICIAL: Forzar encendido y color por defecto al arrancar
        estaEncendida = true;

        // ARRANCAR EL AUDIO INICIALMENTE (Si está asignado)
        if (audioBurbujasLoop != null && !audioBurbujasLoop.isPlaying)
        {
            audioBurbujasLoop.Play();
        }

        ActualizarIluminacion();
        ActualizarVisualBotonMaestro();
        ActualizarVisualBotonesPaleta(); // Ilumina el botón activo inicial automáticamente
    }

    public void AlternarEstadoLampara()
    {
        if (!SceneManager.PermitirCambioActivacionElemento(gameObject, estaEncendida))
            return;

        estaEncendida = !estaEncendida;

        if (botonMaestro.rendererBoton != null)
        {
            AudioSource sourceMaestro = botonMaestro.rendererBoton.GetComponent<AudioSource>();
            if (!SceneManager.EstaAplicandoSeleccionAutomatica && sourceMaestro != null && sonidoClick != null) sourceMaestro.PlayOneShot(sonidoClick);
        }

        if (!estaEncendida)
        {
            // Lámpara OFF: Apagamos la emisión de todos los botones y pausamos el audio
            ApagarEmisionTotalBotones();
            if (audioBurbujasLoop != null) audioBurbujasLoop.Pause(); // <--- PAUSAR AUDIO
        }
        else
        {
            // Lámpara ON: Encendemos la paleta y reanudamos el audio
            ActualizarVisualBotonesPaleta();
            if (audioBurbujasLoop != null) audioBurbujasLoop.Play(); // <--- REANUDAR AUDIO
        }

        ActualizarIluminacion();
        ActualizarVisualBotonMaestro();
    }

    public void SeleccionarBoton(string nombreColor)
    {
        if (!estaEncendida) return;
        listaBotones.Find(b => b.nombreIdentificador.ToLower().Trim() == nombreColor.ToLower().Trim())?.rendererBoton?.GetComponent<AudioSource>()?.PlayOneShot(sonidoClick);
        CambiarColorPorNombre(nombreColor);
        ActualizarVisualBotonesPaleta();
    }

    private void ActualizarVisualBotonesPaleta()
    {
        AsegurarBloquesPropiedades();
        string nombreEnumActivo = colorActivo.ToString().ToLower().Trim();

        foreach (var boton in listaBotones)
        {
            if (boton.rendererBoton != null)
            {
                boton.rendererBoton.GetPropertyBlock(bloquePropiedadesBotones);

                string botonIdLimpio = boton.nombreIdentificador.ToLower().Trim();

                if (botonIdLimpio == nombreEnumActivo)
                {
                    Color colorBrillante = boton.colorBaseOriginal * boton.intensidadActivo;
                    bloquePropiedadesBotones.SetColor(referenciaShaderBoton, colorBrillante);
                }
                else
                {
                    Color colorReposo = boton.colorBaseOriginal * boton.intensidadApagado;
                    colorReposo.a = 1f;
                    bloquePropiedadesBotones.SetColor(referenciaShaderBoton, colorReposo);
                }

                boton.rendererBoton.SetPropertyBlock(bloquePropiedadesBotones);
            }
        }
    }

    private void ApagarEmisionTotalBotones()
    {
        AsegurarBloquesPropiedades();
        foreach (var boton in listaBotones)
        {
            if (boton.rendererBoton != null)
            {
                boton.rendererBoton.GetPropertyBlock(bloquePropiedadesBotones);
                bloquePropiedadesBotones.SetColor(referenciaShaderBoton, Color.black);
                boton.rendererBoton.SetPropertyBlock(bloquePropiedadesBotones);
            }
        }
    }

    private void ActualizarVisualBotonMaestro()
    {
        AsegurarBloquesPropiedades();
        if (botonMaestro.rendererBoton != null)
        {
            botonMaestro.rendererBoton.GetPropertyBlock(bloquePropiedadesBotones);
            float intensidadActual = estaEncendida ? botonMaestro.intensidadOn : botonMaestro.intensidadOff;
            Color colorFinal = botonMaestro.colorBaseOriginal * intensidadActual;
            bloquePropiedadesBotones.SetColor(referenciaShaderBoton, colorFinal);
            botonMaestro.rendererBoton.SetPropertyBlock(bloquePropiedadesBotones);
        }
    }

    public void CambiarColorPorNombre(string nombreColor)
    {
        if (!estaEncendida) return;

        switch (nombreColor.Trim())
        {
            case "CyanGaseoso": colorActivo = ColorPredefinido.CyanGaseoso; break;
            case "RosaNeon": colorActivo = ColorPredefinido.RosaNeon; break;
            case "VerdeRadioactivo": colorActivo = ColorPredefinido.VerdeRadioactivo; break;
            case "AmbarLava": colorActivo = ColorPredefinido.AmbarLava; break;
            case "VioletaProfundo": colorActivo = ColorPredefinido.VioletaProfundo; break;
            case "Blanco": colorActivo = ColorPredefinido.Blanco; break;
            default: Debug.LogWarning($"El color '{nombreColor}' no coincide con ningún caso exacto."); break;
        }
        ActualizarIluminacion();
    }

    private void ActualizarIluminacion()
    {
        AsegurarBloquesPropiedades();
        Color colorBaseRender = estaEncendida ? ObtenerColorActual() : Color.black;
        Color colorEmissionRender = estaEncendida ? ObtenerColorActual() : Color.black;

        if (luzBase != null)
        {
            luzBase.color = colorBaseRender;
            luzBase.enabled = estaEncendida;
        }

        if (tuboInterior != null) tuboInterior.SetActive(estaEncendida);
        if (quadInterior != null) quadInterior.SetActive(estaEncendida);
        if (tapaSuperficie != null) tapaSuperficie.SetActive(estaEncendida);

        if (estaEncendida)
        {
            ActualizarMaterialLampara(tuboInterior?.GetComponent<Renderer>(), colorBaseRender, colorEmissionRender);
            ActualizarMaterialLampara(quadInterior?.GetComponent<Renderer>(), colorBaseRender, colorEmissionRender);
            ActualizarMaterialLampara(tapaSuperficie?.GetComponent<Renderer>(), colorBaseRender, colorEmissionRender);
        }
    }

    private void ActualizarMaterialLampara(Renderer renderer, Color baseCol, Color emiCol)
    {
        AsegurarBloquesPropiedades();
        if (renderer != null && renderer.gameObject.activeSelf)
        {
            renderer.GetPropertyBlock(bloquePropiedadesLampara);
            bloquePropiedadesLampara.SetColor(referenciaShader, baseCol);
            bloquePropiedadesLampara.SetColor(referenciaShaderEmission, emiCol);
            renderer.SetPropertyBlock(bloquePropiedadesLampara);
        }
    }

    private void AsegurarBloquesPropiedades()
    {
        if (bloquePropiedadesBotones == null)
            bloquePropiedadesBotones = new MaterialPropertyBlock();

        if (bloquePropiedadesLampara == null)
            bloquePropiedadesLampara = new MaterialPropertyBlock();
    }

    private Color ObtenerColorActual()
    {
        switch (colorActivo)
        {
            case ColorPredefinido.CyanGaseoso: return color1_Cyan;
            case ColorPredefinido.RosaNeon: return color2_Rosa;
            case ColorPredefinido.VerdeRadioactivo: return color3_Verde;
            case ColorPredefinido.AmbarLava: return color4_Ambar;
            case ColorPredefinido.VioletaProfundo: return color5_Violeta;
            case ColorPredefinido.Blanco: return color6_Blanco;
            default: return color1_Cyan;
        }
    }
}