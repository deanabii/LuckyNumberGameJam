using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using UnityEngine.SceneManagement; // Wajib untuk fungsi ganti scene (Restart, Exit, Next)

public class GameManagerBos3 : MonoBehaviour
{
    [Header("Referensi UI")]
    public Transform cardGrid;
    public GameObject cardPrefab;
    public CardSlot[] craftSlots; 
    public Button btnBuatRamuan;

    [Header("UI Teks Informasi")]
    public TextMeshProUGUI txtRonde;   
    public TextMeshProUGUI txtTarget;  

    [Header("UI Nyawa (Hearts)")]
    public GameObject[] playerHearts; 
    public GameObject[] bossHearts;   

    [Header("Data Master Bahan")]
    public CardData dataDaun;
    public CardData dataAkar;
    public CardData dataGingseng;
    public CardData dataMawarBiru;
    public CardData dataLilyPutih;

    [Header("Panels & Transitions")]
    public GameObject winPanel1;
    public GameObject winPanel2;
    public GameObject losePanel;
    public Image fadePanel;

    [Header("Status Permainan")]
    public int currentRound = 1;
    public int playerHP = 3;
    public int bossHP = 3;

    private bool isTransitioning = false; // Penanda agar pemain tidak bisa interaksi saat layar memudar

    private void Awake()
    {
        bossHP = 3;
        playerHP = 3;
        currentRound = 1;
    }

