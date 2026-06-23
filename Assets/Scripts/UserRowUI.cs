using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserRowUI : MonoBehaviour
{
    private const string RoomWhite = "blanca";
    private const string RoomAdult = "adult";
    private const string RoomChild = "infantil";
    private const string StandingPosture = "DE_PIE";
    private const string SittingPosture = "SENTADO";

    [Header("Textos")]
    public TMP_Text nameText;
    public TMP_InputField nameInput;
    public TMP_Text environmentText;
    public TMP_Text postureText;

    [Header("Checks")]
    public Toggle independentToggle;

    [Tooltip("Marcado = Infant. Desmarcado = Adult.")]
    public Toggle infantilToggle;
    public Toggle whiteRoomToggle;
    public Toggle adultRoomToggle;
    public Toggle sittingToggle;

    public Toggle menuHandsToggle;
    public Toggle particlesToggle;

    [Header("Botones")]
    public Button selectButton;
    public Button saveButton;
    public Button deleteButton;
    public Button infoButton;

    private User currentUser;

    public void Bind(User user, Action<User> onSelect, Action<User> onSave, Action<User> onDelete, Action<User> onInfo = null)
    {
        currentUser = user;

        if (nameText != null)
            nameText.text = user.nom;

        if (nameInput != null)
            nameInput.text = user.nom;

        if (environmentText != null)
            environmentText.text = RoomLabel(GetRoomType(user));

        if (postureText != null)
            postureText.text = IsSitting(user) ? "Assegut" : "Dret";

        if (independentToggle != null)
        {
            independentToggle.onValueChanged.RemoveAllListeners();
            independentToggle.SetIsOnWithoutNotify(user.independent);
            independentToggle.onValueChanged.AddListener(_ => onSave?.Invoke(GetEditedUser()));
        }

        if (infantilToggle != null)
            infantilToggle.SetIsOnWithoutNotify(GetRoomType(user) == RoomChild);

        if (whiteRoomToggle != null)
            whiteRoomToggle.SetIsOnWithoutNotify(GetRoomType(user) == RoomWhite);

        if (adultRoomToggle != null)
            adultRoomToggle.SetIsOnWithoutNotify(GetRoomType(user) == RoomAdult);

        if (sittingToggle != null)
            sittingToggle.SetIsOnWithoutNotify(IsSitting(user));

        if (menuHandsToggle != null)
            menuHandsToggle.SetIsOnWithoutNotify(true);

        if (particlesToggle != null)
            particlesToggle.SetIsOnWithoutNotify(true);

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

        if (infoButton != null)
        {
            infoButton.onClick.RemoveAllListeners();
            infoButton.onClick.AddListener(() => onInfo?.Invoke(currentUser));
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

            tipus_sala = GetEditedRoomType(),
            postura_inicial = sittingToggle != null && sittingToggle.isOn ? SittingPosture : GetPosture(currentUser),
            entorn_adult = GetEditedRoomType() == RoomAdult,

            menu_mans_actiu = true,

            particules_mans_actives = true,

            actiu = currentUser.actiu
        };
    }

    private string GetEditedRoomType()
    {
        if (whiteRoomToggle != null && whiteRoomToggle.isOn)
            return RoomWhite;

        if (adultRoomToggle != null && adultRoomToggle.isOn)
            return RoomAdult;

        if (infantilToggle != null && infantilToggle.isOn)
            return RoomChild;

        return GetRoomType(currentUser);
    }

    private static string GetRoomType(User user)
    {
        if (user == null)
            return RoomWhite;

        if (user.tipus_sala == RoomWhite || user.tipus_sala == RoomAdult || user.tipus_sala == RoomChild)
            return user.tipus_sala;

        return user.entorn_adult ? RoomAdult : RoomChild;
    }

    private static string GetPosture(User user)
    {
        return user != null && user.postura_inicial == SittingPosture ? SittingPosture : StandingPosture;
    }

    private static bool IsSitting(User user)
    {
        return GetPosture(user) == SittingPosture;
    }

    private static string RoomLabel(string roomType)
    {
        if (roomType == RoomAdult)
            return "Adult";

        if (roomType == RoomChild)
            return "Infantil";

        return "Blanca";
    }
}
