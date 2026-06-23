using UnityEngine;

public class SceneManager : MonoBehaviour
{
    public static bool EstaAplicandoSeleccionAutomatica { get; private set; }

    public static bool PermitirCambioActivacionElemento(GameObject origen, bool estaEncendidoActualmente)
    {
        return true;
    }

    public static void SetAplicandoSeleccionAutomatica(bool aplicando)
    {
        EstaAplicandoSeleccionAutomatica = aplicando;
    }
}
