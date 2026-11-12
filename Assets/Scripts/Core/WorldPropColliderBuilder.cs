using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using LuckyNumber.Characters;

namespace LuckyNumber.Core
{
    public enum PropColliderCategory
    {
        None = 0,
        BuildingOrCave = 1,
        Tree = 2,
        SolidProp = 3,
        BorderTile = 4
    }

    /// <summary>
    /// Automatically builds accurate 2D physical colliders for all solid world props
    /// (houses, boss caves, trees, rocks, stumps, logs, bushes, fences, walls, lamps, barrels, lava pools)
    /// across all Prop Tilemaps while excluding small ground details (grass tufts, small flowers, puddles, flat road tiles).
    /// Safe to run in both Edit Mode and Play Mode.
    /// </summary>
    public class WorldPropColliderBuilder : MonoBehaviour
    {
        public static WorldPropColliderBuilder Instance { get; private set; }

        private static readonly string[] PropTilemapNames =
        {
            "Prop",
            "PropCabang",
            "PropBos1",
            "PropBos2",
            "PropBos3",
            "PropBos4"
        };

        // Safe zones where colliders should never trap the player (spawn, NPCs, gateway triggers)
        private static readonly Vector2[] ProtectedPoints =
        {
            new Vector2(0f, -2.2f),     // Malios Spawn
            new Vector2(0f, 6.8f),      // Desa North Gate
            new Vector2(-0.5f, 15.0f),  // Percabangan South Gate
            new Vector2(-9.2f, 23.5f),  // Percabangan West Gate
            new Vector2(-17.0f, 23.5f), // Boss 1 Entrance
            new Vector2(-36.5f, 22.2f), // Boss 1 Cave Door Approach
            new Vector2(-36.5f, 23.4f), // Boss 1 Cave Portal
            new Vector2(-3.8f, 28.8f),  // Percabangan NW Gate
            new Vector2(-11.5f, 44.0f), // Boss 2 Entrance
            new Vector2(3.5f, 28.8f),   // Percabangan NE Gate
            new Vector2(14.5f, 44.0f),  // Boss 3 Entrance
            new Vector2(8.2f, 23.5f),   // Percabangan East Gate
            new Vector2(17.0f, 23.5f)   // Boss 4 Entrance
        };

        // Gateway corridors & walkable bridge bands that perimeter wall generation must never seal
        private static readonly Rect[] ProtectedCorridors =
        {
            new Rect(-5.0f, 7.5f, 10.0f, 6.5f),   // Desa <-> Percabangan
            new Rect(-16.0f, 20.5f, 7.5f, 6.2f),  // Percabangan <-> Boss 1 Bridge
            new Rect(-16.5f, 29.8f, 13.5f, 15.0f),// Percabangan <-> Boss 2 Bridge
            new Rect(0.5f, 29.8f, 19.0f, 15.0f),  // Percabangan <-> Boss 3 Bridge
            new Rect(7.8f, 20.5f, 10.2f, 6.2f),   // Percabangan <-> Boss 4 Bridge
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            RebuildAllWorldColliders();
        }

        public static int RebuildAllWorldColliders()
        {
            int totalColliders = 0;

            Tilemap[] allTilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            foreach (Tilemap tm in allTilemaps)
            {
                if (tm == null) continue;
                string tmName = tm.gameObject.name;
                bool isPropTilemap = false;
                for (int i = 0; i < PropTilemapNames.Length; i++)
                {
                    if (string.Equals(tmName, PropTilemapNames[i], System.StringComparison.OrdinalIgnoreCase))
                    {
                        isPropTilemap = true;
                        break;
                    }
                }

                if (!isPropTilemap) continue;

                // Disable any raw TilemapCollider2D so oversized roof/canopy sprites or grass tiles don't block roads
                TilemapCollider2D rawTmCol = tm.GetComponent<TilemapCollider2D>();
                if (rawTmCol != null)
                {
                    rawTmCol.enabled = false;
                }

                totalColliders += BuildCollidersForTilemap(tm);
            }

            totalColliders += BuildIslandBoundaryColliders(allTilemaps);
            EnsureNPCSolidFootprints();
            return totalColliders;
        }

