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
        Debug.Log("Intentando cargar usuarios...");

        yield return StartCoroutine(UserAPI.GetUsers(
            onSuccess: loadedUsers =>
            {
                Debug.Log("Usuarios recibidos: " + loadedUsers.Length);
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
        Debug.Log("RefreshList users = " + users.Count);

        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        foreach (var user in users)
        {
            Debug.Log("Creando fila para " + user.name);
            GameObject row = Instantiate(userRowPrefab, listContent, false);
            UserRowUI rowUI = row.GetComponent<UserRowUI>();

            if (rowUI == null)
            {
                Debug.LogError("UserRowUI no encontrado en prefab");
                continue;
            }

            rowUI.Bind(
                user.name,
                () => SelectUser(user),
                () => StartCoroutine(DeleteUserFromServer(user))
            );
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)listContent);
        Debug.Log("childCount final = " + listContent.childCount);
    }
    private void CreateUserFromInput()
    {
        string name = createInput != null ? createInput.text.Trim() : "";

        if (string.IsNullOrEmpty(name))
        {
            SetStatus("Introduce un nombre.");
            return;
        }

        StartCoroutine(UserAPI.CreateUser(
            name,
            onSuccess: () =>
            {
                if (createInput != null)
                    createInput.text = "";

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
            user.id,
            onSuccess: () =>
            {
                if (SessionUser.SelectedUserId == user.id)
                {
                    SessionUser.SelectedUserId = null;
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
        SessionUser.SelectedUserId = user.id;
        SessionUser.SelectedUserName = user.name;

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