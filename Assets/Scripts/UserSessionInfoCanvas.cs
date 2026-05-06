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

    private User currentUser;
    private Coroutine loadingCoroutine;
    private CanvasGroup canvasGroup;

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

        TMP_Text item = Instantiate(sessionTextPrefab, sessionsContent, false);
        item.gameObject.SetActive(true);
        item.text = text;
    }

    private void ClearList()
    {
        if (sessionsContent == null)
            return;

        for (int i = sessionsContent.childCount - 1; i >= 0; i--)
            Destroy(sessionsContent.GetChild(i).gameObject);
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
