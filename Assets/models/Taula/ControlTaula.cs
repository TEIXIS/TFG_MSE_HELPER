using UnityEngine;

public class ControlTaula : MonoBehaviour
{

    [Header("Ajustes Físicos")]
    [Tooltip("Arrastra aquí el objeto vacío que marca el centro visual.")]
    [SerializeField] private Transform centroVisualMesa;
    [SerializeField] private float compensacionAltura = 0.8f;

    [Header("Configuración del Cubo (Luz Ambiental)")]
    [SerializeField] private Renderer cuboRenderer;
    [Tooltip("Velocidad del cambio de color. Valores bajos = más relajante.")]
    [SerializeField] private float velocidadCiclo = 0.05f;
    [Tooltip("Intensidad del brillo (HDR) para el cubo ambiental.")]
    [SerializeField] private float intensidadCubo = 2.0f;

    [Header("Configuración del Botón Maestro")]
    [SerializeField] private Renderer botonRenderer;
    [Tooltip("Color base que tendrá el botón maestro.")]
    [SerializeField] private Color colorBaseBoton = Color.white;
    [SerializeField] private float emisionBotonEncendido = 1.5f;
    [SerializeField] private float emisionBotonApagado = 0.2f;

    private Material materialCubo;
    private Material materialBoton;
    private float hueActual = 0.5f;
    private bool estaEncendido = true;

    void Start()
    {
        if (cuboRenderer == null) cuboRenderer = GetComponent<Renderer>();
        if (cuboRenderer != null)
        {
            materialCubo = cuboRenderer.material;
            materialCubo.EnableKeyword("_EMISSION");
        }

        if (botonRenderer != null)
        {
            materialBoton = botonRenderer.material;
            materialBoton.EnableKeyword("_EMISSION");
        }

        Vector3 posicionCentro = centroVisualMesa != null ? centroVisualMesa.position : transform.position;
        Shader.SetGlobalFloat("_AlturaMesaLuz", posicionCentro.y + compensacionAltura);
        Shader.SetGlobalVector("_CentroMesaLuz", posicionCentro);

        ActualizarColorCubo();
        ActualizarEmisionBoton();
    }

    void Update()
    {
        if (!estaEncendido) return;

        hueActual += velocidadCiclo * Time.deltaTime;
        if (hueActual > 1.0f) hueActual -= 1.0f;

        ActualizarColorCubo();
    }

    // Refresca el color que la mesa proyecta sobre los bloques translucidos.
    private void ActualizarColorCubo()
    {
        if (materialCubo == null) return;

        Color colorBase = Color.HSVToRGB(hueActual, 1f, 1f);
        Color colorFinal = colorBase * intensidadCubo;

        materialCubo.SetColor("_EmissionColor", colorFinal);
        Shader.SetGlobalColor("_ColorMesaLuz", colorFinal);
    }

    // Ajusta el brillo del boton asignado sin cambiar si el objeto se ve o no.
    private void ActualizarEmisionBoton()
    {
        if (materialBoton == null) return;
        float intensidadTarget = estaEncendido ? emisionBotonEncendido : emisionBotonApagado;
        materialBoton.SetColor("_EmissionColor", colorBaseBoton * intensidadTarget);
    }

    // Alterna el estado logico de la mesa: encendida o apagada, pero no visible o invisible.
    public void AlternarEstadoLuz()
    {
        if (!SceneManager.PermitirCambioActivacionElemento(gameObject, estaEncendido))
            return;

        estaEncendido = !estaEncendido;

        if (!estaEncendido)
        {
            if (materialCubo != null) materialCubo.SetColor("_EmissionColor", Color.black);
            Shader.SetGlobalColor("_ColorMesaLuz", Color.black);
        }
        else
        {
            ActualizarColorCubo();
        }

        ActualizarEmisionBoton();
    }
}