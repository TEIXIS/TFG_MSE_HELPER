using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserSelectScreen : MonoBehaviour
{
    private const string RoomWhite = "blanca";
    private const string RoomAdult = "adult";
    private const string RoomChild = "infantil";
    private const string StandingPosture = "DE_PIE";
    private const string SittingPosture = "SENTADO";

    [Header("UI")]
    public Transform listContent;
    public GameObject userRowPrefab;

    [Header("Crear usuario")]
    public GameObject createDialog;
    public RectTransform createDialogContent;
    public TMP_InputField createInput;
    public Toggle independentToggle;

    [Tooltip("Marcado = Infant. Desmarcado = Adult.")]
    public Toggle infantilToggle;
    public Toggle adultRoomToggle;
    public Toggle whiteRoomToggle;
    public Toggle sittingToggle;
    public TMP_Dropdown roomTypeDropdown;
    public TMP_Dropdown postureDropdown;

    public Toggle menuHandsToggle;
    public Toggle particlesToggle;
    public Button openCreateButton;
    public Button createButton;
    public Button cancelCreateButton;

    [Header("Estado")]
    public GameObject selectedActionsPanel;
    public TMP_Text selectedUserText;
    public TMP_Text statusText;
    public TMP_Text headsetSearchText;
    public GameObject connectionPopup;
    public RectTransform connectionPopupContent;
    public Button cancelConnectionButton;
    public GameObject infoPopup;
    public RectTransform infoPopupContent;
    public bool closePopupsWhenClickOutside = false;
    public Button connectHeadsetButton;
    public Button closeSelectedActionsButton;
    public Button deleteSelectedButton;
    public Button infoSelectedButton;
    public UIManager uiManager;
    public SessionTracker sessionTracker;
    public UserSessionInfoCanvas sessionInfoCanvas;

    private readonly List<User> users = new();
    private User selectedUser;
    private bool closePopupsRequested;

    private void OnEnable()
    {
        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        if (openCreateButton != null)
        {
            openCreateButton.onClick.RemoveAllListeners();
            openCreateButton.onClick.AddListener(() => SetCreateDialogVisible(true));
        }

        if (createButton != null)
        {
            createButton.onClick.RemoveAllListeners();
            createButton.onClick.AddListener(CreateUserFromInput);
        }

        if (cancelCreateButton != null)
        {
            cancelCreateButton.onClick.RemoveAllListeners();
            cancelCreateButton.onClick.AddListener(CancelCreateUser);
        }

        WireRoomToggle(whiteRoomToggle, RoomWhite);
        WireRoomToggle(adultRoomToggle, RoomAdult);
        WireRoomToggle(infantilToggle, RoomChild);
        ConfigureCreateDropdowns();

        if (connectHeadsetButton != null)
        {
            connectHeadsetButton.onClick.RemoveAllListeners();
            connectHeadsetButton.onClick.AddListener(ConnectSelectedUser);
        }

        if (cancelConnectionButton != null)
        {
            cancelConnectionButton.onClick.RemoveAllListeners();
            cancelConnectionButton.onClick.AddListener(() => SetConnectionPopupVisible(false));
        }

        if (closeSelectedActionsButton != null)
        {
            closeSelectedActionsButton.onClick.RemoveAllListeners();
            closeSelectedActionsButton.onClick.AddListener(CloseSelectedActions);
        }

        if (deleteSelectedButton != null)
        {
            deleteSelectedButton.onClick.RemoveAllListeners();
            deleteSelectedButton.onClick.AddListener(DeleteSelectedUser);
        }

        if (infoSelectedButton != null)
        {
            infoSelectedButton.onClick.RemoveAllListeners();
            infoSelectedButton.onClick.AddListener(ShowSelectedUserSessions);
        }

        SetCreateDefaults();
        SetCreateDialogVisible(false);
        SetConnectionPopupVisible(false);
        SetInfoPopupVisible(false);
        RefreshSelectedActions();
        RefreshSelectedLabel();
        StartCoroutine(LoadUsersFromServer());
    }

    private void Update()
    {
        if (!closePopupsWhenClickOutside || !AnyPopupVisible())
            return;

        if (!PointerDownThisFrame(out Vector2 screenPosition))
            return;

        if (PointerIsInsideAnyPopup(screenPosition))
            return;

        closePopupsRequested = true;
        StartCoroutine(ClosePopupsAtEndOfFrame());
    }

    private IEnumerator LoadUsersFromServer()
    {
        SetStatus("Cargando usuarios...");

        yield return StartCoroutine(UserAPI.GetUsers(
            onSuccess: loadedUsers =>
            {
                users.Clear();
                users.AddRange(loadedUsers);

                RefreshList();
                SetStatus(users.Count == 0 ? "No hay usuarios." : "");
            },
            onError: err =>
            {
                Debug.LogError("Error cargando usuarios: " + err);
                SetStatus("Error cargando usuarios: " + err);
            }
        ));
    }

    private void RefreshList()
    {
        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        foreach (User user in users)
        {
            GameObject row = Instantiate(userRowPrefab, listContent, false);
            UserRowUI rowUI = row.GetComponent<UserRowUI>();

            if (rowUI == null)
            {
                Debug.LogError("UserRowUI no encontrado en prefab");
                continue;
            }

            rowUI.Bind(
                user,
                onSelect: SelectUser,
                onSave: editedUser => StartCoroutine(UpdateUserOnServer(editedUser)),
                onDelete: userToDelete => StartCoroutine(DeleteUserFromServer(userToDelete)),
                onInfo: ShowUserSessions
            );
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)listContent);
    }

    private void CreateUserFromInput()
    {
        string nom = createInput != null ? createInput.text.Trim() : "";

        if (string.IsNullOrEmpty(nom))
        {
            SetStatus("Introduce un nombre.");
            return;
        }

        bool independent = false;

        string roomType = GetSelectedCreateRoomType();
        string posture = GetSelectedCreatePosture();
        bool entornAdult = roomType == RoomAdult;

        bool menuMansActiu = true;
        bool particulesActives = true;

        SetStatus("Creando usuario...");

        StartCoroutine(UserAPI.CreateUser(
            nom,
            roomType,
            posture,
            entornAdult,
            independent,
            menuMansActiu,
            particulesActives,
            onSuccess: () =>
            {
                if (createInput != null)
                    createInput.text = "";

                SetCreateDefaults();
                SetCreateDialogVisible(false);

                StartCoroutine(LoadUsersFromServer());
            },
            onError: err =>
            {
                Debug.LogError("Error creando usuario: " + err);
                SetStatus("Error creando usuario: " + err);
            }
        ));
    }

    private IEnumerator UpdateUserOnServer(User editedUser)
    {
        SetStatus("Guardando cambios...");

        yield return StartCoroutine(UserAPI.UpdateUser(
            editedUser,
            onSuccess: () =>
            {
                SetStatus("Usuario actualizado.");
                StartCoroutine(LoadUsersFromServer());
            },
            onError: err =>
            {
                Debug.LogError("Error actualizando usuario: " + err);
                SetStatus("Error actualizando usuario: " + err);
            }
        ));
    }

    private IEnumerator DeleteUserFromServer(User user)
    {
        SetStatus("Eliminando usuario...");

        yield return StartCoroutine(UserAPI.DeleteUser(
            user.id_usuari,
            onSuccess: () =>
            {
                if (SessionUser.SelectedUserId == user.id_usuari)
                {
                    SessionUser.SelectedUserId = -1;
                    SessionUser.SelectedUserName = null;
                    SessionUser.SelectedRoomType = RoomWhite;
                    SessionUser.SelectedInitialPosture = StandingPosture;
                    selectedUser = null;
                }

                RefreshSelectedLabel();
                RefreshSelectedActions();
                StartCoroutine(LoadUsersFromServer());
            },
            onError: err =>
            {
                Debug.LogError("Error eliminando usuario: " + err);
                SetStatus("Error eliminando usuario: " + err);
            }
        ));
    }

    private void SelectUser(User user)
    {
        SessionUser.SelectedUserId = user.id_usuari;
        SessionUser.SelectedUserName = user.nom;
        SessionUser.SelectedRoomType = GetRoomType(user);
        SessionUser.SelectedInitialPosture = GetPosture(user);
        selectedUser = user;

        if (sessionTracker != null)
            sessionTracker.ApplyInitialSettings(SessionUser.SelectedInitialPosture == SittingPosture, true, true);

        RefreshSelectedLabel();
        RefreshSelectedActions();
    }

    private void ShowUserSessions(User user)
    {
        if (infoPopup != null)
        {
            SetInfoPopupVisible(true);

            UserSessionInfoCanvas popupSessionInfoCanvas = infoPopup.GetComponentInChildren<UserSessionInfoCanvas>(true);
            if (popupSessionInfoCanvas != null)
                sessionInfoCanvas = popupSessionInfoCanvas;
        }

        if (sessionInfoCanvas == null)
        {
            Debug.LogWarning("No hay UserSessionInfoCanvas asignado en UserSelectScreen.");
            SetStatus("Falta asignar el canvas de sesiones.");
            return;
        }

        if (infoPopup != null)
            sessionInfoCanvas.canvasRoot = infoPopup;

        sessionInfoCanvas.Show(user);
    }

    private void RefreshSelectedLabel()
    {
        if (selectedUserText == null) return;

        selectedUserText.text = string.IsNullOrEmpty(SessionUser.SelectedUserName)
            ? "Usuario actual: (ninguno)"
            : $"Usuario actual: {SessionUser.SelectedUserName} | {RoomLabel(SessionUser.SelectedRoomType)} | {PostureLabel(SessionUser.SelectedInitialPosture)}";
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void CancelCreateUser()
    {
        if (createInput != null)
            createInput.text = "";

        SetCreateDefaults();
        SetCreateDialogVisible(false);
        SetStatus("");
    }

    private void ConnectSelectedUser()
    {
        if (selectedUser == null || SessionUser.SelectedUserId <= 0)
        {
            SetStatus("Selecciona un usuario.");
            return;
        }

        if (headsetSearchText != null)
            headsetSearchText.text = "Buscando casco...";

        if (connectionPopup != null)
            SetConnectionPopupVisible(true);
        else if (uiManager != null)
            uiManager.GoToConnectionCanvas();
    }

    private void DeleteSelectedUser()
    {
        if (selectedUser == null)
        {
            SetStatus("Selecciona un usuario.");
            return;
        }

        StartCoroutine(DeleteUserFromServer(selectedUser));
    }

    private void CloseSelectedActions()
    {
        selectedUser = null;
        SessionUser.SelectedUserId = -1;
        SessionUser.SelectedUserName = null;
        SessionUser.SelectedRoomType = RoomWhite;
        SessionUser.SelectedInitialPosture = StandingPosture;

        RefreshSelectedLabel();
        RefreshSelectedActions();
        ClosePopups();
    }

    private void ShowSelectedUserSessions()
    {
        if (selectedUser == null)
        {
            SetStatus("Selecciona un usuario.");
            return;
        }

        ShowUserSessions(selectedUser);
    }

    private void RefreshSelectedActions()
    {
        bool hasSelection = selectedUser != null && SessionUser.SelectedUserId > 0;

        if (selectedActionsPanel != null)
            selectedActionsPanel.SetActive(hasSelection);

        if (connectHeadsetButton != null)
            connectHeadsetButton.interactable = hasSelection;

        if (deleteSelectedButton != null)
            deleteSelectedButton.interactable = hasSelection;

        if (infoSelectedButton != null)
            infoSelectedButton.interactable = hasSelection;

        if (headsetSearchText != null)
            headsetSearchText.text = hasSelection ? "Preparado para buscar casco." : "";
    }

    private void SetCreateDialogVisible(bool visible)
    {
        if (createDialog != null)
            createDialog.SetActive(visible);
    }

    private void SetConnectionPopupVisible(bool visible)
    {
        if (connectionPopup != null)
            connectionPopup.SetActive(visible);
    }

    private void SetInfoPopupVisible(bool visible)
    {
        if (infoPopup != null)
            infoPopup.SetActive(visible);
    }

    private void ClosePopups()
    {
        SetCreateDialogVisible(false);
        SetConnectionPopupVisible(false);

        if (sessionInfoCanvas != null)
            sessionInfoCanvas.Hide();
        else
            SetInfoPopupVisible(false);
    }

    private IEnumerator ClosePopupsAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();

        if (!closePopupsRequested)
            yield break;

        closePopupsRequested = false;
        ClosePopups();
    }

    private bool AnyPopupVisible()
    {
        return IsActivePopup(createDialog)
            || IsActivePopup(connectionPopup)
            || IsActivePopup(infoPopup);
    }

    private bool PointerIsInsideAnyPopup(Vector2 screenPosition)
    {
        return PointerIsInsidePopup(createDialog, createDialogContent, screenPosition)
            || PointerIsInsidePopup(connectionPopup, connectionPopupContent, screenPosition)
            || PointerIsInsidePopup(infoPopup, infoPopupContent, screenPosition);
    }

    private static bool IsActivePopup(GameObject popup)
    {
        return popup != null && popup.activeInHierarchy;
    }

    private static bool PointerIsInsidePopup(GameObject popup, RectTransform content, Vector2 screenPosition)
    {
        if (!IsActivePopup(popup))
            return false;

        RectTransform rectTransform = content != null ? content : popup.GetComponent<RectTransform>();
        return rectTransform != null
            && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition);
    }

    private static bool PointerDownThisFrame(out Vector2 screenPosition)
    {
        screenPosition = default;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began)
                return false;

            screenPosition = touch.position;
            return true;
        }

        if (!Input.GetMouseButtonDown(0))
            return false;

        screenPosition = Input.mousePosition;
        return true;
    }

    private void SetCreateDefaults()
    {
        if (independentToggle != null)
            independentToggle.SetIsOnWithoutNotify(false);

        SelectCreateRoom(RoomWhite);

        if (sittingToggle != null)
            sittingToggle.SetIsOnWithoutNotify(false);

        if (roomTypeDropdown != null)
            roomTypeDropdown.SetValueWithoutNotify(0);

        if (postureDropdown != null)
            postureDropdown.SetValueWithoutNotify(0);

        if (menuHandsToggle != null)
            menuHandsToggle.SetIsOnWithoutNotify(true);

        if (particlesToggle != null)
            particlesToggle.SetIsOnWithoutNotify(true);
    }

    private string GetSelectedCreateRoomType()
    {
        if (roomTypeDropdown != null)
        {
            string selected = NormalizeDropdownText(GetDropdownSelectedText(roomTypeDropdown));

            if (selected.Contains("adult"))
                return RoomAdult;

            if (selected.Contains("infant"))
                return RoomChild;

            return RoomWhite;
        }

        if (whiteRoomToggle == null && adultRoomToggle == null && infantilToggle != null)
            return infantilToggle.isOn ? RoomChild : RoomAdult;

        if (adultRoomToggle != null && adultRoomToggle.isOn)
            return RoomAdult;

        if (infantilToggle != null && infantilToggle.isOn)
            return RoomChild;

        return RoomWhite;
    }

    private string GetSelectedCreatePosture()
    {
        if (postureDropdown != null)
        {
            string selected = NormalizeDropdownText(GetDropdownSelectedText(postureDropdown));
            return selected.Contains("sent") || selected.Contains("assegut") || selected.Contains("asseguda")
                ? SittingPosture
                : StandingPosture;
        }

        return sittingToggle != null && sittingToggle.isOn ? SittingPosture : StandingPosture;
    }

    private void ConfigureCreateDropdowns()
    {
        ConfigureDropdown(roomTypeDropdown, "Blanca", "Adult", "Infantil");
        ConfigureDropdown(postureDropdown, "Dempeus", "Assegut");
    }

    private static void ConfigureDropdown(TMP_Dropdown dropdown, params string[] options)
    {
        if (dropdown == null || dropdown.options.Count > 0)
            return;

        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        dropdown.SetValueWithoutNotify(0);
    }

    private static string GetDropdownSelectedText(TMP_Dropdown dropdown)
    {
        if (dropdown == null || dropdown.options.Count == 0)
            return "";

        int index = Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1);
        return dropdown.options[index].text ?? "";
    }

    private static string NormalizeDropdownText(string text)
    {
        return (text ?? "")
            .Trim()
            .ToLowerInvariant()
            .Replace("à", "a")
            .Replace("á", "a")
            .Replace("è", "e")
            .Replace("é", "e")
            .Replace("í", "i")
            .Replace("ï", "i")
            .Replace("ò", "o")
            .Replace("ó", "o")
            .Replace("ú", "u")
            .Replace("ü", "u");
    }

    private void WireRoomToggle(Toggle toggle, string roomType)
    {
        if (toggle == null)
            return;

        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(isOn =>
        {
            if (isOn)
                SelectCreateRoom(roomType);
        });
    }

    private void SelectCreateRoom(string roomType)
    {
        if (whiteRoomToggle != null)
            whiteRoomToggle.SetIsOnWithoutNotify(roomType == RoomWhite);

        if (adultRoomToggle != null)
            adultRoomToggle.SetIsOnWithoutNotify(roomType == RoomAdult);

        if (infantilToggle != null)
            infantilToggle.SetIsOnWithoutNotify(roomType == RoomChild);
    }

    private static string GetRoomType(User user)
    {
        if (user.tipus_sala == RoomWhite || user.tipus_sala == RoomAdult || user.tipus_sala == RoomChild)
            return user.tipus_sala;

        return user.entorn_adult ? RoomAdult : RoomChild;
    }

    private static string GetPosture(User user)
    {
        return user.postura_inicial == SittingPosture ? SittingPosture : StandingPosture;
    }

    private static string RoomLabel(string roomType)
    {
        if (roomType == RoomAdult)
            return "Adulto";

        if (roomType == RoomChild)
            return "Infantil";

        return "Blanca";
    }

    private static string PostureLabel(string posture)
    {
        return posture == SittingPosture ? "Sentado" : "De pie";
    }
}
