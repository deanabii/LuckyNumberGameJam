using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Card : MonoBehaviour
{
    [HideInInspector] public int cardValue;
    
    [Header("Referensi UI")]
    public TextMeshProUGUI valueText;
    public Button cardButton;

    private GameManager gameManager;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        cardButton.onClick.AddListener(OnCardClicked);
    }

    public void SetupCard(int value)
    {
        cardValue = value;
        valueText.text = value.ToString();
        cardButton.interactable = true; 
    }

    void OnCardClicked()
    {
        gameManager.SelectCard(this);
    }
}