        private static int BuildCollidersForTilemap(Tilemap tilemap)
        {
            string containerName = tilemap.gameObject.name + "_SolidColliders";
            Transform existingContainer = tilemap.transform.Find(containerName);
            if (existingContainer != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(existingContainer.gameObject);
                }
                else
                {
                    DestroyImmediate(existingContainer.gameObject);
                }
            }

            GameObject containerObj = new GameObject(containerName);
            containerObj.transform.SetParent(tilemap.transform, false);
            containerObj.transform.localPosition = Vector3.zero;
            containerObj.transform.localRotation = Quaternion.identity;
            containerObj.transform.localScale = Vector3.one;

            Rigidbody2D staticRb = containerObj.AddComponent<Rigidbody2D>();
            staticRb.bodyType = RigidbodyType2D.Static;
            staticRb.simulated = true;

            BoundsInt bounds = tilemap.cellBounds;
            int count = 0;

            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int cellPos = new Vector3Int(x, y, 0);
                    Sprite sprite = tilemap.GetSprite(cellPos);
                    if (sprite == null) continue;

                    PropColliderCategory category = ClassifyPropSprite(sprite.name, cellPos);
                    if (category == PropColliderCategory.None) continue;

                    Vector3 cellCenterWorld = tilemap.GetCellCenterWorld(cellPos);
                    Bounds spriteBounds = sprite.bounds;
                    float spriteW = spriteBounds.size.x;
                    float spriteH = spriteBounds.size.y;

                    Vector2 colSize;
                    Vector2 colCenter;

                    switch (category)
                    {
                        case PropColliderCategory.BuildingOrCave:
                            // Houses and Boss Caves: cover virtually the full building/cave footprint so player cannot walk behind roofs or caves
                            colSize = new Vector2(Mathf.Max(1.2f, spriteW * 0.90f), Mathf.Max(1.2f, spriteH * 0.88f));
                            colCenter = new Vector2(
                                cellCenterWorld.x + spriteBounds.center.x,
                                cellCenterWorld.y + spriteBounds.center.y - (spriteH * 0.03f)
                            );
                            break;

                        case PropColliderCategory.Tree:
                            // Trees: cover trunk and canopy footprint so player cannot clip behind tree tops
                            colSize = new Vector2(Mathf.Clamp(spriteW * 0.72f, 0.90f, 3.20f), Mathf.Clamp(spriteH * 0.82f, 0.90f, 3.80f));
                            colCenter = new Vector2(
                                cellCenterWorld.x + spriteBounds.center.x,
                                cellCenterWorld.y + spriteBounds.center.y - (spriteH * 0.04f)
                            );
                            break;

                        case PropColliderCategory.BorderTile:
                            // 1x1 Cliff/Water border tiles
                            colSize = new Vector2(0.96f, 0.96f);
                            colCenter = new Vector2(cellCenterWorld.x, cellCenterWorld.y);
                            break;

                        case PropColliderCategory.SolidProp:
                        default:
                            // Rocks, stumps, logs, bushes, fences, walls, lamps, barrels, banners, lava/tar pools
                            colSize = new Vector2(Mathf.Max(0.65f, spriteW * 0.86f), Mathf.Max(0.60f, spriteH * 0.82f));
                            colCenter = new Vector2(
                                cellCenterWorld.x + spriteBounds.center.x,
                                cellCenterWorld.y + spriteBounds.center.y - (spriteH * 0.03f)
                            );
                            break;
                    }

                    // Ensure small props/borders never trap protected spawn or gateway points (never drop major buildings/caves)
                    if (category != PropColliderCategory.BuildingOrCave)
                    {
                        Rect worldColRect = new Rect(colCenter.x - colSize.x * 0.5f, colCenter.y - colSize.y * 0.5f, colSize.x, colSize.y);
                        if (OverlapsProtectedPoint(worldColRect))
                        {
                            continue;
                        }
                    }

