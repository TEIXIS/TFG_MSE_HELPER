using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class FanOption : MonoBehaviour
{
    public static bool debugLogs = false;

    [Header("Tipo de Bot�n")]
    [Tooltip("M�rcalo para los viajes de la app. Desm�rcalo para los botones del men� inicial.")]
    public bool isTeleportButton = true;

    // --- NUEVO: BILLBOARDING ---
    [Header("Billboarding (Mirar al Usuario)")]
    [Tooltip("Si est� marcado, el bot�n girar� constantemente para mirar a la cara del usuario. Ideal para leer textos.")]
    public bool lookAtUser = false;
    [Tooltip("Si est� marcado, el bot�n se mantendr� recto (sin inclinarse hacia arriba/abajo). Ideal para men�s en cubos.")]
    public bool lockRotationToYAxis = true;
    private Transform mainCamera;



    [Header("L�gica Personalizada")]
    public UnityEvent onCustomClick;

    public int optionIndex;
    public int selectionIndex = -1;

    // MEG�FONO 1: Para los viajes antiguos (Teleport, Fadeout)
    public static event System.Action<int> OnOptionSelected;

    // MEG�FONO 2: NUEVO. Solo para el men� de inicio (Canvas, etc.)
    public static event System.Action<int> OnInitialMenuSelected;

    public static event System.Action<int, string> OnInitialMenuSelectedWithLabel;

    // MEG�FONO 3: Para cerrar el men� visual
    public static event System.Action OnMenuInteractionConfirmed;

    Vector3 targetLocalPos;
    Vector3 baseScale;
    Quaternion baseLocalRotation;
    Vector3 interactionLocalCenter;
    Vector3 visualCenterLocal;
    Collider interactionCollider;
    Renderer rend;
    Renderer[] renderers;
    MaterialPropertyBlock mpb;

    public float hover;
    public float press;

    private float selectionBump = 0f;
    private bool isSelected = false;

    private bool needsReset = true;
    private float lifeTime = 0f;

    private Vector3 handWorldPos;

    Vector3 currentInteractionOffset;
    Quaternion currentInteractionRotation = Quaternion.identity;

    bool isAppearing = true;
    bool isFadingOut = false;
    float randomBreathPhase;
    private Vector3 currentAnimPos;
    private Vector3 initialSpawnOffset;

    private float currentScaleMultiplier = 1f;


    [Header("Accesibilidad - Movimiento Relax")]
    [Tooltip("Distancia que el bot�n vuela hacia la mano. Ponlo a 0 para que no se mueva de su sitio (ideal para Quads).")]
    public float attractionDistance = 0.08f;
   
    [Header("Accesibilidad - Movimiento Relax")]
    public float smoothSpeed = 4f;

    [Header("Feedback Visual y Tiempos")]
    public float maxGrowth = 1.25f;
    public float maxBrightness = 1.5f;

    [Range(0f, 1f)] public float requiredInflationToClick = 0.95f;

    [Header("Debug Hitbox")]
    public bool showInteractionHitbox = false;
    public Color hitboxColor = new Color(0f, 1f, 1f, 0.9f);
    public Color selectedHitboxColor = new Color(1f, 0.8f, 0f, 1f);
    public float hitboxLineWidth = 0.006f;
    public bool adjustTransparentMenuMaterials = false;

    static readonly int ColorPropURP = Shader.PropertyToID("_BaseColor");
    static readonly int ColorPropBuiltIn = Shader.PropertyToID("_Color");
    static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");
    static readonly int MetallicProp = Shader.PropertyToID("_Metallic");
    static readonly int SmoothnessProp = Shader.PropertyToID("_Smoothness");
    static readonly int SurfaceProp = Shader.PropertyToID("_Surface");
    static readonly int BaseMapProp = Shader.PropertyToID("_BaseMap");
    static readonly int MainTexProp = Shader.PropertyToID("_MainTex");

    [Header("Audio Feedback")]
    public AudioClip pressSound;
    private AudioSource audioSource;
    private bool hasPlayedPressSound = false;
    private float nextDebugPressLogTime = 0f;
    private LineRenderer hitboxLine;
    private readonly Vector3[] hitboxLinePoints = new Vector3[24];

    public void SetHandPosition(Vector3 pos) { handWorldPos = pos; }

    public void SetTarget(Vector3 localPos)
    {
        targetLocalPos = localPos;
        isAppearing = true;
        lifeTime = 0f;
    }

    public void SetInitialSpawnOffset(Vector3 localOffset)
    {
        initialSpawnOffset = localOffset;
    }

    public Vector3 GetBaseWorldPosition()
    {
        return transform.TransformPoint(interactionLocalCenter);
    }

    public Vector3 GetClosestInteractionPoint(Vector3 worldPoint)
    {
        if (interactionCollider == null)
            interactionCollider = GetComponent<Collider>();

        if (interactionCollider != null && interactionCollider.enabled)
            return interactionCollider.ClosestPoint(worldPoint);

        return GetBaseWorldPosition();
    }

    public float GetInteractionDistance(Vector3 worldPoint)
    {
        return Vector3.Distance(worldPoint, GetClosestInteractionPoint(worldPoint));
    }


    public void SetInteractionLocalCenter(Vector3 localCenter)
    {
        interactionLocalCenter = localCenter;
    }

    public void SetVisualCenterLocal(Vector3 localCenter)
    {
        visualCenterLocal = localCenter;
    }

    public void ApplyBaseScale(Vector3 newBaseScale)
    {
        baseScale = newBaseScale;
        transform.localScale = baseScale;
    }

    public void ApplyBaseRotation(Quaternion newBaseLocalRotation)
    {
        baseLocalRotation = newBaseLocalRotation;
        transform.localRotation = baseLocalRotation;
    }

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        renderers = GetComponentsInChildren<Renderer>(true);
        if (adjustTransparentMenuMaterials)
            PrepareMenuRendererMaterials();
        interactionCollider = GetComponent<Collider>();
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        baseLocalRotation = transform.localRotation;
        randomBreathPhase = Random.Range(0f, Mathf.PI * 2f);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.6f;

        // Buscamos la c�mara principal del casco al nacer
        if (Camera.main != null) mainCamera = Camera.main.transform;
    }

    public void RefreshMenuPreviewMaterials()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        PrepareMenuRendererMaterials();
    }

    public void AnimateAppear(float t)
    {
        Vector3 radial = Vector3.Lerp(initialSpawnOffset, targetLocalPos, t);
        float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.08f;
        radial.y += arcHeight;

        currentAnimPos = radial;
        transform.localScale = baseScale * t;
        currentScaleMultiplier = 1f;

        SetAlpha(t);

        if (t >= 0.999f) isAppearing = false;
    }

    void Update()
    {
        lifeTime += Time.deltaTime;
        if (needsReset && lifeTime > 1.0f) needsReset = false;

        if (!isAppearing && !isFadingOut)
        {
            ProcessInteractions();

            float interactionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
            float targetScale = Mathf.Lerp(1f, maxGrowth, interactionLevel);

            currentScaleMultiplier = Mathf.Lerp(currentScaleMultiplier, targetScale, Time.deltaTime * smoothSpeed * 1.5f);
            transform.localScale = baseScale * currentScaleMultiplier;

            SetAlpha(1f);
        }
        else if (isFadingOut)
        {
            currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, Vector3.zero, Time.deltaTime * smoothSpeed);
            currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
        }

        float interactionLevelForBreath = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
        float breathAmount = Mathf.Lerp(0.005f, 0f, interactionLevelForBreath);
        float breath = Mathf.Sin(Time.time * 1.5f + randomBreathPhase) * breathAmount;
        Vector3 breathOffset = new Vector3(0, breath, 0);

        if (!isAppearing) currentAnimPos = targetLocalPos;

        transform.localPosition = currentAnimPos - GetVisualCenterParentOffset() + currentInteractionOffset + breathOffset;

        // ====================================================
        // LA MAGIA DEL BILLBOARDING (Mirar al usuario)
        // ====================================================
        if (lookAtUser && mainCamera != null)
        {
            Vector3 targetCameraPos = mainCamera.position;

            // Si bloqueamos el eje Y, le mentimos a las matem�ticas y le decimos que 
            // la c�mara est� exactamente a la misma altura que el bot�n.
            if (lockRotationToYAxis)
            {
                targetCameraPos.y = transform.position.y;
            }

            Vector3 dirToCamera = transform.position - targetCameraPos;

            // Seguro anti-errores por si la c�mara est� matem�ticamente en el mismo p�xel
            if (dirToCamera.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(dirToCamera) * baseLocalRotation * currentInteractionRotation;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
        else
        {
            transform.localRotation = baseLocalRotation * currentInteractionRotation;
        }
        //transform.localRotation = currentInteractionRotation;

        if (!isAppearing) selectionBump = Mathf.Lerp(selectionBump, 0f, Time.deltaTime * 6f);
        UpdateHitboxVisual();
    }

    void ProcessInteractions()
    {
        Vector3 targetOffset = Vector3.zero;

        Vector3 directionWorld = handWorldPos - GetBaseWorldPosition();
        Vector3 directionLocal = transform.parent != null
            ? transform.parent.InverseTransformDirection(directionWorld)
            : directionWorld;

        directionLocal.y = 0;

        Vector3 flatDirection = Vector3.back;
        if (directionLocal.sqrMagnitude > 0.001f) flatDirection = directionLocal.normalized;

        float attractionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));

        float effectiveAttractionDistance = Mathf.Min(attractionDistance, 0.035f);
        targetOffset = flatDirection * (attractionLevel * effectiveAttractionDistance);

        Vector3 neighborRepulsion = Vector3.zero;
        if (transform.parent != null)
        {
            foreach (Transform sibling in transform.parent)
            {
                if (sibling == transform) continue;

                Vector3 diff = transform.position - sibling.position;
                diff.y = 0;

                float dist = diff.magnitude;
                float minSpacing = 0.14f;

                if (dist < minSpacing && dist > 0.001f)
                {
                    float pushFactor = 1f - (dist / minSpacing);
                    Vector3 localPushDir = transform.parent.InverseTransformDirection(diff.normalized);
                    localPushDir.y = 0;
                    neighborRepulsion += localPushDir * (pushFactor * 0.02f);
                }
            }
        }
        targetOffset += neighborRepulsion;
        targetOffset += Vector3.down * selectionBump;

        currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, targetOffset, Time.deltaTime * smoothSpeed);
        currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
    }

    public void SetHover(float v)
    {
        if (isAppearing || isFadingOut) return;
        hover = Mathf.Clamp(v, -1f, 1f);
    }

    public void SetPress(float v)
    {
        float incomingPress = Mathf.Clamp01(v);
        press = incomingPress;

        if (isAppearing || isFadingOut) return;

        if (needsReset)
        {
            if (incomingPress < 0.5f) needsReset = false;
            else return;
        }

        float currentInflationProgress = 0f;
        if (maxGrowth > 1.01f)
            currentInflationProgress = Mathf.Clamp01((currentScaleMultiplier - 1f) / (maxGrowth - 1f));
        else
            currentInflationProgress = 1f;

        if (press >= 0.99f && currentInflationProgress >= requiredInflationToClick)
        {
            if (!hasPlayedPressSound && !isSelected)
            {
                hasPlayedPressSound = true;
                isSelected = true;
                selectionBump = 0.05f;

                if (audioSource != null && pressSound != null && audioSource.enabled && audioSource.gameObject.activeInHierarchy)
                {
                    audioSource.pitch = Random.Range(0.95f, 1.05f);
                    audioSource.PlayOneShot(pressSound, 1.0f);
                }

                TMPro.TMP_Text texto = GetComponentInChildren<TMPro.TMP_Text>();
                string label = texto != null ? texto.text.Trim() : "";
                bool esFlecha = (label == "<-" || label == "->");

                if (isTeleportButton)
                {
                    int finalIndex = selectionIndex >= 0 ? selectionIndex : optionIndex;
                    if (debugLogs) Debug.Log($"[FanMenu][FanOption] CLICK_TELEPORT option={name} index={finalIndex} localIndex={optionIndex} press={press:F2} inflation={currentInflationProgress:F2}");
                    OnOptionSelected?.Invoke(finalIndex);
                }
                else
                {
                    if (debugLogs) Debug.Log($"[FanMenu][FanOption] CLICK_INITIAL option={name} index={optionIndex} label={label} press={press:F2} inflation={currentInflationProgress:F2}");
                    OnInitialMenuSelected?.Invoke(optionIndex);
                    OnInitialMenuSelectedWithLabel?.Invoke(optionIndex, label);
                }

                onCustomClick?.Invoke();

                if (!esFlecha)
                {
                    OnMenuInteractionConfirmed?.Invoke();
                }
            }
        }
        else if (press > 0.5f && Time.time >= nextDebugPressLogTime)
        {
            nextDebugPressLogTime = Time.time + 0.5f;
            if (debugLogs) Debug.Log($"[FanMenu][FanOption] PRESS_WAIT option={name} index={optionIndex} press={press:F2} inflation={currentInflationProgress:F2}/{requiredInflationToClick:F2} appearing={isAppearing} fading={isFadingOut}");
        }
        else if (press < 0.1f)
        {
            hasPlayedPressSound = false;
            isSelected = false;
        }
    }

    public void ForceInteractionOff()
    {
        isFadingOut = true;
        press = 0f;
        hover = 0f;
    }

    public void SetAlpha(float alpha)
    {
        if (renderers == null || renderers.Length == 0)
            return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.sharedMaterial == null)
                continue;
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
                continue;

            renderer.GetPropertyBlock(mpb);

            Material material = renderer.sharedMaterial;
            bool hasBaseColor = material.HasProperty(ColorPropURP);
            bool hasBuiltInColor = material.HasProperty(ColorPropBuiltIn);
            Color originalColor = hasBaseColor
                ? material.GetColor(ColorPropURP)
                : hasBuiltInColor ? material.GetColor(ColorPropBuiltIn) : Color.white;

            Color currentColor = originalColor;
            currentColor.a = originalColor.a * alpha;

            if (hasBaseColor)
                mpb.SetColor(ColorPropURP, currentColor);
            else if (hasBuiltInColor)
                mpb.SetColor(ColorPropBuiltIn, currentColor);

            if (material.HasProperty(EmissionProp))
            {
                float scaleProgress = 0f;
                if (maxGrowth > 1f) scaleProgress = Mathf.Clamp01((currentScaleMultiplier - 1f) / (maxGrowth - 1f));

                float currentBrightness = (!isAppearing && !isFadingOut) ? Mathf.Lerp(1f, maxBrightness, scaleProgress) : 1f;
                Color baseEmission = material.GetColor(EmissionProp);
                float originalAlpha = Mathf.Clamp01(originalColor.a);
            Color targetEmission = baseEmission * currentBrightness * alpha;
            mpb.SetColor(EmissionProp, targetEmission);
            }

            renderer.SetPropertyBlock(mpb);
        }
    }

    void PrepareMenuRendererMaterials()
    {
        if (renderers == null)
            return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
                continue;

            Material[] materials = renderer.materials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                bool isTransparentMenuMaterial = IsTransparentMenuMaterial(material);
                bool preservePbrMaterial = ShouldPreservePbrMaterial(material);
                if (!isTransparentMenuMaterial && !preservePbrMaterial)
                    continue;
                if (HasDisplayTexture(material) && !preservePbrMaterial)
                    continue;

                changed = true;
                Color baseColor = GetMaterialColor(material);
                Color emission = material.HasProperty(EmissionProp) ? material.GetColor(EmissionProp) : baseColor;
                if (!preservePbrMaterial)
                {
                    Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (unlitShader == null)
                        unlitShader = Shader.Find("Unlit/Color");
                    if (unlitShader != null)
                        material.shader = unlitShader;
                }

                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

                if (baseColor.maxColorComponent < 0.05f && emission.maxColorComponent > 0.05f)
                    baseColor = new Color(emission.r, emission.g, emission.b, baseColor.a);
                if (baseColor.a < 0.35f)
                    baseColor.a = 0.35f;
                SetMaterialColor(material, baseColor);

                if (material.HasProperty(EmissionProp))
                {
                    if (emission.maxColorComponent < 0.05f)
                        emission = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
                    if (preservePbrMaterial)
                        emission = Color.Lerp(emission, new Color(baseColor.r, baseColor.g, baseColor.b, 1f), 0.35f);
                    material.SetColor(EmissionProp, emission);
                }

                if (material.HasProperty(MetallicProp))
                    material.SetFloat(MetallicProp, preservePbrMaterial ? Mathf.Min(material.GetFloat(MetallicProp), 0.18f) : 0f);
                if (material.HasProperty(SmoothnessProp))
                    material.SetFloat(SmoothnessProp, preservePbrMaterial ? Mathf.Min(material.GetFloat(SmoothnessProp), 0.55f) : 0.35f);
            }

            if (changed)
                renderer.materials = materials;
        }
    }

    bool IsTransparentMenuMaterial(Material material)
    {
        if (material == null)
            return false;

        string materialName = material.name.ToLowerInvariant();
        if (materialName.Contains("transparent"))
            return true;

        if (material.HasProperty(SurfaceProp) && material.GetFloat(SurfaceProp) > 0.5f)
            return true;

        Color color = GetMaterialColor(material);
        return color.a < 0.2f;
    }

    bool ShouldPreservePbrMaterial(Material material)
    {
        if (material == null)
            return false;

        string materialName = material.name.ToLowerInvariant();
        if (materialName.Contains("metal"))
            return true;

        if (material.HasProperty(MetallicProp) && material.GetFloat(MetallicProp) > 0.05f)
            return true;

        return material.HasProperty("_MetallicGlossMap") && material.GetTexture("_MetallicGlossMap") != null;
    }

    bool HasDisplayTexture(Material material)
    {
        if (material == null)
            return false;

        if (material.HasProperty(BaseMapProp) && material.GetTexture(BaseMapProp) != null)
            return true;
        if (material.HasProperty(MainTexProp) && material.GetTexture(MainTexProp) != null)
            return true;

        return false;
    }

    Color GetMaterialColor(Material material)
    {
        if (material.HasProperty(ColorPropURP))
            return material.GetColor(ColorPropURP);
        if (material.HasProperty(ColorPropBuiltIn))
            return material.GetColor(ColorPropBuiltIn);
        return Color.white;
    }

    void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty(ColorPropURP))
            material.SetColor(ColorPropURP, color);
        if (material.HasProperty(ColorPropBuiltIn))
            material.SetColor(ColorPropBuiltIn, color);
    }

    void UpdateHitboxVisual()
    {
        if (!showInteractionHitbox || !gameObject.activeInHierarchy || IsTexturedImageOption())
        {
            if (hitboxLine != null)
                hitboxLine.enabled = false;
            return;
        }

        if (interactionCollider == null)
            interactionCollider = GetComponent<Collider>();

        if (interactionCollider == null)
            return;

        EnsureHitboxLine();
        hitboxLine.enabled = true;
        hitboxLine.startWidth = hitboxLineWidth;
        hitboxLine.endWidth = hitboxLineWidth;
        hitboxLine.startColor = press >= 0.99f ? selectedHitboxColor : hitboxColor;
        hitboxLine.endColor = press >= 0.99f ? selectedHitboxColor : hitboxColor;

        BuildColliderWirePoints(interactionCollider, hitboxLinePoints);
        hitboxLine.SetPositions(hitboxLinePoints);
    }

    bool IsTexturedImageOption()
    {
        if (renderers == null)
            return false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
                continue;

            Material[] sharedMaterials = renderer.sharedMaterials;
            foreach (Material material in sharedMaterials)
            {
                if (HasDisplayTexture(material))
                    return true;
            }
        }

        return false;
    }

    Vector3 GetVisualCenterParentOffset()
    {
        Vector3 scaledCenter = Vector3.Scale(visualCenterLocal, transform.localScale);
        return baseLocalRotation * scaledCenter;
    }

    void EnsureHitboxLine()
    {
        if (hitboxLine != null)
            return;

        GameObject lineObject = new GameObject("__FanOptionHitboxDebug");
        lineObject.transform.SetParent(transform, false);
        hitboxLine = lineObject.AddComponent<LineRenderer>();
        hitboxLine.useWorldSpace = true;
        hitboxLine.loop = false;
        hitboxLine.positionCount = hitboxLinePoints.Length;
        hitboxLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hitboxLine.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader != null)
            hitboxLine.material = new Material(shader);
    }

    void BuildColliderWirePoints(Collider collider, Vector3[] points)
    {
        Vector3[] corners = new Vector3[8];
        BoxCollider box = collider as BoxCollider;

        if (box != null)
        {
            Vector3 center = box.center;
            Vector3 extents = box.size * 0.5f;
            int index = 0;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 localCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        corners[index++] = box.transform.TransformPoint(localCorner);
                    }
                }
            }
        }
        else
        {
            Bounds bounds = collider.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            int index = 0;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        corners[index++] = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    }
                }
            }
        }

        SetLine(points, 0, corners[0], corners[1]);
        SetLine(points, 2, corners[0], corners[2]);
        SetLine(points, 4, corners[0], corners[4]);
        SetLine(points, 6, corners[3], corners[1]);
        SetLine(points, 8, corners[3], corners[2]);
        SetLine(points, 10, corners[3], corners[7]);
        SetLine(points, 12, corners[5], corners[1]);
        SetLine(points, 14, corners[5], corners[4]);
        SetLine(points, 16, corners[5], corners[7]);
        SetLine(points, 18, corners[6], corners[2]);
        SetLine(points, 20, corners[6], corners[4]);
        SetLine(points, 22, corners[6], corners[7]);
    }

    void SetLine(Vector3[] points, int startIndex, Vector3 a, Vector3 b)
    {
        points[startIndex] = a;
        points[startIndex + 1] = b;
    }

    void OnDisable()
    {
        if (hitboxLine != null)
            hitboxLine.enabled = false;
    }
}

