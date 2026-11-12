using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace LuckyNumber.Core
{
    /// <summary>
    /// Configures URP 2D lighting for TestMap: atmospheric zone-based Global Light 2D,
    /// Sprite-Lit-Default material enforcement on all Tilemaps/characters, dynamic flickering
    /// Point Light 2Ds on lamps/houses/crystals/caves/lava, and a subtle player lantern aura.
    /// </summary>
    public class WorldLightingManager : MonoBehaviour
    {
        public static WorldLightingManager Instance { get; private set; }

        [Header("Global Ambient Settings")]
        [SerializeField] private float zoneBlendSpeed = 2.2f;

        [Header("Player Lantern Aura")]
        [SerializeField] private Color playerAuraColor = new Color(1f, 0.91f, 0.76f, 1f);
        [SerializeField] private float playerAuraIntensity = 0.20f;
        [SerializeField] private float playerAuraInnerRadius = 0.20f;
        [SerializeField] private float playerAuraOuterRadius = 3.2f;

        private struct AnimatedLightEntry
        {
            public Light2D light;
            public float baseIntensity;
            public float baseOuterRadius;
            public float pulseAmplitude;
            public float pulseSpeed;
            public float phase;
            public bool isFlicker;
        }

        private Light2D _globalLight;
        private Light2D _playerLight;
        private readonly List<AnimatedLightEntry> _animatedLights = new List<AnimatedLightEntry>();
        private Material _sharedLitSpriteMaterial;

        private Color _currentGlobalColor = new Color(0.92f, 0.94f, 1.0f, 1f);
        private float _currentGlobalIntensity = 0.84f;

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
            SetupWorldLighting();
        }

        private void Update()
        {
            UpdateGlobalZoneAtmosphere();
            UpdateAnimatedPointLights();
        }

        public void SetupWorldLighting()
        {
            EnsureLitMaterialsOnWorldRenderers();
            ConfigureGlobalLight();
            BuildScenePointLights();
            EnsurePlayerLanternLight();
        }

        private void EnsureLitMaterialsOnWorldRenderers()
        {
            // Find existing Sprite-Lit-Default material from scene renderers first so SRP batching matches
            TilemapRenderer[] tileRenderers = FindObjectsByType<TilemapRenderer>(FindObjectsSortMode.None);
            foreach (TilemapRenderer tr in tileRenderers)
            {
                if (tr != null && tr.sharedMaterial != null &&
                    tr.sharedMaterial.shader != null &&
                    tr.sharedMaterial.shader.name.Contains("Sprite-Lit"))
                {
                    _sharedLitSpriteMaterial = tr.sharedMaterial;
                    break;
                }
            }

            if (_sharedLitSpriteMaterial == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (litShader != null)
                {
                    _sharedLitSpriteMaterial = new Material(litShader) { name = "Runtime_Sprite-Lit-Default" };
                }
            }

            if (_sharedLitSpriteMaterial == null) return;

            foreach (TilemapRenderer tr in tileRenderers)
            {
                if (tr == null) continue;
                bool isLit = tr.sharedMaterial != null &&
                             tr.sharedMaterial.shader != null &&
                             tr.sharedMaterial.shader.name.Contains("Sprite-Lit");

                // Fix renderers using unlit or mismatched bush materials (e.g., Ground2 & PropCabang)
                if (!isLit || tr.sharedMaterial.name.Contains("Bush"))
                {
                    tr.sharedMaterial = _sharedLitSpriteMaterial;
                }
            }

            // Ensure characters also receive 2D lighting
            EnsureCharacterLitMaterial("Player_Malios");
            EnsureCharacterLitMaterial("NPC_Arvand");
        }

        private void EnsureCharacterLitMaterial(string objName)
        {
            if (_sharedLitSpriteMaterial == null) return;
            GameObject obj = GameObject.Find(objName);
            if (obj == null) return;

            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                bool isLit = sr.sharedMaterial != null &&
                             sr.sharedMaterial.shader != null &&
                             sr.sharedMaterial.shader.name.Contains("Sprite-Lit");
                if (!isLit)
                {
                    sr.sharedMaterial = _sharedLitSpriteMaterial;
                }
            }
        }

        private void ConfigureGlobalLight()
        {
            Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            foreach (Light2D l in lights)
            {
                if (l != null && l.lightType == Light2D.LightType.Global)
                {
                    _globalLight = l;
                    break;
                }
            }

            if (_globalLight == null)
            {
                GameObject gLightObj = new GameObject("Global Light 2D");
                _globalLight = gLightObj.AddComponent<Light2D>();
                _globalLight.lightType = Light2D.LightType.Global;
            }

            ApplyAllSortingLayers(_globalLight);
            GetZoneAtmosphere(MapZoneId.DesaEldoria, out _currentGlobalColor, out _currentGlobalIntensity);
            _globalLight.color = _currentGlobalColor;
            _globalLight.intensity = _currentGlobalIntensity;
        }

        private void BuildScenePointLights()
        {
            _animatedLights.Clear();

            const string containerName = "LuckyNumber_2DLights";
            GameObject existingContainer = GameObject.Find(containerName);
            if (existingContainer != null)
            {
                if (Application.isPlaying) Destroy(existingContainer);
                else DestroyImmediate(existingContainer);
            }

            GameObject container = new GameObject(containerName);
            container.transform.position = Vector3.zero;

            // 1. Auto-place lights by scanning placed Prop Tilemaps (lamps, houses, lava, ice crystals, caves, mushrooms)
            Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            foreach (Tilemap tm in tilemaps)
            {
                if (tm == null || !tm.gameObject.name.StartsWith("Prop")) continue;
                ScanTilemapForPropLights(tm, container.transform);
            }

            // 2. Chief Arvand / Village Square Warm Hearth Glow
            GameObject arvand = GameObject.Find("NPC_Arvand");
            Vector3 arvandPos = arvand != null ? arvand.transform.position : new Vector3(0f, 1.8f, 0f);
            CreatePointLight(
                container.transform,
                "VillageSquare_ArvandGlow",
                arvandPos + new Vector3(0f, 0.3f, 0f),
                new Color(1f, 0.86f, 0.58f, 1f),
                0.28f, 0.35f, 4.2f, 0.78f,
                0.04f, 1.6f, false
            );

            // 3. Spawn Shrine Soft Golden Light
            CreatePointLight(
                container.transform,
                "Spawn_HokiAura",
                new Vector3(0f, -2.2f, 0f),
                new Color(1f, 0.90f, 0.66f, 1f),
                0.22f, 0.30f, 3.6f, 0.80f,
                0.03f, 1.5f, false
            );

            // 4. Area Percabangan Crossroads Lantern & Colored Branch Beacons
            CreatePointLight(
                container.transform,
                "Crossroads_CenterGlow",
                new Vector3(-0.5f, 22.5f, 0f),
                new Color(1f, 0.88f, 0.62f, 1f),
                0.26f, 0.40f, 4.5f, 0.78f,
                0.04f, 1.7f, false
            );

            // Branch gateway color hints
            CreatePointLight(container.transform, "GateBeacon_Boss1_Fire", new Vector3(-11.2f, 23.5f, 0f),
                new Color(1f, 0.50f, 0.24f, 1f), 0.36f, 0.25f, 3.2f, 0.78f, 0.06f, 3.4f, true);
            CreatePointLight(container.transform, "GateBeacon_Boss2_Ice", new Vector3(-3.8f, 30.8f, 0f),
                new Color(0.45f, 0.86f, 1f, 1f), 0.34f, 0.25f, 3.2f, 0.78f, 0.05f, 2.0f, false);
            CreatePointLight(container.transform, "GateBeacon_Boss3_Poison", new Vector3(3.8f, 30.8f, 0f),
                new Color(0.48f, 0.95f, 0.45f, 1f), 0.34f, 0.25f, 3.2f, 0.78f, 0.05f, 2.2f, false);
            CreatePointLight(container.transform, "GateBeacon_Boss4_Shadow", new Vector3(10.8f, 23.5f, 0f),
                new Color(0.80f, 0.48f, 1f, 1f), 0.36f, 0.25f, 3.2f, 0.78f, 0.06f, 2.8f, true);

            // 5. Boss Sanctuary Subtle Core Lights (enhance mood without overexposing terrain)
            CreatePointLight(container.transform, "Boss1_ArenaGlow", new Vector3(-30.5f, 25.0f, 0f),
                new Color(1f, 0.48f, 0.18f, 1f), 0.45f, 0.65f, 6.8f, 0.76f, 0.07f, 4.0f, true);
            CreatePointLight(container.transform, "Boss2_ArenaGlow", new Vector3(-12.5f, 54.0f, 0f),
                new Color(0.55f, 0.88f, 1f, 1f), 0.40f, 0.65f, 6.8f, 0.76f, 0.05f, 1.8f, false);
            CreatePointLight(container.transform, "Boss3_ArenaGlow", new Vector3(14.5f, 54.0f, 0f),
                new Color(0.52f, 0.95f, 0.48f, 1f), 0.40f, 0.65f, 6.8f, 0.76f, 0.05f, 2.2f, false);
            CreatePointLight(container.transform, "Boss4_ArenaGlow", new Vector3(28.0f, 24.5f, 0f),
                new Color(0.84f, 0.52f, 1f, 1f), 0.44f, 0.65f, 6.8f, 0.76f, 0.06f, 3.0f, true);
        }

        private void ScanTilemapForPropLights(Tilemap tm, Transform parent)
        {
            BoundsInt bounds = tm.cellBounds;
            int placedCount = 0;

            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    Sprite sp = tm.GetSprite(cell);
                    if (sp == null) continue;

                    if (TryClassifyPropLight(sp.name, out Color lightColor, out float intensity,
                        out float innerR, out float outerR, out float pulseAmp, out float pulseSpeed, out bool flicker))
                    {
                        Vector3 worldPos = tm.GetCellCenterWorld(cell) + sp.bounds.center;
                        placedCount++;
                        CreatePointLight(
                            parent,
                            $"{tm.gameObject.name}_Light_{placedCount}_{sp.name}",
                            worldPos,
                            lightColor,
                            intensity,
                            innerR,
                            outerR,
                            0.78f,
                            pulseAmp,
                            pulseSpeed,
                            flicker
                        );
                    }
                }
            }
        }

        private static bool TryClassifyPropLight(
            string spriteName,
            out Color color,
            out float intensity,
            out float innerRadius,
            out float outerRadius,
            out float pulseAmp,
            out float pulseSpeed,
            out bool flicker)
        {
            color = Color.white;
            intensity = 1f;
            innerRadius = 0.5f;
            outerRadius = 3.5f;
            pulseAmp = 0.08f;
            pulseSpeed = 2.5f;
            flicker = false;

            if (string.IsNullOrEmpty(spriteName)) return false;

            int idx = ExtractTrailingIndex(spriteName);
            if (idx < 0) return false;

            // Desa Eldoria Houses (0..5) & Village Lamps/Torches
            if (spriteName.StartsWith("Gemini_Generated_Image_1xox3p1xox3p1xox_"))
            {
                if (idx >= 0 && idx <= 5)
                {
                    // Soft warm cottage window glow
                    color = new Color(1f, 0.82f, 0.52f, 1f);
                    intensity = 0.24f;
                    innerRadius = 0.35f;
                    outerRadius = 3.8f;
                    pulseAmp = 0.03f;
                    pulseSpeed = 1.6f;
                    return true;
                }
                if (idx == 10 || idx == 11 || idx == 33 || idx == 34 || idx == 50 || idx == 51)
                {
                    // Street lamps / lanterns
                    color = new Color(1f, 0.84f, 0.54f, 1f);
                    intensity = 0.38f;
                    innerRadius = 0.20f;
                    outerRadius = 3.2f;
                    pulseAmp = 0.05f;
                    pulseSpeed = 3.2f;
                    flicker = true;
                    return true;
                }
            }

            // Boss 1 Gunung Bara: Cave (0), Burning Trees (15,16), Lava/Fires (11..13, 17, 18)
            if (spriteName.StartsWith("Gemini_Generated_Image_foed9yfoed9yfoed_"))
            {
                if (idx == 0)
                {
                    color = new Color(1f, 0.48f, 0.18f, 1f);
                    intensity = 0.48f;
                    innerRadius = 0.60f;
                    outerRadius = 5.6f;
                    pulseAmp = 0.07f;
                    pulseSpeed = 3.8f;
                    flicker = true;
                    return true;
                }
                if (idx == 11 || idx == 12 || idx == 13 || idx == 15 || idx == 16 || idx == 17 || idx == 18)
                {
                    color = new Color(1f, 0.52f, 0.20f, 1f);
                    intensity = 0.34f;
                    innerRadius = 0.25f;
                    outerRadius = 3.2f;
                    pulseAmp = 0.06f;
                    pulseSpeed = 4.0f;
                    flicker = true;
                    return true;
                }
            }

            // Boss 2 Gua Es Abadi: Ice Cave (0) & Ice Crystals / Banners
            if (spriteName.StartsWith("propes_"))
            {
                if (idx == 0)
                {
                    color = new Color(0.54f, 0.88f, 1f, 1f);
                    intensity = 0.42f;
                    innerRadius = 0.60f;
                    outerRadius = 5.4f;
                    pulseAmp = 0.05f;
                    pulseSpeed = 1.8f;
                    return true;
                }
                if (idx >= 11 && idx <= 16)
                {
                    color = new Color(0.60f, 0.90f, 1f, 1f);
                    intensity = 0.30f;
                    innerRadius = 0.20f;
                    outerRadius = 2.9f;
                    pulseAmp = 0.04f;
                    pulseSpeed = 2.0f;
                    return true;
                }
            }

            // Boss 3 Hutan Racun: Poison Cave (0) & Glowing Swamp Spores/Mushrooms
            if (spriteName.StartsWith("propbos3_"))
            {
                if (idx == 0)
                {
                    color = new Color(0.50f, 0.96f, 0.50f, 1f);
                    intensity = 0.42f;
                    innerRadius = 0.60f;
                    outerRadius = 5.4f;
                    pulseAmp = 0.05f;
                    pulseSpeed = 2.2f;
                    return true;
                }
                if (idx == 1 || (idx >= 9 && idx <= 11) || idx == 17 || idx == 18)
                {
                    color = new Color(0.56f, 0.96f, 0.52f, 1f);
                    intensity = 0.28f;
                    innerRadius = 0.20f;
                    outerRadius = 2.8f;
                    pulseAmp = 0.05f;
                    pulseSpeed = 2.4f;
                    return true;
                }
            }

            // Boss 4 Kastil Bayangan: Shadow Cave (4) & Arcane Lamps/Banners
            if (spriteName.StartsWith("PropBos4_"))
            {
                if (idx == 4)
                {
                    color = new Color(0.84f, 0.48f, 1f, 1f);
                    intensity = 0.46f;
                    innerRadius = 0.60f;
                    outerRadius = 5.6f;
                    pulseAmp = 0.06f;
                    pulseSpeed = 3.0f;
                    flicker = true;
                    return true;
                }
                if (idx == 1 || idx == 7 || idx == 8 || idx == 10 || idx == 11)
                {
                    color = new Color(0.88f, 0.56f, 1f, 1f);
                    intensity = 0.34f;
                    innerRadius = 0.20f;
                    outerRadius = 3.0f;
                    pulseAmp = 0.05f;
                    pulseSpeed = 3.2f;
                    flicker = true;
                    return true;
                }
            }

            return false;
        }

        private void EnsurePlayerLanternLight()
        {
            // Upgrade old serialized default aura values saved before softening pass
            if (Mathf.Approximately(playerAuraIntensity, 0.68f))
            {
                playerAuraIntensity = 0.20f;
                playerAuraInnerRadius = 0.20f;
                playerAuraOuterRadius = 3.2f;
                playerAuraColor = new Color(1f, 0.91f, 0.76f, 1f);
            }

            GameObject playerObj = GameObject.Find("Player_Malios");
            if (playerObj == null) return;

            Transform existing = playerObj.transform.Find("Player_Light2D");
            if (existing != null)
            {
                _playerLight = existing.GetComponent<Light2D>();
            }

            if (_playerLight == null)
            {
                GameObject lightObj = new GameObject("Player_Light2D");
                lightObj.transform.SetParent(playerObj.transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                _playerLight = lightObj.AddComponent<Light2D>();
                _playerLight.lightType = Light2D.LightType.Point;
            }

            ApplyAllSortingLayers(_playerLight);
            _playerLight.color = playerAuraColor;
            _playerLight.intensity = playerAuraIntensity;
            _playerLight.pointLightInnerRadius = playerAuraInnerRadius;
            _playerLight.pointLightOuterRadius = playerAuraOuterRadius;
            _playerLight.falloffIntensity = 0.82f;
        }

        private void CreatePointLight(
            Transform parent,
            string lightName,
            Vector3 worldPos,
            Color color,
            float intensity,
            float innerRadius,
            float outerRadius,
            float falloff,
            float pulseAmp,
            float pulseSpeed,
            bool flicker)
        {
            GameObject go = new GameObject(lightName);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            Light2D light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightInnerRadius = innerRadius;
            light.pointLightOuterRadius = outerRadius;
            light.falloffIntensity = Mathf.Clamp01(falloff);
            ApplyAllSortingLayers(light);

            _animatedLights.Add(new AnimatedLightEntry
            {
                light = light,
                baseIntensity = intensity,
                baseOuterRadius = outerRadius,
                pulseAmplitude = pulseAmp,
                pulseSpeed = pulseSpeed,
                phase = (worldPos.x * 0.73f + worldPos.y * 1.19f) * 0.5f,
                isFlicker = flicker
            });
        }

        private void UpdateGlobalZoneAtmosphere()
        {
            if (_globalLight == null) return;

            MapZoneId activeZone = GameProgressionManager.Instance != null
                ? GameProgressionManager.Instance.CurrentZone
                : MapZoneId.DesaEldoria;

            GetZoneAtmosphere(activeZone, out Color targetColor, out float targetIntensity);

            float t = Time.deltaTime * zoneBlendSpeed;
            _currentGlobalColor = Color.Lerp(_currentGlobalColor, targetColor, t);
            _currentGlobalIntensity = Mathf.Lerp(_currentGlobalIntensity, targetIntensity, t);

            _globalLight.color = _currentGlobalColor;
            _globalLight.intensity = _currentGlobalIntensity;
        }

        private void UpdateAnimatedPointLights()
        {
            float now = Time.time;
            for (int i = 0; i < _animatedLights.Count; i++)
            {
                AnimatedLightEntry entry = _animatedLights[i];
                if (entry.light == null) continue;

                float wave;
                if (entry.isFlicker)
                {
                    float s1 = Mathf.Sin(now * entry.pulseSpeed + entry.phase);
                    float s2 = Mathf.Sin(now * (entry.pulseSpeed * 2.17f) + entry.phase * 1.7f) * 0.45f;
                    wave = (s1 + s2) * 0.7f;
                }
                else
                {
                    wave = Mathf.Sin(now * entry.pulseSpeed + entry.phase);
                }

                entry.light.intensity = Mathf.Max(0.05f, entry.baseIntensity * (1f + wave * entry.pulseAmplitude));
                entry.light.pointLightOuterRadius = Mathf.Max(0.5f, entry.baseOuterRadius * (1f + wave * (entry.pulseAmplitude * 0.35f)));
            }
        }

        private static void ApplyAllSortingLayers(Light2D light)
        {
            if (light == null) return;
            light.AddTargetSortingLayer("Default");
            light.AddTargetSortingLayer("Ground");
            light.AddTargetSortingLayer("Prop");
        }

        private static void GetZoneAtmosphere(MapZoneId zone, out Color color, out float intensity)
        {
            switch (zone)
            {
                case MapZoneId.AreaPercabangan:
                    color = new Color(0.86f, 0.92f, 0.98f, 1f);
                    intensity = 0.80f;
                    break;
                case MapZoneId.Boss1_GunungBara:
                    color = new Color(0.95f, 0.78f, 0.70f, 1f);
                    intensity = 0.76f;
                    break;
                case MapZoneId.Boss2_GuaEsAbadi:
                    color = new Color(0.80f, 0.90f, 1.0f, 1f);
                    intensity = 0.78f;
                    break;
                case MapZoneId.Boss3_HutanRacun:
                    color = new Color(0.76f, 0.90f, 0.78f, 1f);
                    intensity = 0.74f;
                    break;
                case MapZoneId.Boss4_KastilBayangan:
                    color = new Color(0.72f, 0.70f, 0.90f, 1f);
                    intensity = 0.70f;
                    break;
                case MapZoneId.DesaEldoria:
                default:
                    color = new Color(0.92f, 0.94f, 1.0f, 1f);
                    intensity = 0.84f;
                    break;
            }
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
