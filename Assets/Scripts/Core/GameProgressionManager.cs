using System;
using UnityEngine;

namespace LuckyNumber.Core
{
    public enum MapZoneId
    {
        DesaEldoria = 0,
        AreaPercabangan = 1,
        Boss1_GunungBara = 2,
        Boss2_GuaEsAbadi = 3,
        Boss3_HutanRacun = 4,
        Boss4_KastilBayangan = 5
    }

    public class GameProgressionManager : MonoBehaviour
    {
        public static GameProgressionManager Instance { get; private set; }

        public event Action OnProgressionChanged;

        [Header("Quest & Story State")]
        [SerializeField] private bool hasWatchedIntro;
        [SerializeField] private bool hasTalkedToArvand;
        [SerializeField] private bool[] bossDefeated = new bool[4];
        [SerializeField] private bool hasRestoredHokiAtAltar;

        [Header("TestMap Audio Settings (Placeholder)")]
        [Tooltip("AudioSource untuk memutar BGM TestMap. Otomatis dibuat jika belum diisi.")]
        public AudioSource bgmSource;
        [Tooltip("Placeholder untuk klip Background Music (BGM) scene TestMap.")]
        public AudioClip testMapBgmMusic;
        [Range(0f, 1f)] public float bgmVolume = 0.5f;

        private MapZoneId _currentZone = MapZoneId.DesaEldoria;

        // Session state shared across TestMap <-> Boss scenes during a Play session
        private static bool _hasSessionSnapshot;
        private static bool _sessionHasWatchedIntro;
        private static bool _sessionHasTalkedToArvand;
        private static readonly bool[] _sessionBossDefeated = new bool[4];
        private static bool _sessionHasRestoredHoki;

        private static bool _returningFromBossScene;
        private static MapZoneId _pendingReturnZone = MapZoneId.DesaEldoria;
        private static Vector3 _pendingReturnPosition;
        private static int _pendingBossVictoryIndex = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticSessionOnPlay()
        {
            _hasSessionSnapshot = false;
            _sessionHasWatchedIntro = false;
            _sessionHasTalkedToArvand = false;
            for (int i = 0; i < _sessionBossDefeated.Length; i++) _sessionBossDefeated[i] = false;
            _sessionHasRestoredHoki = false;
            _returningFromBossScene = false;
            _pendingReturnZone = MapZoneId.DesaEldoria;
            _pendingReturnPosition = Vector3.zero;
            _pendingBossVictoryIndex = -1;
        }

        public bool HasWatchedIntro => hasWatchedIntro;
        public bool HasTalkedToArvand => hasTalkedToArvand;
        public bool HasRestoredHokiAtAltar => hasRestoredHokiAtAltar;
        public MapZoneId CurrentZone => _currentZone;

        public int HokiFragmentsCollected
        {
            get
            {
                int count = 0;
                for (int i = 0; i < bossDefeated.Length; i++)
                {
                    if (bossDefeated[i]) count++;
                }
                return count;
            }
        }

        public bool HasSixSevenUltimate => IsBossDefeated(1); // Unlocked after Boss 2 (Glaciera)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_hasSessionSnapshot)
            {
                hasWatchedIntro = _sessionHasWatchedIntro;
                hasTalkedToArvand = _sessionHasTalkedToArvand;
                hasRestoredHokiAtAltar = _sessionHasRestoredHoki;
                for (int i = 0; i < bossDefeated.Length && i < _sessionBossDefeated.Length; i++)
                {
                    bossDefeated[i] = _sessionBossDefeated[i];
                }
            }
            else
            {
                SyncSessionSnapshot();
            }
        }

        private void Start()
        {
            PlayBGM();
        }

        public void PlayBGM()
        {
            if (bgmSource == null)
            {
                bgmSource = GetComponent<AudioSource>();
                if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
            }
            if (testMapBgmMusic != null)
            {
                if (bgmSource.clip != testMapBgmMusic || !bgmSource.isPlaying)
                {
                    bgmSource.clip = testMapBgmMusic;
                    bgmSource.loop = true;
                    bgmSource.volume = bgmVolume;
                    bgmSource.Play();
                }
            }
        }

