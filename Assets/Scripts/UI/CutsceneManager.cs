using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using LuckyNumber.Core;
using LuckyNumber.Characters;

namespace LuckyNumber.UI
{
    [Serializable]
    public class CutsceneSlide
    {
        public string chapterTitle;
        [TextArea(3, 6)]
        public string narrativeText;
        public Sprite artwork;
    }

    public class CutsceneManager : MonoBehaviour
    {
        public static CutsceneManager Instance { get; private set; }

        [Header("Font & Cutscene Settings (Bisa Diedit di Inspector)")]
        [SerializeField] private TMP_FontAsset customCutsceneFont;
        [SerializeField] private float typewriterCharDelay = 0.018f;
        [SerializeField] private bool playIntroOnStart = true;

        [Header("Daftar Slide Cutscene (Otomatis Terisi, Bisa Diedit di Inspector)")]
        [SerializeField] private CutsceneSlide[] introSlides;
        [SerializeField] private CutsceneSlide[] endingSlides;

        [Header("UI References (Tersambung ke LuckyNumber_UICanvas/CutsceneComicOverlay)")]
        [SerializeField] private GameObject _cutsceneRoot;
        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private Image _comicArtworkImage;
        [SerializeField] private RectTransform _comicArtworkRect;
        [SerializeField] private TextMeshProUGUI _chapterTitleText;
        [SerializeField] private TextMeshProUGUI _pageCounterText;
        [SerializeField] private TextMeshProUGUI _narrativeText;
        [SerializeField] private TextMeshProUGUI _nextButtonLabel;
        [SerializeField] private TextMeshProUGUI _skipButtonLabel;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _skipButton;

        private CutsceneSlide[] _activeSlides;
        private int _currentSlideIndex;
        private bool _isPlaying;
        private bool _isTyping;
        private string _fullSlideText;
        private Coroutine _typingCoroutine;
        private Action _onCutsceneComplete;
        private float _slideZoomTimer;
        private bool _listenersBound;

        public bool IsPlaying => _isPlaying;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void ApplyFont(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null) return;
            customCutsceneFont = fontAsset;
            if (_chapterTitleText != null) _chapterTitleText.font = fontAsset;
            if (_pageCounterText != null) _pageCounterText.font = fontAsset;
            if (_narrativeText != null) _narrativeText.font = fontAsset;
            if (_nextButtonLabel != null) _nextButtonLabel.font = fontAsset;
            if (_skipButtonLabel != null) _skipButtonLabel.font = fontAsset;
        }

        public void EnsureDefaultSlidesPopulated()
        {
            var registry = GameAssetRegistry.Instance;
            Sprite[] introSprites = registry != null ? registry.introCutsceneSprites : null;
            Sprite[] endingSprites = registry != null ? registry.endingCutsceneSprites : null;

            if (introSlides == null || introSlides.Length == 0)
            {
                introSlides = CreateDefaultIntroSlides(introSprites);
            }
            else
            {
                for (int i = 0; i < introSlides.Length; i++)
                {
                    if (introSlides[i].artwork == null)
                        introSlides[i].artwork = GetSafeSprite(introSprites, i);
                }
            }

            if (endingSlides == null || endingSlides.Length == 0)
            {
                endingSlides = CreateDefaultEndingSlides(endingSprites);
            }
            else
            {
                for (int i = 0; i < endingSlides.Length; i++)
                {
                    if (endingSlides[i].artwork == null)
                        endingSlides[i].artwork = GetSafeSprite(endingSprites, i);
                }
            }
        }