                    BoxCollider2D box = containerObj.AddComponent<BoxCollider2D>();
                    Vector3 localCenter = containerObj.transform.InverseTransformPoint(new Vector3(colCenter.x, colCenter.y, 0f));
                    box.offset = new Vector2(localCenter.x, localCenter.y);
                    box.size = colSize;
                    count++;
                }
            }

            return count;
        }

        private static int BuildIslandBoundaryColliders(Tilemap[] allTilemaps)
        {
            if (allTilemaps == null || allTilemaps.Length == 0) return 0;

            Transform gridTransform = allTilemaps[0].transform.parent;
            if (gridTransform == null) return 0;

            const string boundaryName = "_IslandBoundaryColliders";
            Transform existing = gridTransform.Find(boundaryName);
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            var occupiedCells = new HashSet<Vector2Int>();
            Tilemap referenceTilemap = null;

            foreach (Tilemap tm in allTilemaps)
            {
                if (tm == null) continue;
                string tmName = tm.gameObject.name;
                if (!tmName.StartsWith("Ground", System.StringComparison.OrdinalIgnoreCase) &&
                    !tmName.StartsWith("Prop", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (referenceTilemap == null && tmName.StartsWith("Ground", System.StringComparison.OrdinalIgnoreCase))
                {
                    referenceTilemap = tm;
                }

                BoundsInt bounds = tm.cellBounds;
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    for (int y = bounds.yMin; y < bounds.yMax; y++)
                    {
                        if (tm.HasTile(new Vector3Int(x, y, 0)))
                        {
                            occupiedCells.Add(new Vector2Int(x, y));
                        }
                    }
                }
            }

            if (referenceTilemap == null || occupiedCells.Count == 0) return 0;

            var voidEdgeCells = new HashSet<Vector2Int>();
            Vector2Int[] dirs =
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            foreach (Vector2Int cell in occupiedCells)
            {
                for (int i = 0; i < dirs.Length; i++)
                {
                    Vector2Int neighbor = cell + dirs[i];
                    if (!occupiedCells.Contains(neighbor))
                    {
                        voidEdgeCells.Add(neighbor);
                    }
                }
            }

            GameObject boundaryObj = new GameObject(boundaryName);
            boundaryObj.transform.SetParent(gridTransform, false);
            boundaryObj.transform.localPosition = Vector3.zero;
            boundaryObj.transform.localRotation = Quaternion.identity;
            boundaryObj.transform.localScale = Vector3.one;

            Rigidbody2D staticRb = boundaryObj.AddComponent<Rigidbody2D>();
            staticRb.bodyType = RigidbodyType2D.Static;
            staticRb.simulated = true;

            int added = 0;
            foreach (Vector2Int edgeCell in voidEdgeCells)
            {
                Vector3 worldCenter = referenceTilemap.GetCellCenterWorld(new Vector3Int(edgeCell.x, edgeCell.y, 0));
                Rect edgeRect = new Rect(worldCenter.x - 0.45f, worldCenter.y - 0.45f, 0.90f, 0.90f);

                if (OverlapsProtectedPoint(edgeRect) || OverlapsProtectedCorridor(edgeRect))
                {
                    continue;
                }

                BoxCollider2D box = boundaryObj.AddComponent<BoxCollider2D>();
                Vector3 localCenter = boundaryObj.transform.InverseTransformPoint(worldCenter);
                box.offset = new Vector2(localCenter.x, localCenter.y);
                box.size = new Vector2(0.95f, 0.95f);
                added++;
            }

            return added;
        }

        private static bool OverlapsProtectedCorridor(Rect worldRect)
        {
            for (int i = 0; i < ProtectedCorridors.Length; i++)
            {
                if (ProtectedCorridors[i].Overlaps(worldRect))
                {
                    return true;
                }
            }
            return false;
        }

        private static void EnsureNPCSolidFootprints()
        {
            // Give Chief Arvand a solid collider covering his body footprint so Malios cannot walk through or clip behind him
            GameObject arvandObj = GameObject.Find("NPC_Arvand");
            if (arvandObj != null)
            {
                Rigidbody2D rb = arvandObj.GetComponent<Rigidbody2D>();
                if (rb == null) rb = arvandObj.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Static;

                CircleCollider2D col = arvandObj.GetComponent<CircleCollider2D>();
                if (col == null) col = arvandObj.AddComponent<CircleCollider2D>();
                col.isTrigger = false;
                col.radius = 1.6f; // ~0.35 world units at scale 0.22
                col.offset = new Vector2(0f, -0.5f);
            }
        }

        private static bool OverlapsProtectedPoint(Rect worldRect)
        {
            Rect padded = new Rect(worldRect.xMin - 0.25f, worldRect.yMin - 0.25f, worldRect.width + 0.5f, worldRect.height + 0.5f);
            for (int i = 0; i < ProtectedPoints.Length; i++)
            {
                if (padded.Contains(ProtectedPoints[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Classifies any sprite name from the 6 prop sheets (plus bridge border tiles) into a collider category.
        /// Grass tufts, small flowers, small mushrooms, lily pads, flat puddles, and walkable bridge tiles return None.
        /// </summary>
        public static PropColliderCategory ClassifyPropSprite(string spriteName, Vector3Int cellPos)
        {
            if (string.IsNullOrEmpty(spriteName)) return PropColliderCategory.None;

            // 1. Bridge / Ground border tiles placed on Prop (X=12..13) or PropBos1 (X=-14..-13, Y=16,31)
            if (spriteName.StartsWith("Tileset Grounds_") ||
                spriteName.StartsWith("Gemini_Generated_Image_3n5a2c3n5a2c3n5a_") ||
                spriteName.StartsWith("gr3_"))
            {
                // Walkable bridge road between Y=22 and Y=25 is non-solid
                if (cellPos.y >= 22 && cellPos.y <= 25)
                {
                    return PropColliderCategory.None;
                }
                return PropColliderCategory.BorderTile;
            }

            int idx = ExtractTrailingIndex(spriteName);
            if (idx < 0) return PropColliderCategory.SolidProp;

            // 2. Desa Eldoria Props (Gemini_Generated_Image_1xox3p1xox3p1xox)
            if (spriteName.StartsWith("Gemini_Generated_Image_1xox3p1xox3p1xox_"))
            {
                if (idx >= 0 && idx <= 5) return PropColliderCategory.BuildingOrCave; // 6 Village Houses
                if (idx >= 29 && idx <= 32) return PropColliderCategory.Tree;         // 4 Large/Medium Trees
                if (idx == 9 ||
                    (idx >= 13 && idx <= 23) ||
                    (idx >= 25 && idx <= 28) ||
                    (idx >= 40 && idx <= 49) ||
                    (idx >= 72 && idx <= 75))
                {
                    // Grass tufts, small flowers, flat stone/dirt road tiles, and flat puddles
                    return PropColliderCategory.None;
                }
                return PropColliderCategory.SolidProp; // Rocks, bushes, signpost, fences, walls, lamps, barrels, cart
            }

            // 3. Area Percabangan Props (PropCabang)
            if (spriteName.StartsWith("PropCabang_"))
            {
                if (idx >= 0 && idx <= 5) return PropColliderCategory.Tree; // 6 Big Green Trees
                if ((idx >= 14 && idx <= 19) ||
                    (idx >= 21 && idx <= 24) ||
                    idx == 29 || idx == 30 ||
                    (idx >= 38 && idx <= 45) ||
                    (idx >= 53 && idx <= 57) ||
                    (idx >= 64 && idx <= 68))
                {
                    // Small grass tufts, small flowers, tiny mushrooms, and flat grass/stone road tiles
                    return PropColliderCategory.None;
                }
                return PropColliderCategory.SolidProp; // Logs, stumps, bushes, rocks, fences, barrels, banners, signpost
            }

            // 4. Boss 1 Gunung Bara Props (Gemini_Generated_Image_foed9yfoed9yfoed)
            if (spriteName.StartsWith("Gemini_Generated_Image_foed9yfoed9yfoed_"))
            {
                if (idx == 0) return PropColliderCategory.BuildingOrCave; // Giant Volcano Cave
                if (idx == 15 || idx == 16) return PropColliderCategory.Tree; // Burning Trees
                if (idx == 1 || idx == 4 || idx == 9 || idx == 10 ||
                    (idx >= 24 && idx <= 33) ||
                    idx == 55 || idx == 57 || idx == 58)
                {
                    // Flat volcanic ground patches, small red grass tufts, tiny spark pixels
                    return PropColliderCategory.None;
                }
                return PropColliderCategory.SolidProp; // Lava pools, rocks, stumps, bushes, fences, walls, lamps, barrels, campfires
            }

            // 5. Boss 2 Gua Es Abadi Props (propes)
            if (spriteName.StartsWith("propes_"))
            {
                if (idx == 0) return PropColliderCategory.BuildingOrCave; // Giant Ice Cave
                if ((idx >= 1 && idx <= 4) || (idx >= 17 && idx <= 20)) return PropColliderCategory.Tree; // Snow Trees
                if (idx == 5 || idx == 9 || idx == 10 ||
                    (idx >= 28 && idx <= 37) ||
                    (idx >= 40 && idx <= 43) ||
                    (idx >= 53 && idx <= 55))
                {
                    // Flat snow/cracked ice patches, snow grass tufts, flat snow/stone road tiles
                    return PropColliderCategory.None;
                }
                return PropColliderCategory.SolidProp; // Frozen ponds, ice crystals, rocks, bushes, walls, mushrooms, banners
            }

            // 6. Boss 3 Hutan Racun Props (propbos3)
            if (spriteName.StartsWith("propbos3_"))
            {
                if (idx == 0) return PropColliderCategory.BuildingOrCave; // Giant Poison Cave
                if (idx >= 2 && idx <= 4) return PropColliderCategory.Tree; // Swamp Trees
                if ((idx >= 6 && idx <= 8) ||
                    (idx >= 12 && idx <= 16) ||
                    (idx >= 19 && idx <= 26))
                {
                    // Flat poison puddles, small swamp grass tufts, lily pads
                    return PropColliderCategory.None;
                }
                return PropColliderCategory.SolidProp; // Stumps, mossy rocks, mushroom logs, bushes
            }

            // 7. Boss 4 Kastil Bayangan Props (PropBos4)
            if (spriteName.StartsWith("PropBos4_"))
            {
                if (idx == 4) return PropColliderCategory.BuildingOrCave; // Giant Shadow Cave
                if (idx == 0 || idx == 2 || idx == 3 || idx == 22 || idx == 23) return PropColliderCategory.Tree; // Shadow Trees
                if (idx == 5 || idx == 6 || idx == 9 ||
                    (idx >= 26 && idx <= 35) ||
                    (idx >= 38 && idx <= 40) ||
                    (idx >= 43 && idx <= 45) ||
                    idx == 49 || idx == 51 || idx == 56)
                {
                    // Flat stepping stones, small shadow grass tufts, flat road tiles
                    return PropColliderCategory.None;
                }
                return PropColliderCategory.SolidProp; // Tar pits, rocks, bushes, walls, banners, signpost, stumps
            }

            return PropColliderCategory.SolidProp;
        }

        private static int ExtractTrailingIndex(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            if (underscore < 0 || underscore >= spriteName.Length - 1) return -1;
            string numPart = spriteName.Substring(underscore + 1);
            if (int.TryParse(numPart, out int result))
            {
                return result;
            }
            return -1;
        }
    }
}
