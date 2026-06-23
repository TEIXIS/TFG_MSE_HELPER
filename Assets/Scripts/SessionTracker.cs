using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SessionTracker : MonoBehaviour
{
    public static SessionTracker Instance { get; private set; }

    [Header("Estado actual")]
    public int currentSessionId = -1;
    public string currentPosture = "DE_PIE";
    public bool currentMenuHandsActive = true;
    public bool currentHandParticlesActive = true;

    private bool sessionStartRequested;
    private bool sessionEndInProgress;
    private int pendingPersistenceRequests;
    private DateTime sessionStartedAt;

    // El cierre de una aplicación no debe interrumpir las peticiones que crean
    // la sesión, sus fases o sus elementos. Este margen solo se usa al cerrar.
    private const float SessionCloseWaitTimeoutSeconds = 12f;

    private int currentPhaseId = -1;
    private string currentPhase;
    private DateTime currentPhaseStartedAt;

    private int currentTutorialElementId = -1;
    private DateTime currentTutorialElementStartedAt;

    private readonly Dictionary<string, int> vrElementRowIds = new();
    private readonly Dictionary<string, int> vrElementPositions = new();
    private string currentVrElement;
    private DateTime currentVrElementStartedAt;

    private readonly HashSet<string> savedPreparationElements = new();

    private double tutorialDuration;
    private double preparationDuration;
    private double vrDuration;
    private bool enteredTutorial;
    private bool enteredPreparation;
    private bool enteredVr;

    public static SessionTracker GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        SessionTracker tracker = FindFirstObjectByType<SessionTracker>();
        if (tracker != null)
            return tracker;

        GameObject go = new("SessionTracker");
        DontDestroyOnLoad(go);
        return go.AddComponent<SessionTracker>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartSessionForSelectedUser()
    {
        if (SessionUser.SelectedUserId <= 0)
        {
            Debug.LogWarning("No se puede iniciar sesion: no hay usuario seleccionado.");
            return;
        }

        StartSession(SessionUser.SelectedUserId);
    }

    public void StartSession(int userId)
    {
        if (currentSessionId > 0 || sessionStartRequested)
            return;

        sessionStartRequested = true;
        sessionStartedAt = DateTime.UtcNow;

        StartTrackedPersistenceRequest(UserAPI.CreateSession(
            userId,
            currentPosture,
            currentMenuHandsActive,
            currentHandParticlesActive,
            id =>
            {
                currentSessionId = id;
                sessionStartRequested = false;
                Debug.Log("Sesion iniciada: " + currentSessionId);
            },
            err =>
            {
                sessionStartRequested = false;
                Debug.LogError("Error creando sesion: " + err);
            }
        ));
    }

    public void SetPosture(bool sentado)
    {
        currentPosture = sentado ? "SENTADO" : "DE_PIE";
        SendSettingsSnapshot();
    }

    public void ApplyInitialSettings(bool sentado, bool menuHandsActive, bool handParticlesActive)
    {
        currentPosture = sentado ? "SENTADO" : "DE_PIE";
        currentMenuHandsActive = menuHandsActive;
        currentHandParticlesActive = handParticlesActive;
    }

    public void SetMenuHandsActive(bool active)
    {
        currentMenuHandsActive = active;
        SendSettingsSnapshot();
    }

    public void SetHandParticlesActive(bool active)
    {
        currentHandParticlesActive = active;
        SendSettingsSnapshot();
    }

    public void StartPhase(string phase)
    {
        if (currentPhase == phase)
            return;

        StartSessionForSelectedUser();
        CloseCurrentTutorialElement();
        CloseCurrentVrElement();
        CloseCurrentPhase();

        currentPhase = phase;
        currentPhaseStartedAt = DateTime.UtcNow;

        if (phase == "tutorial") enteredTutorial = true;
        if (phase == "preparacio")
        {
            enteredPreparation = true;
            savedPreparationElements.Clear();
        }
        if (phase == "vr") enteredVr = true;

        StartTrackedPersistenceRequest(WhenSessionReady(() => UserAPI.CreateSessionPhase(
            currentSessionId,
            phase,
            id => currentPhaseId = id,
            err => Debug.LogError("Error creando fase de sesion: " + err)
        )));
    }

    public void TrackTutorialElement(string elementId)
    {
        if (string.IsNullOrEmpty(elementId))
            return;

        StartPhaseIfNeeded("tutorial");
        CloseCurrentTutorialElement();

        currentTutorialElementStartedAt = DateTime.UtcNow;
        StartTrackedPersistenceRequest(WhenSessionReady(() => UserAPI.CreateTutorialElement(
            currentSessionId,
            elementId,
            id => currentTutorialElementId = id,
            err => Debug.LogError("Error guardando elemento tutorial: " + err)
        )));
    }

    public void SavePreparationElements(IEnumerable<string> elementIds)
    {
        StartPhaseIfNeeded("preparacio");

        HashSet<string> currentSelection = new();
        foreach (string elementId in elementIds)
        {
            if (!string.IsNullOrEmpty(elementId))
                currentSelection.Add(elementId);
        }

        List<string> removedElements = new();
        foreach (string savedElementId in savedPreparationElements)
        {
            if (!currentSelection.Contains(savedElementId))
                removedElements.Add(savedElementId);
        }

        foreach (string removedElementId in removedElements)
        {
            savedPreparationElements.Remove(removedElementId);
            StartTrackedPersistenceRequest(WhenSessionReady(() => UserAPI.CreatePreparationElement(
                currentSessionId,
                removedElementId,
                false,
                err => Debug.LogError("Error guardando elemento deseleccionado de preparacion: " + err)
            )));
        }

        foreach (string elementId in currentSelection)
        {
            if (savedPreparationElements.Contains(elementId))
                continue;

            savedPreparationElements.Add(elementId);
            StartTrackedPersistenceRequest(WhenSessionReady(() => UserAPI.CreatePreparationElement(
                currentSessionId,
                elementId,
                true,
                err => Debug.LogError("Error guardando elemento de preparacion: " + err)
            )));
        }
    }

    public void RegisterVrElements(IReadOnlyList<string> elementIds)
    {
        StartPhaseIfNeeded("vr");
        vrElementRowIds.Clear();
        vrElementPositions.Clear();

        List<string> finalElements = new();
        foreach (string elementId in elementIds)
        {
            if (string.IsNullOrEmpty(elementId))
                continue;

            if (finalElements.Contains(elementId))
                continue;

            finalElements.Add(elementId);
            if (finalElements.Count >= 6)
                break;
        }

        for (int i = 0; i < finalElements.Count; i++)
        {
            string elementId = finalElements[i];
            int position = i + 1;
            vrElementPositions[elementId] = position;

            StartTrackedPersistenceRequest(WhenSessionReady(() => UserAPI.CreateVrElement(
                currentSessionId,
                elementId,
                position,
                id => vrElementRowIds[elementId] = id,
                err => Debug.LogError("Error guardando elemento VR: " + err)
            )));
        }
    }

    public void ChangeVrLocation(string elementId)
    {
        if (string.IsNullOrEmpty(elementId) || currentVrElement == elementId)
            return;

        CloseCurrentVrElement();
        currentVrElement = elementId;
        currentVrElementStartedAt = DateTime.UtcNow;
    }

    public void RecordTutorialElementPose(string elementId, double x, double y, double z, double rotY)
    {
        if (string.IsNullOrEmpty(elementId))
            return;

        StartTrackedPersistenceRequest(WhenTutorialElementRowReady(rowId => UserAPI.UpdateTutorialElementPose(
            rowId,
            x,
            y,
            z,
            rotY,
            err => Debug.LogError("Error actualizando posicion de tutorial: " + err)
        )));
    }

    public void RecordVrElementPose(string elementId, double x, double y, double z, double rotY)
    {
        if (string.IsNullOrEmpty(elementId))
            return;

        StartTrackedPersistenceRequest(WhenVrElementRowReady(elementId, rowId => UserAPI.UpdateVrElementPose(
            rowId,
            x,
            y,
            z,
            rotY,
            err => Debug.LogError("Error actualizando posicion VR: " + err)
        )));
    }

    public void EndSession(string observations = null)
    {
        if (sessionEndInProgress)
            return;

        sessionEndInProgress = true;
        StartCoroutine(EndSessionRoutine(observations));
    }

    // Usado por el cierre iniciado desde la tablet. No devuelve hasta que las
    // peticiones de persistencia ya han terminado o han agotado el margen.
    public IEnumerator EndSessionAndWait(string observations = null)
    {
        if (sessionEndInProgress)
        {
            while (sessionEndInProgress)
                yield return null;

            yield break;
        }

        sessionEndInProgress = true;
        yield return StartCoroutine(EndSessionRoutine(observations));
    }

    private void StartPhaseIfNeeded(string phase)
    {
        if (currentPhase != phase)
            StartPhase(phase);
    }

    private void CloseCurrentPhase()
    {
        if (string.IsNullOrEmpty(currentPhase))
            return;

        double duration = SecondsSince(currentPhaseStartedAt);
        if (currentPhase == "tutorial") tutorialDuration += duration;
        if (currentPhase == "preparacio") preparationDuration += duration;
        if (currentPhase == "vr") vrDuration += duration;

        int phaseId = currentPhaseId;
        if (phaseId > 0)
        {
            StartTrackedPersistenceRequest(UserAPI.EndSessionPhase(
                phaseId,
                duration,
                err => Debug.LogError("Error cerrando fase de sesion: " + err)
            ));
        }

        currentPhaseId = -1;
        currentPhase = null;
    }

    private void CloseCurrentTutorialElement()
    {
        if (currentTutorialElementId <= 0)
            return;

        int rowId = currentTutorialElementId;
        double duration = SecondsSince(currentTutorialElementStartedAt);
        StartTrackedPersistenceRequest(UserAPI.EndTutorialElement(
            rowId,
            duration,
            err => Debug.LogError("Error cerrando elemento tutorial: " + err)
        ));

        currentTutorialElementId = -1;
    }

    private void CloseCurrentVrElement()
    {
        if (string.IsNullOrEmpty(currentVrElement))
            return;

        string elementId = currentVrElement;
        double duration = SecondsSince(currentVrElementStartedAt);

        StartTrackedPersistenceRequest(WhenVrElementRowReady(elementId, rowId => UserAPI.EndVrElement(
            rowId,
            duration,
            err => Debug.LogError("Error cerrando elemento VR: " + err)
        )));

        currentVrElement = null;
    }

    private void SendSettingsSnapshot()
    {
        if (SessionUser.SelectedUserId > 0)
            StartSessionForSelectedUser();

        StartTrackedPersistenceRequest(WhenSessionReady(() => UserAPI.UpdateSessionSettings(
            currentSessionId,
            currentPosture,
            currentMenuHandsActive,
            currentHandParticlesActive,
            err => Debug.LogError("Error actualizando configuracion de sesion: " + err)
        )));
    }

    private IEnumerator EndSessionRoutine(string observations)
    {
        yield return StartCoroutine(WaitForPendingPersistenceRequests());

        if (currentSessionId <= 0)
        {
            Debug.LogWarning("No se pudo cerrar una sesion porque no llego a crearse en la API.");
            ResetState();
            sessionEndInProgress = false;
            yield break;
        }

        yield return StartCoroutine(CloseCurrentTutorialElementAndWait());
        yield return StartCoroutine(CloseCurrentVrElementAndWait());
        yield return StartCoroutine(CloseCurrentPhaseAndWait());

        int sessionId = currentSessionId;
        double totalDuration = SecondsSince(sessionStartedAt);

        yield return StartCoroutine(UserAPI.EndSession(
            sessionId,
            totalDuration,
            tutorialDuration,
            preparationDuration,
            vrDuration,
            enteredTutorial,
            enteredPreparation,
            enteredVr,
            currentPosture,
            observations,
            err => Debug.LogError("Error cerrando sesion: " + err)
        ));

        ResetState();
        sessionEndInProgress = false;
    }

    private IEnumerator CloseCurrentPhaseAndWait()
    {
        if (string.IsNullOrEmpty(currentPhase))
            yield break;

        double duration = SecondsSince(currentPhaseStartedAt);
        if (currentPhase == "tutorial") tutorialDuration += duration;
        if (currentPhase == "preparacio") preparationDuration += duration;
        if (currentPhase == "vr") vrDuration += duration;

        int phaseId = currentPhaseId;
        currentPhaseId = -1;
        currentPhase = null;

        if (phaseId > 0)
        {
            yield return StartCoroutine(UserAPI.EndSessionPhase(
                phaseId,
                duration,
                err => Debug.LogError("Error cerrando fase de sesion: " + err)
            ));
        }
    }

    private IEnumerator CloseCurrentTutorialElementAndWait()
    {
        if (currentTutorialElementId <= 0)
            yield break;

        int rowId = currentTutorialElementId;
        double duration = SecondsSince(currentTutorialElementStartedAt);
        currentTutorialElementId = -1;

        yield return StartCoroutine(UserAPI.EndTutorialElement(
            rowId,
            duration,
            err => Debug.LogError("Error cerrando elemento tutorial: " + err)
        ));
    }

    private IEnumerator CloseCurrentVrElementAndWait()
    {
        if (string.IsNullOrEmpty(currentVrElement))
            yield break;

        string elementId = currentVrElement;
        double duration = SecondsSince(currentVrElementStartedAt);
        currentVrElement = null;

        if (!vrElementRowIds.TryGetValue(elementId, out int rowId))
        {
            Debug.LogWarning("No se pudo cerrar el elemento VR '" + elementId + "' porque no se creo su registro.");
            yield break;
        }

        yield return StartCoroutine(UserAPI.EndVrElement(
            rowId,
            duration,
            err => Debug.LogError("Error cerrando elemento VR: " + err)
        ));
    }

    private void StartTrackedPersistenceRequest(IEnumerator request)
    {
        pendingPersistenceRequests++;
        StartCoroutine(RunTrackedPersistenceRequest(request));
    }

    private IEnumerator RunTrackedPersistenceRequest(IEnumerator request)
    {
        yield return StartCoroutine(request);
        pendingPersistenceRequests = Mathf.Max(0, pendingPersistenceRequests - 1);
    }

    private IEnumerator WaitForPendingPersistenceRequests()
    {
        float deadline = Time.realtimeSinceStartup + SessionCloseWaitTimeoutSeconds;

        while ((sessionStartRequested || pendingPersistenceRequests > 0) && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (sessionStartRequested || pendingPersistenceRequests > 0)
        {
            Debug.LogWarning(
                "Se agoto el tiempo de espera de persistencia al cerrar la sesion. " +
                "La aplicacion continuara el cierre para no quedar bloqueada."
            );
        }
    }

    private IEnumerator WhenSessionReady(Func<IEnumerator> action)
    {
        while (sessionStartRequested)
            yield return null;

        if (currentSessionId <= 0)
            yield break;

        yield return StartCoroutine(action());
    }

    private IEnumerator WhenVrElementRowReady(string elementId, Func<int, IEnumerator> action)
    {
        float timeoutAt = Time.realtimeSinceStartup + 5f;
        while (!vrElementRowIds.ContainsKey(elementId) && Time.realtimeSinceStartup < timeoutAt)
            yield return null;

        if (!vrElementRowIds.TryGetValue(elementId, out int rowId))
            yield break;

        yield return StartCoroutine(action(rowId));
    }

    private IEnumerator WhenTutorialElementRowReady(Func<int, IEnumerator> action)
    {
        float timeoutAt = Time.realtimeSinceStartup + 5f;
        while (currentTutorialElementId <= 0 && Time.realtimeSinceStartup < timeoutAt)
            yield return null;

        if (currentTutorialElementId <= 0)
            yield break;

        yield return StartCoroutine(action(currentTutorialElementId));
    }

    private static double SecondsSince(DateTime start)
    {
        return Math.Max(0, (DateTime.UtcNow - start).TotalSeconds);
    }

    private void ResetState()
    {
        currentSessionId = -1;
        sessionStartRequested = false;
        currentPhaseId = -1;
        currentPhase = null;
        currentTutorialElementId = -1;
        vrElementRowIds.Clear();
        vrElementPositions.Clear();
        currentVrElement = null;
        savedPreparationElements.Clear();
        tutorialDuration = 0;
        preparationDuration = 0;
        vrDuration = 0;
        enteredTutorial = false;
        enteredPreparation = false;
        enteredVr = false;
    }

    private void OnApplicationQuit()
    {
        EndSession("Aplicacion cerrada.");
    }
}
