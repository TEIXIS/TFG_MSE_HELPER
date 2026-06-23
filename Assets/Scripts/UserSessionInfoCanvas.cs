using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserSessionInfoCanvas : MonoBehaviour
{
    private const string HeaderColor = "#DCE8FF";
    private const string LabelColor = "#9CB8FF";
    private const string SectionColor = "#A7F0BA";
    private const string MutedColor = "#B8C0CC";
    private const string ActiveColor = "#93E088";
    private const string InactiveColor = "#FFB1A6";

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
    private readonly List<TMP_Text> reportTextInstances = new();
    private const float ItemSpacing = 22f;

    private void Awake()
    {
        AutoWireReferences();

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

    private void OnValidate()
    {
        AutoWireReferences();
        ConfigureSessionsContentLayout();
    }

    private void AutoWireReferences()
    {
        if (canvasRoot == null)
            canvasRoot = gameObject;

        Transform searchRoot = canvasRoot != null ? canvasRoot.transform : transform;

        if (sessionsScrollRect == null)
        {
            if (sessionsContent != null)
                sessionsScrollRect = sessionsContent.GetComponentInParent<ScrollRect>(true);

            if (sessionsScrollRect == null && searchRoot != null)
                sessionsScrollRect = searchRoot.GetComponentInChildren<ScrollRect>(true);
        }

        if (sessionsContent == null && sessionsScrollRect != null)
            sessionsContent = sessionsScrollRect.content;

        if (sessionsScrollRect != null)
        {
            RectTransform contentRect = sessionsContent as RectTransform;
            if (contentRect != null)
                sessionsScrollRect.content = contentRect;

            if (sessionsScrollRect.viewport == null)
            {
                RectTransform viewport = FindChildByName(sessionsScrollRect.transform, "Viewport") as RectTransform;
                if (viewport != null)
                    sessionsScrollRect.viewport = viewport;
            }

            sessionsScrollRect.horizontal = false;
            sessionsScrollRect.vertical = true;
            sessionsScrollRect.inertia = true;
            sessionsScrollRect.scrollSensitivity = Mathf.Max(sessionsScrollRect.scrollSensitivity, 24f);
        }
    }

    public void Show(User user)
    {
        currentUser = user;

        if (canvasRoot != null)
            canvasRoot.SetActive(true);

        SetCanvasInteractive(true);

        if (titleText != null)
            titleText.text = user != null ? "Sessions de " + user.nom : "Sessions";

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
            SetStatus("Usuari invalid.");
            return;
        }

        if (loadingCoroutine != null)
            StopCoroutine(loadingCoroutine);

        loadingCoroutine = StartCoroutine(LoadSessions(currentUser));
    }

    private IEnumerator LoadSessions(User user)
    {
        ClearList();
        SetStatus("Carregant sessions...");

        UserAPI.SessionSummary[] sessions = null;
        string error = null;

        yield return StartCoroutine(UserAPI.GetUserSessions(
            user.id_usuari,
            result => sessions = result,
            err => error = err
        ));

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError("Error carregant sessions: " + error);
            yield return StartCoroutine(LoadLastSessionFallback(user));
            yield break;
        }

        if (sessions == null || sessions.Length == 0)
        {
            SetStatus("Aquest usuari no te sessions.");
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
                err => Debug.LogWarning("No s'han pogut carregar les fases de la sessio " + session.id_sessio + ": " + err)
            ));

            yield return StartCoroutine(UserAPI.GetSessionTutorialElements(
                session.id_sessio,
                result => tutorialElements = result,
                err => Debug.LogWarning("No s'han pogut carregar els elements del tutorial de la sessio " + session.id_sessio + ": " + err)
            ));

            yield return StartCoroutine(UserAPI.GetSessionPreparationElements(
                session.id_sessio,
                result => preparationElements = result,
                err => Debug.LogWarning("No s'han pogut carregar els elements de preparacio de la sessio " + session.id_sessio + ": " + err)
            ));

            yield return StartCoroutine(UserAPI.GetSessionVrElements(
                session.id_sessio,
                result => vrElements = result,
                err => Debug.LogWarning("No s'han pogut carregar els elements VR de la sessio " + session.id_sessio + ": " + err)
            ));

            AddSessionText(BuildSessionText(session, phases, tutorialElements, preparationElements, vrElements));
        }

        RefreshSessionsLayout();
    }

    private IEnumerator LoadLastSessionFallback(User user)
    {
        SetStatus("No s'ha pogut carregar el llistat. Provant l'ultima sessio...");

        UserAPI.LastSessionResponse lastSession = null;
        string error = null;

        yield return StartCoroutine(UserAPI.GetLastSession(
            user.id_usuari,
            result => lastSession = result,
            err => error = err
        ));

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError("Error carregant l'ultima sessio: " + error);
            SetStatus("Error carregant sessions: " + error);
            yield break;
        }

        if (lastSession == null || lastSession.id_sessio <= 0)
        {
            SetStatus("Aquest usuari no te sessions.");
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

        AppendSessionHeader(sb, "Sessio #" + session.id_sessio);
        AppendIfAny(sb, "Inici", FormatDateText(FirstNotEmpty(session.iniciada_a, session.creat_a)));
        AppendIfAny(sb, "Final", FormatDateText(session.finalitzada_a));
        AppendIfAny(sb, "Postura inicial", FormatPosture(session.postura_inicial));
        AppendIfAny(sb, "Postura final", FormatPosture(session.postura_final));
        AppendDuration(sb, "Durada total", session.durada_total_segons);
        AppendDuration(sb, "Tutorial", session.durada_tutorial_segons);
        AppendDuration(sb, "Preparacio", session.durada_preparacio_segons);
        AppendDuration(sb, "VR", session.durada_vr_segons);
        AppendBool(sb, "Menu de mans", session.menu_mans_actiu);
        AppendBool(sb, "Particules de mans", session.particules_mans_actives);
        AppendIfAny(sb, "Observacions", session.observacions);

        AppendPhases(sb, phases);
        AppendElements(sb, "Elements del tutorial", tutorialElements, false);
        AppendElements(sb, "Elements de preparacio", preparationElements, true);
        AppendElements(sb, "Elements VR", vrElements, false);

        return sb.ToString();
    }

    private string BuildLastSessionText(UserAPI.LastSessionResponse session)
    {
        StringBuilder sb = new();

        AppendSessionHeader(sb, "Ultima sessio #" + session.id_sessio);
        AppendIfAny(sb, "Postura inicial", FormatPosture(session.postura_inicial));
        AppendIfAny(sb, "Postura final", FormatPosture(session.postura_final));
        AppendIfAny(sb, "Postura actual", FormatPosture(session.postura_actual));
        AppendBool(sb, "Menu de mans", session.menu_mans_actiu);
        AppendBool(sb, "Particules de mans", session.particules_mans_actives);

        if (session.preparation_elements != null && session.preparation_elements.Length > 0)
        {
            AppendSectionTitle(sb, "Elements de preparacio");
            foreach (UserAPI.LastSessionElement element in session.preparation_elements)
            {
                if (element == null || !element.seleccionat)
                    continue;

                AppendElementBullet(sb, element.id_element, element.numero_posicio, 0, element.seleccionat);
            }
        }

        if (session.vr_elements != null && session.vr_elements.Length > 0)
        {
            AppendSectionTitle(sb, "Elements VR");
            foreach (UserAPI.LastSessionElement element in session.vr_elements)
            {
                if (element == null || !element.seleccionat)
                    continue;

                AppendElementBullet(sb, element.id_element, element.numero_posicio, 0, element.seleccionat);
            }
        }

        return sb.ToString();
    }

    private void AddSessionText(string text)
    {
        if (sessionsContent == null || sessionTextPrefab == null)
        {
            Debug.LogWarning("Falten sessionsContent o sessionTextPrefab a UserSessionInfoCanvas.");
            return;
        }

        TMP_Text textInstance = Instantiate(sessionTextPrefab, sessionsContent, false);
        ConfigureReportTextInstance(textInstance);
        textInstance.text = text;
        reportTextInstances.Add(textInstance);
        ApplyReportTextLayout();
        StartCoroutine(RefreshSessionsLayoutNextFrame());
    }

    private void ConfigureReportTextInstance(TMP_Text textInstance)
    {
        if (textInstance == null)
            return;

        textInstance.gameObject.SetActive(true);
        textInstance.textWrappingMode = TextWrappingModes.Normal;
        textInstance.overflowMode = TextOverflowModes.Overflow;
        textInstance.alignment = TextAlignmentOptions.TopLeft;
        textInstance.richText = true;
        textInstance.fontSize = 24f;
        textInstance.lineSpacing = 7f;
        textInstance.paragraphSpacing = 8f;
        textInstance.color = new Color(0.92f, 0.95f, 1f, 1f);
        textInstance.margin = new Vector4(8f, 4f, 8f, 4f);
        textInstance.rectTransform.localScale = Vector3.one;

        RectTransform textRect = textInstance.GetComponent<RectTransform>();
        if (textRect != null)
        {
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = Vector2.zero;
        }

        LayoutElement layoutElement = textInstance.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = textInstance.gameObject.AddComponent<LayoutElement>();
    }

    private float GetAvailableTextWidth()
    {
        float viewportWidth = GetViewportWidth();
        if (viewportWidth > 1f)
            return Mathf.Max(100f, viewportWidth - 32f);

        if (sessionsContent is RectTransform contentRect)
            return Mathf.Max(100f, contentRect.rect.width - 32f);

        return 600f;
    }

    private void RefreshSessionsLayout(bool resetScrollPosition = false)
    {
        if (sessionsContent == null)
            return;

        RectTransform contentRect = (RectTransform)sessionsContent;
        EnsureContentRectIsScrollable(contentRect, resetScrollPosition);
        ApplyReportTextLayout();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();

        if (sessionsScrollRect != null)
        {
            sessionsScrollRect.vertical = true;
            sessionsScrollRect.horizontal = false;

            if (resetScrollPosition)
                sessionsScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private IEnumerator RefreshSessionsLayoutNextFrame()
    {
        yield return null;
        RefreshSessionsLayout();
    }

    private void EnsureContentRectIsScrollable(RectTransform contentRect, bool resetScrollPosition)
    {
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);

        if (resetScrollPosition)
            contentRect.anchoredPosition = Vector2.zero;
    }

    private void ApplyManualContentHeight(RectTransform contentRect)
    {
        ApplyReportTextLayout();
    }

    private void ApplyReportTextLayout()
    {
        if (sessionsContent == null)
            return;

        RectTransform contentRect = (RectTransform)sessionsContent;

        float viewportWidth = GetViewportWidth();
        float viewportHeight = GetViewportHeight();

        float availableWidth = Mathf.Max(100f, viewportWidth - 48f);
        float y = 16f;

        foreach (TMP_Text textInstance in reportTextInstances)
        {
            if (textInstance == null)
                continue;

            RectTransform textRect = textInstance.GetComponent<RectTransform>();
            if (textRect != null)
                textRect.sizeDelta = new Vector2(availableWidth, 10000f);

            textInstance.ForceMeshUpdate();
            float preferredHeight = Mathf.Ceil(textInstance.GetPreferredValues(textInstance.text, availableWidth, Mathf.Infinity).y) + 32f;
            preferredHeight = Mathf.Max(64f, preferredHeight);

            if (textRect != null)
            {
                textRect.anchorMin = new Vector2(0f, 1f);
                textRect.anchorMax = new Vector2(0f, 1f);
                textRect.pivot = new Vector2(0f, 1f);
                textRect.anchoredPosition = new Vector2(16f, -y);
                textRect.sizeDelta = new Vector2(availableWidth, preferredHeight);
            }

            LayoutElement layoutElement = textInstance.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredWidth = availableWidth;
                layoutElement.minHeight = preferredHeight;
                layoutElement.preferredHeight = preferredHeight;
                layoutElement.flexibleHeight = 0f;
            }

            y += preferredHeight + ItemSpacing;
        }

        float finalHeight = Mathf.Max(y + 16f, viewportHeight);
        contentRect.sizeDelta = new Vector2(Mathf.Max(viewportWidth, availableWidth + 32f), finalHeight);
    }

    private float GetViewportWidth()
    {
        if (sessionsScrollRect != null && sessionsScrollRect.viewport != null && sessionsScrollRect.viewport.rect.width > 1f)
            return sessionsScrollRect.viewport.rect.width;

        if (sessionsScrollRect != null)
        {
            RectTransform scrollRectTransform = sessionsScrollRect.GetComponent<RectTransform>();
            if (scrollRectTransform != null && scrollRectTransform.rect.width > 1f)
                return scrollRectTransform.rect.width;
        }

        if (sessionsContent is RectTransform contentRect && contentRect.rect.width > 1f)
            return contentRect.rect.width;

        return 600f;
    }

    private float GetViewportHeight()
    {
        if (sessionsScrollRect != null && sessionsScrollRect.viewport != null && sessionsScrollRect.viewport.rect.height > 1f)
            return sessionsScrollRect.viewport.rect.height;

        if (sessionsScrollRect != null)
        {
            RectTransform scrollRectTransform = sessionsScrollRect.GetComponent<RectTransform>();
            if (scrollRectTransform != null && scrollRectTransform.rect.height > 1f)
                return scrollRectTransform.rect.height;
        }

        return 0f;
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

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void ClearList()
    {
        if (sessionsContent == null)
            return;

        reportTextInstances.Clear();

        for (int i = sessionsContent.childCount - 1; i >= 0; i--)
        {
            Transform child = sessionsContent.GetChild(i);
            if (sessionTextPrefab != null && child == sessionTextPrefab.transform)
                continue;

            Destroy(child.gameObject);
        }

        RefreshSessionsLayout(true);
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

        AppendSectionTitle(sb, "Fases");
        foreach (UserAPI.SessionPhaseInfo phase in phases)
        {
            sb.Append("  <color=").Append(MutedColor).Append(">-</color> <b>").Append(FormatPhase(phase.fase)).Append("</b>");
            if (phase.durada_segons > 0)
                sb.Append(" <color=").Append(MutedColor).Append(">(").Append(FormatSeconds(phase.durada_segons)).Append(")</color>");
            sb.AppendLine();
        }
    }

    private static void AppendElements(StringBuilder sb, string title, UserAPI.SessionElementInfo[] elements, bool showUnselectedState)
    {
        if (elements == null || elements.Length == 0)
            return;

        bool hasVisibleElements = false;
        foreach (UserAPI.SessionElementInfo element in elements)
        {
            if (element == null || string.IsNullOrWhiteSpace(element.id_element))
                continue;

            if (!showUnselectedState && !element.seleccionat)
                continue;

            hasVisibleElements = true;
            break;
        }

        if (!hasVisibleElements)
            return;

        AppendSectionTitle(sb, title);
        foreach (UserAPI.SessionElementInfo element in elements)
        {
            if (element == null || string.IsNullOrWhiteSpace(element.id_element))
                continue;

            if (!showUnselectedState && !element.seleccionat)
                continue;

            AppendElementBullet(sb, element.id_element, element.numero_posicio, element.durada_segons, element.seleccionat, showUnselectedState);
        }
    }

    private static void AppendIfAny(StringBuilder sb, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sb.Append("<color=").Append(LabelColor).Append("><b>").Append(label).Append("</b></color>: ")
                .Append(value)
                .AppendLine();
    }

    private static void AppendDuration(StringBuilder sb, string label, double seconds)
    {
        if (seconds > 0)
            AppendIfAny(sb, label, FormatSeconds(seconds));
    }

    private static string FirstNotEmpty(string a, string b)
    {
        return !string.IsNullOrWhiteSpace(a) ? a : b;
    }

    private static string BoolText(bool value)
    {
        return value ? "<color=" + ActiveColor + "><b>actiu</b></color>" : "<color=" + InactiveColor + "><b>inactiu</b></color>";
    }

    private static void AppendSessionHeader(StringBuilder sb, string title)
    {
        sb.Append("<size=30><color=").Append(HeaderColor).Append("><b>").Append(title).Append("</b></color></size>").AppendLine();
    }

    private static void AppendSectionTitle(StringBuilder sb, string title)
    {
        sb.AppendLine();
        sb.Append("<color=").Append(SectionColor).Append("><b>").Append(title).Append("</b></color>").AppendLine();
    }

    private static void AppendBool(StringBuilder sb, string label, bool value)
    {
        AppendIfAny(sb, label, BoolText(value));
    }

    private static void AppendElementBullet(StringBuilder sb, string idElement, int numeroPosicion, double duracionSegundos, bool seleccionado, bool showUnselectedState = true)
    {
        if (string.IsNullOrWhiteSpace(idElement))
            return;

        sb.Append("  <color=").Append(MutedColor).Append(">-</color> <b>").Append(FormatElementName(idElement)).Append("</b>");

        if (numeroPosicion > 0)
            sb.Append(" <color=").Append(MutedColor).Append(">pos ").Append(numeroPosicion).Append("</color>");

        if (duracionSegundos > 0)
            sb.Append(" <color=").Append(MutedColor).Append(">(").Append(FormatSeconds(duracionSegundos)).Append(")</color>");

        if (showUnselectedState && !seleccionado)
            sb.Append(" <color=").Append(InactiveColor).Append(">eliminat de la seleccio</color>");

        sb.AppendLine();
    }

    private static string FormatElementName(string idElement)
    {
        if (string.IsNullOrWhiteSpace(idElement))
            return idElement;

        switch (idElement.Trim().ToLowerInvariant())
        {
            case "blau_cel":
                return "Lampara Bombolles";
            case "taronja":
                return "Llum UV";
            case "rosa":
                return "Musica";
            case "groc":
                return "Taula de So";
            case "verd":
                return "Taula d'Elements";
            case "blanc":
                return "Catifa";
            case "blau_fosc":
                return "Control de Llums";
            case "gris":
                return "Projector";
            case "negre":
                return "Visualitzador de So";
            default:
                return idElement;
        }
    }

    private static string FormatDateText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return value.Replace("T", " ").Replace("Z", "");
    }

    private static string FormatPosture(string posture)
    {
        if (posture == "SENTADO")
            return "Assegut";
        if (posture == "DE_PIE")
            return "Dret";
        return posture;
    }

    private static string FormatPhase(string phase)
    {
        if (string.IsNullOrWhiteSpace(phase))
            return phase;

        return phase.Replace("_", " ");
    }

    private static string FormatSeconds(double seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt((float)seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return minutes + "m " + remainingSeconds + "s";
    }
}
