using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using LuckyNumber.Core;

namespace LuckyNumber.UI
{
    /// <summary>
    /// Attached to Canvas 'Button Main Menu':
    /// - Configures Play, Credit, and Exit menu buttons with MainMenuButtonHoverEffect.
    /// - Provides audio hooks for hover sound, click sound, and background music.
    /// - Handles Play transition to 'TestMap' with smooth fade-to-black.
    /// - Controls the existing scene 'Credit' panel (opens and closes the author's Credit panel).
    /// - Handles Exit application.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class MainMenuController : MonoBehaviour
    {
        public static MainMenuController Instance { get; private set; }

        private const string GameSceneName = "TestMap";

        [Header("Audio SFX & Music (Placeholder Hooks)")]
        [Tooltip("Sound played when pointer enters/hovers a menu button")]
        public AudioClip hoverSFX;
        [Tooltip("Sound played when a menu button is clicked")]
        public AudioClip clickSFX;
        [Tooltip("Background music loop for Main Menu")]
        public AudioClip bgmMusic;
        [Range(0f, 1f)] public float sfxVolume = 1.0f;
        [Range(0f, 1f)] public float bgmVolume = 0.7f;

        [Header("Scene Transition Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        [Header("Credits Panel (Existing Scene Panel)")]
        [Tooltip("Referensi ke panel Credit yang sudah ada di scene (di bawah Canvas Button Main Menu).")]
        [SerializeField] private GameObject creditPanel;

        private AudioSource _sfxSource;
        private AudioSource _bgmSource;
        private CanvasGroup _fadeOverlayGroup;
        private bool _isTransitioning;

        public GameObject CreditPanel => creditPanel;

        private void Awake()
        {
            Instance = this;

            EnsureEventSystem();
            SetupAudio();
            ResolveCreditPanel();
            SetupButtons();
            SetupFadeOverlay();
        }

        private void EnsureEventSystem()
        {
            EventSystem es = FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }
            else if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Destroy(legacy);
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private void ResolveCreditPanel()
        {
            if (creditPanel == null)
            {
                Transform creditTr = transform.Find("Credit");
                if (creditTr != null && creditTr.GetComponent<Button>() == null)
                {
                    creditPanel = creditTr.gameObject;
                }
            }

            if (creditPanel != null)
            {
                // Pastikan panel credit tertutup saat awal game dijalankan
                creditPanel.SetActive(false);

                // Pastikan tombol close ('Button' / X) di dalam panel Credit terhubung ke HideCredits
                Button closeBtn = creditPanel.GetComponentInChildren<Button>(true);
                if (closeBtn != null)
                {
                    closeBtn.onClick.RemoveAllListeners();
                    closeBtn.onClick.AddListener(PlayClickSound);
                    if (closeBtn.onClick.GetPersistentEventCount() == 0)
                    {
                        closeBtn.onClick.AddListener(() => HideCredits());
                    }
                }

                // Sinkronkan ke SceneLoader lokal maupun singleton Instance
                var sceneLdr = GetComponent<SceneLoader>();
                if (sceneLdr != null)
                {
                    sceneLdr.SetCreditsPanel(creditPanel);
                }
                if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.SetCreditsPanel(creditPanel);
                }
            }
        }

        private void Start()
        {
            // Smooth fade-in from black on scene start
            if (_fadeOverlayGroup != null)
            {
                StartCoroutine(FadeOverlayRoutine(1f, 0f, 0.6f));
            }

            // Sync lagi di Start untuk memastikan SceneLoader global sudah siap
            if (creditPanel != null && SceneLoader.Instance != null)
            {
                SceneLoader.Instance.SetCreditsPanel(creditPanel);
            }
        }

        private void SetupAudio()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length >= 2)
            {
                _sfxSource = sources[0];
                _bgmSource = sources[1];
            }
            else if (sources.Length == 1)
            {
                _sfxSource = sources[0];
                _bgmSource = gameObject.AddComponent<AudioSource>();
            }
            else
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _bgmSource = gameObject.AddComponent<AudioSource>();
            }

            _sfxSource.playOnAwake = false;
            _sfxSource.volume = sfxVolume;

            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.volume = bgmVolume;

