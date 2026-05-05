using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserRowUI : MonoBehaviour
{
    [Header("Textos")]
    public TMP_Text nameText;
    public TMP_InputField nameInput;
    public TMP_Text environmentText;

    [Header("Checks")]
    public Toggle independentToggle;

    [Tooltip("Marcado = Infant. Desmarcado = Adult.")]
    public Toggle infantilToggle;

    public Toggle menuHandsToggle;
    public Toggle particlesToggle;

    [Header("Botones")]
    public Button selectButton;
    public Button saveButton;
    public Button deleteButton;

    private User currentUser;

    public void Bind(User user, Action<User> onSelect, Action<User> onSave, Action<User> onDelete)
    {
        currentUser = user;

        if (nameText != null)
            nameText.text = user.nom;

        if (nameInput != null)
            nameInput.text = user.nom;

        if (environmentText != null)
            environmentText.text = user.entorn_adult ? "Adult" : "Infant";

        if (independentToggle != null)
            independentToggle.isOn = user.independent;

        if (infantilToggle != null)
            infantilToggle.isOn = !user.entorn_adult;

        if (menuHandsToggle != null)
            menuHandsToggle.isOn = user.menu_mans_actiu;

        if (particlesToggle != null)
            particlesToggle.isOn = user.particules_mans_actives;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect?.Invoke(currentUser));
        }

        if (saveButton != null)
        {
            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(() => onSave?.Invoke(GetEditedUser()));
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => onDelete?.Invoke(currentUser));
        }
    }

    private User GetEditedUser()
    {
        string editedName = currentUser.nom;

        if (nameInput != null)
        {
            string inputName = nameInput.text.Trim();
            if (!string.IsNullOrEmpty(inputName))
                editedName = inputName;
        }

        return new User
        {
            id_usuari = currentUser.id_usuari,
            nom = editedName,

            independent = independentToggle != null
                ? independentToggle.isOn
                : currentUser.independent,

            entorn_adult = infantilToggle != null
                ? !infantilToggle.isOn
                : currentUser.entorn_adult,

            menu_mans_actiu = menuHandsToggle != null
                ? menuHandsToggle.isOn
                : currentUser.menu_mans_actiu,

            particules_mans_actives = particlesToggle != null
                ? particlesToggle.isOn
                : currentUser.particules_mans_actives,

            actiu = currentUser.actiu
        };
    }
}