using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Wajib ditambahkan untuk mengakses TextMeshPro

public class GameManagerBos3 : MonoBehaviour
{
    [Header("Referensi UI")]
    public Transform cardGrid;
    public GameObject cardPrefab;
    public CardSlot[] craftSlots; 
    public Button btnBuatRamuan;

    [Header("UI Teks Informasi")]
    public TextMeshProUGUI txtRonde;   // Referensi untuk teks Ronde
    public TextMeshProUGUI txtTarget;  // Referensi untuk teks Target Ramuan

    [Header("UI Nyawa (Hearts)")]
    public GameObject[] playerHearts; 
    public GameObject[] bossHearts;   

    [Header("Data Master Bahan")]
    public CardData dataDaun;
    public CardData dataAkar;
    public CardData dataGingseng;
    public CardData dataMawarBiru;
    public CardData dataLilyPutih;

    [Header("Status Permainan")]
    public int currentRound = 1;
    public int playerHP = 3;
    public int bossHP = 3;

    private void Start()
    {
        btnBuatRamuan.onClick.AddListener(CheckPotion);
        UpdateHealthUI(); 
        StartRound();
    }

    private void Update()
    {
        bool isSemuaSlotTerisi = (craftSlots[0].transform.childCount > 0 && 
                                  craftSlots[1].transform.childCount > 0 && 
                                  craftSlots[2].transform.childCount > 0);

        btnBuatRamuan.gameObject.SetActive(isSemuaSlotTerisi);
    }

    public void StartRound()
    {
        ClearTable();
        GenerateDeck();
        UpdateInformasiUI(); // Panggil fungsi perbarui teks setiap ronde dimulai
    }

    // Fungsi baru untuk mengatur teks Ronde dan Target
    private void UpdateInformasiUI()
    {
        // 1. Logika Teks Ronde
        if (currentRound == 1) 
            txtRonde.text = "Ronde 1";
        else if (currentRound == 2) 
            txtRonde.text = "Ronde 2";
        else if (currentRound == 3) 
            txtRonde.text = "Last Round!"; // Sesuai permintaan, ronde 3 menjadi Last Round
        else 
            txtRonde.text = "Selesai";

        // 2. Logika Teks Target Ramuan
        if (currentRound == 1) 
            txtTarget.text = "Target: Chronix\n(Butuh: Mawar Biru)";
        else if (currentRound == 2) 
            txtTarget.text = "Target: Aethel\n(Butuh: Lily Putih)";
        else if (currentRound == 3) 
            txtTarget.text = "Target: Panacea\n(Butuh: Gingseng)";
    }

    private void ClearTable()
    {
        foreach (Transform child in cardGrid) Destroy(child.gameObject);
        foreach (CardSlot slot in craftSlots)
        {
            if (slot.transform.childCount > 0)
                Destroy(slot.transform.GetChild(0).gameObject);
        }
    }

    private void GenerateDeck()
    {
        List<CardData> deck = new List<CardData>();

        int valDaun = Random.Range(3, 9);
        int minAkar = Mathf.Max(1, 12 - valDaun);
        int maxAkar = Mathf.Min(9, 20 - valDaun);
        int valAkar = Random.Range(minAkar, maxAkar + 1);
        int valBahan3 = 21 - (valDaun + valAkar);

        deck.Add(CreateDynamicCard(dataDaun, valDaun));
        deck.Add(CreateDynamicCard(dataAkar, valAkar));

        CardData targetBahan = dataMawarBiru; 
        if (currentRound == 2) targetBahan = dataLilyPutih; 
        if (currentRound == 3) targetBahan = dataGingseng; 

        deck.Add(CreateDynamicCard(targetBahan, valBahan3));

        CardData[] allBahan = { dataDaun, dataAkar, dataGingseng, dataMawarBiru, dataLilyPutih };
        for (int i = 0; i < 9; i++)
        {
            CardData randomBahan = allBahan[Random.Range(0, allBahan.Length)];
            int randomVal = Random.Range(1, 10);
            deck.Add(CreateDynamicCard(randomBahan, randomVal));
        }

        for (int i = 0; i < deck.Count; i++)
        {
            CardData temp = deck[i];
            int randomIndex = Random.Range(i, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }

        foreach (CardData cardData in deck)
        {
            GameObject newCard = Instantiate(cardPrefab, cardGrid);
            newCard.GetComponent<CardDisplay>().SetupCard(cardData);
        }
    }

    private CardData CreateDynamicCard(CardData baseData, int value)
    {
        CardData newCard = ScriptableObject.CreateInstance<CardData>();
        newCard.ingredientType = baseData.ingredientType;
        newCard.cardGraphic = baseData.cardGraphic;
        newCard.cardValue = value;
        return newCard;
    }

    public void CheckPotion()
    {
        CardDisplay card1 = craftSlots[0].transform.GetChild(0).GetComponent<CardDisplay>();
        CardDisplay card2 = craftSlots[1].transform.GetChild(0).GetComponent<CardDisplay>();
        CardDisplay card3 = craftSlots[2].transform.GetChild(0).GetComponent<CardDisplay>();

        IngredientType targetIngredient = IngredientType.MawarBiru;
        if (currentRound == 2) targetIngredient = IngredientType.LilyPutih;
        if (currentRound == 3) targetIngredient = IngredientType.Gingseng;

        bool isOrderCorrect = (card1.currentIngredient == IngredientType.Daun) &&
                              (card2.currentIngredient == IngredientType.Akar) &&
                              (card3.currentIngredient == targetIngredient);

        int totalValue = card1.currentCardValue + card2.currentCardValue + card3.currentCardValue;
        bool isTotal21 = (totalValue == 21);

        if (isOrderCorrect && isTotal21)
        {
            Debug.Log("SUKSES! Ramuan dibuat.");
            bossHP--;
            UpdateHealthUI(); 
            
            if (bossHP <= 0) {
                Debug.Log("MENANG!");
                txtRonde.text = "BOSS DIKALAHKAN!";
            } else {
                currentRound++;
                StartRound();
            }
        }
        else
        {
            Debug.Log("GAGAL!");
            playerHP--;
            UpdateHealthUI(); 

            if (playerHP <= 0) {
                Debug.Log("GAME OVER!");
                txtRonde.text = "MALIOS GUGUR...";
            } else {
                StartRound();
            }
        }
    }

    private void UpdateHealthUI()
    {
        for (int i = 0; i < playerHearts.Length; i++)
        {
            playerHearts[i].SetActive(i < playerHP);
        }

        for (int i = 0; i < bossHearts.Length; i++)
        {
            bossHearts[i].SetActive(i < bossHP);
        }
    }
}