            if (bgmMusic != null && !_bgmSource.isPlaying)
            {
                _bgmSource.clip = bgmMusic;
                _bgmSource.Play();
            }
        }

        public void SetupButtons()
        {
            // Cari tombol utama di dalam container "button" atau "Panel" (Play, Credit, Exit)
            Transform panelTr = transform.Find("button") ?? transform.Find("Panel");
            Button[] buttons = panelTr != null ? panelTr.GetComponentsInChildren<Button>(true) : GetComponentsInChildren<Button>(true);

            foreach (Button btn in buttons)
            {
                // JANGAN sentuh tombol yang merupakan bagian dari panel Credit (seperti tombol close X)
                if (creditPanel != null && btn.transform.IsChildOf(creditPanel.transform))
                {
                    continue;
                }

                // Pasang efek visual hover / glow pada tombol menu utama
                MainMenuButtonHoverEffect hover = btn.GetComponent<MainMenuButtonHoverEffect>();
                if (hover == null)
                {
                    hover = btn.gameObject.AddComponent<MainMenuButtonHoverEffect>();
                }
                hover.EnsureButtonLabel();
                hover.EnsureGlowBacking();

                // Pasang listener klik tombol menu tanpa menabrak persistent OnClick() di Inspector (SceneLoader)
                string bName = btn.gameObject.name;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(PlayClickSound);
                if (btn.onClick.GetPersistentEventCount() == 0)
                {
                    btn.onClick.AddListener(() => HandleButtonClick(bName));
                }
            }
        }

        public void PlayHoverSound()
        {
            if (hoverSFX != null && _sfxSource != null)
            {
                _sfxSource.PlayOneShot(hoverSFX, sfxVolume);
            }
        }

        public void PlayClickSound()
        {
            if (clickSFX != null && _sfxSource != null)
            {
                _sfxSource.PlayOneShot(clickSFX, sfxVolume);
            }
        }

        public void HandleButtonClick(string buttonName)
        {
            if (_isTransitioning) return;

            string lower = buttonName.ToLowerInvariant();

            if (lower.Contains("play"))
            {
                OnPlayClicked();
            }
            else if (lower.Contains("credit"))
            {
                OnCreditClicked();
            }
            else if (lower.Contains("exit"))
            {
                OnExitClicked();
            }
        }

        public void OnPlayClicked()
        {
            if (_isTransitioning) return;
            PlayClickSound();
            StartCoroutine(TransitionToGameRoutine());
        }

        private IEnumerator TransitionToGameRoutine()
        {
            _isTransitioning = true;

            // Fade to black
            if (_fadeOverlayGroup != null)
            {
                yield return StartCoroutine(FadeOverlayRoutine(0f, 1f, fadeDuration));
            }
            else
            {
                yield return new WaitForSecondsRealtime(fadeDuration);
            }

            SceneLoader.Load(GameSceneName, false);
        }

        public void OnCreditClicked()
        {
            PlayClickSound();
            ShowCredits();
        }

        public void OnExitClicked()
        {
            PlayClickSound();
            SceneLoader.Quit();
        }

        public void ShowCredits()
        {
            if (creditPanel != null)
            {
                creditPanel.SetActive(true);
                creditPanel.transform.SetAsLastSibling();
            }
            var localLoader = GetComponent<SceneLoader>();
            if (localLoader != null)
                localLoader.ShowCredits();
            else
                SceneLoader.OpenCreditsStatic();
        }

        public void HideCredits()
        {
            if (creditPanel != null)
            {
                creditPanel.SetActive(false);
            }
            var localLoader = GetComponent<SceneLoader>();
            if (localLoader != null)
                localLoader.HideCredits();
            else
                SceneLoader.CloseCreditsStatic();
        }

        public void CloseCredits() => HideCredits();
        public void OpenCredits() => ShowCredits();

        public void ToggleCredits()
        {
            if (creditPanel != null)
            {
                if (creditPanel.activeSelf)
                    HideCredits();
                else
                    ShowCredits();
            }
            else
            {
                var localLoader = GetComponent<SceneLoader>();
                if (localLoader != null) localLoader.ToggleCredits();
            }
        }

        private IEnumerator FadeOverlayRoutine(float from, float to, float duration)
        {
            if (_fadeOverlayGroup == null) yield break;

            _fadeOverlayGroup.gameObject.SetActive(true);
            _fadeOverlayGroup.blocksRaycasts = (to > 0.01f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _fadeOverlayGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            _fadeOverlayGroup.alpha = to;
            if (to <= 0.01f)
            {
                _fadeOverlayGroup.blocksRaycasts = false;
                _fadeOverlayGroup.gameObject.SetActive(false);
            }
        }

        private void SetupFadeOverlay()
        {
            Transform existing = transform.Find("ScreenFadeOverlay");
            if (existing != null)
            {
                _fadeOverlayGroup = existing.GetComponent<CanvasGroup>();
                return;
            }

            GameObject overlayObj = new GameObject("ScreenFadeOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            overlayObj.transform.SetParent(transform, false);
            overlayObj.transform.SetAsLastSibling();

            RectTransform rt = overlayObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = overlayObj.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = true;

            _fadeOverlayGroup = overlayObj.GetComponent<CanvasGroup>();
            _fadeOverlayGroup.alpha = 0f;
            _fadeOverlayGroup.blocksRaycasts = false;
            overlayObj.SetActive(false);
        }

        private void Update()
        {
            if (creditPanel != null && creditPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                HideCredits();
            }
        }
    }
}
