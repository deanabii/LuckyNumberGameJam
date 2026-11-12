using UnityEngine;
using UnityEngine.UI; // Wajib untuk mengakses komponen Image
using TMPro; // Wajib untuk mengakses TextMeshPro

public class CardDisplay : MonoBehaviour
{
    [Header("Komponen UI")]
    public Image cardImage;
    public TextMeshProUGUI valueText;

    [Header("Data Saat Ini")]
    public int currentCardValue;
    public IngredientType currentIngredient;

    // Fungsi ini akan dipanggil oleh GameController nanti saat membagikan kartu
    public void SetupCard(CardData newData)
    {
        // 1. Simpan data agar kartu tahu identitasnya sendiri
        currentCardValue = newData.cardValue;
        currentIngredient = newData.ingredientType;

        // 2. Ubah visual UI sesuai data
        cardImage.sprite = newData.cardGraphic;
        valueText.text = currentCardValue.ToString();
    }
}