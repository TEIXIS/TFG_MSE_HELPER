using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FanMenu : MonoBehaviour
{
    public FanMenuSelection selection;

    public GameObject optionPrefab;
    [Range(1, 7)] public int optionCount = 4;

    public float radius = 0.35f;
    [Tooltip("Grados de separaci�n entre cada opci�n (Recomendado: 25 para Hand Tracking).")]
    public float spacingAngle = 30f;

    [Header("Tamano automatico de opciones")]
    public bool autoSizeOptions = true;
    [Range(0.2f, 1f)] public float optionSizeBySpacing = 0.7f;
    public Vector2 optionSizeLimits = new Vector2(0.08f, 0.14f);
    public Vector2 optionScaleLimits = new Vector2(0.01f, 0.14f);
    [Tooltip("Seguro para modelos extremadamente pequeños: limita cuanto puede crecer una opcion respecto a su escala original.")]
    public float maxOptionUpscaleFactor = 25f;
    public float interactionColliderPadding = 1.0f;
    public Vector2 interactionColliderWorldSizeLimits = new Vector2(0.035f, 0.16f);
    public bool showOptionHitboxes = false;
    public bool orientOptionsUsingViewPoint = true;
    public string viewPointName = "ViewPoint";
    public bool showViewPointMarkers = true;
    public float viewPointMarkerWorldSize = 0.035f;
    public Color viewPointMarkerColor = Color.magenta;

    [Header("Animaci�n Relajante")]
    [Tooltip("Duraci�n base de la animaci�n para cada bot�n.")]
    public float appearDuration = 1.5f;
    [Tooltip("Retraso entre la aparici�n de cada bot�n (Efecto cascada).")]
    public float staggerDelay = 0.15f;
    public Vector3 initialSpawnOffset = new Vector3(0f, -0.28f, -0.18f);

    [Header("Iluminacion local del menu")]
    public bool useLocalMenuLight = true;
    public LightType localMenuLightType = LightType.Point;
    public Color localMenuLightColor = Color.white;
    [Range(0f, 10f)] public float localMenuLightIntensity = 2.5f;
    [Range(0.05f, 3f)] public float localMenuLightRange = 0.9f;
    public Vector3 localMenuLightPosition = new Vector3(0f, 0.12f, -0.18f);

    [Header("Audio del menu")]
    public AudioClip menuOpenClip;
    [Range(0f, 1f)] public float menuOpenVolume = 0.65f;

    [Header("Evitacion simple de colisiones")]
    public bool raiseMenuIfBlocked = true;
    public LayerMask menuCollisionMask = ~0;
    public float menuCollisionCheckRadius = 0.28f;
    public float menuRaiseStep = 0.12f;
    public int menuRaiseAttempts = 4;
  //  [Tooltip("Tiempo que tarda el men� en desvanecerse al cerrarlo o viajar.")]
  //  public float disappearDuration = 1.2f;

    List<FanOption> options = new();
    private Coroutine disappearCoroutine;
    private Light localMenuLight;
    private AudioSource menuAudioSource;

    // Construye los botones visibles del menu y aplica escala, orientacion, colision e iluminacion.
    public void Build(List<GameObject> customOptions = null, bool fitOptionsToMenu = false, List<int> optionIndices = null)
    {
        // Si estaba desapareciendo, lo cortamos de golpe
        if (disappearCoroutine != null) StopCoroutine(disappearCoroutine);
        StopAllCoroutines();

        Clear();
        List<GameObject> prefabsToUse = new();

        if (customOptions != null)
        {
            if (customOptions.Count == 0)
            {
                Debug.LogWarning("[FanMenu][Build] Custom options list is empty; menu cleared without falling back to selection.");
                return;
            }

            int count = Mathf.Clamp(customOptions.Count, 1, 7);
            for (int i = 0; i < count; i++) prefabsToUse.Add(customOptions[i]);
            optionCount = count;
        }
        else if (selection != null && selection.selectedOptions.Count > 0)
        {
            int count = Mathf.Clamp(selection.selectedOptions.Count, 1, 7);
            for (int i = 0; i < count; i++) prefabsToUse.Add(selection.selectedOptions[i]);
            optionCount = count;
        }
        else
        {
            optionCount = Mathf.Clamp(optionCount, 1, 7);
            for (int i = 0; i < optionCount; i++) prefabsToUse.Add(optionPrefab);
        }

        float startAngle = -(optionCount - 1) * spacingAngle / 2f;
        float optionSize = CalculateOptionSize();

        for (int i = 0; i < optionCount; i++)
        {
            float angle = startAngle + spacingAngle * i;
            float rad = angle * Mathf.Deg2Rad;

            Vector3 targetPos = new Vector3(Mathf.Sin(rad) * radius, 0, (Mathf.Cos(rad) * radius) - radius);

            GameObject go = Instantiate(prefabsToUse[i], transform);
            go.transform.localPosition = Vector3.zero;
            DisableMenuPrefabAudio(go);
            Debug.Log($"[FanMenu][Build] Spawn index={i} prefab={prefabsToUse[i].name} instance={go.name} fit={fitOptionsToMenu} hasFanOptionBefore={go.GetComponent<FanOption>() != null}");

            Bounds localBounds;
            bool hasLocalBounds = TryCalculateLocalModelBounds(go, out localBounds);
            FanMenuPoseOverride poseOverride = go.GetComponent<FanMenuPoseOverride>();
            if (autoSizeOptions && fitOptionsToMenu)
            {
                go.transform.localScale = CalculateFittedScale(go, optionSize);
                ConfigureInteractionCollider(go, hasLocalBounds, localBounds);
            }

            if (orientOptionsUsingViewPoint && (poseOverride == null || !poseOverride.disableViewPointOrientation))
                OrientOptionViewPointTowardsUser(go, hasLocalBounds ? localBounds.center : Vector3.zero, targetPos);
            if (poseOverride != null)
                go.transform.localRotation = go.transform.localRotation * Quaternion.Euler(poseOverride.localRotationOffsetEuler);
            if (showViewPointMarkers)
                CreateViewPointMarker(go);

            var opt = go.GetComponent<FanOption>();
            if (opt == null && autoSizeOptions && fitOptionsToMenu)
                opt = AddRuntimeFanOption(go);

            DisableMenuPrefabAudio(go);

            if (opt != null)
            {
                opt.showInteractionHitbox = showOptionHitboxes;
                opt.adjustTransparentMenuMaterials = true;
                opt.RefreshMenuPreviewMaterials();

                if (autoSizeOptions && fitOptionsToMenu)
                {
                    opt.ApplyBaseScale(go.transform.localScale);
                }
                opt.ApplyBaseRotation(go.transform.localRotation);

                opt.optionIndex = i;
                opt.selectionIndex = optionIndices != null && i < optionIndices.Count ? optionIndices[i] : -1;
                opt.SetInitialSpawnOffset(initialSpawnOffset);
                opt.SetTarget(targetPos);
                options.Add(opt);
                Debug.Log($"[FanMenu][Build] Registered option index={i} name={go.name} teleport={opt.isTeleportButton} target={targetPos} scale={go.transform.localScale}");
            }
            else
            {
                Debug.LogWarning($"[FanMenu][Build] Instance {go.name} has no FanOption and will not be clickable.");
            }
        }

        ConfigureLocalMenuLight(true);
        PlayMenuOpenSound();
        StartCoroutine(AppearAnimation());
    }

    // Activa o crea una luz local para que los modelos del menu se vean tambien en salas oscuras.
    void ConfigureLocalMenuLight(bool enabled)
    {
        if (!useLocalMenuLight || !enabled)
        {
            if (localMenuLight != null)
                localMenuLight.enabled = false;
            return;
        }

        if (localMenuLight == null)
        {
            Transform existing = transform.Find("__FanMenuLocalLight");
            if (existing != null)
                localMenuLight = existing.GetComponent<Light>();

            if (localMenuLight == null)
            {
                GameObject lightObject = new GameObject("__FanMenuLocalLight");
                lightObject.transform.SetParent(transform, false);
                localMenuLight = lightObject.AddComponent<Light>();
            }
        }

        localMenuLight.transform.localPosition = localMenuLightPosition;
        localMenuLight.transform.localRotation = Quaternion.identity;
        localMenuLight.type = localMenuLightType;
        localMenuLight.color = localMenuLightColor;
        localMenuLight.intensity = localMenuLightIntensity;
        localMenuLight.range = localMenuLightRange;
        localMenuLight.shadows = LightShadows.None;
        localMenuLight.enabled = true;
    }

    // Reproduce un sonido opcional cada vez que se abre/reconstruye el menu.
    void PlayMenuOpenSound()
    {
        if (menuOpenClip == null)
            return;

        if (menuAudioSource == null)
            menuAudioSource = GetComponent<AudioSource>();

        if (menuAudioSource == null)
            menuAudioSource = gameObject.AddComponent<AudioSource>();

        menuAudioSource.playOnAwake = false;
        menuAudioSource.spatialBlend = 0f;
        menuAudioSource.PlayOneShot(menuOpenClip, menuOpenVolume);
    }

    // Anade FanOption en runtime para prefabs que solo tienen collider/modelo.
    FanOption AddRuntimeFanOption(GameObject option)
    {
        FanOption fanOption = option.AddComponent<FanOption>();
        fanOption.isTeleportButton = true;

        BoxCollider interactionBox = option.GetComponent<BoxCollider>();
        if (interactionBox != null)
        {
            fanOption.SetInteractionLocalCenter(interactionBox.center);
            fanOption.SetVisualCenterLocal(interactionBox.center);
        }

        Debug.Log($"[FanMenu][Build] Added runtime FanOption to {option.name}. collider={(interactionBox != null ? interactionBox.name : "null")} center={(interactionBox != null ? interactionBox.center.ToString() : "none")} size={(interactionBox != null ? interactionBox.size.ToString() : "none")}");
        return fanOption;
    }

    // Silencia audios internos de los objetos mostrados como botones para que no suenen al instanciarse en el menu.
    void DisableMenuPrefabAudio(GameObject option)
    {
        AudioSource[] audioSources = option.GetComponentsInChildren<AudioSource>(true);
        foreach (AudioSource source in audioSources)
        {
            if (source == null)
                continue;

            source.Stop();
            source.playOnAwake = false;
            source.mute = true;
            source.enabled = false;
        }
    }

    void OrientOptionViewPointTowardsUser(GameObject option, Vector3 visualCenterLocal, Vector3 targetLocalPos)
    {
        Transform viewPoint = FindChildRecursive(option.transform, viewPointName);
        if (viewPoint == null)
            return;

        Vector3 viewPointLocal = option.transform.InverseTransformPoint(viewPoint.position);
        Vector3 scaledViewPoint = Vector3.Scale(viewPointLocal, option.transform.localScale);
        Vector3 scaledCenter = Vector3.Scale(visualCenterLocal, option.transform.localScale);
        Transform cameraTransform = Camera.main != null ? Camera.main.transform : null;
        if (cameraTransform == null)
            return;

        Quaternion baseRotation = option.transform.localRotation;
        Quaternion bestRotation = baseRotation;
        float bestDistance = float.MaxValue;

        for (int degrees = 0; degrees < 360; degrees += 5)
        {
            Quaternion candidate = Quaternion.AngleAxis(degrees, Vector3.up) * baseRotation;
            Vector3 originLocal = targetLocalPos - (candidate * scaledCenter);
            Vector3 viewPointWorld = transform.TransformPoint(originLocal + (candidate * scaledViewPoint));
            float distance = (viewPointWorld - cameraTransform.position).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestRotation = candidate;
            }
        }

        option.transform.localRotation = bestRotation;
        Debug.Log($"[FanMenu][ViewPoint] option={option.name} target={targetLocalPos} center={visualCenterLocal} point={viewPointLocal} bestRot={bestRotation.eulerAngles} dist={Mathf.Sqrt(bestDistance):F3}");
    }

    void CreateViewPointMarker(GameObject option)
    {
        Transform viewPoint = FindChildRecursive(option.transform, viewPointName);
        if (viewPoint == null || viewPoint.Find("__FanMenuViewPointDebug") != null)
            return;

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "__FanMenuViewPointDebug";
        marker.transform.SetParent(viewPoint, false);
        marker.transform.localPosition = Vector3.zero;
        float worldScale = Mathf.Max(0.005f, viewPointMarkerWorldSize);
        float parentScale = Mathf.Max(0.0001f, viewPoint.lossyScale.magnitude / Mathf.Sqrt(3f));
        marker.transform.localScale = Vector3.one * (worldScale / parentScale);

        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
            Destroy(markerCollider);

        Renderer markerRenderer = marker.GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                Material material = new Material(shader);
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", viewPointMarkerColor);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", viewPointMarkerColor);
                markerRenderer.material = material;
            }
        }
    }

    Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root)
        {
            if (child.name == childName)
                return child;

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    float CalculateOptionSize()
    {
        float spacingRadians = Mathf.Abs(spacingAngle) * Mathf.Deg2Rad;
        float availableWidth = optionCount > 1
            ? 2f * radius * Mathf.Sin(spacingRadians * 0.5f)
            : radius * 0.45f;

        float minSize = Mathf.Min(optionSizeLimits.x, optionSizeLimits.y);
        float maxSize = Mathf.Max(optionSizeLimits.x, optionSizeLimits.y);
        return Mathf.Clamp(availableWidth * optionSizeBySpacing, minSize, maxSize);
    }

    Vector3 CalculateFittedScale(GameObject option, float targetSize)
    {
        Bounds bounds;
        if (!TryCalculateWorldBounds(option, false, out bounds) &&
            !TryCalculateWorldBounds(option, true, out bounds))
        {
            return Vector3.one * targetSize;
        }

        float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largestDimension <= 0.0001f)
            return option.transform.localScale;

        float fitFactor = targetSize / largestDimension;
        if (fitFactor > 1f)
            fitFactor = Mathf.Min(fitFactor, Mathf.Max(1f, maxOptionUpscaleFactor));

        Vector3 fittedScale = option.transform.localScale * fitFactor;
        float largestScaleAxis = Mathf.Max(Mathf.Abs(fittedScale.x), Mathf.Abs(fittedScale.y), Mathf.Abs(fittedScale.z));
        float minScale = Mathf.Min(optionScaleLimits.x, optionScaleLimits.y);

        if (largestScaleAxis < minScale && largestScaleAxis > 0.0001f)
            fittedScale *= minScale / largestScaleAxis;

        return fittedScale;
    }

    void ConfigureInteractionCollider(GameObject option, bool hasLocalBounds, Bounds localBounds)
    {
        if (!hasLocalBounds)
        {
            Debug.LogWarning($"[FanMenu][Collider] {option.name} has no model renderers to build an interaction collider.");
            return;
        }

        float padding = Mathf.Max(0.01f, interactionColliderPadding);
        Vector3 size = localBounds.size * padding;
        size.x = ClampColliderLocalSizeForWorld(size.x, option.transform.lossyScale.x);
        size.y = ClampColliderLocalSizeForWorld(size.y, option.transform.lossyScale.y);
        size.z = ClampColliderLocalSizeForWorld(size.z, option.transform.lossyScale.z);

        BoxCollider box = option.GetComponent<BoxCollider>();
        if (box == null)
            box = option.AddComponent<BoxCollider>();

        box.isTrigger = true;
        box.center = localBounds.center;
        box.size = size;
        Debug.Log($"[FanMenu][Collider] option={option.name} center={box.center} size={box.size} scale={option.transform.localScale}");

        FanOption fanOption = option.GetComponent<FanOption>();
        if (fanOption != null)
        {
            fanOption.SetInteractionLocalCenter(localBounds.center);
            fanOption.SetVisualCenterLocal(localBounds.center);
        }
    }

    bool TryCalculateLocalModelBounds(GameObject option, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        Renderer[] renderers = option.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is LineRenderer || renderer is TrailRenderer || renderer is ParticleSystemRenderer)
                continue;
            if (renderer.name == "__FanMenuViewPointDebug")
                continue;

            Bounds rendererLocalBounds = renderer.localBounds;
            if (!IsValidBounds(rendererLocalBounds, 1000f))
                continue;

            Vector3 center = rendererLocalBounds.center;
            Vector3 extents = rendererLocalBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 rendererLocalCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 worldCorner = renderer.transform.TransformPoint(rendererLocalCorner);
                        Vector3 optionLocalCorner = option.transform.InverseTransformPoint(worldCorner);

                        if (!hasBounds)
                        {
                            bounds = new Bounds(optionLocalCorner, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            bounds.Encapsulate(optionLocalCorner);
                        }
                    }
                }
            }
        }

        return hasBounds;
    }
    bool TryCalculateWorldBounds(GameObject option, bool useColliders, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = new Bounds(option.transform.position, Vector3.zero);

        if (!useColliders)
        {
            Renderer[] renderers = option.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                Bounds rendererBounds = renderer.bounds;
                if (!IsValidBounds(rendererBounds, 10f))
                    continue;

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }
        }
        else
        {
            Collider[] colliders = option.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                    continue;

                Bounds colliderBounds = collider.bounds;
                if (!IsValidBounds(colliderBounds, 10f))
                    continue;

                if (!hasBounds)
                {
                    bounds = colliderBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(colliderBounds);
                }
            }
        }

        return hasBounds;
    }

    float ClampColliderLocalSizeForWorld(float localSize, float lossyAxisScale)
    {
        float axisScale = Mathf.Max(0.0001f, Mathf.Abs(lossyAxisScale));
        float minWorld = Mathf.Min(interactionColliderWorldSizeLimits.x, interactionColliderWorldSizeLimits.y);
        float maxWorld = Mathf.Max(interactionColliderWorldSizeLimits.x, interactionColliderWorldSizeLimits.y);
        float minLocal = minWorld / axisScale;
        float maxLocal = maxWorld / axisScale;

        if (minLocal > maxLocal)
            minLocal = maxLocal;

        return Mathf.Clamp(localSize, minLocal, maxLocal);
    }

    bool IsValidBounds(Bounds bounds, float maxDimension)
    {
        return IsFinite(bounds.center) &&
               IsFinite(bounds.size) &&
               bounds.size.x >= 0f &&
               bounds.size.y >= 0f &&
               bounds.size.z >= 0f &&
               bounds.size.x <= maxDimension &&
               bounds.size.y <= maxDimension &&
               bounds.size.z <= maxDimension;
    }

    bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    Bounds WorldBoundsToLocalBounds(Transform root, Bounds worldBounds)
    {
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 localCorner = root.InverseTransformPoint(worldCorner);
                    min = Vector3.Min(min, localCorner);
                    max = Vector3.Max(max, localCorner);
                }
            }
        }

        Bounds localBounds = new Bounds((min + max) * 0.5f, max - min);
        return localBounds;
    }

    public Transform GetOptionTransform(int index)
{
    if (index < 0 || index >= transform.childCount) return null;
    return transform.GetChild(index);
}

    void Clear()
    {
        options.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if ((localMenuLight != null && child == localMenuLight.transform) || child.name == "__FanMenuLocalLight")
                continue;

            child.SetParent(null);   // lo sacamos del menu inmediatamente
            Destroy(child.gameObject);
        }

        ConfigureLocalMenuLight(false);
    }

    public void ClearImmediate()
    {
        if (disappearCoroutine != null) StopCoroutine(disappearCoroutine);
        StopAllCoroutines();
        Clear();
    }

    IEnumerator AppearAnimation()
    {
        float totalDuration = appearDuration + (options.Count * staggerDelay);
        float time = 0;

        while (time < totalDuration)
        {
            time += Time.deltaTime;
            for (int i = 0; i < options.Count; i++)
            {
                float startTime = i * staggerDelay;
                float localTime = Mathf.Clamp01((time - startTime) / appearDuration);
                float ease = 1f - Mathf.Pow(1f - localTime, 4f);
                options[i].AnimateAppear(ease);
            }
            yield return null;
        }
    }

    // --- NUEVA L�GICA DE FADE OUT ---
    public void CloseMenuAnimated(float duration, System.Action onComplete)
    {
        if (!gameObject.activeInHierarchy)
        {
            Clear();
            onComplete?.Invoke();
            return;
        }

        if (disappearCoroutine != null) StopCoroutine(disappearCoroutine);
        disappearCoroutine = StartCoroutine(DisappearAnimation(duration, onComplete));
    }

    IEnumerator DisappearAnimation(float duration, System.Action onComplete)
    {
        float time = 0;

        foreach (var opt in options)
        {
            if (opt != null) opt.ForceInteractionOff();
        }

        while (time < duration)
        {
            time += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(time / duration);

            foreach (var opt in options)
            {
                if (opt != null) opt.SetAlpha(alpha);
            }
            yield return null;
        }

        Clear();
        onComplete?.Invoke();
    }

    /*  public void PlaceInFrontOfUser(Transform head, Transform leftHand, Transform rightHand, float distance, float verticalOffset)
      {
          Vector3 forward = head.forward;
          forward.y = 0f;
          forward.Normalize();
          Vector3 targetPos = head.position + forward * distance;
          float handsY = (leftHand.position.y + rightHand.position.y) / 2f;
          targetPos.y = handsY + verticalOffset;

          transform.position = targetPos;
          transform.rotation = Quaternion.LookRotation(forward);
      }*/

    // Coloca el menu delante del usuario y lo sube unos pasos si la posicion inicial queda ocupada.
    public void PlaceInFrontOfUser(Transform head, Transform leftHand, Transform rightHand, float distance, float verticalOffset)
    {
        Vector3 forward = head.forward;
        forward.y = 0f;
        forward.Normalize();

        // 1. Calculamos la posici�n base (X y Z) frente al usuario
        Vector3 targetPos = head.position + forward * distance;

        // --- EL CAMBIO ERGON�MICO ---
        // 2. En lugar de usar la altura de las manos, usamos la altura de la cabeza (ojos).
        // Restamos 0.35f (35 cent�metros) para que quede a la altura c�moda del pecho.
        // Sumamos el verticalOffset por si desde el Inspector quieres ajustarlo un poco.
        targetPos.y = head.position.y - 0.35f + verticalOffset;
        targetPos = RaisePositionIfBlocked(targetPos);

        transform.position = targetPos;
        transform.rotation = Quaternion.LookRotation(forward);
    }

    // Prueba posiciones progresivamente mas altas cuando la esfera aproximada del menu colisiona con la escena.
    Vector3 RaisePositionIfBlocked(Vector3 basePosition)
    {
        if (!raiseMenuIfBlocked)
            return basePosition;

        float radiusCheck = Mathf.Max(0.05f, menuCollisionCheckRadius);
        int attempts = Mathf.Max(0, menuRaiseAttempts);
        float step = Mathf.Max(0.01f, menuRaiseStep);

        for (int i = 0; i <= attempts; i++)
        {
            Vector3 candidate = basePosition + Vector3.up * (step * i);
            if (!Physics.CheckSphere(candidate, radiusCheck, menuCollisionMask, QueryTriggerInteraction.Ignore))
                return candidate;
        }

        return basePosition + Vector3.up * (step * attempts);
    }
}
