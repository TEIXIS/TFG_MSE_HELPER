using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserSelectScreen : MonoBehaviour
{
    [Header("UI")]
    public Transform listContent;
    public GameObject userRowPrefab;

    [Header("Crear usuario")]
    public TMP_InputField createInput;
    public Toggle independentToggle;

    [Tooltip("Marcado = Infant. Desmarcado = Adult.")]
    public Toggle infantilToggle;

    public Toggle menuHandsToggle;
    public Toggle particlesToggle;
    public Button createButton;

    [Header("Estado")]
    public TMP_Text selectedUserText;
    public TMP_Text statusText;
    public UIManager uiManager;
    public SessionTracker sessionTracker;

    private readonly List<User> users = new();

    private void OnEnable()
    {
        if (sessionTracker == null)
            sessionTracker = SessionTracker.GetOrCreate();

        if (createButton != null)
        {
            createButton.onClick.RemoveAllListeners();
            createButton.onClick.AddListener(CreateUserFromInput);
        }

        RefreshSelectedLabel();
        StartCoroutine(LoadUsersFromServer());
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
                onDelete: userToDelete => StartCoroutine(DeleteUserFromServer(userToDelete))
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

        bool independent = independentToggle != null && independentToggle.isOn;

        // En la UI usamos "infantil".
        // En la BD guardamos lo contrario: entorn_adult.
        bool esInfantil = infantilToggle != null && infantilToggle.isOn;
        bool entornAdult = !esInfantil;

        bool menuMansActiu = menuHandsToggle == null || menuHandsToggle.isOn;
        bool particulesActives = particlesToggle == null || particlesToggle.isOn;

        SetStatus("Creando usuario...");

        StartCoroutine(UserAPI.CreateUser(
            nom,
            entornAdult,
            independent,
            menuMansActiu,
            particulesActives,
            onSuccess: () =>
            {
                if (createInput != null)
                    createInput.text = "";

                if (independentToggle != null)
                    independentToggle.isOn = false;

                if (infantilToggle != null)
                    infantilToggle.isOn = false;

                if (menuHandsToggle != null)
                    menuHandsToggle.isOn = true;

                if (particlesToggle != null)
                    particlesToggle.isOn = true;

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
                }

                RefreshSelectedLabel();
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

        RefreshSelectedLabel();
        if (uiManager != null)
            uiManager.GoToConnectionCanvas();
    }

    private void RefreshSelectedLabel()
    {
        if (selectedUserText == null) return;

        selectedUserText.text = string.IsNullOrEmpty(SessionUser.SelectedUserName)
            ? "Usuario actual: (ninguno)"
            : $"Usuario actual: {SessionUser.SelectedUserName}";
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
