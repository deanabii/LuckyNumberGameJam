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
    /// - Automatically configures Play, Credit, and Exit buttons with MainMenuButtonHoverEffect.
    /// - Provides audio hooks for hover sound, click sound, and background music.
    /// - Handles Play transition to 'TestMap' with smooth fade-to-black.
    /// - Handles Credit popup modal (open/close).
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

        private AudioSource _sfxSource;
        private AudioSource _bgmSource;
        private CanvasGroup _fadeOverlayGroup;
        private GameObject _creditsModalObj;
        private CanvasGroup _creditsModalGroup;
        private bool _isTransitioning;
        private bool _isCreditsOpen;

        private void Awake()
        {
            Instance = this;

            EnsureEventSystem();
            SetupAudio();
            SetupButtons();
            SetupFadeOverlay();
            SetupCreditsModal();
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

        private void Start()
        {
            // Smooth fade-in from black on scene start
            if (_fadeOverlayGroup != null)
            {
                StartCoroutine(FadeOverlayRoutine(1f, 0f, 0.6f));
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
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                // Ensure hover effect is attached
                MainMenuButtonHoverEffect hover = btn.GetComponent<MainMenuButtonHoverEffect>();
                if (hover == null)
                {
                    hover = btn.gameObject.AddComponent<MainMenuButtonHoverEffect>();
                }
                hover.EnsureButtonLabel();
                hover.EnsureGlowBacking();

                // Wire click listener
                string bName = btn.gameObject.name;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => HandleButtonClick(bName));
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
            ToggleCredits();
        }

        public void OnExitClicked()
        {
            PlayClickSound();
            SceneLoader.Quit();
        }

        public void ToggleCredits()
        {
            _isCreditsOpen = !_isCreditsOpen;
            if (_creditsModalGroup != null)
            {
                StopCoroutine("FadeCreditsRoutine");
                StartCoroutine(FadeCreditsRoutine(_isCreditsOpen));
            }
        }

        private IEnumerator FadeCreditsRoutine(bool open)
        {
            if (open) _creditsModalObj.SetActive(true);

            float start = _creditsModalGroup.alpha;
            float target = open ? 1f : 0f;
            float elapsed = 0f;
            float dur = 0.25f;

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                _creditsModalGroup.alpha = Mathf.Lerp(start, target, elapsed / dur);
                yield return null;
            }

            _creditsModalGroup.alpha = target;
            _creditsModalGroup.interactable = open;
            _creditsModalGroup.blocksRaycasts = open;

            if (!open) _creditsModalObj.SetActive(false);
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

        private void SetupCreditsModal()
        {
            Transform existing = transform.Find("CreditsModal");
            if (existing != null)
            {
                _creditsModalObj = existing.gameObject;
                _creditsModalGroup = _creditsModalObj.GetComponent<CanvasGroup>();
                return;
            }

            _creditsModalObj = new GameObject("CreditsModal", typeof(RectTransform), typeof(CanvasGroup));
            _creditsModalObj.transform.SetParent(transform, false);
            _creditsModalObj.transform.SetAsLastSibling();

            RectTransform mrt = _creditsModalObj.GetComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;

            _creditsModalGroup = _creditsModalObj.GetComponent<CanvasGroup>();
            _creditsModalGroup.alpha = 0f;
            _creditsModalGroup.interactable = false;
            _creditsModalGroup.blocksRaycasts = false;

            // Dim Backdrop button to click outside and close
            GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdrop.transform.SetParent(_creditsModalObj.transform, false);
            RectTransform brt = backdrop.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            backdrop.GetComponent<Button>().onClick.AddListener(() => ToggleCredits());

            // Modal Card
            GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(_creditsModalObj.transform, false);
            RectTransform crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(520f, 380f);
            crt.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

            // Title
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(card.transform, false);
            RectTransform trt = titleObj.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.sizeDelta = new Vector2(0f, 50f);
            trt.anchoredPosition = new Vector2(0f, -35f);
            TextMeshProUGUI tTmp = titleObj.GetComponent<TextMeshProUGUI>();
            tTmp.text = "CREDITS";
            tTmp.alignment = TextAlignmentOptions.Center;
            tTmp.fontSize = 28f;
            tTmp.fontStyle = FontStyles.Bold;
            tTmp.color = new Color(0.95f, 0.90f, 0.75f, 1f);

            // Body
            GameObject bodyObj = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            bodyObj.transform.SetParent(card.transform, false);
            RectTransform bodyRt = bodyObj.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.08f, 0.22f);
            bodyRt.anchorMax = new Vector2(0.92f, 0.82f);
            bodyRt.offsetMin = Vector2.zero;
            bodyRt.offsetMax = Vector2.zero;
            TextMeshProUGUI bTmp = bodyObj.GetComponent<TextMeshProUGUI>();
            bTmp.text = "<b>HOKI: The Legend of SixSeven</b>\n" +
                        "<size=80%>Lucky Number Game Jam Entry</size>\n\n" +
                        "<color=#A8D5BA>Gameplay & Engineering:</color> Dev Team\n" +
                        "<color=#A8D5BA>Visual Art & UI:</color> Tiny Swords & Custom Assets\n" +
                        "<color=#A8D5BA>Special Thanks:</color> All Game Jam Players!\n\n" +
                        "<size=75%><color=#7A8B99>Press ESC or Click Close to return</color></size>";
            bTmp.alignment = TextAlignmentOptions.Center;
            bTmp.fontSize = 17f;
            bTmp.color = Color.white;

            // Close Button
            GameObject closeBtnObj = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeBtnObj.transform.SetParent(card.transform, false);
            RectTransform cbrt = closeBtnObj.GetComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(0.5f, 0.08f);
            cbrt.anchorMax = new Vector2(0.5f, 0.08f);
            cbrt.sizeDelta = new Vector2(140f, 38f);
            cbrt.anchoredPosition = Vector2.zero;
            closeBtnObj.GetComponent<Image>().color = new Color(0.20f, 0.35f, 0.50f, 0.9f);
            closeBtnObj.GetComponent<Button>().onClick.AddListener(() => ToggleCredits());

            GameObject closeTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
            RectTransform ctrt = closeTxtObj.GetComponent<RectTransform>();
            ctrt.anchorMin = Vector2.zero;
            ctrt.anchorMax = Vector2.one;
            ctrt.offsetMin = Vector2.zero;
            ctrt.offsetMax = Vector2.zero;
            TextMeshProUGUI ctTmp = closeTxtObj.GetComponent<TextMeshProUGUI>();
            ctTmp.text = "CLOSE";
            ctTmp.alignment = TextAlignmentOptions.Center;
            ctTmp.fontSize = 16f;
            ctTmp.fontStyle = FontStyles.Bold;
            ctTmp.color = Color.white;

            _creditsModalObj.SetActive(false);
        }

        private void Update()
        {
            if (_isCreditsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleCredits();
            }
        }
    }
}
