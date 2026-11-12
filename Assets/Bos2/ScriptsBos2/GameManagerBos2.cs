using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManagerBos2 : MonoBehaviour
{
    public static GameManagerBos2 Instance;

    [Header("Status Permainan")]
    public int playerHP = 3;
    public int bossHP = 3;
    public int currentRound = 1;
    public int mistakesAllowed = 1;

    [Header("Referensi UI & Prefab")]
    public GameObject[] playerHearts;
    public GameObject[] bossHearts;
    public TextMeshProUGUI mistakesText;
    public TextMeshProUGUI roundText;
    public GridLayoutGroup boardLayout; // Referensi untuk mengubah kolom kartu
    public GameObject cardPrefab;

    [Header("Variabel Mekanik (Jangan diisi manual)")]
    public bool isChecking = false;
    private CardController firstCard;
    private CardController secondCard;
    private int pairsFound = 0;
    private int pairsNeeded = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        StartRound(currentRound);
    }

    void StartRound(int round)
    {
        pairsFound = 0;
        currentRound = round;

        // Mengatur aturan pasangan, batas kesalahan, dan JUMLAH KOLOM GRID
        boardLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

        switch(round)
        {
            case 1: 
                pairsNeeded = 3; 
                mistakesAllowed = 1; 
                boardLayout.constraintCount = 3; // 3 Atas, 3 Bawah
                break;
            case 2: 
                pairsNeeded = 4; 
                mistakesAllowed = 2; 
                boardLayout.constraintCount = 4; // 4 Atas, 4 Bawah
                break;
            case 3: 
                pairsNeeded = 5; 
                mistakesAllowed = 3; 
                boardLayout.constraintCount = 5; // 5 Atas, 5 Bawah
                break;
            default: 
                Debug.Log("Game Selesai! Pemain Menang Total!"); 
                return;
        }

        UpdateUI();
        GenerateCards();
    }

    void GenerateCards()
    {
        // Bersihkan area kartu sebelumnya
        foreach (Transform child in boardLayout.transform)
        {
            Destroy(child.gameObject);
        }

        List<int> cardValues = new List<int>();

        for (int i = 0; i < pairsNeeded; i++)
        {
            int num1 = Random.Range(2, 12);
            int num2 = 13 - num1;
            cardValues.Add(num1);
            cardValues.Add(num2);
        }

        for (int i = 0; i < cardValues.Count; i++)
        {
            int temp = cardValues[i];
            int randomIndex = Random.Range(i, cardValues.Count);
            cardValues[i] = cardValues[randomIndex];
            cardValues[randomIndex] = temp;
        }

        foreach (int value in cardValues)
        {
            GameObject newCard = Instantiate(cardPrefab, boardLayout.transform);
            newCard.GetComponent<CardController>().SetupCard(value);
        }
    }

    public void OnCardSelected(CardController selectedCard)
    {
        if (firstCard == null)
        {
            firstCard = selectedCard;
        }
        else if (secondCard == null)
        {
            secondCard = selectedCard;
            StartCoroutine(CheckMatchLogic());
        }
    }

    IEnumerator CheckMatchLogic()
    {
        isChecking = true;
        yield return new WaitForSeconds(1f); // Jeda sebelum mengevaluasi

        if (firstCard.cardValue + secondCard.cardValue == 13)
        {
            firstCard.isMatched = true;
            secondCard.isMatched = true;
            pairsFound++;

            if (pairsFound >= pairsNeeded)
            {
                RoundComplete();
            }
        }
        else
        {
            firstCard.CloseCard(); // Animasi menutup akan dipicu di sini
            secondCard.CloseCard();

            if (mistakesAllowed > 0)
            {
                mistakesAllowed--;
            }
            else
            {
                playerHP--;
                if (playerHP <= 0)
                {
                    GameOver();
                }
            }
            UpdateUI();
        }

        // Tambahkan jeda ekstra 0.4 detik agar pemain tidak bisa mengklik kartu lain 
        // selama kartu yang salah sedang melakukan animasi menutup (fade memakan waktu 0.3 detik).
        yield return new WaitForSeconds(0.4f);

        firstCard = null;
        secondCard = null;
        isChecking = false;
    }

    void RoundComplete()
    {
        bossHP--;
        if (bossHP <= 0)
        {
            Debug.Log("Bos Glaciera Kalah!"); 
        }
        else
        {
            StartRound(currentRound + 1); 
        }
        UpdateUI();
    }

    void GameOver()
    {
        Debug.Log("Game Over! Nyawa Pemain Habis."); 
    }

    void UpdateUI()
    {
        for (int i = 0; i < playerHearts.Length; i++)
        {
            playerHearts[i].SetActive(i < playerHP);
        }

        for (int i = 0; i < bossHearts.Length; i++)
        {
            bossHearts[i].SetActive(i < bossHP);
        }

        mistakesText.text = mistakesAllowed.ToString();

        switch (currentRound)
        {
            case 1: roundText.text = "Round 1"; break;
            case 2: roundText.text = "Round 2"; break;
            case 3: roundText.text = "Last Round"; break;
        }
    }
}