        private void SyncSessionSnapshot()
        {
            _hasSessionSnapshot = true;
            _sessionHasWatchedIntro = hasWatchedIntro;
            _sessionHasTalkedToArvand = hasTalkedToArvand;
            _sessionHasRestoredHoki = hasRestoredHokiAtAltar;
            for (int i = 0; i < bossDefeated.Length && i < _sessionBossDefeated.Length; i++)
            {
                _sessionBossDefeated[i] = bossDefeated[i];
            }
        }

        public void PrepareEnterBossScene(MapZoneId returnZone, Vector3 returnPosition)
        {
            hasWatchedIntro = true;
            SyncSessionSnapshot();
            _returningFromBossScene = false;
            _pendingReturnZone = returnZone;
            _pendingReturnPosition = returnPosition;
            _pendingBossVictoryIndex = -1;
        }

        public static void CompleteBossBattleAndPrepareReturn(int bossIndex, bool defeated)
        {
            _hasSessionSnapshot = true;
            _sessionHasWatchedIntro = true;
            _sessionHasTalkedToArvand = true;
            if (defeated && bossIndex >= 0 && bossIndex < _sessionBossDefeated.Length)
            {
                _sessionBossDefeated[bossIndex] = true;
            }
            _returningFromBossScene = true;
            _pendingBossVictoryIndex = defeated ? bossIndex : -1;
            if (_pendingReturnZone == MapZoneId.DesaEldoria)
            {
                _pendingReturnZone = bossIndex == 0 ? MapZoneId.Boss1_GunungBara : MapZoneId.AreaPercabangan;
                _pendingReturnPosition = bossIndex == 0 ? new Vector3(-36.5f, 21.4f, 0f) : new Vector3(-0.5f, 22.5f, 0f);
            }
        }

        public bool ConsumeBossReturnState(out MapZoneId returnZone, out Vector3 returnPosition, out int wonBossIndex)
        {
            if (!_returningFromBossScene)
            {
                returnZone = MapZoneId.DesaEldoria;
                returnPosition = Vector3.zero;
                wonBossIndex = -1;
                return false;
            }

            returnZone = _pendingReturnZone;
            returnPosition = _pendingReturnPosition;
            wonBossIndex = _pendingBossVictoryIndex;
            _returningFromBossScene = false;
            _pendingBossVictoryIndex = -1;
            return true;
        }

        public void MarkIntroWatched()
        {
            hasWatchedIntro = true;
            SyncSessionSnapshot();
            OnProgressionChanged?.Invoke();
        }

        public void MarkTalkedToArvand()
        {
            if (!hasTalkedToArvand)
            {
                hasTalkedToArvand = true;
                SyncSessionSnapshot();
                OnProgressionChanged?.Invoke();
            }
        }

        public bool IsBossDefeated(int bossIndex)
        {
            if (bossIndex < 0 || bossIndex >= bossDefeated.Length) return false;
            return bossDefeated[bossIndex];
        }

        public void MarkBossDefeated(int bossIndex)
        {
            if (bossIndex < 0 || bossIndex >= bossDefeated.Length) return;
            if (!bossDefeated[bossIndex])
            {
                bossDefeated[bossIndex] = true;
                SyncSessionSnapshot();
                OnProgressionChanged?.Invoke();
            }
        }

        public void MarkHokiRestored()
        {
            hasRestoredHokiAtAltar = true;
            SyncSessionSnapshot();
            OnProgressionChanged?.Invoke();
        }

        public void SetCurrentZone(MapZoneId zone)
        {
            _currentZone = zone;
            OnProgressionChanged?.Invoke();
        }

