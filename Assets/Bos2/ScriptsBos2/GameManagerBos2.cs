using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; // Ditambahkan untuk fungsi Restart & Exit

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
    public GridLayoutGroup boardLayout; 
    public GameObject cardPrefab;

    [Header("Panels & Transitions")]
    public GameObject winPanel1;
    public GameObject winPanel2;
    public GameObject losePanel;    // Referensi untuk Lose Panel
    public Image fadePanel;         // Referensi untuk Image hitam pekat

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
        // Pastikan semua panel kemenangan/kekalahan mati di awal game
        if (winPanel1 != null) winPanel1.SetActive(false);
        if (winPanel2 != null) winPanel2.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        
        // Pastikan layar terang/tembus pandang di awal game
        if (fadePanel != null)
        {
            fadePanel.color = new Color(0, 0, 0, 0);
            fadePanel.raycastTarget = false;
        }

        StartRound(currentRound);
    }

    void StartRound(int round)
    {
        pairsFound = 0;
        currentRound = round;

        boardLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

        switch(round)
        {
            case 1: 
                pairsNeeded = 3; 
                mistakesAllowed = 1; 
                boardLayout.constraintCount = 3; 
                break;
            case 2: 
                pairsNeeded = 4; 
                mistakesAllowed = 2; 
                boardLayout.constraintCount = 4; 
                break;
            case 3: 
                pairsNeeded = 5; 
                mistakesAllowed = 3; 
                boardLayout.constraintCount = 5; 
                break;
            default: 
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
            int num2 = 13 - num1; // Typo telah diperbaiki (13 - num1)
            cardValues.Add(num1);
            cardValues.Add(num2);
        }

        // Algoritma pengacakan kartu (Fisher-Yates Shuffle)
        for (int i = 0; i < cardValues.Count; i++)
        {
            int temp = cardValues[i];
            int randomIndex = Random.Range(i, cardValues.Count);
            cardValues[i] = cardValues[randomIndex];
            cardValues[randomIndex] = temp;
        }

        // Spawn kartu
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
            firstCard.CloseCard(); 
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

        yield return new WaitForSeconds(0.4f);

        firstCard = null;
        secondCard = null;
        
        // Buka kunci hanya jika game belum over atau belum menang
        if (playerHP > 0 && bossHP > 0) 
        {
            isChecking = false; 
        }
    }

    void RoundComplete()
    {
        bossHP--;
        if (bossHP <= 0)
        {
            StartCoroutine(ShowWinSequenceRoutine());
        }
        else
        {
            StartRound(currentRound + 1); 
        }
        UpdateUI();
    }

    // --- LOGIKA TRANSISI FADE & PANELS ---

    IEnumerator ShowWinSequenceRoutine()
    {
        isChecking = true; // Kunci interaksi kartu
        
        yield return StartCoroutine(FadeScreen(1f, 1f)); // Fade out ke hitam
        if (winPanel1 != null) winPanel1.SetActive(true); // Tampilkan Panel 1
        yield return StartCoroutine(FadeScreen(0f, 1f)); // Fade in ke terang
    }

    public void GoToNextWinPanel()
    {
        StartCoroutine(SwitchWinPanelRoutine());
    }

    IEnumerator SwitchWinPanelRoutine()
    {
        isChecking = true;
        yield return StartCoroutine(FadeScreen(1f, 1f));

        if (winPanel1 != null) winPanel1.SetActive(false);
        if (winPanel2 != null) winPanel2.SetActive(true);

        yield return StartCoroutine(FadeScreen(0f, 1f));
    }

    IEnumerator FadeScreen(float targetAlpha, float duration)
    {
        if (fadePanel == null) yield break; // Keamanan jika fadePanel belum diisi

        fadePanel.raycastTarget = true; 
        Color startColor = fadePanel.color;
        Color endColor = new Color(0, 0, 0, targetAlpha);
        
        float timer = 0;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            fadePanel.color = Color.Lerp(startColor, endColor, timer / duration);
            yield return null;
        }
        fadePanel.color = endColor;
        
        fadePanel.raycastTarget = (targetAlpha > 0); 
    }

    void GameOver()
    {
        isChecking = true; // Kunci interaksi kartu
        if (losePanel != null) losePanel.SetActive(true);
    }

    // --- LOGIKA TOMBOL RESTART & EXIT ---

    public void RestartGame() 
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ExitGame() 
    {
        // Sesuaikan dengan nama scene menu utama Anda, misalnya "MainMenu"
        SceneManager.LoadScene("MainMenuScene"); 
    }

    public void GoToNextScene() 
    {
        // Ganti "NamaSceneSelanjutnya" dengan nama scene Boss 3 Anda nanti jika sudah dibuat
        SceneManager.LoadScene("NamaSceneSelanjutnya"); 
    }

    // --- UPDATE UI ---

    void UpdateUI()
    {
        for (int i = 0; i < playerHearts.Length; i++)
        {
            if (playerHearts[i] != null)
                playerHearts[i].SetActive(i < playerHP);
        }

        for (int i = 0; i < bossHearts.Length; i++)
        {
            if (bossHearts[i] != null)
                bossHearts[i].SetActive(i < bossHP);
        }

        if (mistakesText != null) mistakesText.text = mistakesAllowed.ToString();

        if (roundText != null)
        {
            switch (currentRound)
            {
                case 1: roundText.text = "Round 1"; break;
                case 2: roundText.text = "Round 2"; break;
                case 3: roundText.text = "Last Round"; break;
            }
        }
    }
}