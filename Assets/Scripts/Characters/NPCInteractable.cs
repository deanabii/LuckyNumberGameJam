using UnityEngine;
using LuckyNumber.Core;
using LuckyNumber.UI;

namespace LuckyNumber.Characters
{
    public enum InteractableRole
    {
        ArvandVillageChief = 0,
        HokiSacredAltar = 1,
        BossEncounter = 2
    }

    public class NPCInteractable : MonoBehaviour, IWorldInteractable
    {
        [Header("Interactable Configuration")]
        [SerializeField] private InteractableRole role = InteractableRole.ArvandVillageChief;
        [SerializeField] private string displayName = "Kepala Desa Arvand";
        [SerializeField] private float interactionRadius = 2.6f;
        [SerializeField] private int bossIndex = 0; // 0=Ignaroth, 1=Glaciera, 2=Venomark, 3=Nocthar

        private Transform _exclamationMarker;
        private Vector3 _exclamationBaseLocalPos;

        public Transform Transform => transform;
        public float InteractionRadius => interactionRadius;
        public bool CanInteract => true;

        public string InteractionPrompt
        {
            get
            {
                switch (role)
                {
                    case InteractableRole.ArvandVillageChief:
                        return $"[E] Bicara dengan {displayName}";
                    case InteractableRole.HokiSacredAltar:
                        return "[E] Periksa Altar Suci HOKI";
                    case InteractableRole.BossEncounter:
                        return $"[E] Hadapi {displayName}";
                    default:
                        return $"[E] Interaksi {displayName}";
                }
            }
        }

        public void Configure(InteractableRole newRole, string nameLabel, int bossIdx = 0, float radius = 2.6f)
        {
            role = newRole;
            displayName = nameLabel;
            bossIndex = bossIdx;
            interactionRadius = radius;
            CreateFloatingIndicator();
        }

        private void OnEnable()
        {
            PlayerController.RegisterInteractable(this);
        }

        private void OnDisable()
        {
            PlayerController.UnregisterInteractable(this);
        }

        private void Start()
        {
            if (_exclamationMarker == null)
            {
                CreateFloatingIndicator();
            }
        }

        private void Update()
        {
            if (_exclamationMarker != null)
            {
                float bob = Mathf.Sin(Time.time * 4f) * 0.12f;
                _exclamationMarker.localPosition = _exclamationBaseLocalPos + new Vector3(0f, bob, 0f);
            }
        }

        private void CreateFloatingIndicator()
        {
            var registry = GameAssetRegistry.Instance;
            if (registry == null || registry.exclamationSprite == null) return;

            Transform existing = transform.Find("ExclamationIndicator");
            if (existing != null)
            {
                _exclamationMarker = existing;
                _exclamationBaseLocalPos = existing.localPosition;
                return;
            }

            GameObject markerObj = new GameObject("ExclamationIndicator", typeof(SpriteRenderer));
            markerObj.transform.SetParent(transform, false);

            // Offset above head depending on whether parent is scaled down (like Arvand 0.22) or 1.0
            float parentScaleY = Mathf.Max(0.05f, transform.localScale.y);
            float localY = (role == InteractableRole.ArvandVillageChief) ? 4.3f : (1.5f / parentScaleY);
            _exclamationBaseLocalPos = new Vector3(0f, localY, 0f);
            markerObj.transform.localPosition = _exclamationBaseLocalPos;

            float invScale = (role == InteractableRole.ArvandVillageChief) ? 2.2f : (0.55f / parentScaleY);
            markerObj.transform.localScale = new Vector3(invScale, invScale, 1f);

            SpriteRenderer sr = markerObj.GetComponent<SpriteRenderer>();
            sr.sprite = registry.exclamationSprite;
            sr.sortingLayerName = "Prop";
            sr.sortingOrder = 150;

            _exclamationMarker = markerObj.transform;
        }

        public void Interact(PlayerController player)
        {
            if (DialogueSystem.Instance == null || DialogueSystem.Instance.IsDialogueOpen) return;

            switch (role)
            {
                case InteractableRole.ArvandVillageChief:
                    InteractWithArvand();
                    break;
                case InteractableRole.HokiSacredAltar:
                    InteractWithAltar();
                    break;
                case InteractableRole.BossEncounter:
                    InteractWithBoss();
                    break;
            }
        }