/*using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class FanOption : MonoBehaviour
{
    public static bool debugLogs = false;


    [Header("L�gica Personalizada")]
    [Tooltip("A�ade aqu� qu� quieres que pase al hacer clic (Ej: Mostrar Canvas)")]
    public UnityEvent onCustomClick;

    public int optionIndex;
    // Evento antiguo (Por si tienes otros scripts us�ndolo para teletransportar)
    public static event System.Action<int> OnOptionSelected;

    // NUEVO: Evento que solo sirve para decirle al men� "Cierra los visuales, que han hecho click"
    public static event System.Action OnMenuInteractionConfirmed;

    Vector3 targetLocalPos;
    Vector3 baseScale;
    Renderer rend;
    MaterialPropertyBlock mpb;

    public float hover;
    public float press;

    private float selectionBump = 0f;
    private bool isSelected = false;

    // Seguros anti-bugs de teletransporte
    private bool needsReset = true;
    private float lifeTime = 0f;

    private Vector3 handWorldPos;

    Vector3 currentInteractionOffset;
    Quaternion currentInteractionRotation = Quaternion.identity;

    bool isAppearing = true;
    bool isFadingOut = false;
    float randomBreathPhase;
    private Vector3 currentAnimPos;

    private float currentScaleMultiplier = 1f;

    [Header("Accesibilidad - Movimiento Relax")]
    [Tooltip("Velocidad de atracci�n y crecimiento (menor = m�s zen)")]
    public float smoothSpeed = 4f;

    [Header("Feedback Visual y Tiempos")]
    [Tooltip("Cu�nto crece el bot�n al acercar la mano (1.25 = 25% m�s grande)")]
    public float maxGrowth = 1.25f;
    [Tooltip("Cu�nto aumenta la luz al cargar (1.5 = 50% m�s brillante)")]
    public float maxBrightness = 1.5f;

    // --- LA REGLA DE ORO DE ACCESIBILIDAD ---
    [Tooltip("Porcentaje m�nimo que debe inflarse el bot�n antes de permitir hacer clic (0.95 = 95%)")]
    [Range(0f, 1f)] public float requiredInflationToClick = 0.95f;


    static readonly int ColorPropURP = Shader.PropertyToID("_BaseColor");
    static readonly int ColorPropBuiltIn = Shader.PropertyToID("_Color");
    static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

    [Header("Audio Feedback")]
    public AudioClip pressSound;
    private AudioSource audioSource;
    private bool hasPlayedPressSound = false;

    public void SetHandPosition(Vector3 pos) { handWorldPos = pos; }

    public void SetTarget(Vector3 localPos)
    {
        targetLocalPos = localPos;
        isAppearing = true;
        lifeTime = 0f; // Reiniciamos el reloj de vida al nacer
    }

    public Vector3 GetBaseWorldPosition()
    {
        if (transform.parent != null)
            return transform.parent.TransformPoint(targetLocalPos);
        return transform.position;
    }

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        interactionCollider = GetComponent<Collider>();
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        randomBreathPhase = Random.Range(0f, Mathf.PI * 2f);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.6f;

        if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(EmissionProp))
        {
            baseEmissionColor = rend.sharedMaterial.GetColor(EmissionProp);
        }
    }

    public void AnimateAppear(float t)
    {
        Vector3 radial = Vector3.Lerp(Vector3.zero, targetLocalPos, t);
        float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.08f;
        radial.y += arcHeight;

        currentAnimPos = radial;
        transform.localScale = baseScale * t;

        // Mientras nace, forzamos que el multiplicador sea 1 para que empiece a crecer desde cero cuando termine
        currentScaleMultiplier = 1f;

        SetAlpha(t);

        if (t >= 0.999f) isAppearing = false;
    }

    void Update()
    {
        // El reloj "Asesino de Bugs": si el bot�n lleva vivo m�s de 1 segundo, 
        // anulamos el seguro del teletransporte porque ya es imposible que sea un bug.
        lifeTime += Time.deltaTime;
        if (needsReset && lifeTime > 1.0f)
        {
            needsReset = false;
        }

        if (!isAppearing && !isFadingOut)
        {
            ProcessInteractions();

            // Calculamos cu�nto DEBE crecer seg�n la proximidad de la mano
            float interactionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
            float targetScale = Mathf.Lerp(1f, maxGrowth, interactionLevel);

            // Crecimiento progresivo y suave (Esto es lo que crea el "Tiempo de Espera")
            currentScaleMultiplier = Mathf.Lerp(currentScaleMultiplier, targetScale, Time.deltaTime * smoothSpeed * 1.5f);
            transform.localScale = baseScale * currentScaleMultiplier;

            SetAlpha(1f);
        }
        else if (isFadingOut)
        {
            currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, Vector3.zero, Time.deltaTime * smoothSpeed);
            currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
        }

        float interactionLevelForBreath = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
        float breathAmount = Mathf.Lerp(0.005f, 0f, interactionLevelForBreath);
        float breath = Mathf.Sin(Time.time * 1.5f + randomBreathPhase) * breathAmount;
        Vector3 breathOffset = new Vector3(0, breath, 0);

        if (!isAppearing)
        {
            currentAnimPos = targetLocalPos;
        }

        transform.localPosition = currentAnimPos + currentInteractionOffset + breathOffset;
        transform.localRotation = currentInteractionRotation;

        if (!isAppearing)
        {
            selectionBump = Mathf.Lerp(selectionBump, 0f, Time.deltaTime * 6f);
        }
    }

    void ProcessInteractions()
    {
        Vector3 targetOffset = Vector3.zero;

        Vector3 directionWorld = handWorldPos - GetBaseWorldPosition();
        Vector3 directionLocal = transform.parent != null
            ? transform.parent.InverseTransformDirection(directionWorld)
            : directionWorld;

        directionLocal.y = 0;

        Vector3 flatDirection = Vector3.back;
        if (directionLocal.sqrMagnitude > 0.001f)
        {
            flatDirection = directionLocal.normalized;
        }

        float attractionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
        targetOffset = flatDirection * (attractionLevel * 0.08f);

        Vector3 neighborRepulsion = Vector3.zero;
        if (transform.parent != null)
        {
            foreach (Transform sibling in transform.parent)
            {
                if (sibling == transform) continue;

                Vector3 diff = transform.position - sibling.position;
                diff.y = 0;

                float dist = diff.magnitude;
                float minSpacing = 0.14f;

                if (dist < minSpacing && dist > 0.001f)
                {
                    float pushFactor = 1f - (dist / minSpacing);
                    Vector3 localPushDir = transform.parent.InverseTransformDirection(diff.normalized);
                    localPushDir.y = 0;
                    neighborRepulsion += localPushDir * (pushFactor * 0.02f);
                }
            }
        }
        targetOffset += neighborRepulsion;

        targetOffset += Vector3.down * selectionBump;

        currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, targetOffset, Time.deltaTime * smoothSpeed);
        currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
    }

    public void SetHover(float v)
    {
        if (isAppearing || isFadingOut) return;
        hover = Mathf.Clamp(v, -1f, 1f);
    }

    public void SetPress(float v)
    {
        float incomingPress = Mathf.Clamp01(v);
        press = incomingPress;

        if (isAppearing || isFadingOut) return;

        // Seguro para el primer fotograma post-teletransporte
        if (needsReset)
        {
            if (incomingPress < 0.5f) needsReset = false;
            else return;
        }

        // --- LA REGLA ESTRICTA DE INFLADO ---
        // Calculamos qu� porcentaje real del inflado se ha completado visualmente (de 0.0 a 1.0)
        float currentInflationProgress = 0f;
        if (maxGrowth > 1.01f)
        {
            currentInflationProgress = Mathf.Clamp01((currentScaleMultiplier - 1f) / (maxGrowth - 1f));
        }
        else
        {
            currentInflationProgress = 1f; // Prevenci�n de errores si el usuario decide poner maxGrowth a 1
        }

        // AHORA EXIGIMOS LAS DOS COSAS A LA VEZ PARA HACER EL CLIC:
        // 1. Que el usuario est� pulsando a fondo (press >= 0.99)
        // 2. Que el bot�n haya tenido tiempo de inflarse en pantalla hasta el nivel exigido (ej: 95%)
        if (press >= 0.99f && currentInflationProgress >= requiredInflationToClick)
        {
            if (!hasPlayedPressSound && !isSelected)
            {
                hasPlayedPressSound = true;
                isSelected = true;
                selectionBump = 0.05f;

                if (audioSource != null && pressSound != null)
                {
                    audioSource.pitch = Random.Range(0.95f, 1.05f);
                    audioSource.PlayOneShot(pressSound, 1.0f);
                }

                // NUEVA L�GICA MODULAR:
                OnOptionSelected?.Invoke(optionIndex); // Para los viajes antiguos
                onCustomClick?.Invoke(); // Ejecuta lo que le pongas en el Inspector (Canvas, etc.)
                OnMenuInteractionConfirmed?.Invoke(); // Le dice al PalmMenuActivator que se cierre
            }
        }
        else if (press < 0.1f)
        {
            hasPlayedPressSound = false;
            isSelected = false;
        }
    }

    public void ForceInteractionOff()
    {
        isFadingOut = true;
        press = 0f;
        hover = 0f;
    }

    public void SetAlpha(float alpha)
    {
        if (rend == null || rend.sharedMaterial == null) return;
        rend.GetPropertyBlock(mpb);

        Color currentColor = rend.sharedMaterial.HasProperty(ColorPropURP)
            ? rend.sharedMaterial.GetColor(ColorPropURP)
            : rend.sharedMaterial.color;

        currentColor.a = alpha;

        if (rend.sharedMaterial.HasProperty(ColorPropURP))
            mpb.SetColor(ColorPropURP, currentColor);
        else
            mpb.SetColor(ColorPropBuiltIn, currentColor);

        if (rend.sharedMaterial.HasProperty(EmissionProp))
        {
            float scaleProgress = 0f;
            if (maxGrowth > 1f) scaleProgress = Mathf.Clamp01((currentScaleMultiplier - 1f) / (maxGrowth - 1f));

            float currentBrightness = (!isAppearing && !isFadingOut) ? Mathf.Lerp(1f, maxBrightness, scaleProgress) : 1f;
            Color targetEmission = baseEmissionColor * currentBrightness * alpha;
            mpb.SetColor(EmissionProp, targetEmission);
        }

        rend.SetPropertyBlock(mpb);
    }
}*/
/*using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FanOption : MonoBehaviour
{
    public static bool debugLogs = false;

    public int optionIndex;
    public static event System.Action<int> OnOptionSelected;

    Vector3 targetLocalPos;
    Vector3 baseScale;
    Renderer rend;
    MaterialPropertyBlock mpb;

    public float hover;
    public float press;

    private float selectionBump = 0f;
    private bool isSelected = false;

    // Seguro: Solo bloquea el Click, pero permite toda la animaci�n visual.
    private bool needsReset = true;

    private Vector3 handWorldPos;

    Vector3 currentInteractionOffset;
    Quaternion currentInteractionRotation = Quaternion.identity;

    bool isAppearing = true;
    bool isFadingOut = false;
    float randomBreathPhase;
    private Vector3 currentAnimPos;

    // Variable para suavizar el crecimiento siempre
    private float currentScaleMultiplier = 1f;

    [Header("Accesibilidad - Movimiento Relax")]
    [Tooltip("Velocidad de atracci�n y crecimiento (menor = m�s zen)")]
    public float smoothSpeed = 4f;

    [Header("Feedback Visual")]
    public float maxGrowth = 1.25f;
    public float maxBrightness = 1.5f;

    static readonly int ColorPropURP = Shader.PropertyToID("_BaseColor");
    static readonly int ColorPropBuiltIn = Shader.PropertyToID("_Color");
    static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

    [Header("Audio Feedback")]
    public AudioClip pressSound;
    private AudioSource audioSource;
    private bool hasPlayedPressSound = false;

    public void SetHandPosition(Vector3 pos) { handWorldPos = pos; }

    public void SetTarget(Vector3 localPos)
    {
        targetLocalPos = localPos;
        isAppearing = true;
    }

    public Vector3 GetBaseWorldPosition()
    {
        if (transform.parent != null)
            return transform.parent.TransformPoint(targetLocalPos);
        return transform.position;
    }

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        interactionCollider = GetComponent<Collider>();
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        randomBreathPhase = Random.Range(0f, Mathf.PI * 2f);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.6f;

        if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(EmissionProp))
        {
            baseEmissionColor = rend.sharedMaterial.GetColor(EmissionProp);
        }
    }

    public void AnimateAppear(float t)
    {
        Vector3 radial = Vector3.Lerp(Vector3.zero, targetLocalPos, t);
        float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.08f;
        radial.y += arcHeight;

        currentAnimPos = radial;
        transform.localScale = baseScale * t;
        currentScaleMultiplier = 1f;

        SetAlpha(t);

        if (t >= 0.999f) isAppearing = false;
    }

    void Update()
    {
        if (!isAppearing && !isFadingOut)
        {
            ProcessInteractions();

            // Crecimiento guiado por la atracci�n pura
            float interactionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
            float targetScale = Mathf.Lerp(1f, maxGrowth, interactionLevel);

            // Crecimiento siempre suave, sin saltos
            currentScaleMultiplier = Mathf.Lerp(currentScaleMultiplier, targetScale, Time.deltaTime * smoothSpeed * 1.5f);
            transform.localScale = baseScale * currentScaleMultiplier;

            SetAlpha(1f);
        }
        else if (isFadingOut)
        {
            currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, Vector3.zero, Time.deltaTime * smoothSpeed);
            currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
        }

        // Flotaci�n constante que no se interrumpe nunca
        float interactionLevelForBreath = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
        float breathAmount = Mathf.Lerp(0.005f, 0f, interactionLevelForBreath);
        float breath = Mathf.Sin(Time.time * 1.5f + randomBreathPhase) * breathAmount;
        Vector3 breathOffset = new Vector3(0, breath, 0);

        if (!isAppearing)
        {
            currentAnimPos = targetLocalPos;
        }

        transform.localPosition = currentAnimPos + currentInteractionOffset + breathOffset;
        transform.localRotation = currentInteractionRotation;

        if (!isAppearing)
        {
            selectionBump = Mathf.Lerp(selectionBump, 0f, Time.deltaTime * 6f);
        }
    }

    void ProcessInteractions()
    {
        Vector3 targetOffset = Vector3.zero;

        // Atracci�n hacia la mano bloqueada en el plano 2D (Y = 0)
        Vector3 directionWorld = handWorldPos - GetBaseWorldPosition();
        Vector3 directionLocal = transform.parent != null
            ? transform.parent.InverseTransformDirection(directionWorld)
            : directionWorld;

        directionLocal.y = 0;

        Vector3 flatDirection = Vector3.back;
        if (directionLocal.sqrMagnitude > 0.001f)
        {
            flatDirection = directionLocal.normalized;
        }

        float attractionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
        targetOffset = flatDirection * (attractionLevel * 0.08f);

        // Repulsi�n de vecinos plana (para mantener distancias sin saltar hacia arriba/abajo)
        Vector3 neighborRepulsion = Vector3.zero;
        if (transform.parent != null)
        {
            foreach (Transform sibling in transform.parent)
            {
                if (sibling == transform) continue;

                Vector3 diff = transform.position - sibling.position;
                diff.y = 0;

                float dist = diff.magnitude;
                float minSpacing = 0.14f;

                if (dist < minSpacing && dist > 0.001f)
                {
                    float pushFactor = 1f - (dist / minSpacing);
                    Vector3 localPushDir = transform.parent.InverseTransformDirection(diff.normalized);
                    localPushDir.y = 0;
                    neighborRepulsion += localPushDir * (pushFactor * 0.02f);
                }
            }
        }
        targetOffset += neighborRepulsion;

        targetOffset += Vector3.down * selectionBump;

        // Movimiento suave y fluido
        currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, targetOffset, Time.deltaTime * smoothSpeed);
        currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
    }

    public void SetHover(float v)
    {
        // �LA VENDA FUERA! Siempre registra la curiosidad de la mano.
        hover = Mathf.Clamp(v, -1f, 1f);
    }

    public void SetPress(float v)
    {
        float incomingPress = Mathf.Clamp01(v);

        // �LA VENDA FUERA! Siempre guarda la presi�n para que las animaciones visuales funcionen.
        press = incomingPress;

        // Si est� naciendo o muriendo, NO EVALUAMOS CLICKS. 
        // As� evitamos viajar por accidente a mitad de animaci�n.
        if (isAppearing || isFadingOut) return;

        // El seguro ahora es perfecto: como el bot�n ya vio llegar tu mano de lejos, 
        // no se asustar� ni se bloquear�.
        if (needsReset)
        {
            if (incomingPress < 0.5f) needsReset = false;
            else return;
        }

        if (press >= 0.99f)
        {
            if (!hasPlayedPressSound && !isSelected)
            {
                hasPlayedPressSound = true;
                isSelected = true;
                selectionBump = 0.05f;

                if (audioSource != null && pressSound != null)
                {
                    audioSource.pitch = Random.Range(0.95f, 1.05f);
                    audioSource.PlayOneShot(pressSound, 1.0f);
                }

                OnOptionSelected?.Invoke(optionIndex);
            }
        }
        else if (press < 0.1f)
        {
            hasPlayedPressSound = false;
            isSelected = false;
        }
    }

    public void ForceInteractionOff()
    {
        isFadingOut = true;
        press = 0f;
        hover = 0f;
    }

    public void SetAlpha(float alpha)
    {
        if (rend == null || rend.sharedMaterial == null) return;
        rend.GetPropertyBlock(mpb);

        Color currentColor = rend.sharedMaterial.HasProperty(ColorPropURP)
            ? rend.sharedMaterial.GetColor(ColorPropURP)
            : rend.sharedMaterial.color;

        currentColor.a = alpha;

        if (rend.sharedMaterial.HasProperty(ColorPropURP))
            mpb.SetColor(ColorPropURP, currentColor);
        else
            mpb.SetColor(ColorPropBuiltIn, currentColor);

        if (rend.sharedMaterial.HasProperty(EmissionProp))
        {
            float scaleProgress = 0f;
            if (maxGrowth > 1f) scaleProgress = Mathf.Clamp01((currentScaleMultiplier - 1f) / (maxGrowth - 1f));

            float currentBrightness = (!isAppearing && !isFadingOut) ? Mathf.Lerp(1f, maxBrightness, scaleProgress) : 1f;
            Color targetEmission = baseEmissionColor * currentBrightness * alpha;
            mpb.SetColor(EmissionProp, targetEmission);
        }

        rend.SetPropertyBlock(mpb);
    }
}*/
/*using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FanOption : MonoBehaviour
{
    public static bool debugLogs = false;

    public int optionIndex;
    public static event System.Action<int> OnOptionSelected;

    Vector3 targetLocalPos;
    Vector3 baseScale;
    Renderer rend;
    MaterialPropertyBlock mpb;

    public float hover;
    public float press;

    private float selectionBump = 0f;
    private bool isSelected = false;

    // Seguro silencioso: Evita clicks fantasmas al teletransportarse, pero no interrumpe NINGUNA animaci�n.
    private bool ghostClickPreventer = true;

    private Vector3 handWorldPos;

    Vector3 currentInteractionOffset;
    Quaternion currentInteractionRotation = Quaternion.identity;

    bool isAppearing = true;
    bool isFadingOut = false;
    float randomBreathPhase;
    private Vector3 currentAnimPos;

    private float currentScaleMultiplier = 1f;

    [Header("Accesibilidad - Movimiento Relax")]
    [Tooltip("Velocidad de todas las animaciones (menor = m�s suave y zen)")]
    public float smoothSpeed = 3.5f;

    [Header("Feedback Visual")]
    [Tooltip("Cu�nto crece el bot�n al acercar la mano (1.25 = 25% m�s grande)")]
    public float maxGrowth = 1.25f;
    [Tooltip("Cu�nto aumenta la luz al cargar (1.5 = 50% m�s brillante)")]
    public float maxBrightness = 1.5f;

    static readonly int ColorPropURP = Shader.PropertyToID("_BaseColor");
    static readonly int ColorPropBuiltIn = Shader.PropertyToID("_Color");
    static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

    [Header("Audio Feedback")]
    public AudioClip pressSound;
    private AudioSource audioSource;
    private bool hasPlayedPressSound = false;

    public void SetHandPosition(Vector3 pos) { handWorldPos = pos; }

    public void SetTarget(Vector3 localPos)
    {
        targetLocalPos = localPos;
        isAppearing = true;
    }

    public Vector3 GetBaseWorldPosition()
    {
        if (transform.parent != null)
            return transform.parent.TransformPoint(targetLocalPos);
        return transform.position;
    }

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        interactionCollider = GetComponent<Collider>();
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        randomBreathPhase = Random.Range(0f, Mathf.PI * 2f);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.6f;

        if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(EmissionProp))
        {
            baseEmissionColor = rend.sharedMaterial.GetColor(EmissionProp);
        }
    }

    public void AnimateAppear(float t)
    {
        Vector3 radial = Vector3.Lerp(Vector3.zero, targetLocalPos, t);
        float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.08f;
        radial.y += arcHeight;

        currentAnimPos = radial;
        transform.localScale = baseScale * t;
        currentScaleMultiplier = 1f;

        SetAlpha(t);

        if (t >= 0.999f) isAppearing = false;
    }

    void Update()
    {
        if (!isAppearing && !isFadingOut)
        {
            ProcessInteractions();

            // Crecimiento suave basado puramente en cu�nto te acercas
            float interactionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
            float targetScale = Mathf.Lerp(1f, maxGrowth, interactionLevel);

            // Armon�a: Se escala usando el mismo suavizado que el movimiento
            currentScaleMultiplier = Mathf.Lerp(currentScaleMultiplier, targetScale, Time.deltaTime * smoothSpeed);
            transform.localScale = baseScale * currentScaleMultiplier;

            SetAlpha(1f);
        }
        else if (isFadingOut)
        {
            // Retorno suave a la posici�n base
            currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, Vector3.zero, Time.deltaTime * smoothSpeed);
            currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, Quaternion.identity, Time.deltaTime * smoothSpeed);
        }

        // Flotaci�n constante (Eje Y) inalterada
        float interactionLevelForBreath = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));
        float breathAmount = Mathf.Lerp(0.005f, 0f, interactionLevelForBreath);
        float breath = Mathf.Sin(Time.time * 1.5f + randomBreathPhase) * breathAmount;
        Vector3 breathOffset = new Vector3(0, breath, 0);

        if (!isAppearing)
        {
            currentAnimPos = targetLocalPos;
        }

        transform.localPosition = currentAnimPos + currentInteractionOffset + breathOffset;
        transform.localRotation = currentInteractionRotation;

        if (!isAppearing)
        {
            selectionBump = Mathf.Lerp(selectionBump, 0f, Time.deltaTime * 6f);
        }
    }

    void ProcessInteractions()
    {
        Vector3 targetOffset = Vector3.zero;
        Quaternion targetRotation = Quaternion.identity;

        // 1. Atracci�n hacia la mano
        Vector3 directionWorld = handWorldPos - GetBaseWorldPosition();
        Vector3 directionLocal = transform.parent != null
            ? transform.parent.InverseTransformDirection(directionWorld)
            : directionWorld;

        // BLOQUEO AL PLANO HORIZONTAL
        directionLocal.y = 0;

        Vector3 flatDirection = Vector3.back;
        if (directionLocal.sqrMagnitude > 0.001f)
        {
            flatDirection = directionLocal.normalized;
        }

        // Simplemente calculamos el nivel de inter�s (hover o press)
        float attractionLevel = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hover), press));

        // El bot�n se desliza hasta un m�ximo de 8cm hacia la mano
        targetOffset = flatDirection * (attractionLevel * 0.08f);

        // 2. Repulsi�n entre vecinos
        Vector3 neighborRepulsion = Vector3.zero;
        if (transform.parent != null)
        {
            foreach (Transform sibling in transform.parent)
            {
                if (sibling == transform) continue;

                Vector3 diff = transform.position - sibling.position;
                // BLOQUEO AL PLANO PARA VECINOS: As� no se empujan hacia arriba o abajo
                diff.y = 0;

                float dist = diff.magnitude;
                float minSpacing = 0.14f;

                if (dist < minSpacing && dist > 0.001f)
                {
                    float pushFactor = 1f - (dist / minSpacing);
                    Vector3 localPushDir = transform.parent.InverseTransformDirection(diff.normalized);
                    localPushDir.y = 0;
                    neighborRepulsion += localPushDir * (pushFactor * 0.02f);
                }
            }
        }
        targetOffset += neighborRepulsion;

        // 3. Rebote de selecci�n
        targetOffset += Vector3.down * selectionBump;

        // Suavizado total e incondicional
        currentInteractionOffset = Vector3.Lerp(currentInteractionOffset, targetOffset, Time.deltaTime * smoothSpeed);
        currentInteractionRotation = Quaternion.Slerp(currentInteractionRotation, targetRotation, Time.deltaTime * smoothSpeed);
    }

    // --- ENTRADA DE DATOS (Input) ---

    public void SetHover(float v)
    {
        if (isAppearing || isFadingOut) return;
        hover = Mathf.Clamp(v, -1f, 1f); // Siempre lo aceptamos sin condiciones
    }

    public void SetPress(float v)
    {
        if (isAppearing || isFadingOut) return;

        float incomingPress = Mathf.Clamp01(v);

        // Siempre lo aceptamos visualmente. Si retira la mano, esto bajar� suavemente a 0.
        press = incomingPress;

        // Seguro para el Click (Para no activar el viaje sin querer)
        if (ghostClickPreventer)
        {
            if (incomingPress < 0.5f) ghostClickPreventer = false;
            else return; // Bloquea �NICAMENTE el click final, pero permite toda la animaci�n visual.
        }

        // Acci�n de Confirmaci�n
        if (press >= 0.99f)
        {
            if (!hasPlayedPressSound && !isSelected)
            {
                hasPlayedPressSound = true;
                isSelected = true;
                selectionBump = 0.05f;

                if (audioSource != null && pressSound != null)
                {
                    audioSource.pitch = Random.Range(0.95f, 1.05f);
                    audioSource.PlayOneShot(pressSound, 1.0f);
                }

                OnOptionSelected?.Invoke(optionIndex);
            }
        }
        else if (press < 0.1f)
        {
            hasPlayedPressSound = false;
            isSelected = false;
        }
    }

    public void ForceInteractionOff()
    {
        isFadingOut = true;
        press = 0f;
        hover = 0f;
    }

    public void SetAlpha(float alpha)
    {
        if (rend == null || rend.sharedMaterial == null) return;
        rend.GetPropertyBlock(mpb);

        Color currentColor = rend.sharedMaterial.HasProperty(ColorPropURP)
            ? rend.sharedMaterial.GetColor(ColorPropURP)
            : rend.sharedMaterial.color;

        currentColor.a = alpha;

        if (rend.sharedMaterial.HasProperty(ColorPropURP))
            mpb.SetColor(ColorPropURP, currentColor);
        else
            mpb.SetColor(ColorPropBuiltIn, currentColor);

        if (rend.sharedMaterial.HasProperty(EmissionProp))
        {
            float scaleProgress = 0f;
            if (maxGrowth > 1f) scaleProgress = Mathf.Clamp01((currentScaleMultiplier - 1f) / (maxGrowth - 1f));

            float currentBrightness = (!isAppearing && !isFadingOut) ? Mathf.Lerp(1f, maxBrightness, scaleProgress) : 1f;
            Color targetEmission = baseEmissionColor * currentBrightness * alpha;
            mpb.SetColor(EmissionProp, targetEmission);
        }

        rend.SetPropertyBlock(mpb);
    }
}*/
