#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using TMPro;
using LuckyNumber.Core;
using LuckyNumber.UI;

namespace LuckyNumber.EditorTools
{
    [InitializeOnLoad]
    public static class SpriteSliceAndProjectSetupEditor
    {
        private const string SessionInitKey = "LuckyNumber_ProjectSetup_Done_v4";

        static SpriteSliceAndProjectSetupEditor()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool(SessionInitKey, false)) return;
                SessionState.SetBool(SessionInitKey, true);

                RunAutomaticSetup(silent: true);
            };
        }

        [MenuItem("Tools/Lucky Number/1. Slice Character Sprites & Setup TestMap Hierarchy")]
        public static void RunManualSetup()
        {
            RunAutomaticSetup(silent: false);
        }

        [MenuItem("Tools/Lucky Number/2. Rebuild Prop Colliders (Exclude Grass & Flowers)")]
        public static void RunRebuildPropCollidersMenu()
        {
            int tileAssetsUpdated = ConfigurePalettePropTileAssets();
            int sceneColliders = WorldPropColliderBuilder.RebuildAllWorldColliders();
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }
            Debug.Log($"[Lucky Number] Berhasil mengatur {tileAssetsUpdated} Tile Asset di PaletProp dan membangun {sceneColliders} collider elemen solid (tanpa rumput/bunga kecil)!");
        }

        [MenuItem("Tools/Lucky Number/3. Toggle Preview Panel Cutscene di Scene")]
        public static void ToggleCutscenePanelPreview()
        {
            GameWorldBootstrapper.EnsureWorldSetup();
            GameObject canvas = GameObject.Find("LuckyNumber_UICanvas");
            if (canvas == null) return;
            Transform cutscene = canvas.transform.Find("CutsceneComicOverlay");
            if (cutscene != null)
            {
                bool nextState = !cutscene.gameObject.activeSelf;
                cutscene.gameObject.SetActive(nextState);
                Selection.activeGameObject = cutscene.gameObject;
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[Lucky Number] Preview CutsceneComicOverlay: {(nextState ? "AKTIF (Silakan edit Font/Layout di Inspector)" : "DISEMBUNYIKAN")}");
            }
        }

        [MenuItem("Tools/Lucky Number/4. Toggle Preview Panel Dialog di Scene")]
        public static void ToggleDialoguePanelPreview()
        {
            GameWorldBootstrapper.EnsureWorldSetup();
            GameObject canvas = GameObject.Find("LuckyNumber_UICanvas");
            if (canvas == null) return;
            Transform dlg = canvas.transform.Find("DialogueSystemPanel");
            if (dlg != null)
            {
                bool nextState = !dlg.gameObject.activeSelf;
                dlg.gameObject.SetActive(nextState);
                Selection.activeGameObject = dlg.gameObject;
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[Lucky Number] Preview DialogueSystemPanel: {(nextState ? "AKTIF (Silakan edit Font/Layout di Inspector)" : "DISEMBUNYIKAN")}");
            }
        }

        [MenuItem("Tools/Lucky Number/5. Apply Custom Font (GameAssetRegistry) ke Semua UI")]
        public static void ApplyCustomFontToAllSceneUI()
        {
            var registry = GameAssetRegistry.Instance;
            if (registry == null || registry.customUIFont == null)
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Resources/GameAssetRegistry.asset");
                Debug.LogWarning("[Lucky Number] Silakan isi field 'Custom UI Font' pada Assets/Resources/GameAssetRegistry.asset terlebih dahulu, lalu klik menu ini lagi!");
                return;
            }

            TMP_FontAsset font = registry.customUIFont;
            int count = 0;

            foreach (var tmp in UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(tmp, "Apply Custom UI Font");
                tmp.font = font;
                EditorUtility.SetDirty(tmp);
                count++;
            }

            foreach (var tmpWorld in UnityEngine.Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(tmpWorld, "Apply Custom World Font");
                tmpWorld.font = font;
                EditorUtility.SetDirty(tmpWorld);
                count++;
            }

            var dlg = UnityEngine.Object.FindFirstObjectByType<DialogueSystem>();
            if (dlg != null) dlg.ApplyFont(font);
            var cut = UnityEngine.Object.FindFirstObjectByType<CutsceneManager>();
            if (cut != null) cut.ApplyFont(font);
            var zone = UnityEngine.Object.FindFirstObjectByType<MapZoneProgressionManager>();
            if (zone != null) zone.ApplyFont(font);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Lucky Number] Berhasil menerapkan font '{font.name}' ke {count} komponen teks UI & World di scene!");
        }

        private static void RunAutomaticSetup(bool silent)
        {
            try
            {
                // 1. Slice / Verify Character Sprite Sheets (6 cols x 4 rows) using ISpriteEditorDataProvider
                EnsureCharacterSheetSliced("Assets/Asset/Karakter/malios.png", "malios", 6, 4);
                EnsureCharacterSheetSliced("Assets/Asset/Karakter/arvand.png", "arvand", 6, 4);
                EnsureCharacterSheetSliced("Assets/Resources/Characters/malios.png", "malios", 6, 4);
                EnsureCharacterSheetSliced("Assets/Resources/Characters/arvand.png", "arvand", 6, 4);

                // 2. Ensure Dust Sprite Sheets in Resources/VFX are sliced (8 frames & 10 frames)
                EnsureCharacterSheetSliced("Assets/Resources/VFX/Dust_01.png", "Dust_01", 8, 1);
                EnsureCharacterSheetSliced("Assets/Resources/VFX/Dust_02.png", "Dust_02", 10, 1);

                // 3. Ensure Cutscene, UI & Cloud textures in Resources are imported as Single Sprites
                ConfigureFolderAsSingleSprites("Assets/Resources/Cutscenes");
                ConfigureFolderAsSingleSprites("Assets/Resources/UI");
                ConfigureCloudsAsSingleSprites("Assets/Resources/VFX");

                // 4. Configure Tile assets in Assets/Sprites/PaletProp (None for grass/flowers, Sprite for solid props)
                ConfigurePalettePropTileAssets();

                // 5. Populate GameAssetRegistry.asset in Assets/Resources
                GameAssetRegistry registry = PopulateGameAssetRegistry();

                // 6. If TestMap scene is currently open in Editor, create full UI Canvas, Systems, Characters & Colliders in Hierarchy!
                Scene activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid() && (activeScene.name == "TestMap" || UnityEngine.Object.FindFirstObjectByType<Grid>() != null))
                {
                    if (UnityEngine.Object.FindFirstObjectByType<GameWorldBootstrapper>() == null)
                    {
                        new GameObject("LuckyNumber_Bootstrapper", typeof(GameWorldBootstrapper));
                    }
                    GameWorldBootstrapper.EnsureWorldSetup();
                    EditorSceneManager.MarkSceneDirty(activeScene);
                }

                if (!silent)
                {
                    Debug.Log("[Lucky Number] Berhasil menyiapkan LuckyNumber_UICanvas (Cutscene, Dialog, HUD, Transisi Awan), VFX Langkah Kaki Malios, dan Collider di Hierarchy TestMap!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Lucky Number Setup] {ex.Message}");
            }
        }

        private static int ConfigurePalettePropTileAssets()
        {
            const string folderPath = "Assets/Sprites/PaletProp";
            if (!AssetDatabase.IsValidFolder(folderPath)) return 0;

            string[] guids = AssetDatabase.FindAssets("t:Tile", new[] { folderPath });
            int updatedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null) continue;

                string spriteName = tile.sprite != null ? tile.sprite.name : tile.name;
                PropColliderCategory category = WorldPropColliderBuilder.ClassifyPropSprite(spriteName, Vector3Int.zero);
                Tile.ColliderType desiredType = (category == PropColliderCategory.None)
                    ? Tile.ColliderType.None
                    : Tile.ColliderType.Sprite;

                if (tile.colliderType != desiredType)
                {
                    tile.colliderType = desiredType;
                    EditorUtility.SetDirty(tile);
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                AssetDatabase.SaveAssets();
            }
            return updatedCount;
        }

        /// <summary>
        /// Follows the Safe Core Pattern with mandatory ISpriteFrameEditCapability checks
        /// to slice or update pivots on a sprite sheet.
        /// </summary>
        public static void EnsureCharacterSheetSliced(string assetPath, string baseName, int cols, int rows)
        {
            TextureImporter texImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (texImporter == null) return;

            bool needsReimportPrelim = false;
            if (texImporter.textureType != TextureImporterType.Sprite)
            {
                texImporter.textureType = TextureImporterType.Sprite;
                needsReimportPrelim = true;
            }
            if (texImporter.spriteImportMode != SpriteImportMode.Multiple)
            {
                texImporter.spriteImportMode = SpriteImportMode.Multiple;
                needsReimportPrelim = true;
            }
            if (!texImporter.isReadable)
            {
                texImporter.isReadable = true;
                needsReimportPrelim = true;
            }
            if (needsReimportPrelim)
            {
                texImporter.SaveAndReimport();
            }

            // 1. Get and Init Data Provider
            var importer = AssetImporter.GetAtPath(assetPath);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (dataProvider == null) return;
            dataProvider.InitSpriteEditorDataProvider();

            // 2. MANDATORY: Check Capabilities - ABORT if not supported
            var editCapability = dataProvider.GetDataProvider<ISpriteFrameEditCapability>();
            if (editCapability == null)
            {
                throw new Exception("Edit capability not supported by importer. Operation aborted.");
            }

            var capability = editCapability.GetEditCapability();
            if (!capability.HasCapability(EEditCapability.EditSpriteRect) ||
                !capability.HasCapability(EEditCapability.EditPivot) ||
                !capability.HasCapability(EEditCapability.EditSpriteName) ||
                !capability.HasCapability(EEditCapability.CreateAndDeleteSprite))
            {
                throw new Exception("Operation not supported by importer. User action aborted.");
            }

            // 3. Read and Modify SpriteRects
            var existingRects = dataProvider.GetSpriteRects();
            bool modified = false;

            if (existingRects == null || existingRects.Length < cols * rows)
            {
                var textureProvider = dataProvider.GetDataProvider<ITextureDataProvider>();
                int actualWidth = 2048;
                int actualHeight = 1360;
                if (textureProvider != null)
                {
                    textureProvider.GetTextureActualWidthAndHeight(out actualWidth, out actualHeight);
                }

                float cellW = (float)actualWidth / cols;
                float cellH = (float)actualHeight / rows;

                List<SpriteRect> newRects = new List<SpriteRect>(cols * rows);
                var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                List<SpriteNameFileIdPair> pairs = new List<SpriteNameFileIdPair>();

                for (int r = 0; r < rows; r++)
                {
                    int unityRow = (rows - 1) - r;
                    for (int c = 0; c < cols; c++)
                    {
                        int index = r * cols + c;
                        float padX = rows > 1 ? cellW * 0.04f : 0f;
                        float padY = rows > 1 ? cellH * 0.01f : 0f;

                        var sr = new SpriteRect
                        {
                            name = $"{baseName}_{index}",
                            spriteID = GUID.Generate(),
                            alignment = (SpriteAlignment)(int)SpriteAlignment.Center,
                            pivot = new Vector2(0.5f, 0.5f),
                            rect = new Rect(
                                Mathf.Round(c * cellW + padX),
                                Mathf.Round(unityRow * cellH + padY),
                                Mathf.Round(cellW - padX * 2f),
                                Mathf.Round(cellH - padY * 2f)
                            )
                        };
                        newRects.Add(sr);
                        pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
                    }
                }

                dataProvider.SetSpriteRects(newRects.ToArray());
                if (nameFileIdProvider != null)
                {
                    nameFileIdProvider.SetNameFileIdPairs(pairs);
                }
                modified = true;
            }
            else
            {
                for (int i = 0; i < existingRects.Length; i++)
                {
                    if (existingRects[i].alignment != (SpriteAlignment)(int)SpriteAlignment.Center)
                    {
                        existingRects[i].alignment = (SpriteAlignment)(int)SpriteAlignment.Center;
                        existingRects[i].pivot = new Vector2(0.5f, 0.5f);
                        modified = true;
                    }
                }

                if (modified)
                {
                    dataProvider.SetSpriteRects(existingRects);
                }
            }

            // 4. Apply and Reimport if changed
            if (modified)
            {
                dataProvider.Apply();
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureFolderAsSingleSprites(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath)) return;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                bool changed = false;
                if (ti.textureType != TextureImporterType.Sprite)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    changed = true;
                }
                if (ti.spriteImportMode != SpriteImportMode.Single)
                {
                    ti.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }
                if (changed)
                {
                    ti.SaveAndReimport();
                }
            }
        }

        private static void ConfigureCloudsAsSingleSprites(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath)) return;
            string[] guids = AssetDatabase.FindAssets("Clouds_ t:Texture2D", new[] { folderPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                bool changed = false;
                if (ti.textureType != TextureImporterType.Sprite)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    changed = true;
                }
                if (ti.spriteImportMode != SpriteImportMode.Single)
                {
                    ti.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }
                if (changed)
                {
                    ti.SaveAndReimport();
                }
            }
        }

        private static GameAssetRegistry PopulateGameAssetRegistry()
        {
            const string registryPath = "Assets/Resources/GameAssetRegistry.asset";
            GameAssetRegistry registry = AssetDatabase.LoadAssetAtPath<GameAssetRegistry>(registryPath);
            if (registry == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }
                registry = ScriptableObject.CreateInstance<GameAssetRegistry>();
                AssetDatabase.CreateAsset(registry, registryPath);
            }

            registry.maliosSprites = LoadSortedSpritesAtPath("Assets/Asset/Karakter/malios.png");
            registry.arvandSprites = LoadSortedSpritesAtPath("Assets/Asset/Karakter/arvand.png");

            registry.dust1Sprites = LoadSortedSpritesAtPath("Assets/Tiny Swords/Particle FX/Dust_01.png");
            registry.dust2Sprites = LoadSortedSpritesAtPath("Assets/Tiny Swords/Particle FX/Dust_02.png");
            registry.cloudSprites = new[]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiny Swords/Terrain/Decorations/Clouds/Clouds_01.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiny Swords/Terrain/Decorations/Clouds/Clouds_02.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiny Swords/Terrain/Decorations/Clouds/Clouds_03.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiny Swords/Terrain/Decorations/Clouds/Clouds_04.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiny Swords/Terrain/Decorations/Clouds/Clouds_05.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiny Swords/Terrain/Decorations/Clouds/Clouds_06.png")
            };

            registry.dialogueBoardSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Fantasy Wooden GUI  Free/normal_ui_set A/UI board Large  parchment.png");
            registry.smallBoardSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Fantasy Wooden GUI  Free/normal_ui_set A/UI board Small  parchment.png");
            registry.stoneBoardSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Fantasy Wooden GUI  Free/normal_ui_set A/UI board Medium stone.png");
            registry.buttonNormalSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Fantasy Wooden GUI  Free/normal_ui_set A/TextBTN_Big.png");
            registry.buttonPressedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Fantasy Wooden GUI  Free/normal_ui_set A/TextBTN_Big_Pressed.png");
            registry.titleBannerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Fantasy Wooden GUI  Free/normal_ui_set A/IRONY TITLE  empty.png");
            registry.exclamationSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Fantasy Wooden GUI  Free/normal_ui_set A/Exclamation_Yellow.png");
            registry.regularPaperSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiny Swords/UI Elements/Papers/RegularPaper.png");
            registry.bigRibbonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiny Swords/UI Elements/Ribbons/BigRibbons 1.png");
            registry.woodTableSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiny Swords/UI Elements/Wood Table/WoodTable.png");

            registry.introCutsceneSprites = new[]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Cutscenes/intro_1_eldoria.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Cutscenes/intro_2_hoki.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Cutscenes/intro_3_vorthak.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Cutscenes/intro_4_demons.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Cutscenes/intro_5_malios.png")
            };

            registry.endingCutsceneSprites = new[]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Cutscenes/ending_1_nocthar.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Cutscenes/ending_2_altar.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Cutscenes/ending_3_vorthak_seal.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Cutscenes/ending_4_peace.png")
            };

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            return registry;
        }

        private static Sprite[] LoadSortedSpritesAtPath(string assetPath)
        {
            UnityEngine.Object[] allObjects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            List<Sprite> sprites = new List<Sprite>();
            if (allObjects != null)
            {
                foreach (var obj in allObjects)
                {
                    if (obj is Sprite sp)
                    {
                        sprites.Add(sp);
                    }
                }
            }
            sprites.Sort(GameAssetRegistry.CompareSpritesByTrailingNumber);
            return sprites.ToArray();
        }
    }
}
#endif
