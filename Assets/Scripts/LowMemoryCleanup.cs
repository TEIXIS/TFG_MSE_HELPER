using System;
using System.Collections;
using UnityEngine;

public class LowMemoryCleanup : MonoBehaviour
{
    private static LowMemoryCleanup instance;
    private bool cleanupRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("LowMemoryCleanup");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LowMemoryCleanup>();
    }

    private void OnEnable()
    {
        Application.lowMemory += OnLowMemory;
    }

    private void OnDisable()
    {
        Application.lowMemory -= OnLowMemory;
        if (instance == this)
            instance = null;
    }

    private void OnLowMemory()
    {
        if (cleanupRunning)
            return;

        Debug.LogWarning("[LowMemoryCleanup] Application.lowMemory received. Unloading unused assets.");
        StartCoroutine(CleanupRoutine());
    }

    private IEnumerator CleanupRoutine()
    {
        cleanupRunning = true;

        AsyncOperation unload = Resources.UnloadUnusedAssets();
        if (unload != null)
            yield return unload;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        cleanupRunning = false;
    }
}
