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
    public TMP_InputField createInput;
    public TMP_InputField ageInput;
    public Toggle independentToggle;
    public Button createButton;
    public TMP_Text selectedUserText;
    public TMP_Text statusText;
    public UIManager uiManager;

    private readonly List<User> users = new();

    private void OnEnable()
    {
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

        foreach (var user in users)
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
                () => SelectUser(user),
                () => StartCoroutine(DeleteUserFromServer(user))
            );
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)listContent);
    }

    private void CreateUserFromInput()
    {
        string nom = createInput != null ? createInput.text.Trim() : "";
        string ageString = ageInput != null ? ageInput.text.Trim() : "";

        if (string.IsNullOrEmpty(nom))
        {
            SetStatus("Introduce un nombre.");
            return;
        }

        if (string.IsNullOrEmpty(ageString) || !int.TryParse(ageString, out int edat))
        {
            SetStatus("Introduce una edad válida.");
            return;
        }

        if (edat < 0 || edat > 120)
        {
            SetStatus("La edad no es válida.");
            return;
        }

        bool independent = independentToggle != null && independentToggle.isOn;

        StartCoroutine(UserAPI.CreateUser(
            nom,
            edat,
            independent,
            onSuccess: () =>
            {
                if (createInput != null) createInput.text = "";
                if (ageInput != null) ageInput.text = "";
                if (independentToggle != null) independentToggle.isOn = true;

                StartCoroutine(LoadUsersFromServer());
            },
            onError: err =>
            {
                SetStatus("Error creando usuario: " + err);
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
                SetStatus("Error eliminando usuario: " + err);
            }
        ));
    }

    private void SelectUser(User user)
    {
        SessionUser.SelectedUserId = user.id_usuari;
        SessionUser.SelectedUserName = user.nom;

        RefreshSelectedLabel();
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