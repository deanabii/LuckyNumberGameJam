using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Linq;

public class GameManagerBos4 : MonoBehaviour
{
    public static GameManagerBos4 Instance;

    [Header("Seals & Clues (Isi urut 1 sampai 3)")]
    public Image[] imageSeals;      
    public GameObject[] imageClues; 
    public Sprite spriteSealGrey;   
    
    [Header("Player Status")]
    public GameObject[] heartImages; 
    
    [Header("Card Setup")]
    public GameObject cardPrefab;
    public Transform[] handSlots;   
    public Transform[] targetSlots; 
    public Button combineButton;    
    
    [Header("Panels & Transitions")]
    public GameObject winPanel1;
    public GameObject winPanel2;
    public GameObject winPanel3;
    public GameObject losePanel;
    public Image fadePanel;         

    private int currentRound = 1;
    private int playerHearts = 3;
    private List<CardBos4> allCards = new List<CardBos4>();
    public List<CardBos4> selectedCards = new List<CardBos4>();
    
    private int[] cardPool = { 1, 2, 3, 4, 6, 7, 8, 9 }; 
    private bool isTransitioning = false; 

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        combineButton.gameObject.SetActive(false);
        combineButton.onClick.AddListener(OnCombineButtonClicked);
        
        winPanel1.SetActive(false);
        winPanel2.SetActive(false);
        winPanel3.SetActive(false);
        losePanel.SetActive(false);
        
        fadePanel.color = new Color(0, 0, 0, 0);
        fadePanel.raycastTarget = false;

        SetupRound();
    }

    private void SetupRound()
    {
        selectedCards.Clear();
        combineButton.gameObject.SetActive(false);
        combineButton.interactable = true; // <--- TAMBAHKAN BARIS INI

        for (int i = 0; i < imageClues.Length; i++)
        {
            imageClues[i].SetActive(i == (currentRound - 1));
        }

        SpawnAndShuffleCards();
        isTransitioning = false;
    }

    private void SpawnAndShuffleCards()
    {
        foreach (var card in allCards) Destroy(card.gameObject);
        allCards.Clear();

        List<int> shuffledValues = cardPool.OrderBy(x => Random.value).ToList();

        for (int i = 0; i < 8; i++)
        {
            GameObject newCardObj = Instantiate(cardPrefab, handSlots[i]);
            CardBos4 card = newCardObj.GetComponent<CardBos4>();
            card.Initialize(shuffledValues[i], handSlots[i]);
            allCards.Add(card);
        }
    }

    public void OnCardClicked(CardBos4 card)
    {
        if (isTransitioning) return;

        if (!card.isInTargetSlot && selectedCards.Count < 4)
        {
            card.MoveToTarget(targetSlots[selectedCards.Count]);
            selectedCards.Add(card);
            card.isInTargetSlot = true;
        }
        else if (card.isInTargetSlot)
        {
            selectedCards.Remove(card);
            card.ReturnToHand();
            
            for (int i = 0; i < selectedCards.Count; i++)
            {
                selectedCards[i].MoveToTarget(targetSlots[i]);
            }
        }

        combineButton.gameObject.SetActive(selectedCards.Count == 4);
    }

    private void OnCombineButtonClicked()
    {
        if (isTransitioning) return;
        StartCoroutine(ValidateSelectionRoutine());
    }

    private IEnumerator ValidateSelectionRoutine()
    {
        isTransitioning = true;
        combineButton.interactable = false;
        

        int val1 = selectedCards[0].cardValue;
        int val2 = selectedCards[1].cardValue;
        int val3 = selectedCards[2].cardValue;
        int val4 = selectedCards[3].cardValue;

        bool isValid = false;

        bool isCard1Even = (val1 % 2 == 0);
        bool isCard3Odd = (val3 % 2 != 0);
        bool isCard4Max = (val4 == 9);

        if (isCard1Even && isCard3Odd && isCard4Max)
        {
            if (currentRound == 1 && val2 == 1) isValid = true;
            else if (currentRound == 2 && val2 > val1) isValid = true;
            else if (currentRound == 3 && val2 < val1) isValid = true;
        }

        if (isValid)
        {
            yield return StartCoroutine(NextRoundTransitionRoutine());
        }
        else
        {
            LoseHeart();
            
            if (playerHearts > 0)
            {
                yield return new WaitForSeconds(0.5f); 
                foreach (var card in selectedCards)
                {
                    card.ReturnToHand();
                }
                selectedCards.Clear();
                combineButton.gameObject.SetActive(false);
            }
        }

        if (playerHearts > 0 && isValid == false)
        {
            combineButton.interactable = true;
            foreach (var card in allCards) card.SetInteractable(true);
            isTransitioning = false;
        }
    }

    private IEnumerator NextRoundTransitionRoutine()
    {
        // Ambil referensi segel yang sedang aktif di ronde ini
        Image currentSeal = imageSeals[currentRound - 1];

        // 1. Fade Out Segel Ungu (Selama 0.5 detik)
        yield return StartCoroutine(FadeSeal(currentSeal, 1f, 0f, 0.5f));
        
        // 2. Ganti Sprite secara instan saat sedang tak terlihat
        currentSeal.sprite = spriteSealGrey;
        
        // 3. Fade In Segel Abu-abu (Selama 0.5 detik)
        yield return StartCoroutine(FadeSeal(currentSeal, 0f, 1f, 0.5f));

        // Jeda sejenak agar pemain menyadari perubahannya sebelum lanjut
        yield return new WaitForSeconds(0.5f);

        currentRound++;

        if (currentRound > 3)
        {
            // Full screen fade hanya saat menang (masuk panel)
            yield return StartCoroutine(FadeScreen(1f, 1f));
            winPanel1.SetActive(true);
            yield return StartCoroutine(FadeScreen(0f, 1f));
        }
        else
        {
            SetupRound();
        }
    }

    // Fungsi Baru: Khusus mem-fade komponen Image Segel
    private IEnumerator FadeSeal(Image sealImage, float startAlpha, float targetAlpha, float duration)
    {
        Color color = sealImage.color;
        float timer = 0;
        
        while (timer < duration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
            sealImage.color = color;
            yield return null;
        }
        
        color.a = targetAlpha;
        sealImage.color = color;
    }

    private void LoseHeart()
    {
        playerHearts--;
        heartImages[playerHearts].SetActive(false);

        if (playerHearts <= 0)
        {
            isTransitioning = true;
            losePanel.SetActive(true);
        }
    }

    private IEnumerator FadeScreen(float targetAlpha, float duration)
    {
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

    // --- BUTTON EVENT LOGIC ---

    public void GoToNextWinPanel(int nextPanelIndex)
    {
        StartCoroutine(SwitchWinPanelRoutine(nextPanelIndex));
    }

    private IEnumerator SwitchWinPanelRoutine(int nextPanelIndex)
    {
        isTransitioning = true;
        yield return StartCoroutine(FadeScreen(1f, 1f));

        winPanel1.SetActive(nextPanelIndex == 1);
        winPanel2.SetActive(nextPanelIndex == 2);
        winPanel3.SetActive(nextPanelIndex == 3);

        yield return StartCoroutine(FadeScreen(0f, 1f));
        isTransitioning = false;
    }

    public void GoToNextScene() 
    {
        SceneManager.LoadScene("NamaSceneSelanjutnya"); 
    }

    public void RestartGame() 
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ExitGame() 
    {
        SceneManager.LoadScene("MainMenuScene"); 
    }
}