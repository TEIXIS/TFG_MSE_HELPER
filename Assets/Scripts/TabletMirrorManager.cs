using UnityEngine;
using System.Collections.Generic;
using System.Globalization;

[System.Serializable]
public struct ElementoEspectador
{
    public string idElemento;
    public GameObject prefabClon;
}

public class TabletMirrorManager : MonoBehaviour
{
    private const string RoomWhite = "blanca";
    private const string RoomAdult = "adult";
    private const string RoomChild = "infantil";
    private static readonly HashSet<string> ElementosVisorPermitidos = new HashSet<string>
    {
        "prefabvisualizadordesonido",
        "taula",
        "taulallums",
        "llumultraviolat",
        "llummano",
        "prefabmesasonido",
        "llums",
        "nouprefabalfombraled",
        "prefablamparabombolles",
        "prefabprojeccio",
        "prefabproyector",
        "lamparaburbujas",
        "alfombra",
        "boladisco"
    };
    [Header("Cámaras y UI")]
    public Transform camaraEspectador;
    public GameObject pantallaUI;
    public bool isPantallaActiva = false;
    [Tooltip("Contenedor donde se instanciara la sala del visor. Si se deja vacio, se busca MundoVR_Tablet.")]
    public Transform contenedorMundoVR;

    [Header("Prefabs de Sala")]
    [Tooltip("Prefab completo del visor de tablet. Si se asigna, se usa este en vez de clonar elementos por red.")]
    public GameObject prefabSensoryRoomMonTablet;
    [Tooltip("Muestra todos los elementos del prefab completo, aunque no lleguen seleccionados desde VR.")]
    public bool mostrarTodosLosElementos = true;
    [Tooltip("Prefab que se vera en la tablet cuando el usuario tenga sala blanca.")]
    public GameObject prefabHabBlanca;
    [Tooltip("Prefab que se vera en la tablet cuando el usuario tenga sala adulta.")]
    public GameObject prefabHabAdult;
    [Tooltip("Prefab que se vera en la tablet cuando el usuario tenga sala infantil.")]
    public GameObject prefabHabInfantil;

    [Header("Base de Datos (Clones 3D)")]
    public List<ElementoEspectador> baseDatosClones;

    [Header("Suavizado de Movimiento (Interpolación)")]
    [Tooltip("A mayor número, más rápido sigue el movimiento (pero menos suave). 15-20 es el punto dulce.")]
    public float velocidadSuavizado = 15f;

    // --- NUEVAS VARIABLES PARA EL MOVIMIENTO SUAVE ---
    private Vector3 posicionObjetivo;
    private Quaternion rotacionObjetivo;
    private bool primeraVez = true; // Para evitar que la cámara "vuele" desde lejos al encenderla

    private List<GameObject> clonesEnEscena = new List<GameObject>();
    private GameObject salaEnEscena;
    private string tipoSalaEnEscena;
    private int capaVR;

    void Start()
    {
        capaVR = LayerMask.NameToLayer("MundoVR_Tablet");
        ResolverContenedorMundoVR();
        OcultarSalasFijasIniciales();
        SetPantallaActiva(false);
    }

    public void SetPantallaActiva(bool activa)
    {
        isPantallaActiva = activa;
        if (contenedorMundoVR != null)
            contenedorMundoVR.gameObject.SetActive(activa);
        if (pantallaUI != null) pantallaUI.SetActive(activa);
        if (camaraEspectador != null) camaraEspectador.gameObject.SetActive(activa);

        // Reiniciamos el "primer frame" al encender
        if (activa)
        {
            primeraVez = true;
            ActualizarSalaEspectador(false);
        }
    }
    public void ActualizarPosicionCamara(string datosTracking)
    {
        if (!isPantallaActiva || camaraEspectador == null) return;

        try
        {
            string[] partes = datosTracking.Split('|');
            string[] posStr = partes[0].Split(',');
            string[] rotStr = partes[1].Split(',');

            Vector3 pos = new Vector3(float.Parse(posStr[0], CultureInfo.InvariantCulture), float.Parse(posStr[1], CultureInfo.InvariantCulture), float.Parse(posStr[2], CultureInfo.InvariantCulture));
            Quaternion rot = new Quaternion(float.Parse(rotStr[0], CultureInfo.InvariantCulture), float.Parse(rotStr[1], CultureInfo.InvariantCulture), float.Parse(rotStr[2], CultureInfo.InvariantCulture), float.Parse(rotStr[3], CultureInfo.InvariantCulture));

            if (float.IsNaN(pos.x) || float.IsInfinity(pos.x) || float.IsNaN(rot.x)) return;

            // --- EL CAMBIO ---
            // Ya no movemos la cámara aquí. Solo guardamos a dónde tiene que ir.
            posicionObjetivo = pos;
            rotacionObjetivo = rot;

            // Si es el primer frame que recibimos al encender la pantalla, 
            // teletransportamos de golpe para que no se vea a la cámara viajar desde el 0,0,0
            if (primeraVez)
            {
                camaraEspectador.position = posicionObjetivo;
                camaraEspectador.rotation = rotacionObjetivo;
                primeraVez = false;
            }
        }
      catch { /* Silenciamos errores de tracking continuo para no saturar consola*/  }
    }

