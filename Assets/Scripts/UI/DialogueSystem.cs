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
    public class DialogueLine
    {
        public string speakerName;
        [TextArea(2, 5)]
        public string text;
        public Sprite portrait;
        public bool isPlayerSpeaking;
    }

    public class DialogueSystem : MonoBehaviour
    {
        public static DialogueSystem Instance { get; private set; }

        [Header("Font & Dialogue Settings (Bisa Diedit di Inspector)")]
        [SerializeField] private TMP_FontAsset customDialogueFont;
        [SerializeField] private float charDelay = 0.02f;

        [Header("Dialogue UI References (LuckyNumber_UICanvas/DialogueSystemPanel)")]
        [SerializeField] private GameObject _dialoguePanel;
        [SerializeField] private Image _portraitImage;
        [SerializeField] private TextMeshProUGUI _speakerNameText;
        [SerializeField] private TextMeshProUGUI _dialogueBodyText;
        [SerializeField] private TextMeshProUGUI _continueHintText;
        [SerializeField] private Button _nextLineButton;

        [Header("HUD & Prompt References (LuckyNumber_UICanvas)")]
        [SerializeField] private TextMeshProUGUI _hudZoneText;
        [SerializeField] private TextMeshProUGUI _hudHokiProgressText;
        [SerializeField] private TextMeshProUGUI _hudObjectiveText;
        [SerializeField] private GameObject _interactionPromptBar;
        [SerializeField] private TextMeshProUGUI _interactionPromptText;

        private DialogueLine[] _currentLines;
        private int _lineIndex;
        private bool _isOpen;
        private bool _isTyping;
        private string _currentFullText;
        private Coroutine _typingRoutine;
        private Action _onDialogueFinished;
        private float _openFrameTime;
        private bool _listenersBound;

        public bool IsDialogueOpen => _isOpen;

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
            customDialogueFont = fontAsset;
            if (_speakerNameText != null) _speakerNameText.font = fontAsset;
            if (_dialogueBodyText != null) _dialogueBodyText.font = fontAsset;
            if (_continueHintText != null) _continueHintText.font = fontAsset;
            if (_hudZoneText != null) _hudZoneText.font = fontAsset;
            if (_hudHokiProgressText != null) _hudHokiProgressText.font = fontAsset;
            if (_hudObjectiveText != null) _hudObjectiveText.font = fontAsset;
            if (_interactionPromptText != null) _interactionPromptText.font = fontAsset;
        }

        public void InitializeUI(Transform canvasTransform)
        {
            var registry = GameAssetRegistry.Instance;
            TMP_FontAsset font = registry != null
                ? registry.GetPreferredFont(customDialogueFont)
                : customDialogueFont ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (_dialoguePanel == null && canvasTransform != null)
            {
                Transform existingPanel = canvasTransform.Find("DialogueSystemPanel");
                if (existingPanel != null) _dialoguePanel = existingPanel.gameObject;
            }

            if (_dialoguePanel != null)
            {
                BindExistingUIReferences(canvasTransform);
                if (customDialogueFont != null)
                {
                    ApplyFont(customDialogueFont);
                }
                else if (_dialogueBodyText != null && _dialogueBodyText.font != null)
                {
                    customDialogueFont = _dialogueBodyText.font;
                }
                BindRuntimeButtons();
                if (Application.isPlaying)
                {
                    if (!_isOpen) _dialoguePanel.SetActive(false);
                    if (_interactionPromptBar != null) _interactionPromptBar.SetActive(false);
                    if (GameProgressionManager.Instance != null)
                    {
                        GameProgressionManager.Instance.OnProgressionChanged -= RefreshHUD;
                        GameProgressionManager.Instance.OnProgressionChanged += RefreshHUD;
                    }
                    RefreshHUD();
                }
                return;
            }

            if (canvasTransform == null) return;

            CreateMainHUD(canvasTransform, registry, font);
            CreateInteractionPromptUI(canvasTransform, registry, font);
            CreateDialogueBoxUI(canvasTransform, registry, font);
            BindRuntimeButtons();

            if (GameProgressionManager.Instance != null)
            {
                GameProgressionManager.Instance.OnProgressionChanged -= RefreshHUD;
                GameProgressionManager.Instance.OnProgressionChanged += RefreshHUD;
            }
            RefreshHUD();
        }

        private void BindExistingUIReferences(Transform canvasTransform)
        {
            if (canvasTransform != null)
            {
                Transform hudBoard = canvasTransform.Find("MainHUD_QuestBoard");
                if (hudBoard != null)
                {
                    if (_hudZoneText == null) _hudZoneText = hudBoard.Find("HUD_ZoneText")?.GetComponent<TextMeshProUGUI>();
                    if (_hudHokiProgressText == null) _hudHokiProgressText = hudBoard.Find("HUD_HokiText")?.GetComponent<TextMeshProUGUI>();
                    if (_hudObjectiveText == null) _hudObjectiveText = hudBoard.Find("HUD_ObjectiveText")?.GetComponent<TextMeshProUGUI>();
                }

                Transform replayBtn = canvasTransform.Find("ReplayIntroCutsceneBtn");
                if (replayBtn != null)
                {
                    if (Application.isPlaying) Destroy(replayBtn.gameObject);
                    else DestroyImmediate(replayBtn.gameObject);
                }

                Transform promptBar = canvasTransform.Find("InteractionPromptBar");
                if (promptBar != null)
                {
                    _interactionPromptBar = promptBar.gameObject;
                    if (_interactionPromptText == null) _interactionPromptText = promptBar.Find("PromptText")?.GetComponent<TextMeshProUGUI>();
                }
            }

            if (_dialoguePanel != null)
            {
                Transform dp = _dialoguePanel.transform;
                if (_speakerNameText == null) _speakerNameText = dp.Find("SpeakerRibbon/SpeakerName")?.GetComponent<TextMeshProUGUI>();
                if (_portraitImage == null) _portraitImage = dp.Find("PortraitFrame/SpeakerPortrait")?.GetComponent<Image>();
                if (_dialogueBodyText == null) _dialogueBodyText = dp.Find("DialogueBodyText")?.GetComponent<TextMeshProUGUI>();
                if (_nextLineButton == null) _nextLineButton = dp.Find("NextDialogueButton")?.GetComponent<Button>();
                if (_continueHintText == null && _nextLineButton != null)
                    _continueHintText = _nextLineButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        private void BindRuntimeButtons()
        {
            if (!Application.isPlaying || _listenersBound) return;
            _listenersBound = true;

            if (_nextLineButton != null)
            {
                _nextLineButton.onClick.RemoveListener(AdvanceOrCompleteLine);
                _nextLineButton.onClick.AddListener(AdvanceOrCompleteLine);
            }
        }

        private void OnDestroy()
        {
            if (GameProgressionManager.Instance != null)
            {
                GameProgressionManager.Instance.OnProgressionChanged -= RefreshHUD;
            }
        }

        private void CreateMainHUD(Transform canvasTransform, GameAssetRegistry registry, TMP_FontAsset font)
        {
            // Top-Left Quest & HOKI Fragment Tracker (Wooden Board + Paper)
            GameObject hudBoard = new GameObject("MainHUD_QuestBoard", typeof(RectTransform), typeof(Image));
            hudBoard.transform.SetParent(canvasTransform, false);
            RectTransform hudRect = hudBoard.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0.015f, 0.77f);
            hudRect.anchorMax = new Vector2(0.35f, 0.98f);
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            Image hudImg = hudBoard.GetComponent<Image>();
            if (registry != null && registry.smallBoardSprite != null)
            {
                hudImg.sprite = registry.smallBoardSprite;
                hudImg.color = Color.white;
            }
            else
            {
                hudImg.color = new Color(0.22f, 0.15f, 0.09f, 0.94f);
            }
            hudImg.raycastTarget = false;

            _hudZoneText = CreateTMP("HUD_ZoneText", hudBoard.transform, font, 16f, FontStyles.Bold, TextAlignmentOptions.Left);
            _hudZoneText.color = new Color(0.32f, 0.18f, 0.06f, 1f);
            _hudZoneText.text = "Lokasi: Desa Eldoria";
            _hudZoneText.rectTransform.anchorMin = new Vector2(0.08f, 0.70f);
            _hudZoneText.rectTransform.anchorMax = new Vector2(0.92f, 0.92f);
            _hudZoneText.rectTransform.offsetMin = Vector2.zero;
            _hudZoneText.rectTransform.offsetMax = Vector2.zero;

            _hudHokiProgressText = CreateTMP("HUD_HokiText", hudBoard.transform, font, 15f, FontStyles.Bold, TextAlignmentOptions.Left);
            _hudHokiProgressText.color = new Color(0.58f, 0.22f, 0.04f, 1f);
            _hudHokiProgressText.text = "Kepingan HOKI: 0 / 4";
            _hudHokiProgressText.rectTransform.anchorMin = new Vector2(0.08f, 0.48f);
            _hudHokiProgressText.rectTransform.anchorMax = new Vector2(0.92f, 0.70f);
            _hudHokiProgressText.rectTransform.offsetMin = Vector2.zero;
            _hudHokiProgressText.rectTransform.offsetMax = Vector2.zero;

            _hudObjectiveText = CreateTMP("HUD_ObjectiveText", hudBoard.transform, font, 14f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _hudObjectiveText.color = new Color(0.18f, 0.12f, 0.06f, 1f);
            _hudObjectiveText.textWrappingMode = TextWrappingModes.Normal;
            _hudObjectiveText.text = "Misi: Temui Kepala Desa Arvand di tengah Desa Eldoria [Tekan E]";
            _hudObjectiveText.rectTransform.anchorMin = new Vector2(0.08f, 0.08f);
            _hudObjectiveText.rectTransform.anchorMax = new Vector2(0.92f, 0.48f);
            _hudObjectiveText.rectTransform.offsetMin = Vector2.zero;
            _hudObjectiveText.rectTransform.offsetMax = Vector2.zero;
        }

        private void CreateInteractionPromptUI(Transform canvasTransform, GameAssetRegistry registry, TMP_FontAsset font)
        {
            _interactionPromptBar = new GameObject("InteractionPromptBar", typeof(RectTransform), typeof(Image));
            _interactionPromptBar.transform.SetParent(canvasTransform, false);
            RectTransform rt = _interactionPromptBar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.32f, 0.04f);
            rt.anchorMax = new Vector2(0.68f, 0.105f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = _interactionPromptBar.GetComponent<Image>();
            if (registry != null && registry.titleBannerSprite != null)
            {
                bg.sprite = registry.titleBannerSprite;
                bg.color = Color.white;
            }
            else
            {
                bg.color = new Color(0.22f, 0.14f, 0.08f, 0.92f);
            }
            bg.raycastTarget = false;

            _interactionPromptText = CreateTMP("PromptText", _interactionPromptBar.transform, font, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
            _interactionPromptText.color = new Color(1f, 0.94f, 0.72f, 1f);
            _interactionPromptText.text = "[E] Bicara dengan Kepala Desa Arvand";
            SetStretch(_interactionPromptText.rectTransform, 14f, 4f, 14f, 6f);

            _interactionPromptBar.SetActive(false);
        }

        private void CreateDialogueBoxUI(Transform canvasTransform, GameAssetRegistry registry, TMP_FontAsset font)
        {
            _dialoguePanel = new GameObject("DialogueSystemPanel", typeof(RectTransform), typeof(Image));
            _dialoguePanel.transform.SetParent(canvasTransform, false);
            RectTransform panelRect = _dialoguePanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.12f, 0.025f);
            panelRect.anchorMax = new Vector2(0.88f, 0.32f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImg = _dialoguePanel.GetComponent<Image>();
            if (registry != null && registry.dialogueBoardSprite != null)
            {
                panelImg.sprite = registry.dialogueBoardSprite;
                panelImg.color = Color.white;
            }
            else
            {
                panelImg.color = new Color(0.25f, 0.16f, 0.09f, 0.97f);
            }

            // Speaker Name Ribbon Header
            GameObject ribbonObj = new GameObject("SpeakerRibbon", typeof(RectTransform), typeof(Image));
            ribbonObj.transform.SetParent(_dialoguePanel.transform, false);
            RectTransform ribbonRect = ribbonObj.GetComponent<RectTransform>();
            ribbonRect.anchorMin = new Vector2(0.23f, 0.80f);
            ribbonRect.anchorMax = new Vector2(0.62f, 1.06f);
            ribbonRect.offsetMin = Vector2.zero;
            ribbonRect.offsetMax = Vector2.zero;

            Image ribbonImg = ribbonObj.GetComponent<Image>();
            if (registry != null && registry.bigRibbonSprite != null)
            {
                ribbonImg.sprite = registry.bigRibbonSprite;
                ribbonImg.color = Color.white;
            }
            else if (registry != null && registry.titleBannerSprite != null)
            {
                ribbonImg.sprite = registry.titleBannerSprite;
                ribbonImg.color = Color.white;
            }
            else
            {
                ribbonImg.color = new Color(0.45f, 0.26f, 0.10f, 1f);
            }

            _speakerNameText = CreateTMP("SpeakerName", ribbonObj.transform, font, 20f, FontStyles.Bold, TextAlignmentOptions.Center);
            _speakerNameText.color = new Color(1f, 0.95f, 0.78f, 1f);
            _speakerNameText.text = "Kepala Desa Arvand";
            SetStretch(_speakerNameText.rectTransform, 14f, 6f, 14f, 8f);

            // Portrait Box (Left side of wooden board)
            GameObject portraitFrameObj = new GameObject("PortraitFrame", typeof(RectTransform), typeof(Image));
            portraitFrameObj.transform.SetParent(_dialoguePanel.transform, false);
            RectTransform pfRect = portraitFrameObj.GetComponent<RectTransform>();
            pfRect.anchorMin = new Vector2(0.035f, 0.12f);
            pfRect.anchorMax = new Vector2(0.21f, 0.88f);
            pfRect.offsetMin = Vector2.zero;
            pfRect.offsetMax = Vector2.zero;

            Image pfImg = portraitFrameObj.GetComponent<Image>();
            if (registry != null && registry.stoneBoardSprite != null)
            {
                pfImg.sprite = registry.stoneBoardSprite;
                pfImg.color = Color.white;
            }
            else
            {
                pfImg.color = new Color(0.18f, 0.13f, 0.09f, 0.95f);
            }

            GameObject portraitObj = new GameObject("SpeakerPortrait", typeof(RectTransform), typeof(Image));
            portraitObj.transform.SetParent(portraitFrameObj.transform, false);
            RectTransform pRect = portraitObj.GetComponent<RectTransform>();
            SetStretch(pRect, 10f, 10f, 10f, 10f);
            _portraitImage = portraitObj.GetComponent<Image>();
            _portraitImage.preserveAspect = true;
            _portraitImage.raycastTarget = false;
            if (registry != null && registry.arvandSprites != null && registry.arvandSprites.Length > 0)
            {
                _portraitImage.sprite = registry.arvandSprites[0];
            }

            // Dialogue Body Text
            _dialogueBodyText = CreateTMP("DialogueBodyText", _dialoguePanel.transform, font, 20f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _dialogueBodyText.color = new Color(0.20f, 0.12f, 0.06f, 1f);
            _dialogueBodyText.textWrappingMode = TextWrappingModes.Normal;
            _dialogueBodyText.text = "Syukurlah kau tiba, Malios! Empat demon Vorthak telah mencuri pecahan Artefak HOKI dari Desa Eldoria.";
            _dialogueBodyText.rectTransform.anchorMin = new Vector2(0.24f, 0.24f);
            _dialogueBodyText.rectTransform.anchorMax = new Vector2(0.94f, 0.77f);
            _dialogueBodyText.rectTransform.offsetMin = Vector2.zero;
            _dialogueBodyText.rectTransform.offsetMax = Vector2.zero;

            // Next Button (Bottom Right of Dialogue Panel)
            GameObject nextBtnObj = new GameObject("NextDialogueButton", typeof(RectTransform), typeof(Image), typeof(Button));
            nextBtnObj.transform.SetParent(_dialoguePanel.transform, false);
            RectTransform nextRect = nextBtnObj.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(0.72f, 0.07f);
            nextRect.anchorMax = new Vector2(0.95f, 0.25f);
            nextRect.offsetMin = Vector2.zero;
            nextRect.offsetMax = Vector2.zero;

            Image nextImg = nextBtnObj.GetComponent<Image>();
            if (registry != null && registry.buttonNormalSprite != null)
            {
                nextImg.sprite = registry.buttonNormalSprite;
                nextImg.color = Color.white;
            }
            else
            {
                nextImg.color = new Color(0.42f, 0.26f, 0.12f, 1f);
            }

            _continueHintText = CreateTMP("NextLabel", nextBtnObj.transform, font, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
            _continueHintText.color = new Color(1f, 0.94f, 0.76f, 1f);
            _continueHintText.text = "Lanjut [E] ▶";
            SetStretch(_continueHintText.rectTransform, 6f, 2f, 6f, 2f);

            _nextLineButton = nextBtnObj.GetComponent<Button>();

            _dialoguePanel.SetActive(false);
        }

        private void Update()
        {
            UpdateInteractionPrompt();

            if (!_isOpen) return;

            Keyboard kb = Keyboard.current;
            // Prevent the exact same frame 'E' press that opened the dialogue from skipping line 0
            if (Time.unscaledTime - _openFrameTime < 0.12f) return;

            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                AdvanceOrCompleteLine();
            }
        }

        private void UpdateInteractionPrompt()
        {
            if (_interactionPromptBar == null) return;

            bool shouldShow = !_isOpen &&
                              (CutsceneManager.Instance == null || !CutsceneManager.Instance.IsPlaying) &&
                              PlayerController.Instance != null &&
                              !PlayerController.Instance.IsMovementLocked &&
                              PlayerController.Instance.NearestInteractable != null;

            if (shouldShow)
            {
                _interactionPromptBar.SetActive(true);
                if (_interactionPromptText != null)
                {
                    _interactionPromptText.text = PlayerController.Instance.NearestInteractable.InteractionPrompt;
                }
            }
            else if (_interactionPromptBar.activeSelf)
            {
                _interactionPromptBar.SetActive(false);
            }
        }

        public void StartDialogue(DialogueLine[] lines, Action onFinished = null)
        {
            if (lines == null || lines.Length == 0)
            {
                onFinished?.Invoke();
                return;
            }

            if (_dialoguePanel == null)
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null) InitializeUI(canvas.transform);
            }
            else
            {
                BindExistingUIReferences(_dialoguePanel.transform.parent);
                BindRuntimeButtons();
            }

            _currentLines = lines;
            _lineIndex = 0;
            _onDialogueFinished = onFinished;
            _isOpen = true;
            _openFrameTime = Time.unscaledTime;

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.IsMovementLocked = true;
            }

            _dialoguePanel.SetActive(true);
            _dialoguePanel.transform.SetAsLastSibling();
            DisplayCurrentLine();
        }

        private void DisplayCurrentLine()
        {
            DialogueLine line = _currentLines[_lineIndex];
            if (_speakerNameText != null)
            {
                _speakerNameText.text = string.IsNullOrEmpty(line.speakerName) ? "NPC" : line.speakerName;
            }

            if (_portraitImage != null)
            {
                Sprite portrait = line.portrait;
                if (portrait == null && GameAssetRegistry.Instance != null)
                {
                    var reg = GameAssetRegistry.Instance;
                    if (line.isPlayerSpeaking && reg.maliosSprites != null && reg.maliosSprites.Length > 0)
                        portrait = reg.maliosSprites[0];
                    else if (!line.isPlayerSpeaking && reg.arvandSprites != null && reg.arvandSprites.Length > 0)
                        portrait = reg.arvandSprites[0];
                }

                _portraitImage.sprite = portrait;
                _portraitImage.color = portrait != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            bool isLast = (_lineIndex == _currentLines.Length - 1);
            if (_continueHintText != null)
            {
                _continueHintText.text = isLast ? "Selesai [E] ✔" : "Lanjut [E] ▶";
            }

            _currentFullText = line.text ?? string.Empty;
            if (_typingRoutine != null) StopCoroutine(_typingRoutine);
            _typingRoutine = StartCoroutine(TypeLineRoutine(_currentFullText));
        }

        private IEnumerator TypeLineRoutine(string text)
        {
            _isTyping = true;
            if (_dialogueBodyText != null) _dialogueBodyText.text = string.Empty;

            for (int i = 0; i < text.Length; i++)
            {
                if (_dialogueBodyText != null) _dialogueBodyText.text = text.Substring(0, i + 1);
                yield return new WaitForSecondsRealtime(charDelay);
            }

            if (_dialogueBodyText != null) _dialogueBodyText.text = text;
            _isTyping = false;
        }

        private void AdvanceOrCompleteLine()
        {
            if (!_isOpen) return;

            if (_isTyping)
            {
                if (_typingRoutine != null) StopCoroutine(_typingRoutine);
                if (_dialogueBodyText != null) _dialogueBodyText.text = _currentFullText;
                _isTyping = false;
                return;
            }

            _lineIndex++;
            if (_currentLines != null && _lineIndex < _currentLines.Length)
            {
                DisplayCurrentLine();
            }
            else
            {
                CloseDialogue();
            }
        }

        public void CloseDialogue()
        {
            _isOpen = false;
            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);

            if (PlayerController.Instance != null &&
                (CutsceneManager.Instance == null || !CutsceneManager.Instance.IsPlaying))
            {
                PlayerController.Instance.IsMovementLocked = false;
            }

            RefreshHUD();

            Action callback = _onDialogueFinished;
            _onDialogueFinished = null;
            callback?.Invoke();
        }

        public void RefreshHUD()
        {
            var prog = GameProgressionManager.Instance;
            if (prog == null) return;

            if (_hudZoneText != null)
            {
                _hudZoneText.text = $"Lokasi: {GameProgressionManager.GetZoneDisplayName(prog.CurrentZone)}";
            }

            if (_hudHokiProgressText != null)
            {
                string sixSevenTag = prog.HasSixSevenUltimate ? " | Ultimate: SIXSEVEN Aktif" : "";
                _hudHokiProgressText.text = $"Kepingan HOKI: {prog.HokiFragmentsCollected} / 4{sixSevenTag}";
            }

            if (_hudObjectiveText != null)
            {
                _hudObjectiveText.text = $"Misi: {prog.GetCurrentQuestObjective()}";
            }
        }

        private static TextMeshProUGUI CreateTMP(
            string name,
            Transform parent,
            TMP_FontAsset font,
            float size,
            FontStyles style,
            TextAlignmentOptions align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
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