        public void InitializeUI(Transform canvasTransform)
        {
            var registry = GameAssetRegistry.Instance;

            EnsureDefaultSlidesPopulated();

            // Check if CutsceneComicOverlay already exists in the Canvas Hierarchy (from Edit Mode)
            if (_cutsceneRoot == null && canvasTransform != null)
            {
                Transform existing = canvasTransform.Find("CutsceneComicOverlay");
                if (existing != null)
                {
                    _cutsceneRoot = existing.gameObject;
                }
            }

            if (_cutsceneRoot != null)
            {
                BindExistingUIReferences();

                // PENTING: Hanya timpa font jika user secara spesifik mengisi customCutsceneFont di Inspector CutsceneManager.
                // Jika customCutsceneFont kosong, JANGAN menimpa font yang sudah diatur manual oleh user di scene hierarchy!
                if (customCutsceneFont != null)
                {
                    ApplyFont(customCutsceneFont);
                }
                else if (_narrativeText != null && _narrativeText.font != null)
                {
                    // Ambil font yang sudah diset user di scene agar teks/tombol lain seragam
                    customCutsceneFont = _narrativeText.font;
                }

                BindRuntimeButtons();
                if (Application.isPlaying && !_isPlaying)
                {
                    _cutsceneRoot.SetActive(false);
                }
                return;
            }

            TMP_FontAsset font = customCutsceneFont 
                ?? (registry != null ? registry.GetPreferredFont() : null) 
                ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (canvasTransform == null) return;

            // Create CutsceneComicOverlay in Canvas Hierarchy
            _cutsceneRoot = new GameObject("CutsceneComicOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _cutsceneRoot.transform.SetParent(canvasTransform, false);
            RectTransform rootRect = _cutsceneRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            _rootCanvasGroup = _cutsceneRoot.GetComponent<CanvasGroup>();
            Image bgImg = _cutsceneRoot.GetComponent<Image>();
            bgImg.color = new Color(0.05f, 0.04f, 0.08f, 0.97f);

            // Outer Comic Frame (Wooden Parchment Board)
            GameObject frameObj = new GameObject("ComicBoardFrame", typeof(RectTransform), typeof(Image));
            frameObj.transform.SetParent(_cutsceneRoot.transform, false);
            RectTransform frameRect = frameObj.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.08f, 0.06f);
            frameRect.anchorMax = new Vector2(0.92f, 0.94f);
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            Image frameImg = frameObj.GetComponent<Image>();
            if (registry != null && registry.dialogueBoardSprite != null)
            {
                frameImg.sprite = registry.dialogueBoardSprite;
                frameImg.color = new Color(0.96f, 0.92f, 0.84f, 1f);
            }
            else
            {
                frameImg.color = new Color(0.18f, 0.13f, 0.09f, 1f);
            }

            // Header Ribbon (Chapter Title)
            GameObject headerObj = new GameObject("ChapterHeader", typeof(RectTransform), typeof(Image));
            headerObj.transform.SetParent(frameObj.transform, false);
            RectTransform headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.18f, 0.89f);
            headerRect.anchorMax = new Vector2(0.82f, 0.985f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;

            Image headerImg = headerObj.GetComponent<Image>();
            if (registry != null && registry.titleBannerSprite != null)
            {
                headerImg.sprite = registry.titleBannerSprite;
                headerImg.color = Color.white;
            }
            else
            {
                headerImg.color = new Color(0.32f, 0.2f, 0.1f, 0.95f);
            }

            _chapterTitleText = CreateTMPText("ChapterTitleText", headerObj.transform, font, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
            _chapterTitleText.color = new Color(1f, 0.93f, 0.72f, 1f);
            SetStretch(_chapterTitleText.rectTransform, 20f, 6f, 20f, 8f);

            // Comic Illustration Viewport (with RectMask2D for clean comic border & zoom)
            GameObject viewportBorderObj = new GameObject("ComicPanelBorder", typeof(RectTransform), typeof(Image));
            viewportBorderObj.transform.SetParent(frameObj.transform, false);
            RectTransform vpBorderRect = viewportBorderObj.GetComponent<RectTransform>();
            vpBorderRect.anchorMin = new Vector2(0.07f, 0.36f);
            vpBorderRect.anchorMax = new Vector2(0.93f, 0.87f);
            vpBorderRect.offsetMin = Vector2.zero;
            vpBorderRect.offsetMax = Vector2.zero;
            Image vpBorderImg = viewportBorderObj.GetComponent<Image>();
            vpBorderImg.color = new Color(0.12f, 0.09f, 0.07f, 1f);

            GameObject viewportObj = new GameObject("ComicPanelViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportObj.transform.SetParent(viewportBorderObj.transform, false);
            RectTransform vpRect = viewportObj.GetComponent<RectTransform>();
            SetStretch(vpRect, 6f, 6f, 6f, 6f);
            Image vpBg = viewportObj.GetComponent<Image>();
            vpBg.color = new Color(0.06f, 0.05f, 0.08f, 1f);

            GameObject artObj = new GameObject("ComicArtwork", typeof(RectTransform), typeof(Image));
            artObj.transform.SetParent(viewportObj.transform, false);
            _comicArtworkRect = artObj.GetComponent<RectTransform>();
            SetStretch(_comicArtworkRect, 0f, 0f, 0f, 0f);
            _comicArtworkImage = artObj.GetComponent<Image>();
            _comicArtworkImage.preserveAspect = true;
            _comicArtworkImage.raycastTarget = false;

            // Page Badge in top-right of comic panel
            GameObject badgeObj = new GameObject("PageBadge", typeof(RectTransform), typeof(Image));
            badgeObj.transform.SetParent(viewportBorderObj.transform, false);
            RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.82f, 0.86f);
            badgeRect.anchorMax = new Vector2(0.985f, 0.975f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;
            Image badgeImg = badgeObj.GetComponent<Image>();
            badgeImg.color = new Color(0.1f, 0.08f, 0.06f, 0.88f);

            _pageCounterText = CreateTMPText("PageCounterText", badgeObj.transform, font, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
            _pageCounterText.color = new Color(1f, 0.85f, 0.45f, 1f);
            SetStretch(_pageCounterText.rectTransform, 4f, 2f, 4f, 2f);

            // Narrative Caption Box (Bottom area of comic frame)
            GameObject captionBoxObj = new GameObject("NarrativeBox", typeof(RectTransform), typeof(Image));
            captionBoxObj.transform.SetParent(frameObj.transform, false);
            RectTransform captionRect = captionBoxObj.GetComponent<RectTransform>();
            captionRect.anchorMin = new Vector2(0.07f, 0.13f);
            captionRect.anchorMax = new Vector2(0.93f, 0.34f);
            captionRect.offsetMin = Vector2.zero;
            captionRect.offsetMax = Vector2.zero;

            Image captionImg = captionBoxObj.GetComponent<Image>();
            if (registry != null && registry.regularPaperSprite != null)
            {
                captionImg.sprite = registry.regularPaperSprite;
                captionImg.color = new Color(0.98f, 0.95f, 0.88f, 1f);
            }
            else
            {
                captionImg.color = new Color(0.93f, 0.88f, 0.76f, 0.98f);
            }

            _narrativeText = CreateTMPText("NarrativeText", captionBoxObj.transform, font, 20f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _narrativeText.color = new Color(0.16f, 0.11f, 0.07f, 1f);
            _narrativeText.textWrappingMode = TextWrappingModes.Normal;
            SetStretch(_narrativeText.rectTransform, 28f, 16f, 28f, 16f);

            // Next Button (Bottom Right)
            _nextButton = CreateWoodenButton(
                "NextSlideButton",
                frameObj.transform,
                new Vector2(0.70f, 0.03f),
                new Vector2(0.93f, 0.115f),
                "Lanjut [E / Spasi] ▶",
                font,
                registry,
                out _nextButtonLabel
            );

            // Skip Button (Bottom Left)
            _skipButton = CreateWoodenButton(
                "SkipCutsceneButton",
                frameObj.transform,
                new Vector2(0.07f, 0.03f),
                new Vector2(0.26f, 0.115f),
                "Lewati Cerita [Esc]",
                font,
                registry,
                out _skipButtonLabel
            );

            // Populate slide 0 preview so when toggled active in Edit Mode, user sees actual cutscene layout
            if (introSlides != null && introSlides.Length > 0)
            {
                _chapterTitleText.text = introSlides[0].chapterTitle;
                _pageCounterText.text = $"PANEL 1 / {introSlides.Length}";
                _narrativeText.text = introSlides[0].narrativeText;
                if (introSlides[0].artwork != null)
                {
                    _comicArtworkImage.sprite = introSlides[0].artwork;
                    _comicArtworkImage.color = Color.white;
                }
            }

            BindRuntimeButtons();
            _cutsceneRoot.SetActive(false);
        }

        private void BindExistingUIReferences()
        {
            if (_cutsceneRoot == null) return;
            if (_rootCanvasGroup == null) _rootCanvasGroup = _cutsceneRoot.GetComponent<CanvasGroup>();

            Transform frame = _cutsceneRoot.transform.Find("ComicBoardFrame");
            if (frame == null) return;

            if (_chapterTitleText == null)
                _chapterTitleText = frame.Find("ChapterHeader/ChapterTitleText")?.GetComponent<TextMeshProUGUI>();
            if (_comicArtworkImage == null)
                _comicArtworkImage = frame.Find("ComicPanelBorder/ComicPanelViewport/ComicArtwork")?.GetComponent<Image>();
            if (_comicArtworkRect == null && _comicArtworkImage != null)
                _comicArtworkRect = _comicArtworkImage.rectTransform;
            if (_pageCounterText == null)
                _pageCounterText = frame.Find("ComicPanelBorder/PageBadge/PageCounterText")?.GetComponent<TextMeshProUGUI>();
            if (_narrativeText == null)
                _narrativeText = frame.Find("NarrativeBox/NarrativeText")?.GetComponent<TextMeshProUGUI>();
            if (_nextButton == null)
                _nextButton = frame.Find("NextSlideButton")?.GetComponent<Button>();
            if (_nextButtonLabel == null && _nextButton != null)
                _nextButtonLabel = _nextButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (_skipButton == null)
                _skipButton = frame.Find("SkipCutsceneButton")?.GetComponent<Button>();
            if (_skipButtonLabel == null && _skipButton != null)
                _skipButtonLabel = _skipButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private void BindRuntimeButtons()
        {
            if (!Application.isPlaying || _listenersBound) return;
            _listenersBound = true;

            if (_nextButton != null)
            {
                _nextButton.onClick.RemoveListener(OnNextClicked);
                _nextButton.onClick.AddListener(OnNextClicked);
            }
            if (_skipButton != null)
            {
                _skipButton.onClick.RemoveListener(SkipCutscene);
                _skipButton.onClick.AddListener(SkipCutscene);
            }
        }

        private void Start()
        {
            if (playIntroOnStart && GameProgressionManager.Instance != null && !GameProgressionManager.Instance.HasWatchedIntro)
            {
                PlayIntroCutscene();
            }
        }

        private void Update()
        {
            if (!_isPlaying) return;

            // Subtle comic panel slow zoom
            if (_comicArtworkRect != null)
            {
                _slideZoomTimer += Time.deltaTime;
                float scale = 1.0f + Mathf.Clamp01(_slideZoomTimer / 7f) * 0.045f;
                _comicArtworkRect.localScale = new Vector3(scale, scale, 1f);
            }

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    SkipCutscene();
                    return;
                }

                if (kb.spaceKey.wasPressedThisFrame ||
                    kb.eKey.wasPressedThisFrame ||
                    kb.enterKey.wasPressedThisFrame ||
                    kb.rightArrowKey.wasPressedThisFrame)
                {
                    OnNextClicked();
                }
            }
        }

        public void PlayIntroCutscene(Action onComplete = null)
        {
            EnsureDefaultSlidesPopulated();
            PlaySequence(introSlides, () =>
            {
                if (GameProgressionManager.Instance != null)
                {
                    GameProgressionManager.Instance.MarkIntroWatched();
                }
                onComplete?.Invoke();
            });
        }

        public void PlayEndingCutscene(Action onComplete = null)
        {
            EnsureDefaultSlidesPopulated();
            PlaySequence(endingSlides, () =>
            {
                if (GameProgressionManager.Instance != null)
                {
                    GameProgressionManager.Instance.MarkHokiRestored();
                }
                onComplete?.Invoke();
            });
        }

        private static CutsceneSlide[] CreateDefaultIntroSlides(Sprite[] sprites)
        {
            return new[]
            {
                new CutsceneSlide
                {
                    chapterTitle = "BAGIAN 1: PULAU AETHERIA & DESA ELDORIA",
                    narrativeText =
                        "Di tengah lautan luas Pulau Aetheria, berdiri sebuah desa makmur bernama Desa Eldoria yang dipimpin oleh Kepala Desa Arvand. " +
                        "Selama ratusan tahun, kedamaian dan kesuburan desa tersebut terjaga berkat perlindungan sebuah artefak kuno.",
                    artwork = GetSafeSprite(sprites, 0)
                },
                new CutsceneSlide
                {
                    chapterTitle = "ARTEFAK HOKI – THE ANCIENT SEAL OF FORTUNE",
                    narrativeText =
                        "Artefak tersebut bernama HOKI—berbentuk dua angka keramat (6 dan 7) yang berdampingan di atas altar rumah suci di tengah Desa Eldoria. " +
                        "Selama Artefak HOKI tetap utuh, air tetap jernih dan penduduk terhindar dari segala bencana.",
                    artwork = GetSafeSprite(sprites, 1)
                },
                new CutsceneSlide
                {
                    chapterTitle = "BAGIAN 2: KEBANGKITAN IBLIS KUNO VORTHAK",
                    narrativeText =
                        "Namun pada suatu malam, iblis kuno bernama Vorthak yang tersegel di bawah altar berusaha membebaskan dirinya. " +
                        "Vorthak memanggil empat demon bawahannya untuk menghancurkan segel hingga Artefak HOKI retak dan terpecah menjadi empat kepingan!",
                    artwork = GetSafeSprite(sprites, 2)
                },
                new CutsceneSlide
                {
                    chapterTitle = "BAGIAN 3: EMPAT DEMON PENJAGA PECAHAN HOKI",
                    narrativeText =
                        "Keempat kepingan HOKI dibawa kabur ke penjuru pulau oleh empat demon: Ignaroth (Api), Glaciera (Es), Venomark (Racun), dan Nocthar (Bayangan). " +
                        "Kutukan pun menyebar—tanaman mengering, sungai keruh, dan Desa Eldoria berada di ambang kehancuran.",
                    artwork = GetSafeSprite(sprites, 3)
                },
                new CutsceneSlide
                {
                    chapterTitle = "BAGIAN 4: AWAL PERJALANAN MALIOS",
                    narrativeText =
                        "Kepala Desa Arvand meminta bantuan kepada Eldric di Desa Valoria, yang kemudian mengutus petualang pemberani bernama Malios. " +
                        "\"Temukan keempat demon, kalahkan mereka secara berurutan, dan kembalikan 4 pecahan HOKI ke altar Desa Eldoria!\"",
                    artwork = GetSafeSprite(sprites, 4)
                }
            };
        }

        private static CutsceneSlide[] CreateDefaultEndingSlides(Sprite[] sprites)
        {
            return new[]
            {
                new CutsceneSlide
                {
                    chapterTitle = "BABAK AKHIR: PECAHAN TERAKHIR HOKI",
                    narrativeText =
                        "Setelah pertarungan panjang di Kastil Bayangan, Malios akhirnya mengalahkan Nocthar! " +
                        "Kekuatan kegelapan perlahan menghilang dan pecahan keempat Artefak HOKI memancarkan cahaya misterius. Seluruh 4 kepingan kini telah terkumpul!",
                    artwork = GetSafeSprite(sprites, 0)
                },
                new CutsceneSlide
                {
                    chapterTitle = "MENYATUKAN KEMBALI EMPAT PECAHAN HOKI",
                    narrativeText =
                        "Malios kembali ke rumah suci Desa Eldoria. Di hadapan altar yang dipenuhi retakan, Kepala Desa Arvand memandu Malios " +
                        "menempatkan keempat pecahan HOKI kembali pada posisi aslinya hingga artefak kuno itu kembali utuh.",
                    artwork = GetSafeSprite(sprites, 1)
                },
                new CutsceneSlide
                {
                    chapterTitle = "KEBANGKITAN SIXSEVEN & PENYEGELAN VORTHAK",
                    narrativeText =
                        "Tiba-tiba tanah bergetar hebat dan Vorthak bangkit dari bawah altar! \"Malios! Jangan biarkan HOKI kehilangan cahayanya!\" seru Arvand. " +
                        "Malios mengerahkan kekuatan SIXSEVEN—dua energi angka 6 dan 7 menyatu dengan cahaya HOKI dan menyegel Vorthak kembali ke dalam kegelapan!",
                    artwork = GetSafeSprite(sprites, 2)
                },
                new CutsceneSlide
                {
                    chapterTitle = "THE GUARDIAN OF HOKI – KEDAMAIAN ELDORIA",
                    narrativeText =
                        "Kutukan di Desa Eldoria pun sirna. Seluruh penduduk mengadakan perayaan dan Kepala Desa Arvand memberikan medali kehormatan kepada Malios: " +
                        "\"Berkat keberanianmu, Artefak HOKI kembali bersinar menjaga keseimbangan Pulau Aetheria!\"",
                    artwork = GetSafeSprite(sprites, 3)
                }
            };
        }

        public void PlaySequence(CutsceneSlide[] slides, Action onComplete = null)
        {
            if (slides == null || slides.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }

            if (_cutsceneRoot == null)
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null) InitializeUI(canvas.transform);
            }
            else
            {
                BindExistingUIReferences();
                BindRuntimeButtons();
            }

            _activeSlides = slides;
            _currentSlideIndex = 0;
            _onCutsceneComplete = onComplete;
            _isPlaying = true;

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.IsMovementLocked = true;
            }

            _cutsceneRoot.SetActive(true);
            _cutsceneRoot.transform.SetAsLastSibling();
            ShowSlide(_currentSlideIndex);
        }