    // --- NUEVO: El Update se encarga de deslizar la cámara a 60 FPS ---
    void Update()
    {

        if (!isPantallaActiva || camaraEspectador == null) return;

        // Movimiento 1:1, teletransporte exacto al tick de la red
      //  camaraEspectador.position = posicionObjetivo;
      //  camaraEspectador.rotation = rotacionObjetivo;
      if (!isPantallaActiva || camaraEspectador == null || primeraVez) return;
       
        // Vector3.Lerp mueve la posición gradualmente
          camaraEspectador.position = Vector3.Lerp(camaraEspectador.position, posicionObjetivo, Time.deltaTime * velocidadSuavizado);

        // Quaternion.Slerp rota la cámara gradualmente
        camaraEspectador.rotation = Quaternion.Slerp(camaraEspectador.rotation, rotacionObjetivo, Time.deltaTime * velocidadSuavizado);
    
     }

    // --- (El resto del código se queda exactamente igual) ---

    public void GenerarEntorno(string paqueteDatos, bool esTutorial)
    {
        foreach (GameObject obj in clonesEnEscena) { Destroy(obj); }
        clonesEnEscena.Clear();
        ActualizarSalaEspectador(esTutorial);

        if (prefabSensoryRoomMonTablet != null && mostrarTodosLosElementos)
            return;

        if (string.IsNullOrEmpty(paqueteDatos)) return;

        string[] elementos = paqueteDatos.Split(';');
        foreach (string el in elementos)
        {
            try
            {
                string[] partes = el.Split('|');
                string id = partes[0];
                string[] posStr = partes[1].Split(',');
                float rotY = float.Parse(partes[2], CultureInfo.InvariantCulture);

                Vector3 pos = new Vector3(float.Parse(posStr[0], CultureInfo.InvariantCulture), float.Parse(posStr[1], CultureInfo.InvariantCulture), float.Parse(posStr[2], CultureInfo.InvariantCulture));

                ElementoEspectador modelo = baseDatosClones.Find(x => x.idElemento == id);
                if (modelo.prefabClon != null)
                {
                    GameObject nuevoClon = Instantiate(modelo.prefabClon, pos, Quaternion.Euler(0, rotY, 0));
                    ForzarCapaMundoVR(nuevoClon.transform, capaVR);
                    clonesEnEscena.Add(nuevoClon);
                }
            }
            catch (System.Exception e) { Debug.LogError($"Error clonando {el}: {e.Message}"); }
        }
    }



    private void ResolverContenedorMundoVR()
    {
        if (contenedorMundoVR != null)
            return;

        GameObject contenedor = GameObject.Find("MundoVR_Tablet");
        if (contenedor != null)
            contenedorMundoVR = contenedor.transform;
    }

    private void OcultarSalasFijasIniciales()
    {
        if (contenedorMundoVR == null)
            return;

        foreach (Transform child in contenedorMundoVR)
        {
            string nombre = child.name.ToLowerInvariant();
            if (nombre.Contains("sensoryroom") || nombre.Contains("hab") || nombre.Contains("room"))
                child.gameObject.SetActive(false);
        }
    }
    private void ActualizarSalaEspectador(bool esTutorial)
    {
        if (esTutorial)
        {
            DestruirSalaEspectador();
            return;
        }

        string tipoSala = NormalizarTipoSala(SessionUser.SelectedRoomType);

        if (prefabSensoryRoomMonTablet != null)
        {
            ActualizarSalaCompleta(tipoSala);
            return;
        }

        GameObject prefabSala = ObtenerPrefabSala(tipoSala);

        if (prefabSala == null)
        {
            DestruirSalaEspectador();
            return;
        }

        if (salaEnEscena != null && tipoSalaEnEscena == tipoSala)
            return;

        DestruirSalaEspectador();

        salaEnEscena = contenedorMundoVR != null
            ? Instantiate(prefabSala, contenedorMundoVR)
            : Instantiate(prefabSala, Vector3.zero, Quaternion.identity);
        salaEnEscena.transform.localPosition = Vector3.zero;
        salaEnEscena.transform.localRotation = Quaternion.identity;
        tipoSalaEnEscena = tipoSala;
        ForzarCapaMundoVR(salaEnEscena.transform, capaVR);
    }

    private void ActualizarSalaCompleta(string tipoSala)
    {
        if (salaEnEscena != null && tipoSalaEnEscena == tipoSala)
            return;

        DestruirSalaEspectador();

        salaEnEscena = contenedorMundoVR != null
            ? Instantiate(prefabSensoryRoomMonTablet, contenedorMundoVR)
            : Instantiate(prefabSensoryRoomMonTablet, Vector3.zero, Quaternion.identity);

        salaEnEscena.transform.localPosition = Vector3.zero;
        salaEnEscena.transform.localRotation = Quaternion.identity;
        tipoSalaEnEscena = tipoSala;

        PrepararSalaCompletaParaTablet(salaEnEscena.transform, tipoSala);
        ForzarCapaMundoVR(salaEnEscena.transform, capaVR);
    }

