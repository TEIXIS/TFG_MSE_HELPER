using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class ElementButton : MonoBehaviour
{
    [Tooltip("El texto exacto que enviará a VR (Ej: 'blau_cel')")]
    public string idElemento;

    [Tooltip("Arrastra aquí el Canvas o el objeto que tiene el TabletSelectionManager")]
    public TabletSelectionManager manager;

    private Button miBoton;
    private Image miImagen;

    void Start()
    {
        miBoton = GetComponent<Button>();
        miImagen = GetComponent<Image>();


        // Iniciamos el botón pidiéndole al Manager la imagen "Normal"
        if (manager != null && manager.spriteBotonNormal != null)
        {
            miImagen.sprite = manager.spriteBotonNormal;
        }
      
        // Conectamos el clic automáticamente
        miBoton.onClick.AddListener(AlSerPulsado);
    }

    void AlSerPulsado()
    {
        if (manager != null && !string.IsNullOrEmpty(idElemento))
        {
            // Le enviamos al Manager el botón entero, no solo el ID
            manager.ProcesarPulsacion(this);
        }
        else
        {
            Debug.LogError("Falta configurar el Manager o el ID en el botón " + gameObject.name);
        }
    }

    // Esta función la llamará el Manager para decirle al botón que cambie su foto
    public void CambiarEstadoVisual(bool estaSeleccionado)
    {
        if (miImagen == null || manager == null) return;

        if (estaSeleccionado && manager.spriteBotonSeleccionado != null)
        {
            miImagen.sprite = manager.spriteBotonSeleccionado;
        }
        else if (!estaSeleccionado && manager.spriteBotonNormal != null)
        {
            miImagen.sprite = manager.spriteBotonNormal;
        }
    }
}