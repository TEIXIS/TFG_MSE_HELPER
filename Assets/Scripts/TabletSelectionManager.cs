using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TabletSelectionManager : MonoBehaviour
{
    public enum PosturaUsuario { DePie, Sentado }

    [Header("Ajustes Terapéuticos (Toggles)")]
    public PosturaUsuario posturaActual = PosturaUsuario.DePie; // De pie por defecto
    [Tooltip("Arrastra aquí la Imagen del botón único 'Sentado'")]
    public Image imgBotonSentado;

    [Tooltip("Arrastra aquí la Imagen del botón 'Permitir Menú VR'")]
    public Image imgBotonMenuVR;
    private bool menuVRPermitido = true; // El menú funciona por defecto

    [Tooltip("Botón para BLOQUEAR las partículas de las manos. Si está inactivo, funcionan.")]
    public Image imgBotonParticulas;
    private bool particulasPermitidas = true; // Por defecto funcionan

    // --- NUEVO: Añadimos 'Ninguno' para que no empiece forzado ---
    public enum ModoTablet { Ninguno, Tutorial, PreparacionVR, VR }

    [Header("Modo Espectador")]
    public TabletMirrorManager mirrorManager;
    [Tooltip("Arrastra aquí el componente Image del botón ACTIVAR de la pantalla negra")]
    public Image imgBotonEspectador;


    [Header("Estado Actual")]
    public ModoTablet modoActual = ModoTablet.Ninguno;

    [Header("Configuración")]
    public int maxSelecciones = 6;

    [Header("Interfaz de Usuario (UI)")]
    public TMP_Text textoContador;

    [Header("Red")]
    public Connection connectionScript;

    private List<ElementButton> botonesSeleccionados = new List<ElementButton>();

    [Header("Estética Global de Botones")]
    [Tooltip("La imagen por defecto para TODOS los botones (arriba y abajo)")]
    public Sprite spriteBotonNormal;
    [Tooltip("La imagen cuando un botón está seleccionado o activo")]
    public Sprite spriteBotonSeleccionado;

    [Header("Referencias a los 3 Botones Superiores")]
    [Tooltip("Arrastra aquí el componente Image de los 3 botones de arriba para que cambien de Sprite")]
    public Image imgBotonTutorial;
    public Image imgBotonPreparacion;
    public Image imgBotonVR;

    // Lista para controlar los botones de teletransporte
    private List<TabletTeleportButton> botonesTeleport = new List<TabletTeleportButton>();
    void Start()
    {
        // --- SOLUCIÓN TEARING: Forzamos la sincronización vertical de la tablet ---
        
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // Al arrancar, no hay ningún modo activo
        modoActual = ModoTablet.Ninguno;
        ActualizarSpritesBotonesSuperiores();
        ActualizarContadorUI();
        // APAGAMOS LOS TELEPORTS (No hacen falta aquí)
        ActualizarVisibilidadTeleports(false);
        ActualizarSpritesPostura();
        ActualizarSpriteMenuVR();
        ActualizarSpriteParticulas();
    }

    // ========================================================
    // LÓGICA DE LOS 3 BOTONES SUPERIORES (Modos de la App)
    // ========================================================

    public void ActivarModoTutorial()
    {
        modoActual = ModoTablet.Tutorial;
        ActualizarSpritesBotonesSuperiores();
        LimpiarSelecciones(); // Empezamos de cero

        // APAGAMOS LOS TELEPORTS (No hacen falta aquí)
        ActualizarVisibilidadTeleports(false);

        //  Vaciamos la cámara espectador de objetos anteriores ---
        if (mirrorManager != null) mirrorManager.GenerarEntorno("", false);

        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send("CMD_TUTORIAL");
    }

    public void ActivarModoPreparacion()
    {
        modoActual = ModoTablet.PreparacionVR;
        ActualizarSpritesBotonesSuperiores();
        LimpiarSelecciones(); // Empezamos de cero para elegir hasta 6

        // APAGAMOS LOS TELEPORTS (No hacen falta aquí)
        ActualizarVisibilidadTeleports(false);

        // Vaciamos la cámara espectador de objetos anteriores ---
        if (mirrorManager != null) mirrorManager.GenerarEntorno("", false);

        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send("CMD_PREPARACION");
    }

    public void ActivarModoVR()
    {
        if (botonesSeleccionados.Count == 0)
        {
            Debug.LogWarning("No puedes iniciar VR sin seleccionar ningún elemento en Preparación.");
            return;
        }

        modoActual = ModoTablet.VR;
        ActualizarSpritesBotonesSuperiores();

        // ENCENDEMOS LOS TELEPORTS (¡Ahora sí se puede viajar!)
        ActualizarVisibilidadTeleports(true);

        List<string> ids = new List<string>();
        foreach (var btn in botonesSeleccionados) { ids.Add(btn.idElemento); }
        string paqueteDeDatos = "VR:" + string.Join(",", ids);

        if (connectionScript != null && connectionScript.connected)
        {
            // --- SOLUCIÓN: Refrescamos la memoria de las gafas justo antes de crear la sala ---
            connectionScript.Send(posturaActual == PosturaUsuario.Sentado ? "POSTURA:SENTADO" : "POSTURA:DE_PIE");

            // Ahora sí, enviamos la orden de generar los elementos
            connectionScript.Send(paqueteDeDatos);
        }
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
    // LÓGICA DE LA PARRILLA DE ELEMENTOS
    // ========================================================

    public void ProcesarPulsacion(ElementButton botonPulsado)
    {
        // Seguro anti-errores: Si el terapeuta toca un color sin elegir modo, no hacemos nada
        if (modoActual == ModoTablet.Ninguno)
        {
            Debug.LogWarning("Selecciona primero 'Tutorial' o 'Preparación VR' arriba.");
            return;
        }

        if (modoActual == ModoTablet.Tutorial)
        {
            LimpiarSelecciones();
            botonesSeleccionados.Add(botonPulsado);
            botonPulsado.CambiarEstadoVisual(true);

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

    //FUNCIÓN: Muestra u oculta las chinchetas de teleport ---
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
        // 1. Vaciamos la cámara espectador al instante (Pantalla en negro)
        if (mirrorManager != null) mirrorManager.GenerarEntorno("", false);

        // 2. Sacamos a la tablet del modo VR para que la interfaz sea coherente
        modoActual = ModoTablet.Ninguno;
        ActualizarSpritesBotonesSuperiores();

        // 3. Apagamos las chinchetas de teletransporte (ya no se puede viajar)
        ActualizarVisibilidadTeleports(false);

        // Opcional: Si quieres que además se desmarquen los botones de colores que tenía elegidos, 
        // descomenta la siguiente línea:
        // LimpiarSelecciones(); 

        // 4. Enviamos la orden de fundido a negro a las gafas
        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send("CMD_SOS");
    }

    //  Enviar orden de Teletransporte Remoto ---
    public void EnviarOrdenTeleport(string idElemento)
    {
        // Solo permitimos mover al usuario si la experiencia VR está encendida
        if (modoActual != ModoTablet.VR)
        {
            Debug.LogWarning("Solo puedes teletransportar al usuario si estás en modo VR activo.");
            return;
        }

        // Comprobamos si el terapeuta realmente metió este elemento en la sala
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

            // Actualizamos la UI de la tablet al instante ---
            ActualizarUIUbicacionUsuario(idElemento);
        }
        else
        {
            Debug.LogWarning($"El elemento {idElemento} no está en la sala, no se puede viajar a él.");
        }
    }


    // 1. Guarda los botones al arrancar
    public void RegistrarBotonTeleport(TabletTeleportButton btn)
    {
        if (!botonesTeleport.Contains(btn)) botonesTeleport.Add(btn);
    }

    // 2. Apaga todos los botones y enciende solo en el que está el usuario
    public void ActualizarUIUbicacionUsuario(string idUbicacionActual)
    {
        foreach (var btn in botonesTeleport)
        {
            btn.CambiarEstadoUbicacion(btn.idElemento == idUbicacionActual);
        }
    }

    // 3. ¡NUEVO! La tablet ahora "escucha" los mensajes que vienen del casco VR
    void Update()
    {
        if (connectionScript != null && connectionScript.connected)
        {
            while (connectionScript.messageQueue.TryDequeue(out string mensajeRecibido))
            {
                // Si el casco nos avisa de que el usuario ha viajado por su cuenta...
                if (mensajeRecibido.StartsWith("UBICACION:"))
                {
                    string idLugar = mensajeRecibido.Split(':')[1];
                    ActualizarUIUbicacionUsuario(idLugar); // Pintamos el botón correcto en la tablet
                }
                else if (mensajeRecibido.StartsWith("HEAD:"))
                {
                    // Le quitamos la palabra "HEAD:" (los primeros 5 caracteres) y pasamos los números
                    if (mirrorManager != null)
                        mirrorManager.ActualizarPosicionCamara(mensajeRecibido.Substring(5));
                }
                else if (mensajeRecibido.StartsWith("SYNC:"))
                {
                    if (mirrorManager != null)
                        mirrorManager.GenerarEntorno(mensajeRecibido.Substring(5), false);
                }
                else if (mensajeRecibido.StartsWith("SYNC_TUT:"))
                {
                    if (mirrorManager != null)
                        mirrorManager.GenerarEntorno(mensajeRecibido.Substring(9), true);
                }
            }
        }
    }
    // FUNCIÓN: Para el botón "ACTIVAR" de la pantalla ---
    public void TogglePantallaEspectador()
    {
        if (mirrorManager == null) return;

        bool nuevoEstado = !mirrorManager.isPantallaActiva;
        mirrorManager.SetPantallaActiva(nuevoEstado);

        // Cambiamos el sprite del botón al instante ---
        if (imgBotonEspectador != null)
        {
            imgBotonEspectador.sprite = nuevoEstado ? spriteBotonSeleccionado : spriteBotonNormal;
        }

        // Le decimos a las gafas que empiecen o dejen de gastar batería transmitiendo
        if (connectionScript != null && connectionScript.connected)
        {
            connectionScript.Send(nuevoEstado ? "CMD_TRACKING:ON" : "CMD_TRACKING:OFF");
        }
    }

    // ========================================================
    // LÓGICA DE POSTURA (Accesibilidad)
    // ========================================================

    // 1. EL BOTÓN DE SENTADO (Cambia entre On y Off al pulsarlo)
    public void TogglePosturaSentado()
    {
        posturaActual = (posturaActual == PosturaUsuario.Sentado) ? PosturaUsuario.DePie : PosturaUsuario.Sentado;
        ActualizarSpritesPostura();
        EnviarPosturaAlCasco();
    }

    private void ActualizarSpritesPostura()
    {
        // Como ya no hay botón de pie, solo encendemos/apagamos el de Sentado
        if (imgBotonSentado != null)
            imgBotonSentado.sprite = (posturaActual == PosturaUsuario.Sentado) ? spriteBotonSeleccionado : spriteBotonNormal;
    }

    private void EnviarPosturaAlCasco()
    {
        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send(posturaActual == PosturaUsuario.Sentado ? "POSTURA:SENTADO" : "POSTURA:DE_PIE");
    }

    // 2. EL BOTÓN DEL MENÚ VR (Lógica visual invertida)
    public void ToggleMenuVR()
    {
        menuVRPermitido = !menuVRPermitido;
        ActualizarSpriteMenuVR();

        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send(menuVRPermitido ? "CMD_MENU_VR:ON" : "CMD_MENU_VR:OFF");
    }

    private void ActualizarSpriteMenuVR()
    {
        // LA CLAVE: Si está permitido (true), el botón se ve NORMAL (apagado). Si está bloqueado (false), se ILUMINA.
        if (imgBotonMenuVR != null)
            imgBotonMenuVR.sprite = menuVRPermitido ? spriteBotonNormal : spriteBotonSeleccionado;
    }
    // 3. EL BOTÓN DE LAS PARTÍCULAS
    public void ToggleParticulasVR()
    {
        particulasPermitidas = !particulasPermitidas;
        ActualizarSpriteParticulas();

        if (connectionScript != null && connectionScript.connected)
            connectionScript.Send(particulasPermitidas ? "CMD_PARTICULAS:ON" : "CMD_PARTICULAS:OFF");
    }

    private void ActualizarSpriteParticulas()
    {
        // LA CLAVE: Si están permitidas (true), el botón se ve NORMAL (apagado). Si están bloqueadas (false), se ILUMINA.
        if (imgBotonParticulas != null)
            imgBotonParticulas.sprite = particulasPermitidas ? spriteBotonNormal : spriteBotonSeleccionado;
    }

}