using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserRowUI : MonoBehaviour
{
    public TMP_Text nameText;
    public Button selectButton;
    public Button deleteButton;

    public void Bind(string userName, Action onSelect, Action onDelete)
    {
        nameText.text = userName;

        selectButton.onClick.RemoveAllListeners();
        deleteButton.onClick.RemoveAllListeners();

        selectButton.onClick.AddListener(() => onSelect?.Invoke());
        deleteButton.onClick.AddListener(() => onDelete?.Invoke());
    }
}