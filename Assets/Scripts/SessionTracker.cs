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
    private DateTime sessionStartedAt;

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

        StartCoroutine(UserAPI.CreateSession(
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

        StartCoroutine(WhenSessionReady(() => UserAPI.CreateSessionPhase(
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
        StartCoroutine(WhenSessionReady(() => UserAPI.CreateTutorialElement(
            currentSessionId,
            elementId,
            id => currentTutorialElementId = id,
            err => Debug.LogError("Error guardando elemento tutorial: " + err)
        )));
    }

    public void SavePreparationElements(IEnumerable<string> elementIds)
    {
        StartPhaseIfNeeded("preparacio");

        foreach (string elementId in elementIds)
        {
            if (string.IsNullOrEmpty(elementId) || savedPreparationElements.Contains(elementId))
                continue;

            savedPreparationElements.Add(elementId);
            StartCoroutine(WhenSessionReady(() => UserAPI.CreatePreparationElement(
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

            StartCoroutine(WhenSessionReady(() => UserAPI.CreateVrElement(
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

        StartCoroutine(WhenTutorialElementRowReady(rowId => UserAPI.UpdateTutorialElementPose(
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

        StartCoroutine(WhenVrElementRowReady(elementId, rowId => UserAPI.UpdateVrElementPose(
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
        CloseCurrentTutorialElement();
        CloseCurrentVrElement();
        CloseCurrentPhase();

        if (currentSessionId <= 0)
            return;

        int sessionId = currentSessionId;
        double totalDuration = SecondsSince(sessionStartedAt);

        StartCoroutine(UserAPI.EndSession(
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
            StartCoroutine(UserAPI.EndSessionPhase(
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
        StartCoroutine(UserAPI.EndTutorialElement(
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

        StartCoroutine(WhenVrElementRowReady(elementId, rowId => UserAPI.EndVrElement(
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

        StartCoroutine(WhenSessionReady(() => UserAPI.UpdateSessionSettings(
            currentSessionId,
            currentPosture,
            currentMenuHandsActive,
            currentHandParticlesActive,
            err => Debug.LogError("Error actualizando configuracion de sesion: " + err)
        )));
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
