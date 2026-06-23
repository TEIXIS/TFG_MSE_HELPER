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
    [Tooltip("Aviso mostrado cuando se solicita el mundo VR de la tablet fuera de la fase VR real del visor.")]
    public GameObject missatgeNoVR;
    [TextArea(2, 3)]
    public string textoMissatgeNoVR = "La vista del món VR estarà disponible quan el visor entri a la fase VR.";


    [Header("Estado Actual")]
    public ModoTablet modoActual = ModoTablet.Ninguno;

    [Header("Configuracion")]
    public int maxSelecciones = 6;

    [Header("Interfaz de Usuario (UI)")]
    public TMP_Text textoContador;
    public TMP_Text textoNombreUsuario;
    public AmbientLightTabletControl ambientLightControl;
    public MusicTabletControl musicControl;

    [Header("Red")]
    public Connection connectionScript;
    public SessionTracker sessionTracker;

    private List<ElementButton> botonesSeleccionados = new List<ElementButton>();
    private List<string> ultimaSeleccionPreparacionIds = new List<string>();
    private bool seleccionRestauradaDesdeCasco;

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
    private string ultimoNombreUsuarioMostrado;
    private bool mundoVrTabletSolicitado;
    private bool visorEnFaseVR;
    private bool trackingEspectadorEnviado;

    private void OnEnable()
    {
        AutoWireUserNameText();
        ActualizarNombreUsuarioUI();

        if (inicializado && connectionScript != null && connectionScript.connected)
            EnviarUsuarioSeleccionadoAlVR();
    }

    void Start()
    {
        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        if (ambientLightControl == null)
            ambientLightControl = FindFirstObjectByType<AmbientLightTabletControl>();

        if (musicControl == null)
            musicControl = FindFirstObjectByType<MusicTabletControl>();

        AutoWireUserNameText();
        ActualizarNombreUsuarioUI();

        // --- SOLUCION TEARING: Forzamos la sincronizacion vertical de la tablet ---

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // Al arrancar, no hay ningun modo activo
        modoActual = ModoTablet.Ninguno;
        ActualizarSpritesBotonesSuperiores();
        ActualizarContadorUI();
        // APAGAMOS LOS TELEPORTS (No hacen falta aqui)
        ActualizarVisibilidadTeleports(false);
        if (SessionUser.SelectedUserId > 0)
            posturaActual = SessionUser.SelectedInitialPosture == "SENTADO" ? PosturaUsuario.Sentado : PosturaUsuario.DePie;

        ActualizarSpritesPostura();
        ActualizarSpriteMenuVR();
        ActualizarSpriteParticulas();

        sessionTracker.ApplyInitialSettings(posturaActual == PosturaUsuario.Sentado, menuVRPermitido, particulasPermitidas);
        ResolverMissatgeNoVR();
        mundoVrTabletSolicitado = mirrorManager != null && mirrorManager.isPantallaActiva;
        AplicarEstadoMundoVRTablet();

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
        connectionScript.Send("ROOM:" + SessionUser.SelectedRoomType);
        connectionScript.Send(SessionUser.SelectedInitialPosture == "SENTADO" ? "POSTURA:SENTADO" : "POSTURA:DE_PIE");

        if (ambientLightControl != null)
            ambientLightControl.SendCurrentLevel();
    }

    private void AutoWireUserNameText()
    {
        if (textoNombreUsuario != null)
            return;

        TMP_Text[] textos = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text texto in textos)
        {
            if (texto != null && texto.name == "Text_nom_usuari")
            {
                textoNombreUsuario = texto;
                return;
            }
        }

        textos = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TMP_Text texto in textos)
        {
            if (texto != null && texto.name == "Text_nom_usuari")
            {
                textoNombreUsuario = texto;
                return;
            }
        }
    }

    private void ActualizarNombreUsuarioUI()
    {
        if (textoNombreUsuario == null)
            return;

        string nombreUsuario = string.IsNullOrWhiteSpace(SessionUser.SelectedUserName)
            ? "-"
            : SessionUser.SelectedUserName;

        textoNombreUsuario.text = nombreUsuario;
        ultimoNombreUsuarioMostrado = nombreUsuario;
    }

    // ========================================================
    // LOGICA DE LOS 3 BOTONES SUPERIORES (Modos de la App)
    // ========================================================

    public void ActivarModoTutorial()
    {
        ActivarModoTutorialInterno(true);
    }

    // El visor puede iniciar el tutorial automaticamente cuando el usuario no
    // tiene una sesion anterior. En ese caso no se reenvia CMD_TUTORIAL.
    private void ActivarModoTutorialInterno(bool informarAlVisor)
    {
        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        GuardarSeleccionPreparacionActual();
        sessionTracker.StartPhase("tutorial");

        modoActual = ModoTablet.Tutorial;
        ActualizarSpritesBotonesSuperiores();
        LimpiarSeleccionesSinBorrarMemoriaPreparacion();

        // APAGAMOS LOS TELEPORTS (No hacen falta aqui)
        ActualizarVisibilidadTeleports(false);

        // Vaciamos la camara espectador de objetos anteriores
        if (mirrorManager != null) mirrorManager.GenerarEntorno("", false);

        if (informarAlVisor && connectionScript != null && connectionScript.connected)
        {
            connectionScript.Send("CMD_TUTORIAL");
        }
    }

    public void ActivarModoPreparacion()
    {
        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        ModoTablet modoAnterior = modoActual;
        sessionTracker.StartPhase("preparacio");

        modoActual = ModoTablet.PreparacionVR;
        ActualizarSpritesBotonesSuperiores();

        if (modoAnterior == ModoTablet.Tutorial && botonesSeleccionados.Count == 0)
            RestaurarSeleccionPreparacionAnterior();
        else if (modoAnterior == ModoTablet.Ninguno && !seleccionRestauradaDesdeCasco && botonesSeleccionados.Count == 0)
            LimpiarSelecciones();

        // APAGAMOS LOS TELEPORTS (No hacen falta aqui)
        ActualizarVisibilidadTeleports(false);

        // Vaciamos la camara espectador de objetos anteriores
        if (mirrorManager != null && botonesSeleccionados.Count == 0)
            mirrorManager.GenerarEntorno("", false);

        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send("CMD_PREPARACION");
    }

    public void ActivarModoVR()
    {
        if (modoActual == ModoTablet.Tutorial)
        {
            Debug.LogWarning("No puedes pasar directamente de Tutorial a VR. Primero entra en Preparacion VR y confirma la seleccion.");
            return;
        }

        if (modoActual != ModoTablet.PreparacionVR && modoActual != ModoTablet.VR)
        {
            Debug.LogWarning("Para iniciar VR primero debes pasar por Preparacion VR.");
            return;
        }

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
        ultimaSeleccionPreparacionIds = new List<string>(ids);
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
        if (modoActual != ModoTablet.Tutorial)
            ultimaSeleccionPreparacionIds = ids != null ? new List<string>(ids) : new List<string>();

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

    private void EnviarPrimerElementoSeleccionadoATutorial()
    {
        if (botonesSeleccionados.Count == 0 || connectionScript == null || !connectionScript.connected)
            return;

        ElementButton primerBoton = botonesSeleccionados[0];
        if (primerBoton == null || string.IsNullOrEmpty(primerBoton.idElemento))
            return;

        connectionScript.Send("TUTORIAL:" + primerBoton.idElemento);
        if (sessionTracker != null)
            sessionTracker.TrackTutorialElement(primerBoton.idElemento);
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
        seleccionRestauradaDesdeCasco = false;
        ultimaSeleccionPreparacionIds.Clear();
        ActualizarContadorUI();
    }

    // Limpia la seleccion visible al entrar en tutorial sin olvidar lo que habia preparado para VR.
    private void LimpiarSeleccionesSinBorrarMemoriaPreparacion()
    {
        foreach (var btn in botonesSeleccionados) { btn.CambiarEstadoVisual(false); }
        botonesSeleccionados.Clear();
        seleccionRestauradaDesdeCasco = false;
        ActualizarContadorUI();
    }

    // Guarda los elementos de preparacion o VR para poder recuperarlos al salir del tutorial.
    private void GuardarSeleccionPreparacionActual()
    {
        if (modoActual == ModoTablet.Tutorial)
            return;

        ultimaSeleccionPreparacionIds = GetSelectedElementIds();
    }

    // Vuelve a marcar en la tablet los elementos que habia antes de entrar en tutorial.
    private void RestaurarSeleccionPreparacionAnterior()
    {
        if (ultimaSeleccionPreparacionIds == null || ultimaSeleccionPreparacionIds.Count == 0)
            return;

        SeleccionarBotonesPorIds(ultimaSeleccionPreparacionIds);
        seleccionRestauradaDesdeCasco = true;
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
            LimpiarSeleccionesSinBorrarMemoriaPreparacion();
            seleccionRestauradaDesdeCasco = false;
            botonesSeleccionados.Add(botonPulsado);
            botonPulsado.CambiarEstadoVisual(true);
            if (sessionTracker != null)
                sessionTracker.TrackTutorialElement(botonPulsado.idElemento);

            if (connectionScript != null && connectionScript.connected)
                connectionScript.Send("TUTORIAL:" + botonPulsado.idElemento);
        }
        else if (modoActual == ModoTablet.PreparacionVR)
        {
            seleccionRestauradaDesdeCasco = false;
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
            GuardarSeleccionPreparacionActual();
        }
        else if (modoActual == ModoTablet.VR)
        {
            seleccionRestauradaDesdeCasco = false;
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
            GuardarSeleccionPreparacionActual();
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
        ActualizarEstadoVRDelVisor(false);

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
        string nombreUsuarioActual = string.IsNullOrWhiteSpace(SessionUser.SelectedUserName)
            ? "-"
            : SessionUser.SelectedUserName;

        if (nombreUsuarioActual != ultimoNombreUsuarioMostrado)
            ActualizarNombreUsuarioUI();

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
                else if (mensajeRecibido.StartsWith("VR_STATE:"))
                {
                    ActualizarEstadoVRDelVisor(mensajeRecibido.Substring("VR_STATE:".Length) == "ON");
                }
                else if (mensajeRecibido == "PHASE:TUTORIAL")
                {
                    ActivarModoTutorialInterno(false);
                }
                else if (mensajeRecibido.StartsWith("CMD_AMBIENT_LIGHT_LEVEL:"))
                {
                    if (ambientLightControl != null &&
                        int.TryParse(mensajeRecibido.Substring("CMD_AMBIENT_LIGHT_LEVEL:".Length), out int lightLevel))
                    {
                        Debug.Log("[LUZ] Recibido nivel de luz aplicado por VR: " + lightLevel);
                        ambientLightControl.SetLevelFromRemote(lightLevel);
                    }
                }
                else if (mensajeRecibido.StartsWith("CMD_AMBIENT_LIGHT_COLOR:"))
                {
                    string lightColor = mensajeRecibido.Substring("CMD_AMBIENT_LIGHT_COLOR:".Length);
                    Debug.Log("[LUZ] Recibido color de luz aplicado por VR: " + lightColor);
                    if (ambientLightControl != null)
                        ambientLightControl.SetColorFromRemote(lightColor);
                }
                else if (mensajeRecibido.StartsWith("CMD_MUSIC_SONG:"))
                {
                    string songName = mensajeRecibido.Substring("CMD_MUSIC_SONG:".Length);
                    Debug.Log("[MUSICA] Recibida cancion aplicada por VR: " + songName);
                    if (musicControl != null)
                        musicControl.SetSongFromRemote(songName);
                }
                else if (mensajeRecibido.StartsWith("CMD_MUSIC_VOLUME:"))
                {
                    if (musicControl != null &&
                        int.TryParse(mensajeRecibido.Substring("CMD_MUSIC_VOLUME:".Length), out int musicVolumeLevel))
                    {
                        Debug.Log("[MUSICA] Recibido volumen aplicado por VR: " + musicVolumeLevel);
                        musicControl.SetVolumeLevelFromRemote(musicVolumeLevel);
                    }
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
                else if (mensajeRecibido.StartsWith("VR_FINAL:"))
                {
                    string payload = mensajeRecibido.Substring("VR_FINAL:".Length);
                    RegistrarFinalVrDesdeCasco(payload);
                }
                else if (mensajeRecibido.StartsWith("PREP:"))
                {
                    string payload = mensajeRecibido.Substring(5);
                    ActivarPreparacionRestauradaDesdeCasco(payload);

                    if (mirrorManager != null)
                        mirrorManager.GenerarEntorno(payload, false);
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

        if (modoActual == ModoTablet.Tutorial)
        {
            Debug.LogWarning("SYNC VR ignorado porque la tablet esta en Tutorial. Primero debe pasarse por Preparacion VR.");
            return;
        }

        List<string> ids = ExtraerIdsDesdePayloadSync(payload);
        if (ids.Count == 0)
            return;

        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        modoActual = ModoTablet.VR;
        ActualizarSpritesBotonesSuperiores();
        SeleccionarBotonesPorIds(ids);
        seleccionRestauradaDesdeCasco = true;
        ActualizarVisibilidadTeleports(true);

        sessionTracker.SavePreparationElements(ids);
        sessionTracker.StartPhase("vr");
        sessionTracker.RegisterVrElements(ids);
    }

    private void ActivarPreparacionRestauradaDesdeCasco(string payload)
    {
        List<string> ids = ExtraerIdsDesdePayloadSync(payload);
        if (ids.Count == 0 && botonesSeleccionados.Count > 0)
            return;

        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        modoActual = ModoTablet.PreparacionVR;
        ActualizarSpritesBotonesSuperiores();
        SeleccionarBotonesPorIds(ids);
        seleccionRestauradaDesdeCasco = ids.Count > 0;
        ActualizarVisibilidadTeleports(false);

        if (sessionTracker != null && ids.Count > 0)
            sessionTracker.SavePreparationElements(ids);
    }

    private void RegistrarFinalVrDesdeCasco(string payload)
    {
        List<string> ids = ExtraerIdsDesdePayloadSync(payload);
        if (ids.Count == 0)
            return;

        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        sessionTracker.RegisterVrElements(ids);
        RegistrarPosicionesVrDesdeCasco(payload);
        SeleccionarBotonesPorIds(ids);
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
        mundoVrTabletSolicitado = !mundoVrTabletSolicitado;
        AplicarEstadoMundoVRTablet();
    }

    private void ActualizarEstadoVRDelVisor(bool enVR)
    {
        visorEnFaseVR = enVR;
        AplicarEstadoMundoVRTablet();
    }

    // Conserva la intencion del boton, pero solo activa MundoVR_Tablet y la
    // camara espectadora cuando el visor confirma que su sala VR esta activa.
    private void AplicarEstadoMundoVRTablet()
    {
        bool debeRenderizarMundoVR = mundoVrTabletSolicitado && visorEnFaseVR;

        if (mirrorManager != null && mirrorManager.isPantallaActiva != debeRenderizarMundoVR)
            mirrorManager.SetPantallaActiva(debeRenderizarMundoVR);

        MostrarMissatgeNoVR(mundoVrTabletSolicitado && !visorEnFaseVR);

        if (imgBotonEspectador != null)
            imgBotonEspectador.sprite = mundoVrTabletSolicitado ? spriteBotonSeleccionado : spriteBotonNormal;

        EnviarEstadoTracking(debeRenderizarMundoVR);
    }

    private void EnviarEstadoTracking(bool activo)
    {
        if (trackingEspectadorEnviado == activo)
            return;

        trackingEspectadorEnviado = activo;
        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send(activo ? "CMD_TRACKING:ON" : "CMD_TRACKING:OFF");
    }

    private void ResolverMissatgeNoVR()
    {
        if (missatgeNoVR == null)
        {
            foreach (Transform candidato in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidato != null &&
                    candidato.gameObject.scene.IsValid() &&
                    (candidato.name == "MissatgeNoVR" || candidato.name == "MensajeNoVR"))
                {
                    missatgeNoVR = candidato.gameObject;
                    break;
                }
            }
        }

        if (missatgeNoVR == null)
            missatgeNoVR = CrearMissatgeNoVRDeReserva();

        MostrarMissatgeNoVR(false);
    }

    private GameObject CrearMissatgeNoVRDeReserva()
    {
        Canvas canvas = imgBotonEspectador != null
            ? imgBotonEspectador.GetComponentInParent<Canvas>(true)
            : FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("No se pudo crear MissatgeNoVR: no se encontro un Canvas.");
            return null;
        }

        GameObject panel = new GameObject("MissatgeNoVR", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, -190f);
        panelRect.sizeDelta = new Vector2(760f, 105f);
        panel.GetComponent<Image>().color = new Color(0.05f, 0.1f, 0.18f, 0.92f);

        GameObject texto = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        texto.transform.SetParent(panel.transform, false);
        RectTransform textoRect = texto.GetComponent<RectTransform>();
        textoRect.anchorMin = Vector2.zero;
        textoRect.anchorMax = Vector2.one;
        textoRect.offsetMin = new Vector2(24f, 12f);
        textoRect.offsetMax = new Vector2(-24f, -12f);

        TextMeshProUGUI textoTMP = texto.GetComponent<TextMeshProUGUI>();
        textoTMP.text = textoMissatgeNoVR;
        textoTMP.fontSize = 28f;
        textoTMP.alignment = TextAlignmentOptions.Center;
        textoTMP.enableWordWrapping = true;
        textoTMP.color = Color.white;

        return panel;
    }

    private void MostrarMissatgeNoVR(bool mostrar)
    {
        if (missatgeNoVR != null && missatgeNoVR.activeSelf != mostrar)
            missatgeNoVR.SetActive(mostrar);
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
