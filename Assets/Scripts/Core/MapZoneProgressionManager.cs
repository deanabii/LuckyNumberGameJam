using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using LuckyNumber.Characters;
using LuckyNumber.UI;

namespace LuckyNumber.Core
{
    [Serializable]
    public class ZoneGatewayDefinition
    {
        public string gatewayName;
        public MapZoneId fromZone;
        public MapZoneId toZone;
        public Rect triggerRect;
        public Vector3 destinationPosition;
        public FacingDirection destinationFacing;
        public Vector3 blockedPushbackPosition;
        public FacingDirection blockedFacing;
    }

    public class MapZoneProgressionManager : MonoBehaviour
    {
        public static MapZoneProgressionManager Instance { get; private set; }

        [Header("Font Settings (Bisa Diedit di Inspector)")]
        [SerializeField] private TMP_FontAsset customTransitionFont;

        [Header("UI References (LuckyNumber_UICanvas)")]
        [SerializeField] private GameObject _transitionOverlayObj;
        [SerializeField] private CanvasGroup _transitionCanvasGroup;
        [SerializeField] private GameObject _warningCardObj;
        [SerializeField] private TextMeshProUGUI _warningTitleText;
        [SerializeField] private TextMeshProUGUI _warningBodyText;
        [SerializeField] private TextMeshProUGUI _warningButtonLabel;
        [SerializeField] private Button _warningCloseButton;
        [SerializeField] private GameObject _zoneBannerObj;
        [SerializeField] private CanvasGroup _zoneBannerGroup;
        [SerializeField] private TextMeshProUGUI _zoneBannerText;

        private const string Boss1SceneName = "Bos1 Test";
        private const string Boss2SceneName = "Bos2";
        private const string Boss3SceneName = "Bos3";
        private const string Boss4SceneName = "Bos4";

        private static readonly Rect Boss1CavePortalRect = new Rect(-38.6f, 22.4f, 4.2f, 2.4f);
        private static readonly Rect Boss2CavePortalRect = new Rect(-14.5f, 51.0f, 4.2f, 3.2f);
        private static readonly Rect Boss3CavePortalRect = new Rect(12.5f, 51.0f, 4.2f, 3.2f);
        private static readonly Rect Boss4CavePortalRect = new Rect(24.5f, 22.0f, 4.5f, 3.5f);

        private readonly List<ZoneGatewayDefinition> _gateways = new List<ZoneGatewayDefinition>();
        private readonly Dictionary<MapZoneId, Rect> _zoneCameraBounds = new Dictionary<MapZoneId, Rect>();
        private readonly List<TextMeshPro> _gateSignpostLabels = new List<TextMeshPro>();
        private TextMeshPro _boss1CaveLabel;
        private TextMeshPro _boss2CaveLabel;
        private TextMeshPro _boss3CaveLabel;
        private TextMeshPro _boss4CaveLabel;
        private static bool _hasShownInitialGuide;

        private Coroutine _bannerRoutine;
        private bool _isTransitioning;
        private bool _waitingForWarningDismiss;
        private float _cooldownUntilTime;
        private bool _listenersBound;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SetupZoneBoundsAndGateways();
        }

        private void Start()
        {
            if (GameProgressionManager.Instance != null)
            {
                GameProgressionManager.Instance.OnProgressionChanged -= RefreshGateSignposts;
                GameProgressionManager.Instance.OnProgressionChanged += RefreshGateSignposts;
            }
            CreateWorldSignpostsAndBossMarkers();
            RefreshGateSignposts();

            if (GameProgressionManager.Instance != null &&
                GameProgressionManager.Instance.ConsumeBossReturnState(out MapZoneId returnZone, out Vector3 returnPos, out int wonBossIndex))
            {
                _cooldownUntilTime = Time.time + 1.0f;
                GameProgressionManager.Instance.SetCurrentZone(returnZone);
                ApplyCameraBoundsForZone(returnZone);

                if (PlayerController.Instance != null)
                {
                    PlayerController.Instance.TeleportTo(returnPos, FacingDirection.Down);
                    PlayerController.Instance.IsMovementLocked = false;
                }

                StartCoroutine(FadeInFromBlackOnReturn(
                    wonBossIndex == 0 ? "★ Fire HOKI Fragment Obtained! ★" :
                    wonBossIndex == 1 ? "★ Ice HOKI Fragment Obtained! (SIXSEVEN Ultimate Unlocked!) ★" :
                    wonBossIndex == 2 ? "★ Poison HOKI Fragment Obtained! ★" :
                    wonBossIndex == 3 ? "★ Shadow HOKI Fragment Obtained! (Return to the Altar!) ★" :
                    GameProgressionManager.GetZoneDisplayName(returnZone)
                ));
            }
            else
            {
                ApplyCameraBoundsForZone(MapZoneId.DesaEldoria);
            }
        }

        private void OnDestroy()
        {
            if (GameProgressionManager.Instance != null)
            {
                GameProgressionManager.Instance.OnProgressionChanged -= RefreshGateSignposts;
            }
        }

