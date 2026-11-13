using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LuckyNumber.Characters;
using LuckyNumber.UI;

namespace LuckyNumber.Core
{
    public class GameWorldBootstrapper : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapOnSceneLoad()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name == "TestMap" || FindFirstObjectByType<Grid>() != null)
            {
                EnsureWorldSetup();
            }
        }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                EnsureWorldSetup();
            }
        }

        /// <summary>
        /// Safe to call in both Edit Mode and Play Mode: ensures Malios (with Footstep VFX),
        /// Arvand, Camera Follow, and Prop Colliders exist in the scene.
        /// </summary>
        public static void EnsureCharactersAndCameraInScene(GameAssetRegistry registry)
        {
            if (registry == null) registry = GameAssetRegistry.Instance;

            // 1. Player Malios in Desa Eldoria
            PlayerController player = FindFirstObjectByType<PlayerController>();
            GameObject maliosObj = player != null ? player.gameObject : GameObject.Find("Player_Malios");
            if (maliosObj == null)
            {
                maliosObj = new GameObject(
                    "Player_Malios",
                    typeof(SpriteRenderer),
                    typeof(Rigidbody2D),
                    typeof(CircleCollider2D),
                    typeof(CharacterSpriteAnimator),
                    typeof(PlayerFootstepVFX),
                    typeof(PlayerController)
                );
                maliosObj.transform.position = new Vector3(0f, -2.2f, 0f);
                maliosObj.transform.localScale = new Vector3(0.22f, 0.22f, 1f);
            }
            else if (maliosObj.GetComponent<PlayerFootstepVFX>() == null)
            {
                maliosObj.AddComponent<PlayerFootstepVFX>();
            }

            SpriteRenderer maliosSr = maliosObj.GetComponent<SpriteRenderer>();
            if (maliosSr != null)
            {
                maliosSr.sortingLayerName = "Prop";
                maliosSr.sortingOrder = 1000;
                if (registry != null && registry.maliosSprites != null && registry.maliosSprites.Length > 0 && registry.maliosSprites[0] != null)
                {
                    maliosSr.sprite = registry.maliosSprites[0];
                }
            }

            CharacterSpriteAnimator maliosAnim = maliosObj.GetComponent<CharacterSpriteAnimator>();
            if (maliosAnim != null && registry != null && registry.maliosSprites != null && registry.maliosSprites.Length > 0)
            {
                maliosAnim.SetSprites(registry.maliosSprites, false);
            }

            // 2. Village Chief Arvand (Idle Animated NPC) in Desa Eldoria
            GameObject arvandObj = GameObject.Find("NPC_Arvand");
            if (arvandObj == null)
            {
                arvandObj = new GameObject(
                    "NPC_Arvand",
                    typeof(SpriteRenderer),
                    typeof(CharacterSpriteAnimator),
                    typeof(NPCInteractable)
                );
                arvandObj.transform.position = new Vector3(0f, 1.5f, 0f);
                arvandObj.transform.localScale = new Vector3(0.22f, 0.22f, 1f);
            }

            SpriteRenderer arvandSr = arvandObj.GetComponent<SpriteRenderer>();
            if (arvandSr != null)
            {
                arvandSr.sortingLayerName = "Prop";
                arvandSr.sortingOrder = 985;
                if (registry != null && registry.arvandSprites != null && registry.arvandSprites.Length > 0 && registry.arvandSprites[0] != null)
                {
                    arvandSr.sprite = registry.arvandSprites[0];
                }
            }

            CharacterSpriteAnimator arvandAnim = arvandObj.GetComponent<CharacterSpriteAnimator>();
            if (arvandAnim != null && registry != null && registry.arvandSprites != null && registry.arvandSprites.Length > 0)
            {
                arvandAnim.SetSprites(registry.arvandSprites, true);
            }

            NPCInteractable arvandNpc = arvandObj.GetComponent<NPCInteractable>();
            if (arvandNpc != null)
            {
                arvandNpc.Configure(InteractableRole.ArvandVillageChief, "Chief Arvand", 0, 2.8f);
            }

            // 3. Attach SmoothCameraFollow to Main Camera
            Camera cam = Camera.main;
            if (cam != null)
            {
                SmoothCameraFollow follow = cam.GetComponent<SmoothCameraFollow>();
                if (follow == null)
                {
                    follow = cam.gameObject.AddComponent<SmoothCameraFollow>();
                }
                follow.SetTarget(maliosObj.transform, true);
            }

            // 4. Ensure solid colliders on all Prop Tilemaps (excluding grass/flowers/puddles)
            WorldPropColliderBuilder.RebuildAllWorldColliders();
        }

        /// <summary>
        /// Safe to call in both Edit Mode and Play Mode!
        /// Creates/updates LuckyNumber_GameSystems and LuckyNumber_UICanvas (with Cutscene, Dialogue, HUD & Cloud Transition panels)
        /// directly in the scene Hierarchy so they can be inspected and customized in Edit Mode.
        /// </summary>
        public static void EnsureWorldSetup()
        {
            var registry = GameAssetRegistry.Instance;

            // 1. Ensure Core Managers exist on LuckyNumber_GameSystems
            GameObject sysObj = GameObject.Find("LuckyNumber_GameSystems");
            GameProgressionManager progMgr = FindFirstObjectByType<GameProgressionManager>();
            if (sysObj == null && progMgr != null)
            {
                sysObj = progMgr.gameObject;
            }
            if (sysObj == null)
            {
                sysObj = new GameObject("LuckyNumber_GameSystems");
            }

            if (sysObj.GetComponent<GameProgressionManager>() == null)
                sysObj.AddComponent<GameProgressionManager>();
            MapZoneProgressionManager zoneMgr = sysObj.GetComponent<MapZoneProgressionManager>();
            if (zoneMgr == null)
                zoneMgr = sysObj.AddComponent<MapZoneProgressionManager>();
            if (sysObj.GetComponent<WorldPropColliderBuilder>() == null)
                sysObj.AddComponent<WorldPropColliderBuilder>();
            if (sysObj.GetComponent<WorldGrassAnimator>() == null)
                sysObj.AddComponent<WorldGrassAnimator>();
            WorldLightingManager lightingMgr = sysObj.GetComponent<WorldLightingManager>();
            if (lightingMgr == null)
                lightingMgr = sysObj.AddComponent<WorldLightingManager>();
            DialogueSystem dlgSys = sysObj.GetComponent<DialogueSystem>();
            if (dlgSys == null)
                dlgSys = sysObj.AddComponent<DialogueSystem>();
            CutsceneManager cutMgr = sysObj.GetComponent<CutsceneManager>();
            if (cutMgr == null)
                cutMgr = sysObj.AddComponent<CutsceneManager>();

            // 2. Ensure EventSystem with InputSystemUIInputModule exists
            EventSystem es = FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }
            else if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                {
                    if (Application.isPlaying) Destroy(legacy);
                    else DestroyImmediate(legacy);
                }
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            // 3. Ensure Main UI Canvas exists in Scene Hierarchy
            Canvas mainCanvas = null;
            GameObject existingCanvasObj = GameObject.Find("LuckyNumber_UICanvas");
            if (existingCanvasObj != null)
            {
                mainCanvas = existingCanvasObj.GetComponent<Canvas>();
            }

            if (mainCanvas == null)
            {
                GameObject canvasObj = new GameObject("LuckyNumber_UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                mainCanvas = canvasObj.GetComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                mainCanvas.sortingOrder = 100;

                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            // Initialize / bind UI panels on LuckyNumber_UICanvas (works in both Edit Mode and Play Mode)
            dlgSys.InitializeUI(mainCanvas.transform);
            zoneMgr.InitializeUI(mainCanvas.transform);
            zoneMgr.CreateWorldSignpostsAndBossMarkers();
            cutMgr.InitializeUI(mainCanvas.transform);

            // 4. Ensure Malios, Arvand, Camera Follow, and Prop Colliders exist
            EnsureCharactersAndCameraInScene(registry);

            // 5. Ensure 2D URP Lighting & Sprite-Lit materials are configured in Hierarchy
            lightingMgr.SetupWorldLighting();
        }
    }
}
