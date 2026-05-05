using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TabletSelectionManager : MonoBehaviour
{
    public enum PosturaUsuario { DePie, Sentado }

    [Header("Ajustes terapeuticos (Toggles)")]
    public PosturaUsuario posturaActual = PosturaUsuario.DePie; // De pie por defecto
    [Tooltip("Arrastra aqui la Imagen del boton unico 'Sentado'")]
    public Image imgBotonSentado;

    [Tooltip("Arrastra aqui la Imagen del boton 'Permitir Menu VR'")]
    public Image imgBotonMenuVR;
    private bool menuVRPermitido = true; // El menu funciona por defecto

    [Tooltip("Boton para BLOQUEAR las particulas de las manos. Si esta inactivo, funcionan.")]
    public Image imgBotonParticulas;
    private bool particulasPermitidas = true; // Por defecto funcionan

    // --- NUEVO: Anadimos 'Ninguno' para que no empiece forzado ---
    public enum ModoTablet { Ninguno, Tutorial, PreparacionVR, VR }

    [Header("Modo Espectador")]
    public TabletMirrorManager mirrorManager;
    [Tooltip("Arrastra aqui el componente Image del boton ACTIVAR de la pantalla negra")]
    public Image imgBotonEspectador;


    [Header("Estado Actual")]
    public ModoTablet modoActual = ModoTablet.Ninguno;

    [Header("Configuracion")]
    public int maxSelecciones = 6;

    [Header("Interfaz de Usuario (UI)")]
    public TMP_Text textoContador;

    [Header("Red")]
    public Connection connectionScript;
    public SessionTracker sessionTracker;

    private List<ElementButton> botonesSeleccionados = new List<ElementButton>();

    [Header("Estetica Global de Botones")]
    [Tooltip("La imagen por defecto para TODOS los botones (arriba y abajo)")]
    public Sprite spriteBotonNormal;
    [Tooltip("La imagen cuando un boton esta seleccionado o activo")]
    public Sprite spriteBotonSeleccionado;

    [Header("Referencias a los 3 Botones Superiores")]
    [Tooltip("Arrastra aqui el componente Image de los 3 botones de arriba para que cambien de Sprite")]
    public Image imgBotonTutorial;
    public Image imgBotonPreparacion;
    public Image imgBotonVR;

    // Lista para controlar los botones de teletransporte
    private List<TabletTeleportButton> botonesTeleport = new List<TabletTeleportButton>();
    private bool inicializado;

    private void OnEnable()
    {
        if (inicializado && connectionScript != null && connectionScript.connected)
            EnviarUsuarioSeleccionadoAlVR();
    }

    void Start()
    {
        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        // --- SOLUCION TEARING: Forzamos la sincronizacion vertical de la tablet ---

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // Al arrancar, no hay ningun modo activo
        modoActual = ModoTablet.Ninguno;
        ActualizarSpritesBotonesSuperiores();
        ActualizarContadorUI();
        // APAGAMOS LOS TELEPORTS (No hacen falta aqui)
        ActualizarVisibilidadTeleports(false);
        ActualizarSpritesPostura();
        ActualizarSpriteMenuVR();
        ActualizarSpriteParticulas();

        if (SessionUser.SelectedUserId <= 0)
        {
            sessionTracker.SetPosture(posturaActual == PosturaUsuario.Sentado);
            sessionTracker.SetMenuHandsActive(menuVRPermitido);
            sessionTracker.SetHandParticlesActive(particulasPermitidas);
        }

        inicializado = true;

        if (connectionScript != null && connectionScript.connected)
            EnviarUsuarioSeleccionadoAlVR();
    }

    private void EnviarUsuarioSeleccionadoAlVR()
    {
        int userId = SessionUser.SelectedUserId;
        if (userId <= 0)
        {
            Debug.LogWarning("No se puede enviar usuario a VR: no hay usuario seleccionado.");
            return;
        }

        connectionScript.Send("USER:" + userId);
    }

    // ========================================================
    // LOGICA DE LOS 3 BOTONES SUPERIORES (Modos de la App)
    // ========================================================

    public void ActivarModoTutorial()
    {
        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        sessionTracker.StartPhase("tutorial");

        modoActual = ModoTablet.Tutorial;
        ActualizarSpritesBotonesSuperiores();
        LimpiarSelecciones(); // Empezamos de cero

        // APAGAMOS LOS TELEPORTS (No hacen falta aqui)
        ActualizarVisibilidadTeleports(false);

        // Vaciamos la camara espectador de objetos anteriores
        if (mirrorManager != null) mirrorManager.GenerarEntorno("", false);

        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send("CMD_TUTORIAL");
    }

    public void ActivarModoPreparacion()
    {
        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        sessionTracker.StartPhase("preparacio");

        modoActual = ModoTablet.PreparacionVR;
        ActualizarSpritesBotonesSuperiores();
        LimpiarSelecciones(); // Empezamos de cero para elegir hasta 6

        // APAGAMOS LOS TELEPORTS (No hacen falta aqui)
        ActualizarVisibilidadTeleports(false);

        // Vaciamos la camara espectador de objetos anteriores
        if (mirrorManager != null) mirrorManager.GenerarEntorno("", false);

        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send("CMD_PREPARACION");
    }

    public void ActivarModoVR()
    {
        if (botonesSeleccionados.Count == 0)
        {
            Debug.LogWarning("No puedes iniciar VR sin seleccionar ningun elemento en Preparacion.");
            return;
        }

        modoActual = ModoTablet.VR;
        ActualizarSpritesBotonesSuperiores();

        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        List<string> ids = GetSelectedElementIds();
        IniciarVRConElementos(ids);
    }

    private void IniciarVRConElementos(List<string> ids)
    {
        if (ids == null || ids.Count == 0)
            return;

        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        modoActual = ModoTablet.VR;
        ActualizarSpritesBotonesSuperiores();

        sessionTracker.SavePreparationElements(ids);
        sessionTracker.StartPhase("vr");
        sessionTracker.RegisterVrElements(ids);

        // ENCENDEMOS LOS TELEPORTS (Ahora si se puede viajar)
        ActualizarVisibilidadTeleports(true);

        string paqueteDeDatos = "VR:" + string.Join(",", ids);

        if (connectionScript != null && connectionScript.connected)
        {
            // --- SOLUCION: Refrescamos la memoria de las gafas justo antes de crear la sala ---
            connectionScript.Send(posturaActual == PosturaUsuario.Sentado ? "POSTURA:SENTADO" : "POSTURA:DE_PIE");

            // Ahora si, enviamos la orden de generar los elementos
            connectionScript.Send(paqueteDeDatos);
        }
    }

    private void SeleccionarBotonesPorIds(List<string> ids)
    {
        foreach (var btn in botonesSeleccionados)
            if (btn != null) btn.CambiarEstadoVisual(false);

        botonesSeleccionados.Clear();

        ElementButton[] botones = FindObjectsByType<ElementButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (string id in ids)
        {
            foreach (ElementButton btn in botones)
            {
                if (btn != null && btn.idElemento == id)
                {
                    botonesSeleccionados.Add(btn);
                    btn.CambiarEstadoVisual(true);
                    break;
                }
            }
        }

        ActualizarContadorUI();
    }

    // --- NUEVO: En lugar de colores, usamos los Sprites para los botones de arriba ---
    private void ActualizarSpritesBotonesSuperiores()
    {
        if (imgBotonTutorial != null)
            imgBotonTutorial.sprite = (modoActual == ModoTablet.Tutorial) ? spriteBotonSeleccionado : spriteBotonNormal;

        if (imgBotonPreparacion != null)
            imgBotonPreparacion.sprite = (modoActual == ModoTablet.PreparacionVR) ? spriteBotonSeleccionado : spriteBotonNormal;

        if (imgBotonVR != null)
            imgBotonVR.sprite = (modoActual == ModoTablet.VR) ? spriteBotonSeleccionado : spriteBotonNormal;
    }

    private void LimpiarSelecciones()
    {
        foreach (var btn in botonesSeleccionados) { btn.CambiarEstadoVisual(false); }
        botonesSeleccionados.Clear();
        ActualizarContadorUI();
    }

    // ========================================================
    // LOGICA DE LA PARRILLA DE ELEMENTOS
    // ========================================================

    public void ProcesarPulsacion(ElementButton botonPulsado)
    {
        // Seguro anti-errores: Si el terapeuta toca un color sin elegir modo, no hacemos nada
        if (modoActual == ModoTablet.Ninguno)
        {
            Debug.LogWarning("Selecciona primero 'Tutorial' o 'Preparacion VR' arriba.");
            return;
        }

        if (modoActual == ModoTablet.Tutorial)
        {
            LimpiarSelecciones();
            botonesSeleccionados.Add(botonPulsado);
            botonPulsado.CambiarEstadoVisual(true);
            if (sessionTracker != null)
                sessionTracker.TrackTutorialElement(botonPulsado.idElemento);

            if (connectionScript != null && connectionScript.connected)
                connectionScript.Send("TUTORIAL:" + botonPulsado.idElemento);
        }
        else if (modoActual == ModoTablet.PreparacionVR)
        {
            if (botonesSeleccionados.Contains(botonPulsado))
            {
                botonesSeleccionados.Remove(botonPulsado);
                botonPulsado.CambiarEstadoVisual(false);
            }
            else if (botonesSeleccionados.Count < maxSelecciones)
            {
                botonesSeleccionados.Add(botonPulsado);
                botonPulsado.CambiarEstadoVisual(true);
            }
        }
        else if (modoActual == ModoTablet.VR)
        {
            if (botonesSeleccionados.Contains(botonPulsado))
            {
                botonesSeleccionados.Remove(botonPulsado);
                botonPulsado.CambiarEstadoVisual(false);
            }
            else if (botonesSeleccionados.Count < maxSelecciones)
            {
                botonesSeleccionados.Add(botonPulsado);
                botonPulsado.CambiarEstadoVisual(true);
            }
            ActivarModoVR(); // Re-enviamos los cambios en vivo al casco
        }

        ActualizarContadorUI();
    }

    private void ActualizarContadorUI()
    {
        if (textoContador != null)
        {
            if (modoActual == ModoTablet.Ninguno)
            {
                textoContador.text = "Selecciona un mode a la part superior";
                textoContador.color = Color.white;
            }
            else if (modoActual == ModoTablet.Tutorial)
            {
                textoContador.text = "Mode Tutorial: 1 element max.";
                textoContador.color = Color.white;
            }
            else
            {
                textoContador.text = $"{botonesSeleccionados.Count} / {maxSelecciones} seleccionats";

                if (botonesSeleccionados.Count == maxSelecciones)
                    textoContador.color = new Color(0.8f, 0.2f, 0.2f); // Rojo cuando se llega a 6
                else
                    textoContador.color = Color.white;
            }
        }
    }

    // FUNCION: Muestra u oculta las chinchetas de teleport
    private void ActualizarVisibilidadTeleports(bool mostrar)
    {
        foreach (var btn in botonesTeleport)
        {
            if (btn != null)
            {
                btn.gameObject.SetActive(mostrar);
            }
        }
    }

    public void EnviarSenalSOS()
    {
        if (sessionTracker != null)
            sessionTracker.EndSession("SOS activado desde la tablet.");

        // 1. Vaciamos la camara espectador al instante (Pantalla en negro)
        if (mirrorManager != null) mirrorManager.GenerarEntorno("", false);

        // 2. Sacamos a la tablet del modo VR para que la interfaz sea coherente
        modoActual = ModoTablet.Ninguno;
        ActualizarSpritesBotonesSuperiores();

        // 3. Apagamos las chinchetas de teletransporte (ya no se puede viajar)
        ActualizarVisibilidadTeleports(false);

        // Opcional: Si quieres que ademas se desmarquen los botones de colores que tenia elegidos,
        // descomenta la siguiente linea:
        // LimpiarSelecciones();

        // 4. Enviamos la orden de fundido a negro a las gafas
        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send("CMD_SOS");
    }

    //  Enviar orden de Teletransporte Remoto ---
    public void EnviarOrdenTeleport(string idElemento)
    {
        // Solo permitimos mover al usuario si la experiencia VR esta encendida
        if (modoActual != ModoTablet.VR)
        {
            Debug.LogWarning("Solo puedes teletransportar al usuario si estas en modo VR activo.");
            return;
        }

        // Comprobamos si el terapeuta realmente metio este elemento en la sala
        bool elementoEstaEnSala = false;
        foreach (var btn in botonesSeleccionados)
        {
            if (btn.idElemento == idElemento)
            {
                elementoEstaEnSala = true;
                break;
            }
        }

        if (elementoEstaEnSala && connectionScript != null && connectionScript.connected)
        {
            connectionScript.Send("TELEPORT:" + idElemento);
            Debug.Log("Forzando teletransporte a: " + idElemento);
        }
        else
        {
            Debug.LogWarning($"El elemento {idElemento} no esta en la sala, no se puede viajar a el.");
        }
    }


    // 1. Guarda los botones al arrancar
    public void RegistrarBotonTeleport(TabletTeleportButton btn)
    {
        if (!botonesTeleport.Contains(btn)) botonesTeleport.Add(btn);
    }

    // 2. Apaga todos los botones y enciende solo en el que esta el usuario
    public void ActualizarUIUbicacionUsuario(string idUbicacionActual)
    {
        foreach (var btn in botonesTeleport)
        {
            btn.CambiarEstadoUbicacion(btn.idElemento == idUbicacionActual);
        }
    }

    // 3. NUEVO: La tablet ahora "escucha" los mensajes que vienen del casco VR
    void Update()
    {
        if (connectionScript != null && connectionScript.connected)
        {
            while (connectionScript.messageQueue.TryDequeue(out string mensajeRecibido))
            {
                // Si el casco nos avisa de que el usuario ha viajado por su cuenta...
                if (mensajeRecibido == "REQ_USER")
                {
                    EnviarUsuarioSeleccionadoAlVR();
                }
                else if (mensajeRecibido.StartsWith("UBICACION:"))
                {
                    string idLugar = mensajeRecibido.Split(':')[1];
                    ActualizarUIUbicacionUsuario(idLugar); // Pintamos el boton correcto en la tablet
                    if (sessionTracker != null)
                        sessionTracker.ChangeVrLocation(idLugar);
                }
                else if (mensajeRecibido.StartsWith("HEAD:"))
                {
                    // Le quitamos la palabra "HEAD:" (los primeros 5 caracteres) y pasamos los numeros
                    if (mirrorManager != null)
                        mirrorManager.ActualizarPosicionCamara(mensajeRecibido.Substring(5));
                }
                else if (mensajeRecibido.StartsWith("SYNC:"))
                {
                    string payload = mensajeRecibido.Substring(5);
                    ActivarVRRestauradoDesdeCasco(payload);

                    if (mirrorManager != null)
                        mirrorManager.GenerarEntorno(payload, false);

                    if (sessionTracker != null)
                        RegistrarPosicionesVrDesdeCasco(payload);
                }
                else if (mensajeRecibido.StartsWith("SYNC_TUT:"))
                {
                    string payload = mensajeRecibido.Substring(9);

                    if (mirrorManager != null)
                        mirrorManager.GenerarEntorno(payload, true);

                    if (sessionTracker != null)
                        RegistrarPosicionTutorialDesdeCasco(payload);
                }
            }
        }
    }

    private void ActivarVRRestauradoDesdeCasco(string payload)
    {
        if (modoActual == ModoTablet.VR)
            return;

        List<string> ids = ExtraerIdsDesdePayloadSync(payload);
        if (ids.Count == 0)
            return;

        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        modoActual = ModoTablet.VR;
        ActualizarSpritesBotonesSuperiores();
        SeleccionarBotonesPorIds(ids);
        ActualizarVisibilidadTeleports(true);

        sessionTracker.SavePreparationElements(ids);
        sessionTracker.StartPhase("vr");
        sessionTracker.RegisterVrElements(ids);
    }

    private List<string> ExtraerIdsDesdePayloadSync(string payload)
    {
        List<string> ids = new List<string>();
        if (string.IsNullOrEmpty(payload))
            return ids;

        string[] elementos = payload.Split(';');
        foreach (string elemento in elementos)
        {
            if (string.IsNullOrWhiteSpace(elemento))
                continue;

            string id = elemento.Split('|')[0];
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                ids.Add(id);
        }

        return ids;
    }
    // FUNCION: Para el boton "ACTIVAR" de la pantalla
    public void TogglePantallaEspectador()
    {
        if (mirrorManager == null) return;

        bool nuevoEstado = !mirrorManager.isPantallaActiva;
        mirrorManager.SetPantallaActiva(nuevoEstado);

        // Cambiamos el sprite del boton al instante
        if (imgBotonEspectador != null)
        {
            imgBotonEspectador.sprite = nuevoEstado ? spriteBotonSeleccionado : spriteBotonNormal;
        }

        // Le decimos a las gafas que empiecen o dejen de gastar bateria transmitiendo
        if (connectionScript != null && connectionScript.connected)
        {
            connectionScript.Send(nuevoEstado ? "CMD_TRACKING:ON" : "CMD_TRACKING:OFF");
        }
    }

    // ========================================================
    // LOGICA DE POSTURA (Accesibilidad)
    // ========================================================

    // 1. EL BOTON DE SENTADO (Cambia entre On y Off al pulsarlo)
    public void TogglePosturaSentado()
    {
        posturaActual = (posturaActual == PosturaUsuario.Sentado) ? PosturaUsuario.DePie : PosturaUsuario.Sentado;
        ActualizarSpritesPostura();
        if (sessionTracker != null)
            sessionTracker.SetPosture(posturaActual == PosturaUsuario.Sentado);
        EnviarPosturaAlCasco();
    }

    private void ActualizarSpritesPostura()
    {
        // Como ya no hay boton de pie, solo encendemos/apagamos el de Sentado
        if (imgBotonSentado != null)
            imgBotonSentado.sprite = (posturaActual == PosturaUsuario.Sentado) ? spriteBotonSeleccionado : spriteBotonNormal;
    }

    private void EnviarPosturaAlCasco()
    {
        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send(posturaActual == PosturaUsuario.Sentado ? "POSTURA:SENTADO" : "POSTURA:DE_PIE");
    }

    // 2. EL BOTON DEL MENU VR (Logica visual invertida)
    public void ToggleMenuVR()
    {
        menuVRPermitido = !menuVRPermitido;
        ActualizarSpriteMenuVR();
        if (sessionTracker != null)
            sessionTracker.SetMenuHandsActive(menuVRPermitido);

        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send(menuVRPermitido ? "CMD_MENU_VR:ON" : "CMD_MENU_VR:OFF");
    }

    private void ActualizarSpriteMenuVR()
    {
        // LA CLAVE: Si esta permitido (true), el boton se ve NORMAL (apagado). Si esta bloqueado (false), se ILUMINA.
        if (imgBotonMenuVR != null)
            imgBotonMenuVR.sprite = menuVRPermitido ? spriteBotonNormal : spriteBotonSeleccionado;
    }
    // 3. EL BOTON DE LAS PARTICULAS
    public void ToggleParticulasVR()
    {
        particulasPermitidas = !particulasPermitidas;
        ActualizarSpriteParticulas();
        if (sessionTracker != null)
            sessionTracker.SetHandParticlesActive(particulasPermitidas);

        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send(particulasPermitidas ? "CMD_PARTICULAS:ON" : "CMD_PARTICULAS:OFF");
    }

    private void ActualizarSpriteParticulas()
    {
        // LA CLAVE: Si estan permitidas (true), el boton se ve NORMAL (apagado). Si estan bloqueadas (false), se ILUMINA.
        if (imgBotonParticulas != null)
            imgBotonParticulas.sprite = particulasPermitidas ? spriteBotonNormal : spriteBotonSeleccionado;
    }

    private List<string> GetSelectedElementIds()
    {
        List<string> ids = new List<string>();
        foreach (var btn in botonesSeleccionados)
        {
            if (btn != null && !string.IsNullOrEmpty(btn.idElemento))
                ids.Add(btn.idElemento);
        }
        return ids;
    }

    private void RegistrarPosicionTutorialDesdeCasco(string payload)
    {
        if (TryParseElementoPose(payload, out string idElemento, out double x, out double y, out double z, out double rotY))
            sessionTracker.RecordTutorialElementPose(idElemento, x, y, z, rotY);
    }

    private void RegistrarPosicionesVrDesdeCasco(string payload)
    {
        if (string.IsNullOrEmpty(payload))
            return;

        string[] elementos = payload.Split(';');
        foreach (string elemento in elementos)
        {
            if (TryParseElementoPose(elemento, out string idElemento, out double x, out double y, out double z, out double rotY))
                sessionTracker.RecordVrElementPose(idElemento, x, y, z, rotY);
        }
    }

    private bool TryParseElementoPose(
        string raw,
        out string idElemento,
        out double x,
        out double y,
        out double z,
        out double rotY)
    {
        idElemento = null;
        x = 0;
        y = 0;
        z = 0;
        rotY = 0;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string[] partes = raw.Split('|');
        if (partes.Length < 3)
            return false;

        string[] posicion = partes[1].Split(',');
        if (posicion.Length < 3)
            return false;

        if (!double.TryParse(posicion[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
            !double.TryParse(posicion[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
            !double.TryParse(posicion[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z) ||
            !double.TryParse(partes[2], NumberStyles.Float, CultureInfo.InvariantCulture, out rotY))
        {
            return false;
        }

        idElemento = partes[0];
        return !string.IsNullOrEmpty(idElemento);
    }

}