        private void ShowSlide(int index)
        {
            if (_activeSlides == null || index < 0 || index >= _activeSlides.Length) return;

            CutsceneSlide slide = _activeSlides[index];
            if (_chapterTitleText != null) _chapterTitleText.text = slide.chapterTitle;
            if (_pageCounterText != null) _pageCounterText.text = $"PANEL {index + 1} / {_activeSlides.Length}";

            if (_comicArtworkImage != null)
            {
                _comicArtworkImage.sprite = slide.artwork;
                _comicArtworkImage.color = slide.artwork != null ? Color.white : new Color(0.2f, 0.15f, 0.25f, 1f);
            }

            _slideZoomTimer = 0f;
            if (_comicArtworkRect != null)
            {
                _comicArtworkRect.localScale = Vector3.one;
            }

            bool isLast = (index == _activeSlides.Length - 1);
            if (_nextButtonLabel != null)
            {
                _nextButtonLabel.text = isLast ? "Mulai Petualangan [E] ✔" : "Panel Berikutnya [E] ▶";
            }

            _fullSlideText = slide.narrativeText ?? string.Empty;
            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            _typingCoroutine = StartCoroutine(TypewriterRoutine(_fullSlideText));
        }

        private IEnumerator TypewriterRoutine(string text)
        {
            _isTyping = true;
            if (_narrativeText != null) _narrativeText.text = string.Empty;

            for (int i = 0; i < text.Length; i++)
            {
                if (_narrativeText != null) _narrativeText.text = text.Substring(0, i + 1);
                yield return new WaitForSecondsRealtime(typewriterCharDelay);
            }

            if (_narrativeText != null) _narrativeText.text = text;
            _isTyping = false;
        }

