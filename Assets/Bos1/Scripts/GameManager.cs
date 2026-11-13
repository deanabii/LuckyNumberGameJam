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

    [Header("Placeholder Sound Effect (Drag AudioClip ke Sini)")]
    public AudioSource bgmAudioSource;
    public AudioSource sfxAudioSource;
    public AudioSource bossAudioSource;
    public AudioClip bgmMusic;
    public AudioClip playerHitEnemySFX;
    public AudioClip playerHeavyFinisherSFX;
    public AudioClip enemyHitPlayerSFX;
    public AudioClip bossSoundSFX;
    public bool loopBossSound = true;
    [Range(0f, 1f)] public float bgmVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float bossSoundVolume = 0.75f;
    
    private List<Card> selectedCards = new List<Card>();
    private Dictionary<Card, Vector3> cardInitialPositions = new Dictionary<Card, Vector3>();
    private Dictionary<Card, Vector3> cardTargetPositions = new Dictionary<Card, Vector3>();

    void Start()
    {
        winPanel.SetActive(false);
        losePanel.SetActive(false);
        LuckyNumber.UI.Boss1IntroGuideBridge.EnsureForActiveScene();
        PlayBGM();
        if (bossSoundSFX != null && loopBossSound)
        {
            PlayBossSoundSFX();
        }
        
        UpdateUI();
        StartNewTurn();
    }

    void StartNewTurn()
    {
        selectedCards.Clear();
        
        if (playerHP <= 0 || bossHP <= 0) return;

        cardLayoutGroup.enabled = true;

        int currentTarget = roundTargets[currentRound - 1];
        
        roundText.text = "Round " + currentRound;
        targetText.text = currentTarget.ToString();
        successText.text = successfulGuesses + "/3";
        statusText.text = "Select two cards"; 
        
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
                card.transform.position = Vector3.Lerp(card.transform.position, cardTargetPositions[card], Time.deltaTime * 28f);
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
            int maxFillerExclusive = Mathf.Max(2, Mathf.Min(10, target));
            newCardValues.Add(Random.Range(1, maxFillerExclusive));
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
        // Immediate validation on correct answer (0ms delay)

        int currentTarget = roundTargets[currentRound - 1];
        int total = selectedCards[0].cardValue + selectedCards[1].cardValue;

        if (total == currentTarget)
        {
            successfulGuesses++;
            bool isFinisherHit = (successfulGuesses >= 3);
            AudioClip hitClip = (isFinisherHit && playerHeavyFinisherSFX != null) ? playerHeavyFinisherSFX : playerHitEnemySFX;
            LuckyNumber.UI.Boss1IntroGuideBridge.PlayPlayerAttackEffect(isFinisherHit, () => { PlayBattleSFX(hitClip); PlayBossSoundSFX(); });
            successText.text = successfulGuesses + "/3";
            
            if (successfulGuesses >= 3)
            {
                bossHP--;
                successfulGuesses = 0; 
                
                if (bossHP > 0)
                {
                    currentRound++;
                    statusText.text = "MEGA BOOM! Ignaroth lost 1 HP!";
                }
            }
            else
            {
                statusText.text = successfulGuesses == 1 ? "BOOM! Direct hit!" : "BLAM! Combo hit!";
            }
        }
        else
        {
            yield return new WaitForSeconds(0.10f);
            playerHP--;
            statusText.text = "Wrong combination!";
            LuckyNumber.UI.Boss1IntroGuideBridge.PlayBossAttackOnPlayerEffect(() => PlayBattleSFX(enemyHitPlayerSFX));
        }

        UpdateUI();
        
        if (playerHP <= 0)
        {
            statusText.text = " ";
            StopBossSoundSFX();
            losePanel.SetActive(true);
            LuckyNumber.UI.Boss1IntroGuideBridge.SetBattleEndedUI();
        }
        else if (bossHP <= 0)
        {
            statusText.text = " ";
            StopBossSoundSFX();
            winPanel.SetActive(true);
            LuckyNumber.UI.Boss1IntroGuideBridge.SetBattleEndedUI();
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
        yield return new WaitForSeconds(0.80f);
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
        StopBossSoundSFX();
        LuckyNumber.UI.Boss1IntroGuideBridge.MarkRestartNextLoad();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ExitGame()
    {
        StopBossSoundSFX();
        LuckyNumber.UI.Boss1IntroGuideBridge.ReturnToMapFromBoss1(false);
    }

    public void ContinueGame()
    {
        StopBossSoundSFX();
        LuckyNumber.UI.Boss1IntroGuideBridge.ReturnToMapFromBoss1(true);
    }

    public void PlayBGM()
    {
        if (bgmAudioSource == null)
        {
            bgmAudioSource = gameObject.AddComponent<AudioSource>();
            bgmAudioSource.playOnAwake = false;
        }
        if (bgmMusic != null)
        {
            if (bgmAudioSource.clip != bgmMusic || !bgmAudioSource.isPlaying)
            {
                bgmAudioSource.clip = bgmMusic;
                bgmAudioSource.loop = true;
                bgmAudioSource.volume = bgmVolume;
                bgmAudioSource.Play();
            }
        }
    }

    public void PlayBossSoundSFX()
    {
        if (bossSoundSFX == null) return;
        EnsureBossAudioSource();
        if (bossAudioSource != null)
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

    private void EnsureBossAudioSource()
    {
        if (bossAudioSource == null)
        {
            bossAudioSource = gameObject.AddComponent<AudioSource>();
            bossAudioSource.playOnAwake = false;
            bossAudioSource.loop = loopBossSound;
        }
    }

    private void PlayBattleSFX(AudioClip clip)
    {
        if (clip == null) return;
        if (sfxAudioSource == null)
        {
            sfxAudioSource = GetComponent<AudioSource>();
            if (sfxAudioSource == null)
            {
                sfxAudioSource = gameObject.AddComponent<AudioSource>();
            }
            sfxAudioSource.playOnAwake = false;
        }
        sfxAudioSource.PlayOneShot(clip, sfxVolume);
    }
}