using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace LuckyNumber.Core
{
    [CreateAssetMenu(fileName = "GameAssetRegistry", menuName = "LuckyNumber/Game Asset Registry")]
    public class GameAssetRegistry : ScriptableObject
    {
        private static GameAssetRegistry _instance;
        public static GameAssetRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GameAssetRegistry>("GameAssetRegistry");
                    if (_instance == null)
                    {
                        _instance = CreateInstance<GameAssetRegistry>();
                    }
                    _instance.EnsureRuntimeFallbacks();
                }
                return _instance;
            }
        }

        public TMP_FontAsset GetPreferredFont(TMP_FontAsset localOverride = null)
        {
            if (localOverride != null) return localOverride;
            if (customUIFont != null) return customUIFont;
            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        [Header("Custom Font (Isi untuk mengganti Font Dialog, Cutscene & UI)")]
        public TMP_FontAsset customUIFont;

        [Header("Character Sprites (24 Frames: 0-5 Down, 6-11 Row2, 12-17 Row3, 18-23 Up)")]
        public Sprite[] maliosSprites;
        public Sprite[] arvandSprites;

        [Header("VFX Sprites (Walking Dust & Cloud Transition)")]
        public Sprite[] dust1Sprites;
        public Sprite[] dust2Sprites;
        public Sprite[] cloudSprites;

        [Header("Wooden GUI & Tiny Swords UI Sprites")]
        public Sprite dialogueBoardSprite;
        public Sprite smallBoardSprite;
        public Sprite stoneBoardSprite;
        public Sprite buttonNormalSprite;
        public Sprite buttonPressedSprite;
        public Sprite titleBannerSprite;
        public Sprite exclamationSprite;
        public Sprite regularPaperSprite;
        public Sprite bigRibbonSprite;
        public Sprite woodTableSprite;

        [Header("Intro Cutscene Sprites (FigJam AWAL)")]
        public Sprite[] introCutsceneSprites;

        [Header("Ending Cutscene Sprites (FigJam AKHIR)")]
        public Sprite[] endingCutsceneSprites;

        private bool _initializedFallbacks;

        public void EnsureRuntimeFallbacks()
        {
            if (_initializedFallbacks) return;
            _initializedFallbacks = true;

            if (maliosSprites == null || maliosSprites.Length < 24 || maliosSprites[0] == null)
            {
                maliosSprites = LoadOrSliceCharacterSheet("Characters/malios", 6, 4);
            }

            if (arvandSprites == null || arvandSprites.Length < 24 || arvandSprites[0] == null)
            {
                arvandSprites = LoadOrSliceCharacterSheet("Characters/arvand", 6, 4);
            }

            if (dust1Sprites == null || dust1Sprites.Length < 4 || dust1Sprites[0] == null)
            {
                dust1Sprites = LoadOrSliceCharacterSheet("VFX/Dust_01", 8, 1);
            }

            if (dust2Sprites == null || dust2Sprites.Length < 4 || dust2Sprites[0] == null)
            {
                dust2Sprites = LoadOrSliceCharacterSheet("VFX/Dust_02", 10, 1);
            }

            string[] cloudPaths =
            {
                "VFX/Clouds_01",
                "VFX/Clouds_02",
                "VFX/Clouds_03",
                "VFX/Clouds_04",
                "VFX/Clouds_05",
                "VFX/Clouds_06"
            };
            if (cloudSprites == null || cloudSprites.Length < cloudPaths.Length || cloudSprites[0] == null)
            {
                cloudSprites = new Sprite[cloudPaths.Length];
                for (int i = 0; i < cloudPaths.Length; i++)
                {
                    cloudSprites[i] = LoadSingleSprite(cloudPaths[i]);
                }
            }

            if (dialogueBoardSprite == null) dialogueBoardSprite = LoadSingleSprite("UI/board_large_parchment");
            if (smallBoardSprite == null) smallBoardSprite = LoadSingleSprite("UI/board_small_parchment");
            if (stoneBoardSprite == null) stoneBoardSprite = LoadSingleSprite("UI/board_medium_stone");
            if (buttonNormalSprite == null) buttonNormalSprite = LoadSingleSprite("UI/btn_big");
            if (buttonPressedSprite == null) buttonPressedSprite = LoadSingleSprite("UI/btn_big_pressed");
            if (titleBannerSprite == null) titleBannerSprite = LoadSingleSprite("UI/title_banner");
            if (exclamationSprite == null) exclamationSprite = LoadSingleSprite("UI/exclamation_yellow");
            if (regularPaperSprite == null) regularPaperSprite = LoadSingleSprite("UI/regular_paper");
            if (bigRibbonSprite == null) bigRibbonSprite = LoadSingleSprite("UI/big_ribbon");
            if (woodTableSprite == null) woodTableSprite = LoadSingleSprite("UI/wood_table");

            string[] introPaths =
            {
                "Cutscenes/intro_1_eldoria",
                "Cutscenes/intro_2_hoki",
                "Cutscenes/intro_3_vorthak",
                "Cutscenes/intro_4_demons",
                "Cutscenes/intro_5_malios"
            };
            if (introCutsceneSprites == null || introCutsceneSprites.Length < introPaths.Length || introCutsceneSprites[0] == null)
            {
                introCutsceneSprites = new Sprite[introPaths.Length];
                for (int i = 0; i < introPaths.Length; i++)
                {
                    introCutsceneSprites[i] = LoadSingleSprite(introPaths[i]);
                }
            }

            string[] endingPaths =
            {
                "Cutscenes/ending_1_nocthar",
                "Cutscenes/ending_2_altar",
                "Cutscenes/ending_3_vorthak_seal",
                "Cutscenes/ending_4_peace"
            };
            if (endingCutsceneSprites == null || endingCutsceneSprites.Length < endingPaths.Length || endingCutsceneSprites[0] == null)
            {
                endingCutsceneSprites = new Sprite[endingPaths.Length];
                for (int i = 0; i < endingPaths.Length; i++)
                {
                    endingCutsceneSprites[i] = LoadSingleSprite(endingPaths[i]);
                }
            }
        }

        public static Sprite LoadSingleSprite(string resourcePath)
        {
            Sprite sp = Resources.Load<Sprite>(resourcePath);
            if (sp != null) return sp;

            Sprite[] all = Resources.LoadAll<Sprite>(resourcePath);
            if (all != null && all.Length > 0) return all[0];

            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
            {
                return Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
            }
            return null;
        }

        public static Sprite[] LoadOrSliceCharacterSheet(string resourcePath, int cols, int rows)
        {
            Sprite[] loaded = Resources.LoadAll<Sprite>(resourcePath);
            if (loaded != null && loaded.Length >= cols * rows)
            {
                Array.Sort(loaded, CompareSpritesByTrailingNumber);
                return loaded;
            }

            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) return Array.Empty<Sprite>();

            List<Sprite> sliced = new List<Sprite>(cols * rows);
            float cellW = (float)tex.width / cols;
            float cellH = (float)tex.height / rows;

            // Row 0 is top row in sheet (Unity Y=0 is bottom, so top row is rows - 1 - r)
            for (int r = 0; r < rows; r++)
            {
                int unityRow = (rows - 1) - r;
                for (int c = 0; c < cols; c++)
                {
                    // Trim horizontal cell padding slightly so adjacent frames never bleed
                    float padX = cellW * 0.04f;
                    float padY = cellH * 0.01f;
                    Rect rect = new Rect(
                        c * cellW + padX,
                        unityRow * cellH + padY,
                        cellW - padX * 2f,
                        cellH - padY * 2f
                    );

                    Sprite frame = Sprite.Create(
                        tex,
                        rect,
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                    frame.name = $"{tex.name}_{r * cols + c}";
                    sliced.Add(frame);
                }
            }

            return sliced.ToArray();
        }

        public static int CompareSpritesByTrailingNumber(Sprite a, Sprite b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int numA = ExtractTrailingNumber(a.name);
            int numB = ExtractTrailingNumber(b.name);
            if (numA != numB) return numA.CompareTo(numB);
            return string.Compare(a.name, b.name, StringComparison.Ordinal);
        }

        private static int ExtractTrailingNumber(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            int underscore = name.LastIndexOf('_');
            if (underscore >= 0 && underscore + 1 < name.Length)
            {
                if (int.TryParse(name.Substring(underscore + 1), out int val))
                {
                    return val;
                }
            }
            return 0;
        }
    }
}
