using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class TabletTeleportButton : MonoBehaviour
{
    [Tooltip("El texto exacto del elemento al que viajaremos (Ej: 'blau_cel')")]
    public string idElemento;

    [Tooltip("Arrastra aquí el objeto que tiene el TabletSelectionManager")]
    public TabletSelectionManager manager;

    [Header("Imágenes del Botón")]
    public Sprite spriteNormal;
    [Tooltip("La imagen que se mostrará cuando el usuario esté físicamente en este lugar")]
    public Sprite spriteSeleccionado;

    private Button miBoton;
    private Image miImagen;

    void Start()
    {
        miBoton = GetComponent<Button>();
        miImagen = GetComponent<Image>();

        // Empezamos con el sprite normal apagado
        if (spriteNormal != null)
        {
            miImagen.sprite = spriteNormal;
        }

        miBoton.onClick.AddListener(AlSerPulsado);

        // Nos presentamos al Manager para que nos tenga en su lista
        if (manager != null) manager.RegistrarBotonTeleport(this);
    }

    void AlSerPulsado()
    {
        if (manager != null && !string.IsNullOrEmpty(idElemento))
        {
            // Le pedimos al Manager que envíe la orden a las gafas
            manager.EnviarOrdenTeleport(idElemento);
        }
    }

    // El Manager llamará a esta función para cambiarnos la foto cuando el usuario llegue o se vaya
    public void CambiarEstadoUbicacion(bool estaAqui)
    {
        if (miImagen == null) return;

        if (estaAqui && spriteSeleccionado != null)
        {
            miImagen.sprite = spriteSeleccionado;
        }
        else if (!estaAqui && spriteNormal != null)
        {
            miImagen.sprite = spriteNormal;
        }
    }
}