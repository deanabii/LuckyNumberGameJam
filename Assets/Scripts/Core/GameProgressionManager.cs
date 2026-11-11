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

        private MapZoneId _currentZone = MapZoneId.DesaEldoria;

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
        }

        public void MarkIntroWatched()
        {
            hasWatchedIntro = true;
            OnProgressionChanged?.Invoke();
        }

        public void MarkTalkedToArvand()
        {
            if (!hasTalkedToArvand)
            {
                hasTalkedToArvand = true;
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
                OnProgressionChanged?.Invoke();
            }
        }

        public void MarkHokiRestored()
        {
            hasRestoredHokiAtAltar = true;
            OnProgressionChanged?.Invoke();
        }

        public void SetCurrentZone(MapZoneId zone)
        {
            _currentZone = zone;
            OnProgressionChanged?.Invoke();
        }

        public bool CanEnterZone(MapZoneId targetZone, out string warningTitle, out string warningMessage)
        {
            warningTitle = "BAGIAN INI BELUM BISA KAMU MASUKI";
            warningMessage = string.Empty;

            switch (targetZone)
            {
                case MapZoneId.DesaEldoria:
                    return true;

                case MapZoneId.AreaPercabangan:
                    if (!hasTalkedToArvand)
                    {
                        warningMessage =
                            "Kamu belum menerima petunjuk misi!\n\n" +
                            "Temui Kepala Desa Arvand di tengah Desa Eldoria terlebih dahulu (Tekan E) sebelum keluar desa.";
                        return false;
                    }
                    return true;

                case MapZoneId.Boss1_GunungBara:
                    if (!hasTalkedToArvand)
                    {
                        warningMessage =
                            "Temui Kepala Desa Arvand di Desa Eldoria terlebih dahulu untuk memulai perjalanan!";
                        return false;
                    }
                    return true;

                case MapZoneId.Boss2_GuaEsAbadi:
                    if (!IsBossDefeated(0))
                    {
                        warningMessage =
                            "Jalur menuju Gua Es Abadi (Glaciera) masih disegel oleh dinding es kuno!\n\n" +
                            "Kamu harus mengalahkan BOS 1 – Ignaroth di Gunung Bara (Jalur Kiri) terlebih dahulu untuk mendapatkan Kunci Gua Es & Pecahan HOKI Pertama.";
                        return false;
                    }
                    return true;

                case MapZoneId.Boss3_HutanRacun:
                    if (!IsBossDefeated(1))
                    {
                        warningMessage =
                            "Kabut beracun di Hutan Racun (Venomark) masih terlalu pekat untuk dilewati!\n\n" +
                            "Kamu harus mengalahkan BOS 2 – Glaciera di Gua Es Abadi (Jalur Kiri Atas) terlebih dahulu untuk mendapatkan Kunci Hutan Racun & Pecahan HOKI Kedua.";
                        return false;
                    }
                    return true;

                case MapZoneId.Boss4_KastilBayangan:
                    if (!IsBossDefeated(2))
                    {
                        warningMessage =
                            "Gerbang Kastil Bayangan (Nocthar) terkunci rapat oleh segel kegelapan!\n\n" +
                            "Kamu harus mengalahkan BOS 3 – Venomark di Hutan Racun (Jalur Kanan Atas) terlebih dahulu untuk mendapatkan Kunci Kastil Bayangan & Pecahan HOKI Ketiga.";
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
                return "Temui Kepala Desa Arvand di Desa Eldoria [Tekan E]";

            if (!IsBossDefeated(0))
                return "Pergi ke Utara (Percabangan) → Jalur Kiri (Gunung Bara) & kalahkan BOS 1: Ignaroth";

            if (!IsBossDefeated(1))
                return "Pergi ke Percabangan → Jalur Kiri Atas (Gua Es Abadi) & kalahkan BOS 2: Glaciera";

            if (!IsBossDefeated(2))
                return "Pergi ke Percabangan → Jalur Kanan Atas (Hutan Racun) & kalahkan BOS 3: Venomark";

            if (!IsBossDefeated(3))
                return "Pergi ke Percabangan → Jalur Kanan (Kastil Bayangan) & kalahkan BOS 4: Nocthar";

            if (!hasRestoredHokiAtAltar)
                return "Kembali ke Desa Eldoria & satukan 4 Kepingan HOKI di Altar Suci [Tekan E]!";

            return "Desa Eldoria kembali damai! Artefak HOKI telah pulih.";
        }

        public static string GetZoneDisplayName(MapZoneId zone)
        {
            switch (zone)
            {
                case MapZoneId.DesaEldoria: return "Desa Eldoria (Desa Terkutuk)";
                case MapZoneId.AreaPercabangan: return "Persimpangan Empat Demon";
                case MapZoneId.Boss1_GunungBara: return "Gunung Bara – Arena Ignaroth (Boss 1)";
                case MapZoneId.Boss2_GuaEsAbadi: return "Gua Es Abadi – Arena Glaciera (Boss 2)";
                case MapZoneId.Boss3_HutanRacun: return "Hutan Racun – Arena Venomark (Boss 3)";
                case MapZoneId.Boss4_KastilBayangan: return "Kastil Bayangan – Arena Nocthar (Boss 4)";
                default: return "Pulau Aetheria";
            }
        }
    }
}