        public bool CanEnterZone(MapZoneId targetZone, out string warningTitle, out string warningMessage)
        {
            warningTitle = "THIS AREA CANNOT BE ACCESSED YET";
            warningMessage = string.Empty;

            switch (targetZone)
            {
                case MapZoneId.DesaEldoria:
                    return true;

                case MapZoneId.AreaPercabangan:
                    if (!hasTalkedToArvand)
                    {
                        warningMessage =
                            "You have not received your quest briefing yet!\n\n" +
                            "Speak with Chief Arvand in the center of Eldoria Village first [Press E] before venturing out of the village.";
                        return false;
                    }
                    return true;

                case MapZoneId.Boss1_GunungBara:
                    if (!hasTalkedToArvand)
                    {
                        warningMessage =
                            "Speak with Chief Arvand in Eldoria Village first to begin your adventure!";
                        return false;
                    }
                    return true;

                case MapZoneId.Boss2_GuaEsAbadi:
                    if (!IsBossDefeated(0))
                    {
                        warningMessage =
                            "The path to the Eternal Ice Cavern (Glaciera) is sealed by an ancient frost barrier!\n\n" +
                            "You must defeat BOSS 1: Ignaroth at Mount Cinder (Left Path) first to obtain the Ice Cavern Key & First HOKI Fragment.";
                        return false;
                    }
                    return true;

                case MapZoneId.Boss3_HutanRacun:
                    if (!IsBossDefeated(1))
                    {
                        warningMessage =
                            "The toxic mist in the Poison Forest (Venomark) is far too thick to cross safely!\n\n" +
                            "You must defeat BOSS 2: Glaciera in the Eternal Ice Cavern (Upper-Left Path) first to obtain the Poison Forest Key & Second HOKI Fragment.";
                        return false;
                    }
                    return true;

                case MapZoneId.Boss4_KastilBayangan:
                    if (!IsBossDefeated(2))
                    {
                        warningMessage =
                            "The gates of Shadow Castle (Nocthar) are sealed tight by shadowy magic!\n\n" +
                            "You must defeat BOSS 3: Venomark in the Poison Forest (Upper-Right Path) first to obtain the Shadow Castle Key & Third HOKI Fragment.";
                        return false;
                    }
                    return true;

                default:
                    return true;
            }
        }

        public string GetCurrentQuestObjective()
        {
            if (!hasTalkedToArvand)
                return "Speak with Chief Arvand in Eldoria Village [Press E]";

            if (!IsBossDefeated(0))
                return "Head North (Crossroads) -> Left Path (Mount Cinder) & defeat BOSS 1: Ignaroth";

            if (!IsBossDefeated(1))
                return "Head to Crossroads -> Upper-Left Path (Eternal Ice Cavern) & defeat BOSS 2: Glaciera";

            if (!IsBossDefeated(2))
                return "Head to Crossroads -> Upper-Right Path (Poison Forest) & defeat BOSS 3: Venomark";

            if (!IsBossDefeated(3))
                return "Head to Crossroads -> Right Path (Shadow Castle) & defeat BOSS 4: Nocthar";

            if (!hasRestoredHokiAtAltar)
                return "Defeat all four bosses to shatter the dark curse!";

            return "Eldoria Village is at peace! The dark curse has been lifted.";
        }

        public static string GetZoneDisplayName(MapZoneId zone)
        {
            switch (zone)
            {
                case MapZoneId.DesaEldoria: return "Eldoria Village (Cursed Village)";
                case MapZoneId.AreaPercabangan: return "Crossroads of the Four Demons";
                case MapZoneId.Boss1_GunungBara: return "Mount Cinder: Ignaroth Arena (Boss 1)";
                case MapZoneId.Boss2_GuaEsAbadi: return "Eternal Ice Cavern: Glaciera Arena (Boss 2)";
                case MapZoneId.Boss3_HutanRacun: return "Poison Forest: Venomark Arena (Boss 3)";
                case MapZoneId.Boss4_KastilBayangan: return "Shadow Castle: Nocthar Arena (Boss 4)";
                default: return "Aetheria Island";
            }
        }
    }
}
