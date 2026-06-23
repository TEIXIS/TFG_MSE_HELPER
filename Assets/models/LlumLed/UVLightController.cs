using UnityEngine;

public class UVLightController : MonoBehaviour
{
    [Header("Configuración del Botón Maestro")]
    [SerializeField] private Renderer botonRenderer;
    [Tooltip("Color base de la emisión del botón.")]
    [SerializeField] private Color colorBaseBoton = Color.white;
    [SerializeField] private float emisionBotonEncendido = 1.5f;
    [SerializeField] private float emisionBotonApagado = 0.2f;

    [Header("Configuración de la Manta (Reactiva a UV)")]
    [SerializeField] private Renderer mantaRenderer;
    [Tooltip("Color de la emisión de la manta cuando recibe luz UV.")]
    [SerializeField] private Color colorEmisionManta = new Color(0.6f, 0.1f, 0.9f);
    [SerializeField] private float intensidadManta = 2.0f;

    [Header("Configuración del Tubo Fluorescente")]
    [SerializeField] private Renderer tuboRenderer;
    [Tooltip("Color de emisión del propio tubo físico.")]
    [SerializeField] private Color colorEmisionTubo = new Color(0.5f, 0f, 1f);
    [SerializeField] private float intensidadTubo = 3.0f;

    [Header("Configuración de la Luz de Unity")]
    [Tooltip("El componente Light real que ilumina la escena.")]
    [SerializeField] private Light luzUV;

    [Header("Configuración de Canvas (Imágenes UV)")]
    [Tooltip("Arrastra aquí los GameObjects de los Canvas o Imágenes que quieres ocultar/mostrar.")]
    [SerializeField] private GameObject[] listaCanvasUV;

    [Header("Configuración de Cilindros Emisivos")]
    [Tooltip("Arrastra aquí los Renderers de los 3 cilindros. Sus materiales ya deben tener el color de emisión configurado.")]
    [SerializeField] private Renderer[] listaCilindros;

    [Header("Estado Inicial")]
    [SerializeField] private bool empezarEncendido = false;

    // Materiales instanciados
    private Material materialBoton;
    private Material materialManta;
    private Material materialTubo;
    private Material[] materialesCilindros;

    private bool estaEncendido;

    void Start()
    {
        estaEncendido = empezarEncendido;

        // Inicialización del Botón
        if (botonRenderer != null)
        {
            materialBoton = botonRenderer.material;
            materialBoton.EnableKeyword("_EMISSION");
        }

        // Inicialización de la Manta
        if (mantaRenderer != null)
        {
            materialManta = mantaRenderer.material;
            materialManta.EnableKeyword("_EMISSION");
        }

        // Inicialización del Tubo Fluorescente
        if (tuboRenderer != null)
        {
            materialTubo = tuboRenderer.material;
            materialTubo.EnableKeyword("_EMISSION");
        }

        // Inicialización del Array de Materiales para los Cilindros
        if (listaCilindros != null && listaCilindros.Length > 0)
        {
            materialesCilindros = new Material[listaCilindros.Length];
            for (int i = 0; i < listaCilindros.Length; i++)
            {
                if (listaCilindros[i] != null)
                {
                    materialesCilindros[i] = listaCilindros[i].material;
                    // No forzamos el Enable aquí; lo hará ActualizarElementos() basándose en el estado inicial
                }
            }
        }

        // Aplicamos el estado inicial a todos los elementos
        ActualizarElementos();
    }

    /// <summary>
    /// Método Maestro para encender/apagar. Llámalo desde los eventos de tu XR Interactable.
    /// </summary>
    public void AlternarEstadoLuzUV()
    {
        if (!SceneManager.PermitirCambioActivacionElemento(gameObject, estaEncendido))
            return;

        estaEncendido = !estaEncendido;
        ActualizarElementos();
    }

    /// <summary>
    /// Sincroniza todos los componentes visuales basándose en la variable 'estaEncendido'
    /// </summary>
    private void ActualizarElementos()
    {
        // 1. Actualizar Emisión del Botón Maestro
        if (materialBoton != null)
        {
            float intensidadBoton = estaEncendido ? emisionBotonEncendido : emisionBotonApagado;
            materialBoton.SetColor("_EmissionColor", colorBaseBoton * intensidadBoton);
        }

        // 2. Actualizar Emisión de la Manta
        if (materialManta != null)
        {
            if (estaEncendido)
                materialManta.SetColor("_EmissionColor", colorEmisionManta * intensidadManta);
            else
                materialManta.SetColor("_EmissionColor", Color.black);
        }

        // 3. Actualizar Emisión del Tubo Fluorescente
        if (materialTubo != null)
        {
            if (estaEncendido)
                materialTubo.SetColor("_EmissionColor", colorEmisionTubo * intensidadTubo);
            else
                materialTubo.SetColor("_EmissionColor", Color.black);
        }

        // 4. Activar/Desactivar el componente Light
        if (luzUV != null)
        {
            luzUV.enabled = estaEncendido;
        }

        // 5. Activar/Desactivar los Canvas (Muestra/Oculta las imágenes)
        if (listaCanvasUV != null)
        {
            foreach (GameObject canvasGO in listaCanvasUV)
            {
                if (canvasGO != null)
                {
                    canvasGO.SetActive(estaEncendido);
                }
            }
        }

        // 6. Activar/Desactivar Emisión de los Cilindros (Usa sus propios valores de material)
        if (materialesCilindros != null)
        {
            foreach (Material matCilindro in materialesCilindros)
            {
                if (matCilindro != null)
                {
                    if (estaEncendido)
                    {
                        matCilindro.EnableKeyword("_EMISSION");
                    }
                    else
                    {
                        matCilindro.DisableKeyword("_EMISSION");
                    }
                }
            }
        }
    }
}