        public void ApplyFont(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null) return;
            customTransitionFont = fontAsset;
            if (_warningTitleText != null) _warningTitleText.font = fontAsset;
            if (_warningBodyText != null) _warningBodyText.font = fontAsset;
            if (_warningButtonLabel != null) _warningButtonLabel.font = fontAsset;
            if (_zoneBannerText != null) _zoneBannerText.font = fontAsset;
        }

        private void SetupZoneBoundsAndGateways()
        {
            // Exact Tilemap bounds from TestMap.unity
            _zoneCameraBounds[MapZoneId.DesaEldoria] = new Rect(-12f, -10f, 24f, 19.5f);
            _zoneCameraBounds[MapZoneId.AreaPercabangan] = new Rect(-12f, 13f, 24f, 19.5f);
            _zoneCameraBounds[MapZoneId.Boss1_GunungBara] = new Rect(-40f, 16f, 26f, 15f);
            _zoneCameraBounds[MapZoneId.Boss2_GuaEsAbadi] = new Rect(-24f, 42f, 23f, 19f);
            _zoneCameraBounds[MapZoneId.Boss3_HutanRacun] = new Rect(3f, 42f, 23f, 19f);
            _zoneCameraBounds[MapZoneId.Boss4_KastilBayangan] = new Rect(14f, 17f, 25f, 14f);

            _gateways.Clear();

            // 1. Desa Eldoria (North Exit) -> Area Percabangan (South Entrance)
            _gateways.Add(new ZoneGatewayDefinition
            {
                gatewayName = "Desa -> Percabangan",
                fromZone = MapZoneId.DesaEldoria,
                toZone = MapZoneId.AreaPercabangan,
                triggerRect = new Rect(-4.5f, 8.0f, 9.0f, 3.0f),
                destinationPosition = new Vector3(-0.5f, 15.0f, 0f),
                destinationFacing = FacingDirection.Up,
                blockedPushbackPosition = new Vector3(0f, 6.4f, 0f),
                blockedFacing = FacingDirection.Down
            });

            // 2. Area Percabangan (South Exit) -> Desa Eldoria (North Entrance)
            _gateways.Add(new ZoneGatewayDefinition
            {
                gatewayName = "Percabangan -> Desa",
                fromZone = MapZoneId.AreaPercabangan,
                toZone = MapZoneId.DesaEldoria,
                triggerRect = new Rect(-4.5f, 11.5f, 9.0f, 2.3f),
                destinationPosition = new Vector3(0f, 6.8f, 0f),
                destinationFacing = FacingDirection.Down,
                blockedPushbackPosition = new Vector3(-0.5f, 15.2f, 0f),
                blockedFacing = FacingDirection.Up
            });

            // 3. Percabangan (West / Left Branch) -> Boss 1: Gunung Bara (Ignaroth)
            _gateways.Add(new ZoneGatewayDefinition
            {
                gatewayName = "Percabangan -> Boss 1 (Gunung Bara)",
                fromZone = MapZoneId.AreaPercabangan,
                toZone = MapZoneId.Boss1_GunungBara,
                triggerRect = new Rect(-13.5f, 21.0f, 2.8f, 5.2f),
                destinationPosition = new Vector3(-17.0f, 23.5f, 0f),
                destinationFacing = FacingDirection.Left,
                blockedPushbackPosition = new Vector3(-9.2f, 23.5f, 0f),
                blockedFacing = FacingDirection.Right
            });

            // Return from Boss 1 -> Percabangan
            _gateways.Add(new ZoneGatewayDefinition
            {
                gatewayName = "Boss 1 -> Percabangan",
                fromZone = MapZoneId.Boss1_GunungBara,
                toZone = MapZoneId.AreaPercabangan,
                triggerRect = new Rect(-15.5f, 20.5f, 2.5f, 6.0f),
                destinationPosition = new Vector3(-9.2f, 23.5f, 0f),
                destinationFacing = FacingDirection.Right,
                blockedPushbackPosition = new Vector3(-17.5f, 23.5f, 0f),
                blockedFacing = FacingDirection.Left
            });

            // 4. Percabangan (North-West / Top-Left Branch) -> Boss 2: Gua Es Abadi (Glaciera)
            _gateways.Add(new ZoneGatewayDefinition
            {
                gatewayName = "Percabangan -> Boss 2 (Gua Es Abadi)",
                fromZone = MapZoneId.AreaPercabangan,
                toZone = MapZoneId.Boss2_GuaEsAbadi,
                triggerRect = new Rect(-6.8f, 30.8f, 5.8f, 3.0f),
                destinationPosition = new Vector3(-11.5f, 44.0f, 0f),
                destinationFacing = FacingDirection.Up,
                blockedPushbackPosition = new Vector3(-3.8f, 28.8f, 0f),
                blockedFacing = FacingDirection.Down
            });

            // Return from Boss 2 -> Percabangan
            _gateways.Add(new ZoneGatewayDefinition
            {
                gatewayName = "Boss 2 -> Percabangan",
                fromZone = MapZoneId.Boss2_GuaEsAbadi,
                toZone = MapZoneId.AreaPercabangan,
                triggerRect = new Rect(-16.0f, 40.5f, 9.0f, 2.3f),
                destinationPosition = new Vector3(-3.8f, 28.8f, 0f),
                destinationFacing = FacingDirection.Down,
                blockedPushbackPosition = new Vector3(-11.5f, 44.5f, 0f),
                blockedFacing = FacingDirection.Up
            });

            // 5. Percabangan (North-East / Top-Right Branch) -> Boss 3: Hutan Racun (Venomark)
            _gateways.Add(new ZoneGatewayDefinition
            {
                gatewayName = "Percabangan -> Boss 3 (Hutan Racun)",
                fromZone = MapZoneId.AreaPercabangan,
                toZone = MapZoneId.Boss3_HutanRacun,
                triggerRect = new Rect(1.0f, 30.8f, 5.8f, 3.0f),
                destinationPosition = new Vector3(14.5f, 44.0f, 0f),
                destinationFacing = FacingDirection.Up,
                blockedPushbackPosition = new Vector3(3.5f, 28.8f, 0f),
                blockedFacing = FacingDirection.Down
            });

            // Return from Boss 3 -> Percabangan
            _gateways.Add(new ZoneGatewayDefinition
            {
                gatewayName = "Boss 3 -> Percabangan",
                fromZone = MapZoneId.Boss3_HutanRacun,
                toZone = MapZoneId.AreaPercabangan,
                triggerRect = new Rect(10.0f, 40.5f, 9.0f, 2.3f),
                destinationPosition = new Vector3(3.5f, 28.8f, 0f),
                destinationFacing = FacingDirection.Down,
                blockedPushbackPosition = new Vector3(14.5f, 44.5f, 0f),
                blockedFacing = FacingDirection.Up
            });

            // 6. Percabangan (East / Right Branch) -> Boss 4: Kastil Bayangan (Nocthar)
            _gateways.Add(new ZoneGatewayDefinition
            {
                gatewayName = "Percabangan -> Boss 4 (Kastil Bayangan)",
                fromZone = MapZoneId.AreaPercabangan,
                toZone = MapZoneId.Boss4_KastilBayangan,
                triggerRect = new Rect(9.8f, 21.0f, 2.8f, 5.2f),
                destinationPosition = new Vector3(17.0f, 23.5f, 0f),
                destinationFacing = FacingDirection.Right,
                blockedPushbackPosition = new Vector3(8.2f, 23.5f, 0f),
                blockedFacing = FacingDirection.Left
            });

            // Return from Boss 4 -> Percabangan
            _gateways.Add(new ZoneGatewayDefinition
            {
                gatewayName = "Boss 4 -> Percabangan",
                fromZone = MapZoneId.Boss4_KastilBayangan,
                toZone = MapZoneId.AreaPercabangan,
                triggerRect = new Rect(13.5f, 20.5f, 2.5f, 6.0f),
                destinationPosition = new Vector3(8.2f, 23.5f, 0f),
                destinationFacing = FacingDirection.Left,
                blockedPushbackPosition = new Vector3(17.5f, 23.5f, 0f),
                blockedFacing = FacingDirection.Right
            });
        }

        public void InitializeUI(Transform canvasTransform)
        {
            var registry = GameAssetRegistry.Instance;
            TMP_FontAsset font = registry != null
                ? registry.GetPreferredFont(customTransitionFont)
                : customTransitionFont ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (_transitionOverlayObj == null && canvasTransform != null)
            {
                Transform existingOverlay = canvasTransform.Find("BlackZoneTransitionOverlay");
                if (existingOverlay != null) _transitionOverlayObj = existingOverlay.gameObject;
            }
            if (_zoneBannerObj == null && canvasTransform != null)
            {
                Transform existingBanner = canvasTransform.Find("ZoneBannerToast");
                if (existingBanner != null) _zoneBannerObj = existingBanner.gameObject;
            }

            if (_transitionOverlayObj != null)
            {
                RemoveLegacyCloudCurtainIfAny();
                BindExistingUIReferences();
                if (customTransitionFont != null)
                {
                    ApplyFont(customTransitionFont);
                }
                else if (_warningBodyText != null && _warningBodyText.font != null)
                {
                    customTransitionFont = _warningBodyText.font;
                }
                BindRuntimeButtons();
                if (Application.isPlaying && !_isTransitioning)
                {
                    _transitionOverlayObj.SetActive(false);
                    if (_zoneBannerObj != null) _zoneBannerObj.SetActive(false);
                }
                return;
            }

            if (canvasTransform == null) return;

            // Top Area Banner Toast
            _zoneBannerObj = new GameObject("ZoneBannerToast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _zoneBannerObj.transform.SetParent(canvasTransform, false);
            RectTransform zbRect = _zoneBannerObj.GetComponent<RectTransform>();
            zbRect.anchorMin = new Vector2(0.30f, 0.89f);
            zbRect.anchorMax = new Vector2(0.70f, 0.965f);
            zbRect.offsetMin = Vector2.zero;
            zbRect.offsetMax = Vector2.zero;

            _zoneBannerGroup = _zoneBannerObj.GetComponent<CanvasGroup>();
            _zoneBannerGroup.alpha = 0f;
            _zoneBannerGroup.blocksRaycasts = false;

            Image zbImg = _zoneBannerObj.GetComponent<Image>();
            if (registry != null && registry.titleBannerSprite != null)
            {
                zbImg.sprite = registry.titleBannerSprite;
                zbImg.color = Color.white;
            }
            else
            {
                zbImg.color = new Color(0.25f, 0.15f, 0.08f, 0.92f);
            }
            zbImg.raycastTarget = false;

            _zoneBannerText = CreateTMP("ZoneBannerText", _zoneBannerObj.transform, font, 19f, FontStyles.Bold, TextAlignmentOptions.Center);
            _zoneBannerText.color = new Color(1f, 0.95f, 0.76f, 1f);
            _zoneBannerText.text = "Eldoria Village";
            SetStretch(_zoneBannerText.rectTransform, 14f, 4f, 14f, 6f);
            _zoneBannerObj.SetActive(false);

            // Fullscreen Black Transition & Locked Area Warning Overlay (Original Style)
            _transitionOverlayObj = new GameObject("BlackZoneTransitionOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            _transitionOverlayObj.transform.SetParent(canvasTransform, false);
            RectTransform trRect = _transitionOverlayObj.GetComponent<RectTransform>();
            trRect.anchorMin = Vector2.zero;
            trRect.anchorMax = Vector2.one;
            trRect.offsetMin = Vector2.zero;
            trRect.offsetMax = Vector2.zero;

            _transitionCanvasGroup = _transitionOverlayObj.GetComponent<CanvasGroup>();
            _transitionCanvasGroup.alpha = 0f;

            Image blackImg = _transitionOverlayObj.GetComponent<Image>();
            blackImg.color = new Color(0.03f, 0.02f, 0.05f, 0.96f);

            // Warning Card in Center of Black Transition
            _warningCardObj = new GameObject("LockedZoneWarningCard", typeof(RectTransform), typeof(Image));
            _warningCardObj.transform.SetParent(_transitionOverlayObj.transform, false);
            RectTransform wcRect = _warningCardObj.GetComponent<RectTransform>();
            wcRect.anchorMin = new Vector2(0.20f, 0.28f);
            wcRect.anchorMax = new Vector2(0.80f, 0.72f);
            wcRect.offsetMin = Vector2.zero;
            wcRect.offsetMax = Vector2.zero;

            Image wcImg = _warningCardObj.GetComponent<Image>();
            if (registry != null && registry.dialogueBoardSprite != null)
            {
                wcImg.sprite = registry.dialogueBoardSprite;
                wcImg.color = Color.white;
            }
            else
            {
                wcImg.color = new Color(0.20f, 0.12f, 0.08f, 0.98f);
            }

            _warningTitleText = CreateTMP("WarningTitle", _warningCardObj.transform, font, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
            _warningTitleText.color = new Color(0.68f, 0.14f, 0.08f, 1f);
            _warningTitleText.text = "PATH CURRENTLY LOCKED";
            _warningTitleText.rectTransform.anchorMin = new Vector2(0.08f, 0.72f);
            _warningTitleText.rectTransform.anchorMax = new Vector2(0.92f, 0.90f);
            _warningTitleText.rectTransform.offsetMin = Vector2.zero;
            _warningTitleText.rectTransform.offsetMax = Vector2.zero;

            _warningBodyText = CreateTMP("WarningBody", _warningCardObj.transform, font, 19f, FontStyles.Normal, TextAlignmentOptions.Center);
            _warningBodyText.color = new Color(0.20f, 0.12f, 0.06f, 1f);
            _warningBodyText.textWrappingMode = TextWrappingModes.Normal;
            _warningBodyText.text = "The magical seal along this path is firmly locked. Defeat the previous boss first to break the barrier!";
            _warningBodyText.rectTransform.anchorMin = new Vector2(0.10f, 0.26f);
            _warningBodyText.rectTransform.anchorMax = new Vector2(0.90f, 0.70f);
            _warningBodyText.rectTransform.offsetMin = Vector2.zero;
            _warningBodyText.rectTransform.offsetMax = Vector2.zero;

            GameObject closeBtnObj = new GameObject("DismissWarningBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            closeBtnObj.transform.SetParent(_warningCardObj.transform, false);
            RectTransform cbRect = closeBtnObj.GetComponent<RectTransform>();
            cbRect.anchorMin = new Vector2(0.32f, 0.07f);
            cbRect.anchorMax = new Vector2(0.68f, 0.22f);
            cbRect.offsetMin = Vector2.zero;
            cbRect.offsetMax = Vector2.zero;

            Image cbImg = closeBtnObj.GetComponent<Image>();
            if (registry != null && registry.buttonNormalSprite != null)
            {
                cbImg.sprite = registry.buttonNormalSprite;
                cbImg.color = Color.white;
            }
            else
            {
                cbImg.color = new Color(0.45f, 0.26f, 0.12f, 1f);
            }

            _warningButtonLabel = CreateTMP("Label", closeBtnObj.transform, font, 17f, FontStyles.Bold, TextAlignmentOptions.Center);
            _warningButtonLabel.color = new Color(1f, 0.94f, 0.78f, 1f);
            _warningButtonLabel.text = "Understood [E / Space]";
            SetStretch(_warningButtonLabel.rectTransform, 6f, 2f, 6f, 2f);

            _warningCloseButton = closeBtnObj.GetComponent<Button>();
            BindRuntimeButtons();

            _transitionOverlayObj.SetActive(false);
        }

        private void RemoveLegacyCloudCurtainIfAny()
        {
            if (_transitionOverlayObj == null) return;
            Transform cc = _transitionOverlayObj.transform.Find("CloudCurtainContainer");
            if (cc != null)
            {
                if (Application.isPlaying) Destroy(cc.gameObject);
                else DestroyImmediate(cc.gameObject);
            }

            Image blackImg = _transitionOverlayObj.GetComponent<Image>();
            if (blackImg != null)
            {
                blackImg.color = new Color(0.03f, 0.02f, 0.05f, 0.96f);
            }
        }

        private void BindExistingUIReferences()
        {
            if (_zoneBannerObj != null)
            {
                if (_zoneBannerGroup == null) _zoneBannerGroup = _zoneBannerObj.GetComponent<CanvasGroup>();
                if (_zoneBannerText == null) _zoneBannerText = _zoneBannerObj.transform.Find("ZoneBannerText")?.GetComponent<TextMeshProUGUI>();
            }

            if (_transitionOverlayObj != null)
            {
                if (_transitionCanvasGroup == null) _transitionCanvasGroup = _transitionOverlayObj.GetComponent<CanvasGroup>();

                Transform wc = _transitionOverlayObj.transform.Find("LockedZoneWarningCard");
                if (wc != null)
                {
                    _warningCardObj = wc.gameObject;
                    if (_warningTitleText == null) _warningTitleText = wc.Find("WarningTitle")?.GetComponent<TextMeshProUGUI>();
                    if (_warningBodyText == null) _warningBodyText = wc.Find("WarningBody")?.GetComponent<TextMeshProUGUI>();
                    if (_warningCloseButton == null) _warningCloseButton = wc.Find("DismissWarningBtn")?.GetComponent<Button>();
                    if (_warningButtonLabel == null && _warningCloseButton != null)
                        _warningButtonLabel = _warningCloseButton.GetComponentInChildren<TextMeshProUGUI>(true);
                }
            }
        }

        private void BindRuntimeButtons()
        {
            if (!Application.isPlaying || _listenersBound) return;
            _listenersBound = true;

            if (_warningCloseButton != null)
            {
                _warningCloseButton.onClick.RemoveAllListeners();
                _warningCloseButton.onClick.AddListener(() => _waitingForWarningDismiss = false);
            }
        }

        private void Update()
        {
            if (_waitingForWarningDismiss)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                {
                    _waitingForWarningDismiss = false;
                }
                return;
            }

            if (_isTransitioning || Time.time < _cooldownUntilTime) return;
            if (PlayerController.Instance == null || PlayerController.Instance.IsMovementLocked) return;

            Vector2 playerPos = PlayerController.Instance.transform.position;
            MapZoneId currentZone = GameProgressionManager.Instance != null
                ? GameProgressionManager.Instance.CurrentZone
                : MapZoneId.DesaEldoria;

            // Tekan [H] untuk membuka petunjuk kapan saja
            Keyboard kbPress = Keyboard.current;
            if (kbPress != null && kbPress.hKey.wasPressedThisFrame)
            {
                ShowInitialGuidePrompt();
                return;
            }

            // Pintu Masuk / Trigger Goa 4 Bos
            if (currentZone == MapZoneId.Boss1_GunungBara && Boss1CavePortalRect.Contains(playerPos))
            {
                TriggerBossCaveEntry(0);
                return;
            }
            if (currentZone == MapZoneId.Boss2_GuaEsAbadi && Boss2CavePortalRect.Contains(playerPos))
            {
                TriggerBossCaveEntry(1);
                return;
            }
            if (currentZone == MapZoneId.Boss3_HutanRacun && Boss3CavePortalRect.Contains(playerPos))
            {
                TriggerBossCaveEntry(2);
                return;
            }
            if (currentZone == MapZoneId.Boss4_KastilBayangan && Boss4CavePortalRect.Contains(playerPos))
            {
                TriggerBossCaveEntry(3);
                return;
            }

            for (int i = 0; i < _gateways.Count; i++)
            {
                ZoneGatewayDefinition gw = _gateways[i];
                if (gw.fromZone == currentZone && gw.triggerRect.Contains(playerPos))
                {
                    TriggerGateway(gw);
                    break;
                }
            }

            // Clamp player softly inside the current zone's outer boundaries so they don't wander into the void
            if (!_isTransitioning && _zoneCameraBounds.TryGetValue(currentZone, out Rect zoneRect))
            {
                Vector3 pos = PlayerController.Instance.transform.position;
                float clampedX = Mathf.Clamp(pos.x, zoneRect.xMin + 0.6f, zoneRect.xMax - 0.6f);
                float clampedY = Mathf.Clamp(pos.y, zoneRect.yMin + 0.6f, zoneRect.yMax - 0.6f);
                if (Mathf.Abs(clampedX - pos.x) > 0.001f || Mathf.Abs(clampedY - pos.y) > 0.001f)
                {
                    PlayerController.Instance.transform.position = new Vector3(clampedX, clampedY, pos.z);
                }
            }
        }

        private void TriggerGateway(ZoneGatewayDefinition gateway)
        {
            var prog = GameProgressionManager.Instance;
            if (prog != null && !prog.CanEnterZone(gateway.toZone, out string warnTitle, out string warnBody))
            {
                StartCoroutine(BlockedZoneTransitionRoutine(gateway, warnTitle, warnBody));
            }
            else
            {
                StartCoroutine(AllowedZoneTransitionRoutine(gateway));
            }
        }

        private IEnumerator BlockedZoneTransitionRoutine(ZoneGatewayDefinition gateway, string warnTitle, string warnBody)
        {
            yield return StartCoroutine(ShowBlockedAccessRoutine(
                gateway.blockedPushbackPosition,
                gateway.blockedFacing,
                warnTitle,
                warnBody
            ));
        }

        private IEnumerator ShowBlockedAccessRoutine(Vector3 pushbackPos, FacingDirection pushbackFacing, string warnTitle, string warnBody)
        {
            _isTransitioning = true;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.IsMovementLocked = true;
            }

            _transitionOverlayObj.SetActive(true);
            _transitionOverlayObj.transform.SetAsLastSibling();
            if (_warningCardObj != null) _warningCardObj.SetActive(true);
            if (_warningTitleText != null) _warningTitleText.text = warnTitle;
            if (_warningBodyText != null) _warningBodyText.text = warnBody;

            // Fade to black
            float t = 0f;
            while (t < 0.22f)
            {
                t += Time.unscaledDeltaTime;
                _transitionCanvasGroup.alpha = Mathf.Clamp01(t / 0.22f);
                yield return null;
            }
            _transitionCanvasGroup.alpha = 1f;

            // Push player back to safe spot while screen is black
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.TeleportTo(pushbackPos, pushbackFacing);
            }

            // Wait until player presses E / Space / Click (or max 4.5 seconds)
            _waitingForWarningDismiss = true;
            float waitTimer = 0f;
            while (_waitingForWarningDismiss && waitTimer < 4.5f)
            {
                waitTimer += Time.unscaledDeltaTime;
                yield return null;
            }
            _waitingForWarningDismiss = false;

            // Fade back in
            t = 0f;
            while (t < 0.22f)
            {
                t += Time.unscaledDeltaTime;
                _transitionCanvasGroup.alpha = 1f - Mathf.Clamp01(t / 0.22f);
                yield return null;
            }

            _transitionCanvasGroup.alpha = 0f;
            _transitionOverlayObj.SetActive(false);

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.IsMovementLocked = false;
            }

            _cooldownUntilTime = Time.time + 0.55f;
            _isTransitioning = false;
        }

        private IEnumerator AllowedZoneTransitionRoutine(ZoneGatewayDefinition gateway)
        {
            _isTransitioning = true;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.IsMovementLocked = true;
            }

            _transitionOverlayObj.SetActive(true);
            _transitionOverlayObj.transform.SetAsLastSibling();
            if (_warningCardObj != null) _warningCardObj.SetActive(false);

            // Quick fade out to black
            float t = 0f;
            while (t < 0.16f)
            {
                t += Time.unscaledDeltaTime;
                _transitionCanvasGroup.alpha = Mathf.Clamp01(t / 0.16f);
                yield return null;
            }
            _transitionCanvasGroup.alpha = 1f;

            // Update zone & teleport player
            if (GameProgressionManager.Instance != null)
            {
                GameProgressionManager.Instance.SetCurrentZone(gateway.toZone);
            }
            ApplyCameraBoundsForZone(gateway.toZone);

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.TeleportTo(gateway.destinationPosition, gateway.destinationFacing);
            }

            yield return new WaitForSecondsRealtime(0.06f);

            // Quick fade in from black
            t = 0f;
            while (t < 0.18f)
            {
                t += Time.unscaledDeltaTime;
                _transitionCanvasGroup.alpha = 1f - Mathf.Clamp01(t / 0.18f);
                yield return null;
            }
            _transitionCanvasGroup.alpha = 0f;
            _transitionOverlayObj.SetActive(false);

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.IsMovementLocked = false;
            }

            ShowZoneBanner(GameProgressionManager.GetZoneDisplayName(gateway.toZone));

            _cooldownUntilTime = Time.time + 0.45f;
            _isTransitioning = false;
        }

        public void TriggerBossCaveEntry(int bossIdx)
        {
            if (_isTransitioning) return;
            var prog = GameProgressionManager.Instance;
            if (prog != null && prog.IsBossDefeated(bossIdx))
            {
                StartCoroutine(ShowBlockedAccessRoutine(
                    GetBossCavePushbackPosition(bossIdx),
                    FacingDirection.Down,
                    "STAGE ALREADY CLEARED",
                    $"You have already defeated {GetBossName(bossIdx)}!\n\nThis cave has been conquered and its HOKI fragment retrieved. Proceed to the next boss cave or return to the Sacred Altar!"
                ));
            }
            else
            {
                EnterBossScene(bossIdx);
            }
        }

        public void EnterBossScene(int bossIdx)
        {
            if (_isTransitioning) return;
            var prog = GameProgressionManager.Instance;
            if (prog != null && prog.IsBossDefeated(bossIdx))
            {
                StartCoroutine(ShowBlockedAccessRoutine(
                    GetBossCavePushbackPosition(bossIdx),
                    FacingDirection.Down,
                    "STAGE ALREADY CLEARED",
                    $"You have already defeated {GetBossName(bossIdx)}!\n\nThis cave has been conquered and its HOKI fragment retrieved."
                ));
                return;
            }
            StartCoroutine(EnterBossCaveRoutine(bossIdx));
        }

        private IEnumerator EnterBossCaveRoutine(int bossIdx)
        {
            _isTransitioning = true;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.IsMovementLocked = true;
            }

            if (_transitionOverlayObj != null)
            {
                _transitionOverlayObj.SetActive(true);
                _transitionOverlayObj.transform.SetAsLastSibling();
            }
            if (_warningCardObj != null) _warningCardObj.SetActive(false);

            float t = 0f;
            while (t < 0.28f)
            {
                t += Time.unscaledDeltaTime;
                if (_transitionCanvasGroup != null)
                {
                    _transitionCanvasGroup.alpha = Mathf.Clamp01(t / 0.28f);
                }
                yield return null;
            }
            if (_transitionCanvasGroup != null) _transitionCanvasGroup.alpha = 1f;

            if (GameProgressionManager.Instance != null)
            {
                GameProgressionManager.Instance.PrepareEnterBossScene(
                    GetBossReturnZone(bossIdx),
                    GetBossReturnPosition(bossIdx)
                );
            }

            string sceneName = GetBossSceneName(bossIdx);
            Debug.Log($"[MapZoneProgressionManager] Memasuki Goa/Arena {GetBossName(bossIdx)} -> Memuat scene '{sceneName}'...");
            SceneManager.LoadScene(sceneName);
        }

        private static string GetBossName(int idx)
        {
            switch (idx)
            {
                case 0: return "BOSS 1: Ignaroth";
                case 1: return "BOSS 2: Glaciera";
                case 2: return "BOSS 3: Venomark";
                case 3: return "BOSS 4: Nocthar";
                default: return "Boss";
            }
        }

        private static string GetBossSceneName(int idx)
        {
            switch (idx)
            {
                case 0: return Boss1SceneName;
                case 1: return Boss2SceneName;
                case 2: return Boss3SceneName;
                case 3: return Boss4SceneName;
                default: return Boss1SceneName;
            }
        }

        private static MapZoneId GetBossReturnZone(int idx)
        {
            switch (idx)
            {
                case 0: return MapZoneId.Boss1_GunungBara;
                case 1: return MapZoneId.Boss2_GuaEsAbadi;
                case 2: return MapZoneId.Boss3_HutanRacun;
                case 3: return MapZoneId.Boss4_KastilBayangan;
                default: return MapZoneId.Boss1_GunungBara;
            }
        }

        private static Vector3 GetBossReturnPosition(int idx)
        {
            switch (idx)
            {
                case 0: return new Vector3(-36.5f, 21.2f, 0f);
                case 1: return new Vector3(-12.5f, 49.8f, 0f);
                case 2: return new Vector3(14.5f, 49.8f, 0f);
                case 3: return new Vector3(24.0f, 23.5f, 0f);
                default: return new Vector3(-36.5f, 21.2f, 0f);
            }
        }

        private static Vector3 GetBossCavePushbackPosition(int idx)
        {
            switch (idx)
            {
                case 0: return new Vector3(-36.5f, 20.8f, 0f);
                case 1: return new Vector3(-12.5f, 49.2f, 0f);
                case 2: return new Vector3(14.5f, 49.2f, 0f);
                case 3: return new Vector3(23.2f, 23.5f, 0f);
                default: return new Vector3(-36.5f, 20.8f, 0f);
            }
        }

        private IEnumerator DelayedInitialGuidePrompt()
        {
            yield return new WaitForSeconds(0.6f);
            while (LuckyNumber.UI.CutsceneManager.Instance != null && LuckyNumber.UI.CutsceneManager.Instance.IsPlaying)
            {
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            if (!_hasShownInitialGuide)
            {
                ShowInitialGuidePrompt();
            }
        }

        public void ShowInitialGuidePrompt()
        {
            if (_isTransitioning) return;
            StartCoroutine(ShowGuideModalRoutine());
        }

        private IEnumerator ShowGuideModalRoutine()
        {
            _hasShownInitialGuide = true;
            _isTransitioning = true;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.IsMovementLocked = true;
            }

            if (_transitionOverlayObj != null)
            {
                _transitionOverlayObj.SetActive(true);
                _transitionOverlayObj.transform.SetAsLastSibling();
            }
            if (_warningCardObj != null) _warningCardObj.SetActive(true);
            if (_warningTitleText != null) _warningTitleText.text = "★ MALIOS JOURNEY GUIDE ★";
            if (_warningBodyText != null)
            {
                _warningBodyText.text =
                    "Welcome to Aetheria Island!\n\n" +
                    "<b>Main Steps and Guide:</b>\n" +
                    "1. <b>Talk to Village Chief Arvand</b> in the center of Eldoria Village [Press E] for initial guidance.\n" +
                    "2. <b>Head North (Demon Crossroads)</b> and defeat the 4 cave bosses in sequence:\n" +
                    "   • <b>Cave 1: Ignaroth (Mount Cinder)</b>: Lucky 13 Challenge\n" +
                    "   • <b>Cave 2: Glaciera (Eternal Ice Cavern)</b>: Frozen Memory 13 Pairs\n" +
                    "   • <b>Cave 3: Venomark (Poison Forest)</b>: Poison Concoction 21\n" +
                    "   • <b>Cave 4: Nocthar (Shadow Castle)</b>: Shadow Logic Sequence\n" +
                    "3. <b>Collect all 4 HOKI Fragments</b> and restore the Sacred Altar in Eldoria Village!\n\n" +
                    "<b>Controls:</b> [W,A,S,D / Arrows] Move  |  [E] Interact and Enter Cave  |  [H] Guide";
            }
            if (_warningButtonLabel != null) _warningButtonLabel.text = "Start Adventure [E / Space]";

            float t = 0f;
            while (t < 0.22f)
            {
                t += Time.unscaledDeltaTime;
                if (_transitionCanvasGroup != null) _transitionCanvasGroup.alpha = Mathf.Clamp01(t / 0.22f);
                yield return null;
            }
            if (_transitionCanvasGroup != null) _transitionCanvasGroup.alpha = 1f;

            _waitingForWarningDismiss = true;
            while (_waitingForWarningDismiss)
            {
                yield return null;
            }

            if (_warningCardObj != null) _warningCardObj.SetActive(false);
            if (_warningButtonLabel != null) _warningButtonLabel.text = "Return to Safe Path [E / Space]";

            t = 0f;
            while (t < 0.22f)
            {
                t += Time.unscaledDeltaTime;
                if (_transitionCanvasGroup != null) _transitionCanvasGroup.alpha = 1f - Mathf.Clamp01(t / 0.22f);
                yield return null;
            }
            if (_transitionCanvasGroup != null) _transitionCanvasGroup.alpha = 0f;
            if (_transitionOverlayObj != null) _transitionOverlayObj.SetActive(false);

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.IsMovementLocked = false;
            }
            _cooldownUntilTime = Time.time + 0.5f;
            _isTransitioning = false;
        }

        private IEnumerator FadeInFromBlackOnReturn(string bannerTitle)
        {
            if (_transitionOverlayObj != null && _transitionCanvasGroup != null)
            {
                _transitionOverlayObj.SetActive(true);
                _transitionOverlayObj.transform.SetAsLastSibling();
                if (_warningCardObj != null) _warningCardObj.SetActive(false);
                _transitionCanvasGroup.alpha = 1f;

                float t = 0f;
                while (t < 0.25f)
                {
                    t += Time.unscaledDeltaTime;
                    _transitionCanvasGroup.alpha = 1f - Mathf.Clamp01(t / 0.25f);
                    yield return null;
                }
                _transitionCanvasGroup.alpha = 0f;
                _transitionOverlayObj.SetActive(false);
            }

            ShowZoneBanner(bannerTitle);
        }

        private void ApplyCameraBoundsForZone(MapZoneId zone)
        {
            if (SmoothCameraFollow.Instance != null && _zoneCameraBounds.TryGetValue(zone, out Rect bounds))
            {
                SmoothCameraFollow.Instance.SetZoneBounds(bounds, true);
                SmoothCameraFollow.Instance.SnapToTarget();
            }
        }

        public void ShowZoneBanner(string zoneTitle)
        {
            if (_zoneBannerObj == null) return;
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(ZoneBannerRoutine(zoneTitle));
        }

        private IEnumerator ZoneBannerRoutine(string zoneTitle)
        {
            _zoneBannerObj.SetActive(true);
            if (_zoneBannerText != null) _zoneBannerText.text = zoneTitle;

            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                _zoneBannerGroup.alpha = Mathf.Clamp01(t / 0.25f);
                yield return null;
            }
            _zoneBannerGroup.alpha = 1f;

            yield return new WaitForSeconds(1.8f);

            t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                _zoneBannerGroup.alpha = 1f - Mathf.Clamp01(t / 0.35f);
                yield return null;
            }
            _zoneBannerGroup.alpha = 0f;
            _zoneBannerObj.SetActive(false);
        }

        public void CreateWorldSignpostsAndBossMarkers()
        {
            _gateSignpostLabels.Clear();

            // 4 Crossroads Signposts so the player clearly sees the 4 sequential boss paths
            _gateSignpostLabels.Add(GetOrCreateWorldLabel("Sign_Boss1", new Vector3(-9.5f, 25.0f, 0f), ""));
            _gateSignpostLabels.Add(GetOrCreateWorldLabel("Sign_Boss2", new Vector3(-4.0f, 30.8f, 0f), ""));
            _gateSignpostLabels.Add(GetOrCreateWorldLabel("Sign_Boss3", new Vector3(3.8f, 30.8f, 0f), ""));
            _gateSignpostLabels.Add(GetOrCreateWorldLabel("Sign_Boss4", new Vector3(8.5f, 25.0f, 0f), ""));
            GetOrCreateWorldLabel("Sign_BackToVillage", new Vector3(0f, 14.0f, 0f), "▼ South: Eldoria Village");
            GetOrCreateWorldLabel("Sign_NorthToCrossroads", new Vector3(0f, 7.8f, 0f), "▲ North: Crossroads of the 4 Demons");

            // Clean up Sacred Altar and its elements
            Transform altarObj = transform.Find("Altar_Suci_HOKI");
            if (altarObj != null)
            {
                if (Application.isPlaying) Destroy(altarObj.gameObject);
                else DestroyImmediate(altarObj.gameObject);
            }
            Transform altarLabel = transform.Find("AltarLabel");
            if (altarLabel != null)
            {
                if (Application.isPlaying) Destroy(altarLabel.gameObject);
                else DestroyImmediate(altarLabel.gameObject);
            }

            // Clean up any old text labels in front of the caves
            string[] labelsToClean = {
                "Boss1_CavePortalLabel", "Boss2_CavePortalLabel", "Boss3_CavePortalLabel", "Boss4_CavePortalLabel",
                "BossLabel_1", "BossLabel_2", "BossLabel_3", "BossLabel_4"
            };
            foreach (string lbl in labelsToClean)
            {
                Transform t = transform.Find(lbl);
                if (t != null)
                {
                    if (Application.isPlaying) Destroy(t.gameObject);
                    else DestroyImmediate(t.gameObject);
                }
            }

            // Create Boss Encounter markers right in front of the cave entrances with floating exclamation mark icon (!)
            CreateBossPedestal(0, "BOSS 1: Ignaroth", new Vector3(-36.5f, 22.8f, 0f));
            CreateBossPedestal(1, "BOSS 2: Glaciera", new Vector3(-12.5f, 52.0f, 0f));
            CreateBossPedestal(2, "BOSS 3: Venomark", new Vector3(14.5f, 52.0f, 0f));
            CreateBossPedestal(3, "BOSS 4: Nocthar", new Vector3(26.5f, 23.5f, 0f));
        }

        private void RefreshGateSignposts()
        {
            if (_gateSignpostLabels.Count < 4) return;
            var prog = GameProgressionManager.Instance;

            bool b1Open = prog != null && prog.HasTalkedToArvand;
            bool b1Done = prog != null && prog.IsBossDefeated(0);
            bool b2Done = prog != null && prog.IsBossDefeated(1);
            bool b3Done = prog != null && prog.IsBossDefeated(2);
            bool b4Done = prog != null && prog.IsBossDefeated(3);

            _gateSignpostLabels[0].text = b1Done
                ? "◀ BOSS 1: Ignaroth\n[CLEARED ✔]"
                : (b1Open ? "◀ BOSS 1: Mount Cinder\n[OPEN]" : "◀ BOSS 1: Mount Cinder\n[SPEAK WITH ARVAND]");
            _gateSignpostLabels[0].color = b1Done ? new Color(0.5f, 1f, 0.5f) : (b1Open ? new Color(1f, 0.92f, 0.45f) : new Color(1f, 0.5f, 0.5f));

            _gateSignpostLabels[1].text = b2Done
                ? "▲ BOSS 2: Glaciera\n[CLEARED ✔]"
                : (b1Done ? "▲ BOSS 2: Eternal Ice Cavern\n[OPEN]" : "▲ BOSS 2: Eternal Ice Cavern\n[LOCKED: Needs Boss 1 Key]");
            _gateSignpostLabels[1].color = b2Done ? new Color(0.5f, 1f, 0.5f) : (b1Done ? new Color(1f, 0.92f, 0.45f) : new Color(1f, 0.5f, 0.5f));

            _gateSignpostLabels[2].text = b3Done
                ? "▲ BOSS 3: Venomark\n[CLEARED ✔]"
                : (b2Done ? "▲ BOSS 3: Poison Forest\n[OPEN]" : "▲ BOSS 3: Poison Forest\n[LOCKED: Needs Boss 2 Key]");
            _gateSignpostLabels[2].color = b3Done ? new Color(0.5f, 1f, 0.5f) : (b2Done ? new Color(1f, 0.92f, 0.45f) : new Color(1f, 0.5f, 0.5f));

            _gateSignpostLabels[3].text = b4Done
                ? "▶ BOSS 4: Nocthar\n[CLEARED ✔]"
                : (b3Done ? "▶ BOSS 4: Shadow Castle\n[OPEN]" : "▶ BOSS 4: Shadow Castle\n[LOCKED: Needs Boss 3 Key]");
            _gateSignpostLabels[3].color = b4Done ? new Color(0.5f, 1f, 0.5f) : (b3Done ? new Color(1f, 0.92f, 0.45f) : new Color(1f, 0.5f, 0.5f));

            // Hide the exclamation mark icon (!) in front of the cave if the boss is already defeated
            for (int i = 0; i < 4; i++)
            {
                Transform encounter = transform.Find($"BossEncounter_{i + 1}");
                if (encounter != null)
                {
                    bool isDefeated = prog != null && prog.IsBossDefeated(i);
                    encounter.gameObject.SetActive(!isDefeated);
                }
            }
        }

        private TextMeshPro GetOrCreateWorldLabel(string name, Vector3 position, string content)
        {
            Transform existing = transform.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(TextMeshPro));
            if (existing == null)
            {
                go.transform.SetParent(transform, false);
            }
            go.transform.position = position;

            TextMeshPro tmp = go.GetComponent<TextMeshPro>();
            if (existing == null || !string.IsNullOrEmpty(content))
            {
                tmp.text = content;
            }
            tmp.fontSize = 3.2f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.95f, 0.78f, 1f);
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = new Color32(20, 12, 6, 255);
            tmp.sortingOrder = 120;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerName = "Prop";
                mr.sortingOrder = 120;
            }
            return tmp;
        }

        private void CreateBossPedestal(int bossIndex, string bossTitle, Vector3 position)
        {
            string objName = $"BossEncounter_{bossIndex + 1}";
            Transform existing = transform.Find(objName);
            GameObject bossObj = existing != null ? existing.gameObject : new GameObject(objName, typeof(NPCInteractable));
            if (existing == null)
            {
                bossObj.transform.SetParent(transform, false);
            }
            bossObj.transform.position = position;

            NPCInteractable interactable = bossObj.GetComponent<NPCInteractable>();
            interactable.Configure(InteractableRole.BossEncounter, bossTitle, bossIndex, 2.8f);

            // Ensure no old text labels remain
            Transform oldLabel = transform.Find($"BossLabel_{bossIndex + 1}");
            if (oldLabel != null)
            {
                if (Application.isPlaying) Destroy(oldLabel.gameObject);
                else DestroyImmediate(oldLabel.gameObject);
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
