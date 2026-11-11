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
        /// Safe to call in both Edit Mode and Play Mode: ensures Malios and Arvand exist in the scene
        /// with their initial sliced sprites visible in Scene View and Game View.
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
                    typeof(PlayerController)
                );
                maliosObj.transform.position = new Vector3(0f, -2.2f, 0f);
                maliosObj.transform.localScale = new Vector3(0.22f, 0.22f, 1f);
            }

            SpriteRenderer maliosSr = maliosObj.GetComponent<SpriteRenderer>();
            if (maliosSr != null)
            {
                maliosSr.sortingLayerName = "Prop";
                maliosSr.sortingOrder = 60;
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
                arvandSr.sortingOrder = 45;
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
                arvandNpc.Configure(InteractableRole.ArvandVillageChief, "Kepala Desa Arvand", 0, 2.8f);
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
                // Ensure solid colliders on all Prop Tilemaps (excluding grass/flowers/puddles)
                WorldPropColliderBuilder.RebuildAllWorldColliders();
            }
        }

        public static void EnsureWorldSetup()
        {
            var registry = GameAssetRegistry.Instance;

            // 1. Ensure Core Managers exist
            GameProgressionManager progMgr = FindFirstObjectByType<GameProgressionManager>();
            if (progMgr == null)
            {
                GameObject sysObj = new GameObject("LuckyNumber_GameSystems");
                progMgr = sysObj.AddComponent<GameProgressionManager>();
                sysObj.AddComponent<MapZoneProgressionManager>();
                sysObj.AddComponent<WorldPropColliderBuilder>();
                sysObj.AddComponent<DialogueSystem>();
                sysObj.AddComponent<CutsceneManager>();
            }
            else
            {
                if (FindFirstObjectByType<MapZoneProgressionManager>() == null)
                    progMgr.gameObject.AddComponent<MapZoneProgressionManager>();
                if (FindFirstObjectByType<WorldPropColliderBuilder>() == null)
                    progMgr.gameObject.AddComponent<WorldPropColliderBuilder>();
                if (FindFirstObjectByType<DialogueSystem>() == null)
                    progMgr.gameObject.AddComponent<DialogueSystem>();
                if (FindFirstObjectByType<CutsceneManager>() == null)
                    progMgr.gameObject.AddComponent<CutsceneManager>();
            }

            WorldPropColliderBuilder.RebuildAllWorldColliders();

            // 2. Ensure EventSystem with InputSystemUIInputModule exists
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

            // 3. Ensure Main UI Canvas exists
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

            // Initialize UIs on Canvas
            if (DialogueSystem.Instance != null)
                DialogueSystem.Instance.InitializeUI(mainCanvas.transform);
            if (MapZoneProgressionManager.Instance != null)
                MapZoneProgressionManager.Instance.InitializeUI(mainCanvas.transform);
            if (CutsceneManager.Instance != null)
                CutsceneManager.Instance.InitializeUI(mainCanvas.transform);

            // 4. Ensure Malios, Arvand, and Camera Follow exist
            EnsureCharactersAndCameraInScene(registry);
        }
    }
}