        private void InteractWithArvand()
        {
            var prog = GameProgressionManager.Instance;
            var reg = GameAssetRegistry.Instance;
            Sprite arvandPortrait = (reg != null && reg.arvandSprites != null && reg.arvandSprites.Length > 0) ? reg.arvandSprites[0] : null;
            Sprite maliosPortrait = (reg != null && reg.maliosSprites != null && reg.maliosSprites.Length > 0) ? reg.maliosSprites[0] : null;

            if (prog == null || !prog.HasTalkedToArvand)
            {
                DialogueLine[] introQuestLines =
                {
                    new DialogueLine
                    {
                        speakerName = "Kepala Desa Arvand",
                        text = "Syukurlah kau telah tiba di Desa Eldoria, Malios! Kutukan Vorthak membuat desa kami berada di ambang kehancuran.",
                        portrait = arvandPortrait,
                        isPlayerSpeaking = false
                    },
                    new DialogueLine
                    {
                        speakerName = "Malios",
                        text = "Kepala Desa Eldric dari Valoria telah menceritakan semuanya kepadaku, Tuan Arvand. Bagaimana cara kita memulihkan Artefak HOKI?",
                        portrait = maliosPortrait,
                        isPlayerSpeaking = true
                    },
                    new DialogueLine
                    {
                        speakerName = "Kepala Desa Arvand",
                        text = "Artefak HOKI—segel suci angka 6 dan 7—telah dipecahkan menjadi empat kepingan oleh empat demon bawahan Vorthak.",
                        portrait = arvandPortrait,
                        isPlayerSpeaking = false
                    },
                    new DialogueLine
                    {
                        speakerName = "Kepala Desa Arvand",
                        text = "Kau harus mengalahkan mereka secara berurutan! Pergilah ke Utara keluar desa menuju Area Percabangan, lalu ambil JALUR KIRI menuju Gunung Bara untuk menghadapi BOS 1: Ignaroth (Demon Api)!",
                        portrait = arvandPortrait,
                        isPlayerSpeaking = false
                    },
                    new DialogueLine
                    {
                        speakerName = "Malios",
                        text = "Baik! Aku akan menuju Gunung Bara di jalur kiri terlebih dahulu dan merebut kembali seluruh pecahan HOKI!",
                        portrait = maliosPortrait,
                        isPlayerSpeaking = true
                    }
                };

                DialogueSystem.Instance.StartDialogue(introQuestLines, () =>
                {
                    if (prog != null) prog.MarkTalkedToArvand();
                });
                return;
            }

            int fragments = prog.HokiFragmentsCollected;
            if (fragments == 0)
            {
                DialogueSystem.Instance.StartDialogue(new[]
                {
                    new DialogueLine
                    {
                        speakerName = "Kepala Desa Arvand",
                        text = "Berjalanlah ke jalan Utara Desa Eldoria menuju Persimpangan. Ambil JALUR KIRI (Gunung Bara) untuk mengalahkan Demon Api Ignaroth terlebih dahulu!",
                        portrait = arvandPortrait
                    }
                });
            }
            else if (fragments == 1)
            {
                DialogueSystem.Instance.StartDialogue(new[]
                {
                    new DialogueLine
                    {
                        speakerName = "Kepala Desa Arvand",
                        text = "Luar biasa, Malios! Kau sudah mendapatkan Pecahan HOKI Pertama dan Kunci Gua Es dari Ignaroth!",
                        portrait = arvandPortrait
                    },
                    new DialogueLine
                    {
                        speakerName = "Kepala Desa Arvand",
                        text = "Sekarang pergilah ke JALUR KIRI ATAS di Persimpangan untuk memasuki Gua Es Abadi dan kalahkan BOS 2: Glaciera!",
                        portrait = arvandPortrait
                    }
                });
            }
            else if (fragments == 2)
            {
                DialogueSystem.Instance.StartDialogue(new[]
                {
                    new DialogueLine
                    {
                        speakerName = "Kepala Desa Arvand",
                        text = "Kekuatan SIXSEVEN telah bangkit di dalam dirimu! Dengan Kunci Hutan Racun dari Glaciera, kini JALUR KANAN ATAS telah terbuka.",
                        portrait = arvandPortrait
                    },
                    new DialogueLine
                    {
                        speakerName = "Kepala Desa Arvand",
                        text = "Masuklah ke Hutan Racun dan kalahkan BOS 3: Venomark untuk merebut Pecahan HOKI Ketiga!",
                        portrait = arvandPortrait
                    }
                });
            }
            else if (fragments == 3)
            {
                DialogueSystem.Instance.StartDialogue(new[]
                {
                    new DialogueLine
                    {
                        speakerName = "Kepala Desa Arvand",
                        text = "Tinggal satu pecahan lagi! Gunakan Kunci Kastil Bayangan untuk membuka JALUR KANAN di Persimpangan dan kalahkan BOS 4: Nocthar!",
                        portrait = arvandPortrait
                    }
                });
            }
            else
            {
                DialogueSystem.Instance.StartDialogue(new[]
                {
                    new DialogueLine
                    {
                        speakerName = "Kepala Desa Arvand",
                        text = "Kau berhasil mengumpulkan keempat pecahan HOKI, Malios! Cepat berinteraksi dengan Altar Suci HOKI di sebelahku untuk menyatukannya kembali!",
                        portrait = arvandPortrait
                    }
                });
            }
        }

