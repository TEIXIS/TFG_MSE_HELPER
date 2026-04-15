using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserRowUI : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text ageText;
    public Toggle independentToggle;
    public Button selectButton;
    public Button deleteButton;

    public void Bind(User user, Action onSelect, Action onDelete)
    {
        if (nameText != null)
            nameText.text = user.nom;

        if (ageText != null)
            ageText.text = user.edat.ToString();

        if (independentToggle != null)
        {
            independentToggle.isOn = user.independent;
            independentToggle.interactable = false;
        }

        selectButton.onClick.RemoveAllListeners();
        deleteButton.onClick.RemoveAllListeners();

        selectButton.onClick.AddListener(() => onSelect?.Invoke());
        deleteButton.onClick.AddListener(() => onDelete?.Invoke());
    }
}