    [Header("Audio Settings (Placeholder)")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;
    public AudioSource bossAudioSource;
    public AudioClip bgmMusic;
    public AudioClip playerHitEnemySFX;
    public AudioClip enemyHitPlayerSFX;
    public AudioClip bossSoundSFX;
    public bool loopBossSound = true;
    [Range(0f, 1f)] public float bgmVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 1.0f;
    [Range(0f, 1f)] public float bossSoundVolume = 0.75f;

    public void PlayBGM()
    {
        EnsureAudioSources();
        if (bgmSource != null && bgmMusic != null)
        {
            if (bgmSource.clip != bgmMusic || !bgmSource.isPlaying)
            {
                bgmSource.clip = bgmMusic;
                bgmSource.loop = true;
                bgmSource.volume = bgmVolume;
                bgmSource.Play();
            }
        }
    }

    public void PlayPlayerHitEnemySFX()
    {
        EnsureAudioSources();
        if (sfxSource != null && playerHitEnemySFX != null)
        {
            sfxSource.PlayOneShot(playerHitEnemySFX, sfxVolume);
        }
    }

    public void PlayEnemyHitPlayerSFX()
    {
        EnsureAudioSources();
        if (sfxSource != null && enemyHitPlayerSFX != null)
        {
            sfxSource.PlayOneShot(enemyHitPlayerSFX, sfxVolume);
        }
    }

    public void PlayBossSoundSFX()
    {
        EnsureAudioSources();
        if (bossAudioSource != null && bossSoundSFX != null)
        {
            bossAudioSource.clip = bossSoundSFX;
            bossAudioSource.loop = loopBossSound;
            bossAudioSource.volume = bossSoundVolume;
            if (!bossAudioSource.isPlaying)
            {
                bossAudioSource.Play();
            }
        }
    }

    public void StopBossSoundSFX()
    {
        if (bossAudioSource != null && bossAudioSource.isPlaying)
        {
            bossAudioSource.Stop();
        }
    }

    private void EnsureAudioSources()
    {
        if (bgmSource == null)
        {
            bgmSource = GetComponent<AudioSource>();
            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
        }
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
        if (bossAudioSource == null)
        {
            bossAudioSource = gameObject.AddComponent<AudioSource>();
            bossAudioSource.playOnAwake = false;
            bossAudioSource.loop = loopBossSound;
        }
    }

    void Start()
    {
        bossHP = 3;
        playerHP = 3;
        currentRound = 1;
        PlayBGM();
        if (bossSoundSFX != null && loopBossSound)
        {
            PlayBossSoundSFX();
        }
        LuckyNumber.UI.Boss1IntroGuideBridge.EnsureForActiveScene();
        // Pastikan semua panel tertutup di awal game
        if (winPanel1 != null) winPanel1.SetActive(false);
        if (winPanel2 != null) winPanel2.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        
        // Pastikan layar terang/tembus pandang di awal game
        if (fadePanel != null)
        {
            fadePanel.color = new Color(0, 0, 0, 0);
            fadePanel.raycastTarget = false;
        }

        btnBuatRamuan.onClick.AddListener(CheckPotion);
        UpdateHealthUI(); 
        StartRound();
    }

    private void Update()
    {
        // Jika sedang transisi/animasi, matikan tombol agar tidak di-spam
        if (isTransitioning)
        {
            btnBuatRamuan.gameObject.SetActive(false);
            return;
        }

        bool isSemuaSlotTerisi = (craftSlots[0].transform.childCount > 0 && 
                                  craftSlots[1].transform.childCount > 0 && 
                                  craftSlots[2].transform.childCount > 0);

        btnBuatRamuan.gameObject.SetActive(isSemuaSlotTerisi);
    }

    public void StartRound()
    {
        ClearTable();
        GenerateDeck();
        UpdateInformasiUI(); 
    }

    private void UpdateInformasiUI()
    {
        if (currentRound == 1) 
            txtRonde.text = "Round 1";
        else if (currentRound == 2) 
            txtRonde.text = "Round 2";
        else if (currentRound == 3) 
            txtRonde.text = "Last Round!";
        else 
            txtRonde.text = "Finished";

        if (currentRound == 1) 
            txtTarget.text = "Target: Chronix";
        else if (currentRound == 2) 
            txtTarget.text = "Target: Aethel";
        else if (currentRound == 3) 
            txtTarget.text = "Target: Panacea";
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
        int minAkar = Mathf.Max(1, 12 - valDaun);   // Typo diperbaiki (-)
        int maxAkar = Mathf.Min(9, 20 - valDaun);   // Typo diperbaiki (-)
        int valAkar = Random.Range(minAkar, maxAkar + 1);
        int valBahan3 = 21 - (valDaun + valAkar);   // Typo diperbaiki (-)

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
            bossHP--;
            UpdateHealthUI(); 
            
            if (bossHP <= 0) {
                StartCoroutine(ShowWinSequenceRoutine());
            } else {
                currentRound++;
                StartRound();
            }
        }
        else
        {
            playerHP--;
            UpdateHealthUI(); 

            if (playerHP <= 0) {
                GameOver();
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

    // --- LOGIKA TRANSISI FADE & PANELS ---

    private IEnumerator ShowWinSequenceRoutine()
    {
        isTransitioning = true; // Kunci permainan
        StopBossSoundSFX();
        yield return StartCoroutine(FadeScreen(1f, 1f)); // Fade out ke hitam
        if (winPanel1 != null) winPanel1.SetActive(true); // Tampilkan Panel 1
        yield return StartCoroutine(FadeScreen(0f, 1f)); // Fade in ke terang
    }

    // Hubungkan fungsi ini ke tombol "Continue" di WinPanel 1
    public void GoToNextWinPanel()
    {
        StartCoroutine(SwitchWinPanelRoutine());
    }

    private IEnumerator SwitchWinPanelRoutine()
    {
        isTransitioning = true;
        yield return StartCoroutine(FadeScreen(1f, 1f));

        if (winPanel1 != null) winPanel1.SetActive(false);
        if (winPanel2 != null) winPanel2.SetActive(true);

        yield return StartCoroutine(FadeScreen(0f, 1f));
    }

    private IEnumerator FadeScreen(float targetAlpha, float duration)
    {
        if (fadePanel == null) yield break; 

        fadePanel.raycastTarget = true; // Blokir interaksi UI di belakang
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

    private void GameOver()
    {
        isTransitioning = true; 
        StopBossSoundSFX();
        if (losePanel != null) losePanel.SetActive(true);
    }

    // --- LOGIKA TOMBOL ---

    // Hubungkan ke tombol "Restart" di Lose Panel
    public void RestartGame() 
    {
        StopBossSoundSFX();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Hubungkan ke tombol "Exit" di Lose Panel
    public void ExitGame() 
    {
        StopBossSoundSFX();
        LuckyNumber.Core.GameProgressionManager.CompleteBossBattleAndPrepareReturn(2, false);
        SceneManager.LoadScene("TestMap");
    }

    // Hubungkan ke tombol "Continue" di Win Panel 2
    public void GoToNextScene() 
    {
        StopBossSoundSFX();
        LuckyNumber.Core.GameProgressionManager.CompleteBossBattleAndPrepareReturn(2, true);
        SceneManager.LoadScene("TestMap");
    }
}