        private void InteractWithAltar()
        {
            var prog = GameProgressionManager.Instance;
            var reg = GameAssetRegistry.Instance;
            Sprite arvandPortrait = (reg != null && reg.arvandSprites != null && reg.arvandSprites.Length > 0) ? reg.arvandSprites[0] : null;
            Sprite maliosPortrait = (reg != null && reg.maliosSprites != null && reg.maliosSprites.Length > 0) ? reg.maliosSprites[0] : null;

            int count = prog != null ? prog.HokiFragmentsCollected : 0;
            if (count < 4)
            {
                DialogueSystem.Instance.StartDialogue(new[]
                {
                    new DialogueLine
                    {
                        speakerName = "Altar Suci HOKI",
                        text = $"Altar kuno tempat Artefak HOKI (Angka 6 & 7) disemayamkan tampak retak.\n(Kepingan HOKI terkumpul: {count} / 4. Kumpulkan seluruh 4 kepingan dari keempat demon untuk memulihkan altar!)",
                        portrait = maliosPortrait,
                        isPlayerSpeaking = true
                    }
                });
            }
            else
            {
                DialogueLine[] restoreLines =
                {
                    new DialogueLine
                    {
                        speakerName = "Kepala Desa Arvand",
                        text = "Malios! Letakkan keempat pecahan HOKI itu pada slot altar sesuai urutannya agar kekuatannya kembali menyatu!",
                        portrait = arvandPortrait,
                        isPlayerSpeaking = false
                    },
                    new DialogueLine
                    {
                        speakerName = "Malios",
                        text = "Keempat pecahan HOKI mulai bersinar... dan kekuatan SIXSEVEN di tubuhku ikut beresonansi!",
                        portrait = maliosPortrait,
                        isPlayerSpeaking = true
                    }
                };

                DialogueSystem.Instance.StartDialogue(restoreLines, () =>
                {
                    if (CutsceneManager.Instance != null)
                    {
                        CutsceneManager.Instance.PlayEndingCutscene();
                    }
                });
            }
        }

        private void InteractWithBoss()
        {
            var prog = GameProgressionManager.Instance;
            var reg = GameAssetRegistry.Instance;
            Sprite maliosPortrait = (reg != null && reg.maliosSprites != null && reg.maliosSprites.Length > 0) ? reg.maliosSprites[0] : null;

            if (prog != null && prog.IsBossDefeated(bossIndex))
            {
                DialogueSystem.Instance.StartDialogue(new[]
                {
                    new DialogueLine
                    {
                        speakerName = "Malios",
                        text = $"{displayName} sudah dikalahkan dan kepingan HOKI di area ini telah diamankan. Saatnya melanjutkan perjalanan!",
                        portrait = maliosPortrait,
                        isPlayerSpeaking = true
                    }
                });
                return;
            }

            string cardGameName = GetBossCardGameTitle(bossIndex);
            string rewardDesc = GetBossRewardDescription(bossIndex);

            DialogueLine[] bossLines =
            {
                new DialogueLine
                {
                    speakerName = displayName,
                    text = $"Kau berani memasuki arenaku, Malios?! Untuk menghancurkan perisaiku, kau harus menaklukkan tantangan kartu {cardGameName}!",
                    portrait = null,
                    isPlayerSpeaking = false
                },
                new DialogueLine
                {
                    speakerName = "Malios",
                    text = $"Aku tidak akan mundur! Dengan perhitungan Lucky Number, aku akan mematahkan segelmu dan mengambil kembali pecahan HOKI!",
                    portrait = maliosPortrait,
                    isPlayerSpeaking = true
                },
                new DialogueLine
                {
                    speakerName = "Sistem Pertarungan",
                    text = $"[Simulasi Kemenangan Boss {bossIndex + 1} – Card Battle UI akan diaktifkan pada tahap berikutnya]\n\nBerhasil mengalahkan {displayName}!\nReward diperoleh: {rewardDesc}",
                    portrait = maliosPortrait,
                    isPlayerSpeaking = true
                }
            };

            DialogueSystem.Instance.StartDialogue(bossLines, () =>
            {
                if (prog != null)
                {
                    prog.MarkBossDefeated(bossIndex);
                }
            });
        }

        private static string GetBossCardGameTitle(int idx)
        {
            switch (idx)
            {
                case 0: return "Lucky 13 (3 Pilar Api - Target Angka 13)";
                case 1: return "Frozen Memory (8 Kartu Es - Pasangan Jumlah 13)";
                case 2: return "Poison Combination (Resep Daun + Akar + Bunga = 21)";
                case 3: return "Shadow Sequence (4 Segel Urutan Logika Angka)";
                default: return "Lucky Number";
            }
        }

        private static string GetBossRewardDescription(int idx)
        {
            switch (idx)
            {
                case 0: return "Pecahan HOKI #1 + Kunci Gua Es Abadi (Jalur Boss 2 Terbuka!)";
                case 1: return "Pecahan HOKI #2 + Ultimate SIXSEVEN Terbuka + Kunci Hutan Racun (Jalur Boss 3 Terbuka!)";
                case 2: return "Pecahan HOKI #3 + Kunci Kastil Bayangan (Jalur Boss 4 Terbuka!)";
                case 3: return "Pecahan HOKI #4 (Seluruh Kepingan Terkumpul! Kembali ke Altar Desa Eldoria!)";
                default: return "Pecahan HOKI";
            }
        }
    }
}