    private void PrepararSalaCompletaParaTablet(Transform raiz, string tipoSala)
    {
        MonoBehaviour[] scripts = raiz.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour script in scripts)
        {
            if (script != null)
                script.enabled = false;
        }

        ConfigurarVisibilidadSalaCompleta(raiz, tipoSala, false, false, false, true);
    }

    // Prepara el prefab completo para que la tablet solo muestre la sala escogida y los elementos reales del visor.
    private void ConfigurarVisibilidadSalaCompleta(Transform objeto, string tipoSala, bool dentroHabitacionOculta, bool dentroHabitacionVisible, bool dentroElementoPermitido, bool esRaiz)
    {
        string nombreOriginal = (objeto.name ?? "").Trim().ToLowerInvariant();
        string nombre = NormalizarNombreObjeto(objeto.name);
        bool esHabitacion = nombre == "habblanca" || nombre == "habadult" || nombre == "habinfantil";
        bool habitacionSeleccionada =
            (tipoSala == RoomWhite && nombre == "habblanca") ||
            (tipoSala == RoomAdult && nombre == "habadult") ||
            (tipoSala == RoomChild && nombre == "habinfantil");

        bool esElementoPermitido = EsElementoPermitidoEnTablet(nombre);
        bool ocultar = dentroHabitacionOculta;
        bool habitacionVisibleActual = dentroHabitacionVisible;
        bool elementoPermitidoActual = dentroElementoPermitido || esElementoPermitido;

        if (esHabitacion)
        {
            ocultar = !habitacionSeleccionada;
            habitacionVisibleActual = habitacionSeleccionada;
        }

        if (EsObjetoAuxiliarTablet(nombreOriginal) || (!habitacionVisibleActual && !elementoPermitidoActual && EsObjetoPruebaTablet(nombre)))
            ocultar = true;

        if (!esRaiz)
            objeto.gameObject.SetActive(!ocultar);

        bool hijosDentroHabitacionOculta = ocultar && esHabitacion;
        foreach (Transform hijo in objeto)
            ConfigurarVisibilidadSalaCompleta(hijo, tipoSala, hijosDentroHabitacionOculta, habitacionVisibleActual, elementoPermitidoActual, false);
    }

    // Detecta objetos de colocacion o pruebas que no forman parte de la experiencia final del visor.
    private bool EsObjetoAuxiliarTablet(string nombre)
    {
        return nombre.Contains("borrar") ||
               nombre.Contains("jumpingpoints") ||
               nombre.StartsWith("pos_") ||
               nombre == "emptyplace";
    }

    // Oculta restos claros de pruebas sin tocar cubos genericos que pueden ser paredes o suelo.
    private bool EsObjetoPruebaTablet(string nombre)
    {
        return nombre == "groc" ||
               nombre == "rosa" ||
               nombre == "taronja" ||
               nombre == "verd" ||
               nombre == "blaucel" ||
               nombre == "blaufosc" ||
               nombre == "sphere" ||
               nombre.StartsWith("sphere");
    }
    // Comprueba si un objeto suelto fuera de la sala es uno de los elementos que existen en VR.
    private bool EsElementoPermitidoEnTablet(string nombre)
    {
        if (ElementosVisorPermitidos.Contains(nombre))
            return true;

        foreach (string nombrePermitido in ElementosVisorPermitidos)
        {
            if (nombre.StartsWith(nombrePermitido))
                return true;
        }

        return false;
    }

    // Deja los nombres comparables aunque Unity añada espacios, guiones bajos o el sufijo Clone.
    private string NormalizarNombreObjeto(string nombre)
    {
        return (nombre ?? "")
            .Replace("(Clone)", "")
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Trim()
            .ToLowerInvariant();
    }
    private void DestruirSalaEspectador()
    {
        if (salaEnEscena != null)
            Destroy(salaEnEscena);

        salaEnEscena = null;
        tipoSalaEnEscena = null;
    }

    private string NormalizarTipoSala(string tipoSala)
    {
        string valor = (tipoSala ?? "").Trim().ToLowerInvariant();

        if (valor == RoomAdult || valor.Contains("adult"))
            return RoomAdult;

        if (valor == RoomChild || valor.Contains("infant"))
            return RoomChild;

        return RoomWhite;
    }

    private GameObject ObtenerPrefabSala(string tipoSala)
    {
        if (tipoSala == RoomAdult && prefabHabAdult != null)
            return prefabHabAdult;

        if (tipoSala == RoomChild && prefabHabInfantil != null)
            return prefabHabInfantil;

        return prefabHabBlanca;
    }
    private void ForzarCapaMundoVR(Transform objeto, int idCapa)
    {
        objeto.gameObject.layer = idCapa;
        foreach (Transform hijo in objeto)
        {
            ForzarCapaMundoVR(hijo, idCapa);
        }
    }
}

