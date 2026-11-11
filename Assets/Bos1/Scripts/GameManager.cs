using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI; 
using UnityEngine.SceneManagement; 

public class GameManager : MonoBehaviour
{
    [Header("Status Entitas")]
    public int playerHP = 3;
    public int bossHP = 3; 
    
    [Header("Status Ronde")]
    public int currentRound = 1;
    public int successfulGuesses = 0;
    private int[] roundTargets = { 9, 11, 13 }; 
    
    [Header("Referensi UI Nyawa Visual")]
    public GameObject[] playerHearts; // Menampung 3 ikon hati pemain
    public GameObject[] bossHearts;   // Menampung 3 ikon hati bos
    
    [Header("Referensi UI Teks")]
    public TextMeshProUGUI statusText; 
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI targetText;
    public TextMeshProUGUI successText;
    
    [Header("Referensi UI Layar Akhir")]
    public GameObject winPanel;
    public GameObject losePanel;
    
    [Header("Referensi Animasi & Layout")]
    public List<Card> cards; 
    public HorizontalLayoutGroup cardLayoutGroup; 
    public Transform centerPoint; 
    
    private List<Card> selectedCards = new List<Card>();
    private Dictionary<Card, Vector3> cardInitialPositions = new Dictionary<Card, Vector3>();
    private Dictionary<Card, Vector3> cardTargetPositions = new Dictionary<Card, Vector3>();

    void Start()
    {
        winPanel.SetActive(false);
        losePanel.SetActive(false);
        
        UpdateUI();
        StartNewTurn();
    }

    void StartNewTurn()
    {
        selectedCards.Clear();
        
        if (playerHP <= 0 || bossHP <= 0) return;

        cardLayoutGroup.enabled = true;

        int currentTarget = roundTargets[currentRound - 1];
        
        roundText.text = "Ronde " + currentRound;
        targetText.text = currentTarget.ToString();
        successText.text = successfulGuesses + "/3";
        statusText.text = "Pilih dua kartu"; 
        
        GenerateCards(currentTarget);
        BukaSemuaKartu(); 
        
        StartCoroutine(SetupCardPositions());
    }

    IEnumerator SetupCardPositions()
    {
        yield return new WaitForEndOfFrame();

        cardInitialPositions.Clear();
        cardTargetPositions.Clear();

        foreach (Card card in cards)
        {
            cardInitialPositions[card] = card.transform.position;
            cardTargetPositions[card] = card.transform.position;
        }

        cardLayoutGroup.enabled = false;
    }

    void Update()
    {
        foreach (Card card in cards)
        {
            if (cardTargetPositions.ContainsKey(card))
            {
                card.transform.position = Vector3.Lerp(card.transform.position, cardTargetPositions[card], Time.deltaTime * 10f);
            }
        }
    }

    void GenerateCards(int target)
    {
        List<int> newCardValues = new List<int>();
        List<int[]> possiblePairs = new List<int[]>();

        if (target == 9)
        {
            possiblePairs.Add(new int[] {1, 8});
            possiblePairs.Add(new int[] {2, 7});
            possiblePairs.Add(new int[] {3, 6});
            possiblePairs.Add(new int[] {4, 5});
        }
        else if (target == 11)
        {
            possiblePairs.Add(new int[] {2, 9});
            possiblePairs.Add(new int[] {3, 8});
            possiblePairs.Add(new int[] {4, 7});
            possiblePairs.Add(new int[] {5, 6});
        }
        else if (target == 13)
        {
            possiblePairs.Add(new int[] {4, 9});
            possiblePairs.Add(new int[] {5, 8});
            possiblePairs.Add(new int[] {6, 7});
        }
        
        int randomPairIndex = Random.Range(0, possiblePairs.Count);
        newCardValues.Add(possiblePairs[randomPairIndex][0]);
        newCardValues.Add(possiblePairs[randomPairIndex][1]);

        for (int i = 0; i < 3; i++)
        {
            newCardValues.Add(Random.Range(1, 10));
        }

        for (int i = 0; i < newCardValues.Count; i++)
        {
            int temp = newCardValues[i];
            int randomIndex = Random.Range(i, newCardValues.Count);
            newCardValues[i] = newCardValues[randomIndex];
            newCardValues[randomIndex] = temp;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].SetupCard(newCardValues[i]);
        }
    }

    public void SelectCard(Card selectedCard)
    {
        if (selectedCards.Contains(selectedCard))
        {
            selectedCards.Remove(selectedCard);
            cardTargetPositions[selectedCard] = cardInitialPositions[selectedCard];
            
            if (selectedCards.Count == 1)
            {
                cardTargetPositions[selectedCards[0]] = centerPoint.position + new Vector3(-80f, 0, 0);
            }
            return;
        }

        if (selectedCards.Count < 2)
        {
            selectedCards.Add(selectedCard);

            if (selectedCards.Count == 1)
            {
                cardTargetPositions[selectedCard] = centerPoint.position + new Vector3(-80f, 0, 0);
            }
            else if (selectedCards.Count == 2)
            {
                cardTargetPositions[selectedCard] = centerPoint.position + new Vector3(80f, 0, 0);
                
                KunciSemuaKartu(); 
                StartCoroutine(ValidateSelection());
            }
        }
    }

    IEnumerator ValidateSelection()
    {
        yield return new WaitForSeconds(1f); 

        int currentTarget = roundTargets[currentRound - 1];
        int total = selectedCards[0].cardValue + selectedCards[1].cardValue;

        if (total == currentTarget)
        {
            successfulGuesses++;
            successText.text = successfulGuesses + "/3";
            
            if (successfulGuesses >= 3)
            {
                bossHP--;
                successfulGuesses = 0; 
                
                if (bossHP > 0)
                {
                    currentRound++;
                    statusText.text = "Luar Biasa! Ignaroth terkena damage.";
                }
            }
            else
            {
                statusText.text = "Tebakan Tepat!";
            }
        }
        else
        {
            playerHP--;
            statusText.text = "Kombinasi Salah!";
        }

        UpdateUI();
        
        if (playerHP <= 0)
        {
            statusText.text = "Game Over! Malios gugur.";
            losePanel.SetActive(true); 
        }
        else if (bossHP <= 0)
        {
            statusText.text = "Menang! Ignaroth hancur.";
            winPanel.SetActive(true); 
        }
        else
        {
            StartCoroutine(DelayNextTurn());
        }
    }

    void KunciSemuaKartu()
    {
        foreach (Card card in cards)
        {
            card.cardButton.interactable = false;
        }
    }

    void BukaSemuaKartu()
    {
        foreach (Card card in cards)
        {
            card.cardButton.interactable = true;
        }
    }

    IEnumerator DelayNextTurn()
    {
        yield return new WaitForSeconds(1.5f);
        StartNewTurn();
    }

    void UpdateUI()
    {
        // Menghidupkan atau mematikan visual hati berdasarkan jumlah HP saat ini
        for (int i = 0; i < playerHearts.Length; i++)
        {
            playerHearts[i].SetActive(i < playerHP);
        }

        for (int i = 0; i < bossHearts.Length; i++)
        {
            bossHearts[i].SetActive(i < bossHP);
        }
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ExitGame()
    {
        Debug.Log("Pindah ke Scene Exit (Belum Dibuat)");
    }

    public void ContinueGame()
    {
        Debug.Log("Lanjut ke level berikutnya (Belum Dibuat)");
    }
}