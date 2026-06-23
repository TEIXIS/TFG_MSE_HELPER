using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mantiene la maqueta de la tablet limitada al catálogo de objetos que existe
/// en la escena activa del visor. No refleja la selección ni el estado activo
/// de los elementos de VR: todos los elementos del catálogo se muestran como
/// referencia espacial estable para el profesional.
/// </summary>
public sealed class TabletMirrorCatalogFilter : MonoBehaviour
{
    private static readonly HashSet<string> ElementosDelVisor = new HashSet<string>
    {
        // Equivalencia con los nueve objetoHabitacion de Passthrough_menu.unity.
        "nouprefabalfombraled",
        "prefablamparabombolles",
        "taulallums",
        "prefabprojeccio",
        "prefabmesasonido",
        "prefabvisualizadordesonido",
        "prefabmusica",
        "llumultraviolat",
        "taula"
    };

    private Transform salaMonitorizada;

    private void LateUpdate()
    {
        Transform salaActual = ResolverSalaMonitorizada();
        if (salaActual == null)
            return;

        bool haCambiadoSala = salaActual != salaMonitorizada;
        salaMonitorizada = salaActual;

        AplicarCatalogoDelVisor(salaMonitorizada);

        if (haCambiadoSala)
        {
            Debug.Log(
                $"[TabletMirror] Catálogo fijo aplicado: {ElementosDelVisor.Count} elementos del visor, " +
                "independientes de los mensajes SYNC/PREP.");
        }
    }

    private Transform ResolverSalaMonitorizada()
    {
        if (salaMonitorizada != null && salaMonitorizada.gameObject.activeInHierarchy)
            return salaMonitorizada;

        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform candidate in transforms)
        {
            if (candidate.name == "SensoryRoomMonTablet(Clone)")
                return candidate;
        }

        return null;
    }

    private static void AplicarCatalogoDelVisor(Transform raiz)
    {
        foreach (Transform hijo in raiz)
        {
            string nombre = NormalizarNombre(hijo.name);

            // Room conserva la lógica existente de TabletMirrorManager: únicamente
            // muestra la variante blanca, adulta o infantil seleccionada.
            if (nombre == "room" || nombre == "arealight")
                continue;

            // Todos los elementos válidos se mantienen visibles, aunque el visor
            // los tenga apagados o no formen parte de la selección de la sesión.
            hijo.gameObject.SetActive(ElementosDelVisor.Contains(nombre));
        }
    }

    private static string NormalizarNombre(string nombre)
    {
        return (nombre ?? string.Empty)
            .Replace("(Clone)", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Trim()
            .ToLowerInvariant();
    }
}