        private void OnNextClicked()
        {
            if (!_isPlaying) return;

            if (_isTyping)
            {
                if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
                if (_narrativeText != null) _narrativeText.text = _fullSlideText;
                _isTyping = false;
                return;
            }

            _currentSlideIndex++;
            if (_activeSlides != null && _currentSlideIndex < _activeSlides.Length)
            {
                ShowSlide(_currentSlideIndex);
            }
            else
            {
                FinishCutscene();
            }
        }

        public void SkipCutscene()
        {
            if (!_isPlaying) return;
            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            _isTyping = false;
            FinishCutscene();
        }

        private void FinishCutscene()
        {
            _isPlaying = false;
            if (_cutsceneRoot != null) _cutsceneRoot.SetActive(false);

            if (PlayerController.Instance != null &&
                (DialogueSystem.Instance == null || !DialogueSystem.Instance.IsDialogueOpen))
            {
                PlayerController.Instance.IsMovementLocked = false;
            }

            Action callback = _onCutsceneComplete;
            _onCutsceneComplete = null;
            callback?.Invoke();
        }

        private static Sprite GetSafeSprite(Sprite[] array, int index)
        {
            if (array == null || index < 0 || index >= array.Length) return null;
            return array[index];
        }

        private static TextMeshProUGUI CreateTMPText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateWoodenButton(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string label,
            TMP_FontAsset font,
            GameAssetRegistry registry,
            out TextMeshProUGUI labelText)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = btnObj.GetComponent<Image>();
            if (registry != null && registry.buttonNormalSprite != null)
            {
                img.sprite = registry.buttonNormalSprite;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.45f, 0.28f, 0.12f, 1f);
            }

            Button btn = btnObj.GetComponent<Button>();
            if (registry != null && registry.buttonPressedSprite != null)
            {
                SpriteState st = new SpriteState
                {
                    pressedSprite = registry.buttonPressedSprite,
                    highlightedSprite = registry.buttonNormalSprite
                };
                btn.transition = Selectable.Transition.SpriteSwap;
                btn.spriteState = st;
            }

            labelText = CreateTMPText("Label", btnObj.transform, font, 17f, FontStyles.Bold, TextAlignmentOptions.Center);
            labelText.color = new Color(0.98f, 0.93f, 0.78f, 1f);
            SetStretch(labelText.rectTransform, 8f, 4f, 8f, 4f);
            labelText.text = label;

            return btn;
        }

        private static void SetStretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
