using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserSessionInfoCanvas : MonoBehaviour
{
    [Header("Canvas")]
    public GameObject canvasRoot;
    public TMP_Text titleText;
    public TMP_Text statusText;
    public Button closeButton;
    public Button refreshButton;

    [Header("Listado")]
    public Transform sessionsContent;
    public TMP_Text sessionTextPrefab;
    public ScrollRect sessionsScrollRect;

    private User currentUser;
    private Coroutine loadingCoroutine;
    private CanvasGroup canvasGroup;
    private TMP_Text reportTextInstance;
    private readonly StringBuilder reportBuilder = new();

    private void Awake()
    {
        if (canvasRoot == null)
            canvasRoot = gameObject;

        canvasGroup = canvasRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = canvasRoot.AddComponent<CanvasGroup>();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.onClick.AddListener(Refresh);
        }

        ConfigureSessionsContentLayout();
        Hide();
    }

    public void Show(User user)
    {
        currentUser = user;

        if (canvasRoot != null)
            canvasRoot.SetActive(true);

        SetCanvasInteractive(true);

        if (titleText != null)
            titleText.text = user != null ? "Sesiones de " + user.nom : "Sesiones";

        Refresh();
    }

    public void Hide()
    {
        SetCanvasInteractive(false);

        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    private void SetCanvasInteractive(bool interactive)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = interactive ? 1f : 0f;
        canvasGroup.interactable = interactive;
        canvasGroup.blocksRaycasts = interactive;
    }

    public void Refresh()
    {
        if (currentUser == null || currentUser.id_usuari <= 0)
        {
            SetStatus("Usuario invalido.");
            return;
        }

        if (loadingCoroutine != null)
            StopCoroutine(loadingCoroutine);

        loadingCoroutine = StartCoroutine(LoadSessions(currentUser));
    }

    private IEnumerator LoadSessions(User user)
    {
        ClearList();
        SetStatus("Cargando sesiones...");

        UserAPI.SessionSummary[] sessions = null;
        string error = null;

        yield return StartCoroutine(UserAPI.GetUserSessions(
            user.id_usuari,
            result => sessions = result,
            err => error = err
        ));

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError("Error cargando sesiones: " + error);
            yield return StartCoroutine(LoadLastSessionFallback(user));
            yield break;
        }

        if (sessions == null || sessions.Length == 0)
        {
            SetStatus("Este usuario no tiene sesiones.");
            RefreshSessionsLayout();
            yield break;
        }

        SetStatus("");

        foreach (UserAPI.SessionSummary session in sessions)
        {
            UserAPI.SessionPhaseInfo[] phases = null;
            UserAPI.SessionElementInfo[] tutorialElements = null;
            UserAPI.SessionElementInfo[] preparationElements = null;
            UserAPI.SessionElementInfo[] vrElements = null;

            yield return StartCoroutine(UserAPI.GetSessionPhases(
                session.id_sessio,
                result => phases = result,
                err => Debug.LogWarning("No se pudieron cargar fases de sesion " + session.id_sessio + ": " + err)
            ));

            yield return StartCoroutine(UserAPI.GetSessionTutorialElements(
                session.id_sessio,
                result => tutorialElements = result,
                err => Debug.LogWarning("No se pudieron cargar elementos tutorial de sesion " + session.id_sessio + ": " + err)
            ));

            yield return StartCoroutine(UserAPI.GetSessionPreparationElements(
                session.id_sessio,
                result => preparationElements = result,
                err => Debug.LogWarning("No se pudieron cargar elementos preparacion de sesion " + session.id_sessio + ": " + err)
            ));

            yield return StartCoroutine(UserAPI.GetSessionVrElements(
                session.id_sessio,
                result => vrElements = result,
                err => Debug.LogWarning("No se pudieron cargar elementos VR de sesion " + session.id_sessio + ": " + err)
            ));

            AddSessionText(BuildSessionText(session, phases, tutorialElements, preparationElements, vrElements));
        }

        RefreshSessionsLayout();
    }

    private IEnumerator LoadLastSessionFallback(User user)
    {
        SetStatus("No se pudo cargar el listado. Probando ultima sesion...");

        UserAPI.LastSessionResponse lastSession = null;
        string error = null;

        yield return StartCoroutine(UserAPI.GetLastSession(
            user.id_usuari,
            result => lastSession = result,
            err => error = err
        ));

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError("Error cargando ultima sesion: " + error);
            SetStatus("Error cargando sesiones: " + error);
            yield break;
        }

        if (lastSession == null || lastSession.id_sessio <= 0)
        {
            SetStatus("Este usuario no tiene sesiones.");
            yield break;
        }

        SetStatus("");
        AddSessionText(BuildLastSessionText(lastSession));
        RefreshSessionsLayout();
    }

    private string BuildSessionText(
        UserAPI.SessionSummary session,
        UserAPI.SessionPhaseInfo[] phases,
        UserAPI.SessionElementInfo[] tutorialElements,
        UserAPI.SessionElementInfo[] preparationElements,
        UserAPI.SessionElementInfo[] vrElements)
    {
        StringBuilder sb = new();

        sb.AppendLine("Sesion #" + session.id_sessio);
        AppendIfAny(sb, "Inicio", FirstNotEmpty(session.iniciada_a, session.creat_a));
        AppendIfAny(sb, "Fin", session.finalitzada_a);
        AppendIfAny(sb, "Postura inicial", session.postura_inicial);
        AppendIfAny(sb, "Postura final", session.postura_final);
        AppendDuration(sb, "Duracion total", session.durada_total_segons);
        AppendDuration(sb, "Tutorial", session.durada_tutorial_segons);
        AppendDuration(sb, "Preparacion", session.durada_preparacio_segons);
        AppendDuration(sb, "VR", session.durada_vr_segons);
        sb.AppendLine("Menu manos: " + BoolText(session.menu_mans_actiu));
        sb.AppendLine("Particulas manos: " + BoolText(session.particules_mans_actives));
        AppendIfAny(sb, "Observaciones", session.observacions);

        AppendPhases(sb, phases);
        AppendElements(sb, "Elementos tutorial", tutorialElements);
        AppendElements(sb, "Elementos preparacion", preparationElements);
        AppendElements(sb, "Elementos VR", vrElements);

        return sb.ToString();
    }

    private string BuildLastSessionText(UserAPI.LastSessionResponse session)
    {
        StringBuilder sb = new();

        sb.AppendLine("Ultima sesion #" + session.id_sessio);
        AppendIfAny(sb, "Postura inicial", session.postura_inicial);
        AppendIfAny(sb, "Postura final", session.postura_final);
        AppendIfAny(sb, "Postura actual", session.postura_actual);
        sb.AppendLine("Menu manos: " + BoolText(session.menu_mans_actiu));
        sb.AppendLine("Particulas manos: " + BoolText(session.particules_mans_actives));

        if (session.vr_elements != null && session.vr_elements.Length > 0)
        {
            sb.AppendLine("Elementos VR:");
            foreach (UserAPI.LastSessionElement element in session.vr_elements)
            {
                sb.Append("  - ").Append(element.id_element);
                if (element.numero_posicio > 0)
                    sb.Append(" pos ").Append(element.numero_posicio);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private void AddSessionText(string text)
    {
        if (sessionsContent == null || sessionTextPrefab == null)
        {
            Debug.LogWarning("Faltan sessionsContent o sessionTextPrefab en UserSessionInfoCanvas.");
            return;
        }

        if (reportBuilder.Length > 0)
            reportBuilder.AppendLine().AppendLine("----------------------------------------").AppendLine();

        reportBuilder.Append(text);
        EnsureReportTextInstance();
        ApplyReportTextLayout();
        StartCoroutine(RefreshSessionsLayoutNextFrame());
    }

    private void EnsureReportTextInstance()
    {
        if (reportTextInstance != null)
            return;

        reportTextInstance = Instantiate(sessionTextPrefab, sessionsContent, false);
        reportTextInstance.gameObject.SetActive(true);
        reportTextInstance.enableWordWrapping = true;
        reportTextInstance.overflowMode = TextOverflowModes.Overflow;
        reportTextInstance.alignment = TextAlignmentOptions.TopLeft;
        reportTextInstance.margin = Vector4.zero;
        reportTextInstance.rectTransform.localScale = Vector3.one;

        RectTransform textRect = reportTextInstance.GetComponent<RectTransform>();
        if (textRect != null)
        {
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = new Vector2(16f, -16f);
        }

        LayoutElement layoutElement = reportTextInstance.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = reportTextInstance.gameObject.AddComponent<LayoutElement>();
    }

    private float GetAvailableTextWidth()
    {
        if (sessionsScrollRect != null && sessionsScrollRect.viewport != null)
            return Mathf.Max(100f, sessionsScrollRect.viewport.rect.width - 32f);

        if (sessionsContent is RectTransform contentRect)
            return Mathf.Max(100f, contentRect.rect.width - 32f);

        return 600f;
    }

    private void RefreshSessionsLayout()
    {
        if (sessionsContent == null)
            return;

        RectTransform contentRect = (RectTransform)sessionsContent;
        EnsureContentRectIsScrollable(contentRect);
        ApplyReportTextLayout();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();

        if (sessionsScrollRect != null)
        {
            sessionsScrollRect.vertical = true;
            sessionsScrollRect.horizontal = false;
            sessionsScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private IEnumerator RefreshSessionsLayoutNextFrame()
    {
        yield return null;
        RefreshSessionsLayout();
    }

    private void EnsureContentRectIsScrollable(RectTransform contentRect)
    {
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
    }

    private void ApplyManualContentHeight(RectTransform contentRect)
    {
        ApplyReportTextLayout();
    }

    private void ApplyReportTextLayout()
    {
        if (sessionsContent == null || reportTextInstance == null)
            return;

        RectTransform contentRect = (RectTransform)sessionsContent;
        RectTransform textRect = reportTextInstance.GetComponent<RectTransform>();

        float viewportWidth = sessionsScrollRect != null && sessionsScrollRect.viewport != null
            ? sessionsScrollRect.viewport.rect.width
            : 600f;
        float viewportHeight = sessionsScrollRect != null && sessionsScrollRect.viewport != null
            ? sessionsScrollRect.viewport.rect.height
            : 0f;

        float availableWidth = Mathf.Max(100f, viewportWidth - 48f);
        reportTextInstance.text = reportBuilder.ToString();
        if (textRect != null)
            textRect.sizeDelta = new Vector2(availableWidth, 10000f);

        reportTextInstance.ForceMeshUpdate();

        float preferredHeight = Mathf.Ceil(reportTextInstance.GetPreferredValues(reportTextInstance.text, availableWidth, Mathf.Infinity).y) + 48f;
        float finalHeight = Mathf.Max(preferredHeight, viewportHeight);

        if (textRect != null)
            textRect.sizeDelta = new Vector2(availableWidth, finalHeight - 32f);

        LayoutElement layoutElement = reportTextInstance.GetComponent<LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.preferredWidth = availableWidth;
            layoutElement.minHeight = finalHeight;
            layoutElement.preferredHeight = finalHeight;
            layoutElement.flexibleHeight = 0f;
        }

        contentRect.sizeDelta = new Vector2(Mathf.Max(viewportWidth, availableWidth + 32f), finalHeight);
    }

    private void ConfigureSessionsContentLayout()
    {
        if (sessionsContent == null)
            return;

        VerticalLayoutGroup layoutGroup = sessionsContent.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null)
            layoutGroup.enabled = false;

        ContentSizeFitter fitter = sessionsContent.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.enabled = false;
    }

    private void ClearList()
    {
        if (sessionsContent == null)
            return;

        reportTextInstance = null;
        reportBuilder.Clear();

        for (int i = sessionsContent.childCount - 1; i >= 0; i--)
        {
            Transform child = sessionsContent.GetChild(i);
            if (sessionTextPrefab != null && child == sessionTextPrefab.transform)
                continue;

            Destroy(child.gameObject);
        }

        RefreshSessionsLayout();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private static void AppendPhases(StringBuilder sb, UserAPI.SessionPhaseInfo[] phases)
    {
        if (phases == null || phases.Length == 0)
            return;

        sb.AppendLine("Fases:");
        foreach (UserAPI.SessionPhaseInfo phase in phases)
        {
            sb.Append("  - ").Append(phase.fase);
            if (phase.durada_segons > 0)
                sb.Append(" (").Append(FormatSeconds(phase.durada_segons)).Append(")");
            sb.AppendLine();
        }
    }

    private static void AppendElements(StringBuilder sb, string title, UserAPI.SessionElementInfo[] elements)
    {
        if (elements == null || elements.Length == 0)
            return;

        sb.AppendLine(title + ":");
        foreach (UserAPI.SessionElementInfo element in elements)
        {
            sb.Append("  - ").Append(element.id_element);

            if (element.numero_posicio > 0)
                sb.Append(" pos ").Append(element.numero_posicio);

            if (element.durada_segons > 0)
                sb.Append(" (").Append(FormatSeconds(element.durada_segons)).Append(")");

            sb.AppendLine();
        }
    }

    private static void AppendIfAny(StringBuilder sb, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sb.AppendLine(label + ": " + value);
    }

    private static void AppendDuration(StringBuilder sb, string label, double seconds)
    {
        if (seconds > 0)
            sb.AppendLine(label + ": " + FormatSeconds(seconds));
    }

    private static string FirstNotEmpty(string a, string b)
    {
        return !string.IsNullOrWhiteSpace(a) ? a : b;
    }

    private static string BoolText(bool value)
    {
        return value ? "activo" : "inactivo";
    }

    private static string FormatSeconds(double seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt((float)seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return minutes + "m " + remainingSeconds + "s";
    }
}
