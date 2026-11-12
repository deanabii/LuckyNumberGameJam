using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LuckyNumber.Core
{
    /// <summary>
    /// Animates grass tufts, flowers, swamp reeds, bushes, and subtle tree canopy sway across all Prop Tilemaps
    /// using root-anchored shear & scale transform matrices plus player-proximity rustle when Malios walks past.
    /// </summary>
    public class WorldGrassAnimator : MonoBehaviour
    {
        public static WorldGrassAnimator Instance { get; private set; }

        [Header("Wind Sway Settings")]
        [SerializeField] private float windSpeed = 2.15f;
        [SerializeField] private float grassSwayAmplitude = 0.14f;
        [SerializeField] private float grassVerticalPulse = 0.045f;
        [SerializeField] private bool animateTreesAndBushes = true;
        [SerializeField] private float treeSwayAmplitude = 0.028f;

        [Header("Player Proximity Rustle")]
        [SerializeField] private float rustleRadius = 1.55f;
        [SerializeField] private float rustleStrength = 0.26f;
        [SerializeField] private float rustleSmoothing = 10f;

        private struct AnimatedFoliageCell
        {
            public Tilemap tilemap;
            public Vector3Int cellPos;
            public Vector3 worldCenter;
            public float rootX;
            public float rootY;
            public float swayAmp;
            public float pulseAmp;
            public float phase;
            public bool reactToPlayer;
            public float currentRustle;
        }

        private readonly List<AnimatedFoliageCell> _foliageCells = new List<AnimatedFoliageCell>();
        private Transform _playerTransform;

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
            CollectFoliageCells();
            GameObject player = GameObject.Find("Player_Malios");
            if (player != null) _playerTransform = player.transform;
        }

        private void OnDisable()
        {
            ResetAllTransforms();
        }

        public void CollectFoliageCells()
        {
            _foliageCells.Clear();

            Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            foreach (Tilemap tm in tilemaps)
            {
                if (tm == null || !tm.gameObject.name.StartsWith("Prop")) continue;

                BoundsInt bounds = tm.cellBounds;
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    for (int y = bounds.yMin; y < bounds.yMax; y++)
                    {
                        Vector3Int cell = new Vector3Int(x, y, 0);
                        Sprite sp = tm.GetSprite(cell);
                        if (sp == null) continue;

                        if (!ClassifyAnimatedFoliage(sp, cell, out float swayAmp, out float pulseAmp, out bool reactToPlayer))
                        {
                            continue;
                        }

                        // Unlock tile transform editing on this cell
                        tm.SetTileFlags(cell, TileFlags.None);

                        Vector3 worldCenter = tm.GetCellCenterWorld(cell) + sp.bounds.center;
                        _foliageCells.Add(new AnimatedFoliageCell
                        {
                            tilemap = tm,
                            cellPos = cell,
                            worldCenter = worldCenter,
                            rootX = sp.bounds.center.x,
                            rootY = sp.bounds.min.y,
                            swayAmp = swayAmp,
                            pulseAmp = pulseAmp,
                            phase = worldCenter.x * 0.62f + worldCenter.y * 0.41f,
                            reactToPlayer = reactToPlayer,
                            currentRustle = 0f
                        });
                    }
                }
            }
        }

        private void Update()
        {
            if (_foliageCells.Count == 0) return;

            if (_playerTransform == null)
            {
                GameObject player = GameObject.Find("Player_Malios");
                if (player != null) _playerTransform = player.transform;
            }

            Vector3 playerPos = _playerTransform != null ? _playerTransform.position : new Vector3(9999f, 9999f, 0f);
            bool hasPlayer = _playerTransform != null;
            float sqrRustleR = rustleRadius * rustleRadius;
            float t = Time.time * windSpeed;
            float dt = Time.deltaTime;

            for (int i = 0; i < _foliageCells.Count; i++)
            {
                AnimatedFoliageCell item = _foliageCells[i];
                if (item.tilemap == null) continue;

                // Two overlapping sine waves create natural gusting wind across the meadow
                float gust = Mathf.Sin(t + item.phase) * 0.68f + Mathf.Sin(t * 1.73f + item.phase * 0.55f) * 0.32f;
                float windShear = gust * item.swayAmp;

                float targetRustle = 0f;
                float rustleSquash = 0f;
                if (hasPlayer && item.reactToPlayer)
                {
                    float dx = item.worldCenter.x - playerPos.x;
                    float dy = item.worldCenter.y - playerPos.y;
                    float sqrDist = dx * dx + dy * dy;
                    if (sqrDist < sqrRustleR && sqrDist > 0.0001f)
                    {
                        float factor = 1f - Mathf.Sqrt(sqrDist) / rustleRadius;
                        float pushDir = dx >= 0f ? 1f : -1f;
                        targetRustle = pushDir * factor * rustleStrength;
                        rustleSquash = factor * 0.07f;
                    }
                }

                item.currentRustle = Mathf.Lerp(item.currentRustle, targetRustle, dt * rustleSmoothing);
                _foliageCells[i] = item;

                float totalShear = windShear + item.currentRustle;
                float scalePulse = 1f + Mathf.Sin(t * 1.4f + item.phase) * item.pulseAmp - rustleSquash;

                Matrix4x4 toRoot = Matrix4x4.Translate(new Vector3(-item.rootX, -item.rootY, 0f));
                Matrix4x4 deform = Matrix4x4.identity;
                deform.m00 = 1f;
                deform.m01 = totalShear;
                deform.m11 = scalePulse;
                Matrix4x4 fromRoot = Matrix4x4.Translate(new Vector3(item.rootX, item.rootY, 0f));

                item.tilemap.SetTransformMatrix(item.cellPos, fromRoot * deform * toRoot);
            }
        }

        private void ResetAllTransforms()
        {
            for (int i = 0; i < _foliageCells.Count; i++)
            {
                if (_foliageCells[i].tilemap != null)
                {
                    _foliageCells[i].tilemap.SetTransformMatrix(_foliageCells[i].cellPos, Matrix4x4.identity);
                }
            }
        }

        private bool ClassifyAnimatedFoliage(
            Sprite sprite,
            Vector3Int cellPos,
            out float swayAmp,
            out float pulseAmp,
            out bool reactToPlayer)
        {
            swayAmp = 0f;
            pulseAmp = 0f;
            reactToPlayer = false;

            string name = sprite.name;
            if (string.IsNullOrEmpty(name)) return false;

            PropColliderCategory colCat = WorldPropColliderBuilder.ClassifyPropSprite(name, cellPos);
            int idx = ExtractTrailingIndex(name);

            // Never sway houses/caves or cliff/water tiles
            if (colCat == PropColliderCategory.BuildingOrCave || colCat == PropColliderCategory.BorderTile)
            {
                return false;
            }

            // Subtle canopy breeze for trees when enabled
            if (colCat == PropColliderCategory.Tree)
            {
                if (!animateTreesAndBushes) return false;
                swayAmp = treeSwayAmplitude;
                pulseAmp = 0.012f;
                reactToPlayer = false;
                return true;
            }

            if (idx < 0) return false;

            // Grass tufts, flowers, swamp reeds & small non-solid meadow foliage
            bool isGrassOrFlower = false;

            if (name.StartsWith("Gemini_Generated_Image_1xox3p1xox3p1xox_"))
            {
                if ((idx >= 40 && idx <= 49) || (idx >= 72 && idx <= 75) || (idx >= 25 && idx <= 28))
                    isGrassOrFlower = true;
            }
            else if (name.StartsWith("PropCabang_"))
            {
                if ((idx >= 14 && idx <= 19) || (idx >= 21 && idx <= 24) || idx == 29 || idx == 30 ||
                    (idx >= 38 && idx <= 45) || (idx >= 53 && idx <= 57) || (idx >= 64 && idx <= 68))
                    isGrassOrFlower = true;
            }
            else if (name.StartsWith("Gemini_Generated_Image_foed9yfoed9yfoed_"))
            {
                if ((idx >= 24 && idx <= 33) || idx == 55 || idx == 57 || idx == 58)
                    isGrassOrFlower = true;
            }
            else if (name.StartsWith("propes_"))
            {
                if ((idx >= 28 && idx <= 37) || (idx >= 40 && idx <= 43) || (idx >= 53 && idx <= 55))
                    isGrassOrFlower = true;
            }
            else if (name.StartsWith("propbos3_"))
            {
                if ((idx >= 12 && idx <= 16) || (idx >= 19 && idx <= 26))
                    isGrassOrFlower = true;
            }
            else if (name.StartsWith("PropBos4_"))
            {
                if ((idx >= 26 && idx <= 35) || (idx >= 38 && idx <= 40) ||
                    (idx >= 43 && idx <= 45) || idx == 49 || idx == 51 || idx == 56)
                    isGrassOrFlower = true;
            }

            if (isGrassOrFlower)
            {
                swayAmp = grassSwayAmplitude;
                pulseAmp = grassVerticalPulse;
                reactToPlayer = true;
                return true;
            }

            return false;
        }

        private static int ExtractTrailingIndex(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            if (underscore < 0 || underscore >= spriteName.Length - 1) return -1;
            string numPart = spriteName.Substring(underscore + 1);
            return int.TryParse(numPart, out int result) ? result : -1;
        }
    }
}
