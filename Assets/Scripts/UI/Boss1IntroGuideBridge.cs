using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using LuckyNumber.Core;

namespace LuckyNumber.UI
{
    /// <summary>
    /// Automatically initializes whenever "Bos1 Test" is loaded (via TestMap transition or directly in Editor):
    /// 1) Plays fade-in from black and presents the Stage 1 (Ignaroth) Card Battle Guide overlay.
    /// 2) Renders visual Attack FX when Malios solves a Lucky Number pair, and Damage FX when Ignaroth strikes Malios.
    /// 3) Handles returning cleanly to TestMap.
    /// </summary>
    public class Boss1IntroGuideBridge : MonoBehaviour
    {
        private const string Boss1SceneName = "Bos1 Test";
        private const string TestMapSceneName = "TestMap";

        public static Boss1IntroGuideBridge Instance { get; private set; }

        private static bool _sceneHookRegistered;
        private static bool _skipGuideOnNextRestart;
        private static Sprite _proceduralGlowSprite;

        private static Sprite[] _cachedFireFrames;
        private static Sprite[] _cachedExplosionFrames;
        private static Sprite[] _cachedSlashFrames;
        private static Sprite[] _cachedShockwaveFrames;
        private static Sprite[] _cachedBlastFrames;
        private static Sprite[] _cachedCritFrames;

        private Canvas _overlayCanvas;
        private Transform _fxLayer;
        private Image _damageVignetteImg;

        private GameObject _fadeObj;
        private CanvasGroup _fadeGroup;
        private GameObject _guideOverlayObj;
        private CanvasGroup _guideGroup;
        private GameObject _reopenHelpBtnObj;

        private RectTransform _mcRect;
        private Image _mcImage;
        private Vector2 _mcBasePos;
        private bool _hasMcBasePos;

        private RectTransform _bosRect;
        private Image _bosImage;
        private Vector2 _bosBasePos;
        private Vector3 _bosBaseScale = Vector3.one;
        private bool _hasBosBasePos;

        private RectTransform _playerHpBarRect;
        private Vector2 _playerHpBarBasePos;
        private bool _hasPlayerHpBasePos;

        private RectTransform _bossHpBarRect;
        private Vector2 _bossHpBarBasePos;
        private bool _hasBossHpBasePos;

        private static readonly string[] PunchyHitWords =
        {
            "BOOM!",
            "DUARRR!",
            "KABOOM!",
            "BLAM!"
        };

        private readonly List<RectTransform> _battleCanvasChildren = new List<RectTransform>();
        private Vector2 _appliedCanvasShake;
        private Coroutine _screenShakeRoutine;

        private bool _isBuilt;
        private bool _isGuideOpen;
        private bool _isReturning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadHook()
        {
            if (_sceneHookRegistered) return;
            _sceneHookRegistered = true;
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
        }

        public static bool IsBattleScene(string sceneName)
        {
            return sceneName == "Bos1 Test" || sceneName == "Bos1" || sceneName == "Bos2" || sceneName == "Bos3" || sceneName == "Bos4";
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.IsValid() && IsBattleScene(scene.name))
            {
                EnsureForActiveScene();
            }
        }

        public static Boss1IntroGuideBridge EnsureForActiveScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !IsBattleScene(active.name)) return null;

            if (Instance != null)
            {
                Instance.EnsureInitialized();
                return Instance;
            }

            Boss1IntroGuideBridge existing = FindFirstObjectByType<Boss1IntroGuideBridge>();
            if (existing != null)
            {
                existing.EnsureInitialized();
                return existing;
            }

            GameObject go = new GameObject("BossBattle_VFXBridge");
            Boss1IntroGuideBridge created = go.AddComponent<Boss1IntroGuideBridge>();
            created.EnsureInitialized();
            return created;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void EnsureInitialized()
        {
            if (_isBuilt) return;
            _isBuilt = true;

            PrewarmBattleFXSprites();
            CacheBattleSceneActors();
            BuildOverlayCanvasAndGuideUI();
            StartCoroutine(PrewarmGpuTexturesRoutine());
            StartCoroutine(IntroTransitionSequence());
        }

        private static void PrewarmBattleFXSprites()
        {
            GetFireFrames();
            GetExplosionFrames();
            GetVfxPackFrames(VfxPackLayer.Slash);
            GetVfxPackFrames(VfxPackLayer.Shockwave);
            GetVfxPackFrames(VfxPackLayer.Blast);
            GetVfxPackFrames(VfxPackLayer.CritCross);
            GetOrCreateGlowSprite();
        }

        private IEnumerator PrewarmGpuTexturesRoutine()
        {
            if (_fxLayer == null) yield break;

            Sprite[][] groups =
            {
                _cachedFireFrames,
                _cachedExplosionFrames,
                _cachedSlashFrames,
                _cachedShockwaveFrames,
                _cachedBlastFrames,
                _cachedCritFrames
            };

            List<GameObject> tempWarmers = new List<GameObject>(groups.Length);
            for (int i = 0; i < groups.Length; i++)
            {
                Sprite[] arr = groups[i];
                if (arr == null || arr.Length == 0 || arr[0] == null) continue;

                GameObject go = new GameObject("VFXGpuPrewarm", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_fxLayer, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(2f, 2f);
                rt.anchoredPosition = Vector2.zero;

                Image img = go.GetComponent<Image>();
                img.sprite = arr[0];
                img.color = new Color(1f, 1f, 1f, 0.004f);
                img.raycastTarget = false;
                tempWarmers.Add(go);
            }

            yield return null;
            yield return null;

            for (int i = 0; i < tempWarmers.Count; i++)
            {
                if (tempWarmers[i] != null) Destroy(tempWarmers[i]);
            }
        }

        private void Update()
        {
            if (!_isGuideOpen || _isReturning) return;

            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame ||
                               kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
            {
                CloseGuide();
            }
        }

        public static void MarkRestartNextLoad()
        {
            _skipGuideOnNextRestart = true;
            Time.timeScale = 1f;
        }

        public static void SetBattleEndedUI()
        {
            if (Instance != null)
            {
                Instance.HideBattleOverlaysForEndPanel();
            }
        }

        public static void ReturnToMapFromBoss1(bool bossDefeated)
        {
            _skipGuideOnNextRestart = false;
            Time.timeScale = 1f;
            if (Instance != null)
            {
                Instance.StartReturnRoutine(bossDefeated);
            }
            else
            {
                GameProgressionManager.CompleteBossBattleAndPrepareReturn(0, bossDefeated);
                SceneManager.LoadScene(TestMapSceneName);
            }
        }

        private void HideBattleOverlaysForEndPanel()
        {
            if (_guideOverlayObj != null) _guideOverlayObj.SetActive(false);
            if (_reopenHelpBtnObj != null) _reopenHelpBtnObj.SetActive(false);
            _isGuideOpen = false;
        }

        public static void PlayPlayerAttackEffect(bool brokePillarOrBossHp, System.Action onImpact = null)
        {
            Boss1IntroGuideBridge bridge = EnsureForActiveScene();
            if (bridge != null)
            {
                bridge.StartCoroutine(bridge.PlayerAttackRoutine(brokePillarOrBossHp, onImpact));
            }
        }

        public static void PlayBossAttackOnPlayerEffect(System.Action onImpact = null)
        {
            Boss1IntroGuideBridge bridge = EnsureForActiveScene();
            if (bridge != null)
            {
                bridge.StartCoroutine(bridge.BossAttackOnPlayerRoutine(onImpact));
            }
        }

        private void StartReturnRoutine(bool bossDefeated)
        {
            if (_isReturning) return;
            StartCoroutine(ReturnToTestMapRoutine(bossDefeated));
        }

        private void CacheBattleSceneActors()
        {
            GameObject mcObj = GameObject.Find("MC");
            if (mcObj == null) mcObj = GameObject.Find("KarakterMC");
            if (mcObj == null) mcObj = GameObject.Find("Player");
            if (mcObj != null)
            {
                _mcRect = mcObj.GetComponent<RectTransform>();
                _mcImage = mcObj.GetComponent<Image>();
                if (_mcRect != null)
                {
                    _mcBasePos = _mcRect.anchoredPosition;
                    _hasMcBasePos = true;
                }
            }

            GameObject bosObj = GameObject.Find("BOS");
            if (bosObj == null) bosObj = GameObject.Find("KarakterBOS");
            if (bosObj == null) bosObj = GameObject.Find("Boss");
            if (bosObj != null)
            {
                _bosRect = bosObj.GetComponent<RectTransform>();
                _bosImage = bosObj.GetComponent<Image>();
                if (_bosRect != null)
                {
                    _bosBasePos = _bosRect.anchoredPosition;
                    _bosBaseScale = _bosRect.localScale;
                    _hasBosBasePos = true;
                }
            }

            GameObject hpBarObj = GameObject.Find("PlayerHealthBar");
            if (hpBarObj == null) hpBarObj = GameObject.Find("PlayerHeartPanel");
            if (hpBarObj == null) hpBarObj = GameObject.Find("PlayerHP");
            if (hpBarObj == null) hpBarObj = GameObject.Find("PlayerHeart");
            if (hpBarObj != null)
            {
                _playerHpBarRect = hpBarObj.GetComponent<RectTransform>();
                if (_playerHpBarRect != null)
                {
                    _playerHpBarBasePos = _playerHpBarRect.anchoredPosition;
                    _hasPlayerHpBasePos = true;
                }
            }

            GameObject bossHpObj = GameObject.Find("BossHealthBar");
            if (bossHpObj == null) bossHpObj = GameObject.Find("BossHeartPanel");
            if (bossHpObj == null) bossHpObj = GameObject.Find("BossHP");
            if (bossHpObj == null) bossHpObj = GameObject.Find("BossHeart");
            if (bossHpObj != null)
            {
                _bossHpBarRect = bossHpObj.GetComponent<RectTransform>();
                if (_bossHpBarRect != null)
                {
                    _bossHpBarBasePos = _bossHpBarRect.anchoredPosition;
                    _hasBossHpBasePos = true;
                }
            }

            _battleCanvasChildren.Clear();
            GameObject battleCanvasObj = GameObject.Find("Canvas");
            if (battleCanvasObj != null)
            {
                Transform ct = battleCanvasObj.transform;
                for (int i = 0; i < ct.childCount; i++)
                {
                    RectTransform childRt = ct.GetChild(i) as RectTransform;
                    if (childRt != null)
                    {
                        _battleCanvasChildren.Add(childRt);
                    }
                }
            }
        }

        private void BuildOverlayCanvasAndGuideUI()
        {
            var registry = GameAssetRegistry.Instance;
            TMP_FontAsset font = registry != null
                ? registry.GetPreferredFont(null)
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            GameObject canvasObj = new GameObject(
                "Boss1OverlayCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );
            _overlayCanvas = canvasObj.GetComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 250;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 0. VFX Layer & Screen Red Damage Vignette (behind Guide overlay)
            GameObject fxObj = new GameObject("BattleFXLayer", typeof(RectTransform));
            fxObj.transform.SetParent(canvasObj.transform, false);
            SetStretch(fxObj.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            _fxLayer = fxObj.transform;

            GameObject dmgFlashObj = new GameObject("PlayerDamageFlash", typeof(RectTransform), typeof(Image));
            dmgFlashObj.transform.SetParent(canvasObj.transform, false);
            SetStretch(dmgFlashObj.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            _damageVignetteImg = dmgFlashObj.GetComponent<Image>();
            _damageVignetteImg.color = new Color(0.92f, 0.10f, 0.08f, 0f);
            _damageVignetteImg.raycastTarget = false;

            // 1. Small Top-Left Help Button so player can reopen rules anytime (avoids top-right enemy HP bar)
            _reopenHelpBtnObj = new GameObject("Boss1HelpBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            _reopenHelpBtnObj.transform.SetParent(canvasObj.transform, false);
            RectTransform helpRt = _reopenHelpBtnObj.GetComponent<RectTransform>();
            helpRt.anchorMin = new Vector2(0.015f, 0.915f);
            helpRt.anchorMax = new Vector2(0.16f, 0.98f);
            helpRt.offsetMin = Vector2.zero;
            helpRt.offsetMax = Vector2.zero;

            Image helpImg = _reopenHelpBtnObj.GetComponent<Image>();
            if (registry != null && registry.buttonNormalSprite != null)
            {
                helpImg.sprite = registry.buttonNormalSprite;
                helpImg.color = Color.white;
            }
            else
            {
                helpImg.color = new Color(0.35f, 0.20f, 0.10f, 0.95f);
            }

            TextMeshProUGUI helpLabel = CreateTMP("Label", _reopenHelpBtnObj.transform, font, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
            helpLabel.color = new Color(1f, 0.95f, 0.80f, 1f);
            helpLabel.text = "? How to Play";
            SetStretch(helpLabel.rectTransform, 6f, 2f, 6f, 2f);

            _reopenHelpBtnObj.GetComponent<Button>().onClick.AddListener(OpenGuide);
            _reopenHelpBtnObj.SetActive(false);

            // 2. Guide Overlay Panel (dark backdrop + Wooden RPG Board)
            _guideOverlayObj = new GameObject("Boss1GuideOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _guideOverlayObj.transform.SetParent(canvasObj.transform, false);
            SetStretch(_guideOverlayObj.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            _guideGroup = _guideOverlayObj.GetComponent<CanvasGroup>();
            _guideGroup.alpha = 1f;
            _guideGroup.blocksRaycasts = true;

            Image backdrop = _guideOverlayObj.GetComponent<Image>();
            backdrop.color = new Color(0.04f, 0.02f, 0.03f, 0.84f);
            backdrop.raycastTarget = true;

            GameObject boardObj = new GameObject("GuideBoard", typeof(RectTransform), typeof(Image));
            boardObj.transform.SetParent(_guideOverlayObj.transform, false);
            RectTransform boardRt = boardObj.GetComponent<RectTransform>();
            boardRt.anchorMin = new Vector2(0.14f, 0.10f);
            boardRt.anchorMax = new Vector2(0.86f, 0.90f);
            boardRt.offsetMin = Vector2.zero;
            boardRt.offsetMax = Vector2.zero;

            Image boardImg = boardObj.GetComponent<Image>();
            if (registry != null && registry.dialogueBoardSprite != null)
            {
                boardImg.sprite = registry.dialogueBoardSprite;
                boardImg.color = new Color(0.98f, 0.94f, 0.86f, 1f);
            }
            else
            {
                boardImg.color = new Color(0.22f, 0.13f, 0.08f, 0.98f);
            }

            // Banner Header
            GameObject headerObj = new GameObject("HeaderRibbon", typeof(RectTransform), typeof(Image));
            headerObj.transform.SetParent(boardObj.transform, false);
            RectTransform headerRt = headerObj.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.13f, 0.84f);
            headerRt.anchorMax = new Vector2(0.87f, 0.96f);
            headerRt.offsetMin = Vector2.zero;
            headerRt.offsetMax = Vector2.zero;

            Image headerImg = headerObj.GetComponent<Image>();
            if (registry != null && registry.titleBannerSprite != null)
            {
                headerImg.sprite = registry.titleBannerSprite;
                headerImg.color = Color.white;
            }
            else
            {
                headerImg.color = new Color(0.45f, 0.16f, 0.08f, 0.98f);
            }

            GetGuideContentForCurrentScene(out string titleText, out string subText, out string bodyText);

            TextMeshProUGUI titleTmp = CreateTMP("TitleText", headerObj.transform, font, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
            titleTmp.color = new Color(1f, 0.94f, 0.74f, 1f);
            titleTmp.text = titleText;
            SetStretch(titleTmp.rectTransform, 14f, 4f, 14f, 6f);

            // Subtitle
            TextMeshProUGUI subTmp = CreateTMP("SubtitleText", boardObj.transform, font, 21f, FontStyles.Bold, TextAlignmentOptions.Center);
            subTmp.color = new Color(0.62f, 0.14f, 0.06f, 1f);
            subTmp.text = subText;
            subTmp.rectTransform.anchorMin = new Vector2(0.08f, 0.74f);
            subTmp.rectTransform.anchorMax = new Vector2(0.92f, 0.83f);
            subTmp.rectTransform.offsetMin = Vector2.zero;
            subTmp.rectTransform.offsetMax = Vector2.zero;

            // Rules Body
            TextMeshProUGUI bodyTmp = CreateTMP("RulesBodyText", boardObj.transform, font, 20f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            bodyTmp.color = new Color(0.18f, 0.10f, 0.05f, 1f);
            bodyTmp.textWrappingMode = TextWrappingModes.Normal;
            bodyTmp.lineSpacing = 6f;
            bodyTmp.rectTransform.anchorMin = new Vector2(0.09f, 0.20f);
            bodyTmp.rectTransform.anchorMax = new Vector2(0.91f, 0.73f);
            bodyTmp.rectTransform.offsetMin = Vector2.zero;
            bodyTmp.rectTransform.offsetMax = Vector2.zero;
            bodyTmp.text = bodyText;

            // Start Battle Button
            GameObject startBtnObj = new GameObject("StartBossBattleBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            startBtnObj.transform.SetParent(boardObj.transform, false);
            RectTransform startRt = startBtnObj.GetComponent<RectTransform>();
            startRt.anchorMin = new Vector2(0.27f, 0.055f);
            startRt.anchorMax = new Vector2(0.73f, 0.175f);
            startRt.offsetMin = Vector2.zero;
            startRt.offsetMax = Vector2.zero;

            Image startImg = startBtnObj.GetComponent<Image>();
            if (registry != null && registry.buttonNormalSprite != null)
            {
                startImg.sprite = registry.buttonNormalSprite;
                startImg.color = Color.white;
            }
            else
            {
                startImg.color = new Color(0.50f, 0.24f, 0.10f, 1f);
            }

            TextMeshProUGUI startLabel = CreateTMP("Label", startBtnObj.transform, font, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
            startLabel.color = new Color(1f, 0.95f, 0.80f, 1f);
            startLabel.text = "Start Battle [E / Space]";
            SetStretch(startLabel.rectTransform, 8f, 4f, 8f, 4f);

            startBtnObj.GetComponent<Button>().onClick.AddListener(CloseGuide);

            // 3. Fullscreen Fade Overlay (always topmost)
            _fadeObj = new GameObject("Boss1TransitionFade", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _fadeObj.transform.SetParent(canvasObj.transform, false);
            SetStretch(_fadeObj.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            _fadeGroup = _fadeObj.GetComponent<CanvasGroup>();
            _fadeGroup.alpha = 1f;
            _fadeGroup.blocksRaycasts = true;

            _fadeObj.GetComponent<Image>().color = new Color(0.03f, 0.02f, 0.05f, 1f);
        }

        private void GetGuideContentForCurrentScene(out string title, out string subtitle, out string body)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == "Bos2")
            {
                title = "STAGE 2: ETERNAL ICE CAVERN - GLACIERA";
                subtitle = "Objective: Match Frozen Number Pairs that Add Up to 13";
                body =
                    "<b>Objective:</b> Defeat <b>Glaciera (3 HP)</b> to reclaim the <b>Second HOKI Fragment</b>!\n\n" +
                    "<b>How to Play:</b>\n" +
                    "  1. Flip and find pairs of cards that <b>add up to 13</b> (e.g. 2+11, 3+10, 4+9, 5+8, 6+7).\n" +
                    "  2. Match all required pairs before exceeding the maximum allowed mistakes:\n" +
                    "     • <i>Round 1: 3 pairs (Max 1 mistake)</i>\n" +
                    "     • <i>Round 2: 4 pairs (Max 2 mistakes)</i>\n" +
                    "     • <i>Round 3: 5 pairs (Max 3 mistakes)</i>\n\n" +
                    "<b>Battle Rules:</b>\n" +
                    "  • <b>Complete all pairs:</b> <b>Malios strikes Glaciera</b> for 1 damage and advances to the next round!\n" +
                    "  • <b>Exceeding mistake limit:</b> <b>Glaciera strikes back</b>, and Malios loses 1 HP.";
            }
            else if (sceneName == "Bos3")
            {
                title = "STAGE 3: POISON FOREST - VENOMARK";
                subtitle = "Objective: Brew Alchemical Counter-Potions Equal to 21";
                body =
                    "<b>Objective:</b> Defeat <b>Venomark (3 HP)</b> to reclaim the <b>Third HOKI Fragment</b>!\n\n" +
                    "<b>How to Play:</b>\n" +
                    "  1. Drag <b>3 ingredient cards</b> into the crafting slots in the exact recipe order:\n" +
                    "     • <b>Slot 1:</b> Leaf (Daun)\n" +
                    "     • <b>Slot 2:</b> Root (Akar)\n" +
                    "     • <b>Slot 3:</b> Target Herb (<i>Round 1: Blue Rose | Round 2: White Lily | Round 3: Ginseng</i>)\n" +
                    "  2. The sum of all 3 card values must equal exactly <b>21</b>!\n" +
                    "  3. Click <b>Brew Potion</b> once all 3 slots are filled.\n\n" +
                    "<b>Battle Rules:</b>\n" +
                    "  • <b>Correct Recipe (Sum = 21):</b> <b>Malios strikes Venomark</b> for 1 damage!\n" +
                    "  • <b>Wrong Recipe or Sum:</b> The concoction backfires, and Malios loses 1 HP.";
            }
            else if (sceneName == "Bos4")
            {
                title = "STAGE 4: SHADOW CASTLE - NOCTHAR";
                subtitle = "Objective: Break the 3 Shadow Seals using Logic Sequence";
                body =
                    "<b>Objective:</b> Defeat <b>Nocthar (3 HP)</b> to reclaim the <b>Final HOKI Fragment</b>!\n\n" +
                    "<b>How to Play:</b>\n" +
                    "  1. Review the <b>Order Instructions</b> clue displayed on the right for the active round.\n" +
                    "  2. Select <b>4 cards</b> from your hand in sequence according to the rules:\n" +
                    "     • <b>Slot 1:</b> Must be an <b>Even number</b> (2, 4, 6, 8).\n" +
                    "     • <b>Slot 2:</b> Follow the clue (<i>Round 1 & 2: Greater than Slot 1 | Round 3: Smaller than Slot 1</i>).\n" +
                    "     • <b>Slot 3:</b> Must be an <b>Odd number</b> (1, 3, 7, 9).\n" +
                    "     • <b>Slot 4:</b> Must be the <b>Largest number</b> (9).\n" +
                    "  3. Click <b>Combine</b> to break the Shadow Seal!\n\n" +
                    "<b>Battle Rules:</b>\n" +
                    "  • <b>Correct Sequence:</b> Breaks 1 Shadow Seal and damages Nocthar!\n" +
                    "  • <b>Wrong Sequence:</b> Nocthar strikes back, and Malios loses 1 HP.";
            }
            else
            {
                title = "STAGE 1: MOUNT CINDER - IGNAROTH";
                subtitle = "Objective: Number Cards Addition Target";
                body =
                    "<b>Objective:</b> Defeat <b>Ignaroth (3 HP)</b> to reclaim the <b>First HOKI Fragment</b>!\n\n" +
                    "<b>How to Play:</b>\n" +
                    "  1. Observe the <b>Target Number</b> requested on screen (<i>Round 1: 9  →  Round 2: 11  →  Round 3: 13</i>).\n" +
                    "  2. From the <b>5 number cards</b> below, select <b>2 cards</b> that <b>add up</b> to the required target number.\n" +
                    "  3. Click a selected card again if you want to cancel or change your selection.\n\n" +
                    "<b>Battle Rules:</b>\n" +
                    "  • <b>3 Correct Pairs (3/3)</b> in a round: <b>Malios attacks</b>, Ignaroth loses 1 HP and you advance to the next round!\n" +
                    "  • <b>Wrong Combination:</b> <b>Ignaroth counters</b>, and Malios loses 1 HP.";
            }
        }

        private IEnumerator IntroTransitionSequence()
        {
            bool skipAutoGuide = _skipGuideOnNextRestart;
            _skipGuideOnNextRestart = false;

            if (_guideOverlayObj != null)
            {
                bool showGuide = !skipAutoGuide;
                _guideOverlayObj.SetActive(showGuide);
                _guideGroup.alpha = showGuide ? 1f : 0f;
                _isGuideOpen = showGuide;
                if (_reopenHelpBtnObj != null) _reopenHelpBtnObj.SetActive(!showGuide);
            }

            if (_fadeObj != null && _fadeGroup != null)
            {
                _fadeObj.transform.SetAsLastSibling();
                float t = 0f;
                while (t < 0.28f)
                {
                    t += Time.unscaledDeltaTime;
                    _fadeGroup.alpha = 1f - Mathf.Clamp01(t / 0.28f);
                    yield return null;
                }
                _fadeGroup.alpha = 0f;
                _fadeObj.SetActive(false);
            }
        }

        private void OpenGuide()
        {
            if (_guideOverlayObj == null || _isReturning) return;
            _guideOverlayObj.SetActive(true);
            _guideOverlayObj.transform.SetAsLastSibling();
            _guideGroup.alpha = 1f;
            _isGuideOpen = true;
            if (_reopenHelpBtnObj != null) _reopenHelpBtnObj.SetActive(false);
        }

        private void CloseGuide()
        {
            if (_guideOverlayObj == null) return;
            _guideOverlayObj.SetActive(false);
            _isGuideOpen = false;
            if (_reopenHelpBtnObj != null) _reopenHelpBtnObj.SetActive(true);
        }

        private IEnumerator PlayerAttackRoutine(bool heavyBossHit, System.Action onImpact = null)
        {
            if (_fxLayer == null)
            {
                onImpact?.Invoke();
                yield break;
            }

            Vector3 startPos = _mcRect != null ? _mcRect.position : new Vector3(Screen.width * 0.25f, Screen.height * 0.55f, 0f);
            Vector3 targetPos = _bosRect != null ? _bosRect.position : new Vector3(Screen.width * 0.75f, Screen.height * 0.55f, 0f);

            // 1. Malios fast energetic lunge forward
            if (_mcRect != null && _hasMcBasePos)
            {
                StartCoroutine(LungeActorRoutine(_mcRect, _mcBasePos, new Vector2(68f, 16f), 0.18f));
            }

            // 2. Simultaneous blazing fire streak + INSTANT impact (0ms delay on correct click!)
            Sprite[] fireFrames = GetFireFrames();
            Color boltColor = heavyBossHit ? new Color(1f, 0.85f, 0.38f, 1f) : new Color(1f, 0.92f, 0.72f, 1f);
            float fireSize = heavyBossHit ? 148f : 114f;

            StartCoroutine(FlyAttackProjectileRoutine(fireFrames, startPos, targetPos, fireSize, boltColor, false, 0.06f));

            // Impact frame fires immediately: SFX trigger + full-screen camera shake + fiery red explosion flash!
            onImpact?.Invoke();
            TriggerScreenShake(heavyBossHit ? 22f : 12f, heavyBossHit ? 0.34f : 0.22f);

            // 3. Heavy Multi-Layer VFXPack + Fiery Red Explosion Impact on Ignaroth
            Sprite[] shockwaveFrames = GetVfxPackFrames(VfxPackLayer.Shockwave);
            Sprite[] slashFrames = GetVfxPackFrames(VfxPackLayer.Slash);
            Sprite[] blastFrames = GetVfxPackFrames(VfxPackLayer.Blast);
            Sprite[] critFrames = GetVfxPackFrames(VfxPackLayer.CritCross);
            Sprite[] explosionFrames = GetExplosionFrames();

            // Red explosive screen vignette flash when player hits enemy
            StartCoroutine(FlashScreenColorRoutine(
                new Color(0.96f, 0.16f, 0.06f),
                heavyBossHit ? 0.34f : 0.22f,
                heavyBossHit ? 0.28f : 0.20f
            ));

            // Glowing crimson-orange explosion core behind sprites
            StartCoroutine(PulseGlowCoreRoutine(
                targetPos,
                heavyBossHit ? 430f : 310f,
                new Color(1f, 0.24f, 0.06f, 0.88f),
                heavyBossHit ? 0.26f : 0.18f
            ));

            // Layer A: Expanding fiery-red shockwave ring (VFXPack CircleImpact02)
            if (shockwaveFrames != null && shockwaveFrames.Length > 0)
            {
                float ringSize = heavyBossHit ? 510f : 380f;
                StartCoroutine(PlaySpriteSheetBurstRoutine(
                    shockwaveFrames, targetPos, ringSize,
                    heavyBossHit ? new Color(1f, 0.36f, 0.14f, 0.96f) : new Color(1f, 0.45f, 0.18f, 0.94f),
                    28f, 0f, heavyBossHit ? 1.25f : 1.12f
                ));
            }

            // Layer B: Crisp anime slash across boss (VFXPack SlashV3)
            if (slashFrames != null && slashFrames.Length > 0)
            {
                float slashSize = heavyBossHit ? 440f : 340f;
                float tilt = Random.Range(-18f, 18f);
                StartCoroutine(PlaySpriteSheetBurstRoutine(
                    slashFrames, targetPos, slashSize,
                    heavyBossHit ? new Color(1f, 0.96f, 0.65f, 1f) : new Color(1f, 0.85f, 0.55f, 1f),
                    30f, tilt, 1.1f
                ));
            }

            // Layer C: Fiery impact burst (VFXPack ImpactValo)
            if (blastFrames != null && blastFrames.Length > 0)
            {
                float blastSize = heavyBossHit ? 450f : 340f;
                StartCoroutine(PlaySpriteSheetBurstRoutine(
                    blastFrames, targetPos + new Vector3(0f, Screen.height * 0.015f, 0f), blastSize,
                    new Color(1f, 0.72f, 0.52f, 1f), 26f, Random.Range(-10f, 10f), heavyBossHit ? 1.18f : 1.05f
                ));
            }

            // Layer D: Extra Critical X-Cross + Tiny Swords fiery red explosion burst
            if (heavyBossHit && critFrames != null && critFrames.Length > 0)
            {
                StartCoroutine(PlaySpriteSheetBurstRoutine(critFrames, targetPos, 470f, new Color(1f, 0.42f, 0.18f, 1f), 24f, 0f, 1.2f));
            }
            if (explosionFrames != null && explosionFrames.Length > 0)
            {
                float expSize = heavyBossHit ? 340f : 240f;
                StartCoroutine(PlaySpriteSheetBurstRoutine(explosionFrames, targetPos, expSize, new Color(1f, 0.48f, 0.25f, 0.96f), 22f));
            }
            else if ((blastFrames == null || blastFrames.Length == 0) && (slashFrames == null || slashFrames.Length == 0))
            {
                StartCoroutine(SpawnBurstParticles(targetPos, new Color(1f, 0.25f, 0.10f, 1f), heavyBossHit ? 16 : 11, heavyBossHit ? 240f : 175f));
            }

            // Punchy comic callout text ("BOOM!", "DUARRR!", "MEGA BOOM! -1 HP!")
            string hitWord = heavyBossHit
                ? "MEGA BOOM!! -1 HP!"
                : PunchyHitWords[Random.Range(0, PunchyHitWords.Length)];
            StartCoroutine(SpawnPunchyCalloutText(
                targetPos + new Vector3(0f, Screen.height * 0.11f, 0f),
                hitWord,
                heavyBossHit ? new Color(1f, 0.92f, 0.20f, 1f) : new Color(1f, 0.86f, 0.30f, 1f),
                heavyBossHit ? 44f : 38f
            ));

            // Ignaroth white-hot hit flash, squash recoil & strong shake (+ Boss HP bar shake on finisher)
            if (_bosImage != null)
            {
                StartCoroutine(FlashImpactWhiteThenRedRoutine(_bosImage, heavyBossHit ? 0.30f : 0.22f));
            }
            if (_bosRect != null && _hasBosBasePos)
            {
                StartCoroutine(PunchScaleRoutine(_bosRect, _bosBaseScale, heavyBossHit ? 1.18f : 1.10f, heavyBossHit ? 0.28f : 0.20f));
                StartCoroutine(ShakeRectRoutine(_bosRect, _bosBasePos, heavyBossHit ? 42f : 25f, heavyBossHit ? 0.40f : 0.26f));
            }
            if (heavyBossHit && _bossHpBarRect != null && _hasBossHpBasePos)
            {
                StartCoroutine(ShakeRectRoutine(_bossHpBarRect, _bossHpBarBasePos, 15f, 0.34f));
            }
        }

        private IEnumerator BossAttackOnPlayerRoutine(System.Action onImpact = null)
        {
            if (_fxLayer == null)
            {
                onImpact?.Invoke();
                yield break;
            }

            Vector3 startPos = _bosRect != null ? _bosRect.position : new Vector3(Screen.width * 0.75f, Screen.height * 0.55f, 0f);
            Vector3 targetPos = _mcRect != null ? _mcRect.position : new Vector3(Screen.width * 0.25f, Screen.height * 0.55f, 0f);

            // 1. Ignaroth lunge toward Malios
            if (_bosRect != null && _hasBosBasePos)
            {
                StartCoroutine(LungeActorRoutine(_bosRect, _bosBasePos, new Vector2(-46f, 8f), 0.20f));
            }

            // 2. Fiery Tiny Swords Fire projectile from Ignaroth to Malios
            Sprite[] fireFrames = GetFireFrames();
            Sprite[] explosionFrames = GetExplosionFrames();
            Color fireColor = new Color(1f, 0.78f, 0.62f, 1f);

            yield return StartCoroutine(FlyAttackProjectileRoutine(fireFrames, startPos, targetPos, 118f, fireColor, true, 0.14f));

            // Impact frame on Malios: SFX trigger + screen shake + red vignette flash
            onImpact?.Invoke();
            TriggerScreenShake(14f, 0.26f);

            // 3. Impact on Malios: Tiny Swords Explosion + screen red flash, actor red flash & shake, HP bar shake & "-1 NYAWA!"
            if (_damageVignetteImg != null)
            {
                StartCoroutine(FlashScreenRedRoutine(0.38f, 0.35f));
            }
            if (_mcImage != null)
            {
                StartCoroutine(FlashImageColorRoutine(_mcImage, new Color(1f, 0.25f, 0.25f, 1f), 0.28f));
            }
            if (_mcRect != null && _hasMcBasePos)
            {
                StartCoroutine(ShakeRectRoutine(_mcRect, _mcBasePos, 22f, 0.34f));
            }
            if (_playerHpBarRect != null && _hasPlayerHpBasePos)
            {
                StartCoroutine(ShakeRectRoutine(_playerHpBarRect, _playerHpBarBasePos, 12f, 0.32f));
            }

            if (explosionFrames != null && explosionFrames.Length > 0)
            {
                StartCoroutine(PlaySpriteSheetBurstRoutine(explosionFrames, targetPos, 215f, new Color(1f, 0.85f, 0.75f, 1f), 18f));
            }
            else
            {
                StartCoroutine(SpawnBurstParticles(targetPos, new Color(1f, 0.36f, 0.12f, 1f), 11, 175f));
            }

            StartCoroutine(SpawnFloatingText(
                targetPos + new Vector3(0f, Screen.height * 0.09f, 0f),
                "-1 NYAWA!",
                new Color(1f, 0.26f, 0.22f, 1f),
                32f
            ));
        }

        private static Sprite[] GetFireFrames()
        {
            if (_cachedFireFrames != null && _cachedFireFrames.Length > 0 && _cachedFireFrames[0] != null)
            {
                return _cachedFireFrames;
            }
            var registry = GameAssetRegistry.Instance;
            if (registry != null)
            {
                registry.EnsureRuntimeFallbacks();
                if (registry.fireSprites != null && registry.fireSprites.Length > 0 && registry.fireSprites[0] != null)
                {
                    _cachedFireFrames = registry.fireSprites;
                    return _cachedFireFrames;
                }
            }
            _cachedFireFrames = GameAssetRegistry.LoadOrSliceCharacterSheet("VFX/Fire_02", 10, 1);
            return _cachedFireFrames;
        }

        private enum VfxPackLayer
        {
            Slash,
            Shockwave,
            Blast,
            CritCross
        }

        private static Sprite[] GetVfxPackFrames(VfxPackLayer layer)
        {
            switch (layer)
            {
                case VfxPackLayer.Slash:
                    if (_cachedSlashFrames != null && _cachedSlashFrames.Length > 0 && _cachedSlashFrames[0] != null) return _cachedSlashFrames;
                    break;
                case VfxPackLayer.Shockwave:
                    if (_cachedShockwaveFrames != null && _cachedShockwaveFrames.Length > 0 && _cachedShockwaveFrames[0] != null) return _cachedShockwaveFrames;
                    break;
                case VfxPackLayer.Blast:
                    if (_cachedBlastFrames != null && _cachedBlastFrames.Length > 0 && _cachedBlastFrames[0] != null) return _cachedBlastFrames;
                    break;
                case VfxPackLayer.CritCross:
                    if (_cachedCritFrames != null && _cachedCritFrames.Length > 0 && _cachedCritFrames[0] != null) return _cachedCritFrames;
                    break;
            }

            var registry = GameAssetRegistry.Instance;
            if (registry != null)
            {
                registry.EnsureRuntimeFallbacks();
            }

            switch (layer)
            {
                case VfxPackLayer.Slash:
                    _cachedSlashFrames = (registry != null && registry.vfxSlashSprites != null && registry.vfxSlashSprites.Length > 0 && registry.vfxSlashSprites[0] != null)
                        ? registry.vfxSlashSprites
                        : GameAssetRegistry.LoadOrSliceFlipbookSheet("VFX/SlashV3", 3, 3);
                    return _cachedSlashFrames;
                case VfxPackLayer.Shockwave:
                    _cachedShockwaveFrames = (registry != null && registry.vfxShockwaveSprites != null && registry.vfxShockwaveSprites.Length > 0 && registry.vfxShockwaveSprites[0] != null)
                        ? registry.vfxShockwaveSprites
                        : GameAssetRegistry.LoadOrSliceFlipbookSheet("VFX/CircleImpact02", 3, 3);
                    return _cachedShockwaveFrames;
                case VfxPackLayer.Blast:
                    _cachedBlastFrames = (registry != null && registry.vfxImpactBlastSprites != null && registry.vfxImpactBlastSprites.Length > 0 && registry.vfxImpactBlastSprites[0] != null)
                        ? registry.vfxImpactBlastSprites
                        : GameAssetRegistry.LoadOrSliceFlipbookSheet("VFX/ImpactValo", 3, 3);
                    return _cachedBlastFrames;
                case VfxPackLayer.CritCross:
                    _cachedCritFrames = (registry != null && registry.vfxCritCrossSprites != null && registry.vfxCritCrossSprites.Length > 0 && registry.vfxCritCrossSprites[0] != null)
                        ? registry.vfxCritCrossSprites
                        : GameAssetRegistry.LoadOrSliceFlipbookSheet("VFX/Impact06", 3, 2, 5);
                    return _cachedCritFrames;
                default:
                    return System.Array.Empty<Sprite>();
            }
        }

        private static Sprite[] GetExplosionFrames()
        {
            if (_cachedExplosionFrames != null && _cachedExplosionFrames.Length > 0 && _cachedExplosionFrames[0] != null)
            {
                return _cachedExplosionFrames;
            }
            var registry = GameAssetRegistry.Instance;
            if (registry != null)
            {
                registry.EnsureRuntimeFallbacks();
                if (registry.explosionSprites != null && registry.explosionSprites.Length > 0 && registry.explosionSprites[0] != null)
                {
                    _cachedExplosionFrames = registry.explosionSprites;
                    return _cachedExplosionFrames;
                }
            }
            _cachedExplosionFrames = GameAssetRegistry.LoadOrSliceCharacterSheet("VFX/Explosion_01", 8, 1);
            return _cachedExplosionFrames;
        }

        private IEnumerator FlyAttackProjectileRoutine(Sprite[] fireFrames, Vector3 startPos, Vector3 targetPos, float size, Color tint, bool flipX, float travelDur)
        {
            bool hasSpriteSheet = fireFrames != null && fireFrames.Length > 0;
            Image proj = null;
            if (hasSpriteSheet)
            {
                GameObject go = new GameObject("AttackFireFX", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_fxLayer, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.position = startPos;
                rt.sizeDelta = new Vector2(size, size);
                proj = go.GetComponent<Image>();
                proj.sprite = fireFrames[0];
                proj.preserveAspect = true;
                proj.color = tint;
                proj.raycastTarget = false;
            }
            else
            {
                proj = CreateGlowOrb("AttackGlowFallback", startPos, size * 0.72f, tint);
            }

            float elapsed = 0f;
            float signX = flipX ? -1f : 1f;
            while (elapsed < travelDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / travelDur);
                float ease = 1f - Mathf.Pow(1f - u, 2f);
                Vector3 pos = Vector3.Lerp(startPos, targetPos, ease);
                pos.y += Mathf.Sin(u * Mathf.PI) * (Screen.height * 0.045f);
                if (proj != null)
                {
                    proj.rectTransform.position = pos;
                    float scale = Mathf.Lerp(0.90f, 1.25f, u);
                    proj.rectTransform.localScale = new Vector3(scale * signX, scale, 1f);
                    if (hasSpriteSheet)
                    {
                        int frameIdx = Mathf.FloorToInt(elapsed * 24f) % fireFrames.Length;
                        if (fireFrames[frameIdx] != null) proj.sprite = fireFrames[frameIdx];
                    }
                }
                yield return null;
            }

            if (proj != null) Destroy(proj.gameObject);
        }

        private IEnumerator PlaySpriteSheetBurstRoutine(
            Sprite[] frames,
            Vector3 screenPos,
            float size,
            Color tint,
            float fps,
            float rotationDeg = 0f,
            float scalePunch = 1f)
        {
            if (_fxLayer == null || frames == null || frames.Length == 0) yield break;

            GameObject go = new GameObject("ExplosionBurstFX", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_fxLayer, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.position = screenPos;
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, rotationDeg);
            rt.localScale = new Vector3(scalePunch, scalePunch, 1f);

            Image img = go.GetComponent<Image>();
            img.sprite = frames[0];
            img.preserveAspect = true;
            img.color = tint;
            img.raycastTarget = false;

            float frameDur = 1f / Mathf.Max(1f, fps);
            for (int i = 0; i < frames.Length; i++)
            {
                if (img == null) yield break;
                if (frames[i] != null) img.sprite = frames[i];
                yield return new WaitForSecondsRealtime(frameDur);
            }

            if (go != null) Destroy(go);
        }

        private IEnumerator LungeActorRoutine(RectTransform rt, Vector2 basePos, Vector2 offset, float duration)
        {
            float half = duration * 0.4f;
            float t = 0f;
            while (t < half && rt != null)
            {
                t += Time.unscaledDeltaTime;
                rt.anchoredPosition = Vector2.Lerp(basePos, basePos + offset, Mathf.Clamp01(t / half));
                yield return null;
            }
            t = 0f;
            float back = duration - half;
            while (t < back && rt != null)
            {
                t += Time.unscaledDeltaTime;
                rt.anchoredPosition = Vector2.Lerp(basePos + offset, basePos, Mathf.Clamp01(t / back));
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = basePos;
        }

        private IEnumerator ShakeRectRoutine(RectTransform rt, Vector2 basePos, float magnitude, float duration)
        {
            float t = 0f;
            while (t < duration && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float decay = 1f - Mathf.Clamp01(t / duration);
                float dx = Mathf.Sin(t * 72f) * magnitude * decay;
                float dy = Mathf.Cos(t * 58f) * (magnitude * 0.45f) * decay;
                rt.anchoredPosition = basePos + new Vector2(dx, dy);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = basePos;
        }

        private IEnumerator FlashImageColorRoutine(Image img, Color flashColor, float duration)
        {
            if (img == null) yield break;
            Color orig = Color.white;
            img.color = flashColor;
            float t = 0f;
            while (t < duration && img != null)
            {
                t += Time.unscaledDeltaTime;
                img.color = Color.Lerp(flashColor, orig, Mathf.Clamp01(t / duration));
                yield return null;
            }
            if (img != null) img.color = orig;
        }

        private void TriggerScreenShake(float magnitude, float duration)
        {
            if (_screenShakeRoutine != null)
            {
                StopCoroutine(_screenShakeRoutine);
                ApplyCanvasShakeDelta(-_appliedCanvasShake);
                _appliedCanvasShake = Vector2.zero;
            }
            _screenShakeRoutine = StartCoroutine(ShakeScreenRoutine(magnitude, duration));
        }

        private IEnumerator ShakeScreenRoutine(float magnitude, float duration)
        {
            Camera cam = Camera.main;
            Vector3 camBase = cam != null ? cam.transform.position : Vector3.zero;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float decay = 1f - Mathf.Clamp01(t / duration);
                float dx = Mathf.Sin(t * 78f) * magnitude * decay;
                float dy = Mathf.Cos(t * 64f) * (magnitude * 0.65f) * decay;
                Vector2 targetOffset = new Vector2(dx, dy);

                ApplyCanvasShakeDelta(targetOffset - _appliedCanvasShake);
                _appliedCanvasShake = targetOffset;

                if (cam != null)
                {
                    cam.transform.position = camBase + new Vector3(dx * 0.015f, dy * 0.015f, 0f);
                }
                yield return null;
            }

            ApplyCanvasShakeDelta(-_appliedCanvasShake);
            _appliedCanvasShake = Vector2.zero;
            if (cam != null) cam.transform.position = camBase;
            _screenShakeRoutine = null;
        }

        private void ApplyCanvasShakeDelta(Vector2 delta)
        {
            if (delta.sqrMagnitude < 0.00001f) return;
            for (int i = 0; i < _battleCanvasChildren.Count; i++)
            {
                RectTransform rt = _battleCanvasChildren[i];
                if (rt != null)
                {
                    rt.anchoredPosition += delta;
                }
            }
        }

        private IEnumerator PulseGlowCoreRoutine(Vector3 screenPos, float maxSize, Color color, float duration)
        {
            if (_fxLayer == null) yield break;
            Image orb = CreateGlowOrb("ExplosionRedCore", screenPos, maxSize * 0.45f, color);
            float t = 0f;
            while (t < duration && orb != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float s = Mathf.Lerp(0.55f, 2.35f, 1f - Mathf.Pow(1f - u, 2f));
                orb.rectTransform.localScale = new Vector3(s, s, 1f);
                Color c = color;
                c.a = color.a * (1f - u);
                orb.color = c;
                yield return null;
            }
            if (orb != null) Destroy(orb.gameObject);
        }

        private IEnumerator PunchScaleRoutine(RectTransform rt, Vector3 baseScale, float peakMultiplier, float duration)
        {
            if (rt == null) yield break;
            float t = 0f;
            float phase1 = duration * 0.30f;
            while (t < phase1 && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / phase1);
                rt.localScale = Vector3.Lerp(baseScale * peakMultiplier, baseScale * 0.88f, u);
                yield return null;
            }
            t = 0f;
            float phase2 = Mathf.Max(0.01f, duration - phase1);
            while (t < phase2 && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / phase2);
                rt.localScale = Vector3.Lerp(baseScale * 0.88f, baseScale, u);
                yield return null;
            }
            if (rt != null) rt.localScale = baseScale;
        }

        private IEnumerator FlashImpactWhiteThenRedRoutine(Image img, float duration)
        {
            if (img == null) yield break;
            // Bright white-gold hit flash briefly, then hot impact crimson back to normal
            img.color = new Color(1f, 0.96f, 0.72f, 1f);
            yield return new WaitForSecondsRealtime(0.06f);
            yield return StartCoroutine(FlashImageColorRoutine(img, new Color(1f, 0.32f, 0.16f, 1f), Mathf.Max(0.08f, duration - 0.06f)));
        }

        private IEnumerator FlashScreenRedRoutine(float maxAlpha, float duration)
        {
            yield return StartCoroutine(FlashScreenColorRoutine(new Color(0.92f, 0.10f, 0.08f), maxAlpha, duration));
        }

        private IEnumerator FlashScreenColorRoutine(Color rgb, float maxAlpha, float duration)
        {
            if (_damageVignetteImg == null) yield break;
            float t = 0f;
            while (t < duration && _damageVignetteImg != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float a = Mathf.Sin(u * Mathf.PI) * maxAlpha;
                _damageVignetteImg.color = new Color(rgb.r, rgb.g, rgb.b, a);
                yield return null;
            }
            if (_damageVignetteImg != null)
            {
                _damageVignetteImg.color = new Color(rgb.r, rgb.g, rgb.b, 0f);
            }
        }

        private IEnumerator SpawnBurstParticles(Vector3 centerScreenPos, Color baseColor, int count, float speed)
        {
            var particles = new List<(RectTransform rt, Image img, Vector2 vel)>(count);
            for (int i = 0; i < count; i++)
            {
                float angle = (Mathf.PI * 2f * i / count) + Random.Range(-0.25f, 0.25f);
                float spd = speed * Random.Range(0.55f, 1.15f);
                float size = Random.Range(18f, 34f);
                Image p = CreateGlowOrb("SparkFX", centerScreenPos, size, baseColor);
                particles.Add((p.rectTransform, p, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spd));
            }

            float dur = 0.40f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                for (int i = 0; i < particles.Count; i++)
                {
                    var item = particles[i];
                    if (item.rt == null) continue;
                    item.rt.position += (Vector3)(item.vel * Time.unscaledDeltaTime);
                    float s = 1f - u * 0.7f;
                    item.rt.localScale = new Vector3(s, s, 1f);
                    Color c = baseColor;
                    c.a = 1f - u;
                    item.img.color = c;
                }
                yield return null;
            }

            for (int i = 0; i < particles.Count; i++)
            {
                if (particles[i].rt != null) Destroy(particles[i].rt.gameObject);
            }
        }

        private IEnumerator SpawnPunchyCalloutText(Vector3 screenPos, string msg, Color color, float fontSize)
        {
            if (_fxLayer == null) yield break;
            var registry = GameAssetRegistry.Instance;
            TMP_FontAsset font = registry != null
                ? registry.GetPreferredFont(null)
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            TextMeshProUGUI tmp = CreateTMP("ComicCalloutPopup", _fxLayer, font, fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            tmp.color = color;
            tmp.text = msg;
            tmp.outlineWidth = 0.34f;
            tmp.outlineColor = new Color32(18, 4, 2, 255);

            RectTransform rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(620f, 110f);
            rt.position = screenPos;
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));

            float dur = 0.92f;
            float t = 0f;
            while (t < dur && tmp != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float pop = u < 0.14f
                    ? Mathf.Lerp(1.62f, 0.96f, u / 0.14f)
                    : (u < 0.24f ? Mathf.Lerp(0.96f, 1.08f, (u - 0.14f) / 0.10f) : 1.04f);
                rt.localScale = new Vector3(pop, pop, 1f);
                rt.position = screenPos + new Vector3(0f, u * (Screen.height * 0.095f), 0f);
                Color c = color;
                c.a = u < 0.68f ? 1f : Mathf.Lerp(1f, 0f, (u - 0.68f) / 0.32f);
                tmp.color = c;
                yield return null;
            }

            if (tmp != null) Destroy(tmp.gameObject);
        }

        private IEnumerator SpawnFloatingText(Vector3 screenPos, string msg, Color color, float fontSize)
        {
            if (_fxLayer == null) yield break;
            var registry = GameAssetRegistry.Instance;
            TMP_FontAsset font = registry != null
                ? registry.GetPreferredFont(null)
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            TextMeshProUGUI tmp = CreateTMP("DamagePopup", _fxLayer, font, fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            tmp.color = color;
            tmp.text = msg;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(18, 6, 4, 255);

            RectTransform rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(480f, 80f);
            rt.position = screenPos;

            float dur = 0.85f;
            float t = 0f;
            while (t < dur && tmp != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                rt.position = screenPos + new Vector3(0f, u * (Screen.height * 0.08f), 0f);
                Color c = color;
                c.a = u < 0.65f ? 1f : Mathf.Lerp(1f, 0f, (u - 0.65f) / 0.35f);
                tmp.color = c;
                yield return null;
            }

            if (tmp != null) Destroy(tmp.gameObject);
        }

        private Image CreateGlowOrb(string name, Vector3 screenPos, float size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_fxLayer, false);
            Image img = go.GetComponent<Image>();
            img.sprite = GetOrCreateGlowSprite();
            img.color = color;
            img.raycastTarget = false;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            rt.position = screenPos;
            return img;
        }

        private static Sprite GetOrCreateGlowSprite()
        {
            if (_proceduralGlowSprite != null) return _proceduralGlowSprite;

            const int res = 64;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
            float maxR = res * 0.5f;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _proceduralGlowSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
            return _proceduralGlowSprite;
        }

        private IEnumerator ReturnToTestMapRoutine(bool bossDefeated)
        {
            _isReturning = true;

            if (_fadeObj != null && _fadeGroup != null)
            {
                _fadeObj.SetActive(true);
                _fadeObj.transform.SetAsLastSibling();
                _fadeGroup.blocksRaycasts = true;

                float t = 0f;
                while (t < 0.26f)
                {
                    t += Time.unscaledDeltaTime;
                    _fadeGroup.alpha = Mathf.Clamp01(t / 0.26f);
                    yield return null;
                }
                _fadeGroup.alpha = 1f;
            }

            GameProgressionManager.CompleteBossBattleAndPrepareReturn(0, bossDefeated);
            SceneManager.LoadScene(TestMapSceneName);
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
