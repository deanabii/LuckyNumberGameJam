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
        [SerializeField] private string displayName = "Chief Arvand";
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
                        return $"[E] Talk to {displayName}";
                    case InteractableRole.HokiSacredAltar:
                        return "[E] Inspect Sacred HOKI Altar";
                    case InteractableRole.BossEncounter:
                        return $"[E] Challenge {displayName}";
                    default:
                        return $"[E] Interact with {displayName}";
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
                        speakerName = "Chief Arvand",
                        text = "Thank goodness you have arrived in Eldoria Village, Malios! The curse of Vorthak has pushed our village to the brink of ruin.",
                        portrait = arvandPortrait,
                        isPlayerSpeaking = false
                    },
                    new DialogueLine
                    {
                        speakerName = "Malios",
                        text = "Elder Eldric of Valoria told me everything, Chief Arvand. How do we restore the HOKI Artifact?",
                        portrait = maliosPortrait,
                        isPlayerSpeaking = true
                    },
                    new DialogueLine
                    {
                        speakerName = "Chief Arvand",
                        text = "The HOKI Artifact, sacred seal of numbers 6 and 7, was shattered into four fragments by Vorthak's four demon lieutenants.",
                        portrait = arvandPortrait,
                        isPlayerSpeaking = false
                    },
                    new DialogueLine
                    {
                        speakerName = "Chief Arvand",
                        text = "You must defeat them in sequence! Head North out of the village to the Crossroads, then take the LEFT PATH toward Mount Cinder to confront BOSS 1: Ignaroth, the Fire Demon!",
                        portrait = arvandPortrait,
                        isPlayerSpeaking = false
                    },
                    new DialogueLine
                    {
                        speakerName = "Malios",
                        text = "Understood! I will head to Mount Cinder on the left path first and reclaim all the lost fragments of HOKI!",
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
                        speakerName = "Chief Arvand",
                        text = "Walk north out of Eldoria Village to the Crossroads. Take the LEFT PATH to Mount Cinder and defeat the Fire Demon Ignaroth first!",
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
                        speakerName = "Chief Arvand",
                        text = "Outstanding, Malios! You have secured the First HOKI Fragment and the Ice Cavern Key from Ignaroth!",
                        portrait = arvandPortrait
                    },
                    new DialogueLine
                    {
                        speakerName = "Chief Arvand",
                        text = "Now head to the UPPER-LEFT PATH at the Crossroads to enter the Eternal Ice Cavern and defeat BOSS 2: Glaciera!",
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
                        speakerName = "Chief Arvand",
                        text = "The power of SIXSEVEN awakens within you! With the Poison Forest Key obtained from Glaciera, the UPPER-RIGHT PATH is now open.",
                        portrait = arvandPortrait
                    },
                    new DialogueLine
                    {
                        speakerName = "Chief Arvand",
                        text = "Enter the Poison Forest and defeat BOSS 3: Venomark to claim the Third HOKI Fragment!",
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
                        speakerName = "Chief Arvand",
                        text = "Only one fragment remains! Use the Shadow Castle Key to unlock the RIGHT PATH at the Crossroads and defeat BOSS 4: Nocthar!",
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
                        speakerName = "Chief Arvand",
                        text = "You have gathered all four HOKI fragments, Malios! Approach the Sacred HOKI Altar beside me to unite them once more!",
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
                        speakerName = "Sacred HOKI Altar",
                        text = $"The ancient altar enshrining the HOKI Artifact (Numbers 6 & 7) is cracked and dormant.\n(HOKI Fragments collected: {count} / 4. Gather all 4 fragments from the four demons to restore the altar!)",
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
                        speakerName = "Chief Arvand",
                        text = "Malios! Place all four HOKI fragments into the altar slots in sequence to restore their sacred power!",
                        portrait = arvandPortrait,
                        isPlayerSpeaking = false
                    },
                    new DialogueLine
                    {
                        speakerName = "Malios",
                        text = "The four HOKI fragments are beginning to glow... and the power of SIXSEVEN resonates within me!",
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
                        text = $"{displayName} has already been defeated and this cave is conquered! Proceed to the next cave or return to the Sacred Altar.",
                        portrait = maliosPortrait,
                        isPlayerSpeaking = true
                    }
                });
                return;
            }

            string cardGameName = GetBossCardGameTitle(bossIndex);

            DialogueLine[] bossLines =
            {
                new DialogueLine
                {
                    speakerName = displayName,
                    text = $"You dare enter my domain, Malios?! Face the {cardGameName} challenge within this cave!",
                    portrait = null,
                    isPlayerSpeaking = false
                },
                new DialogueLine
                {
                    speakerName = "Malios",
                    text = "I will not back down! With the calculations of Lucky Number, I will break your seal and reclaim the HOKI Fragment!",
                    portrait = maliosPortrait,
                    isPlayerSpeaking = true
                }
            };

            DialogueSystem.Instance.StartDialogue(bossLines, () =>
            {
                if (MapZoneProgressionManager.Instance != null)
                {
                    MapZoneProgressionManager.Instance.EnterBossScene(bossIndex);
                }
            });
        }

        private static string GetBossCardGameTitle(int idx)
        {
            switch (idx)
            {
                case 0: return "Lucky 13 (3 Fire Pillars: Target 13)";
                case 1: return "Frozen Memory (8 Ice Cards: Pairs Summing to 13)";
                case 2: return "Poison Concoction (Leaf + Root + Flower = 21)";
                case 3: return "Shadow Sequence (4 Number Logic Seals)";
                default: return "Lucky Number";
            }
        }

        private static string GetBossRewardDescription(int idx)
        {
            switch (idx)
            {
                case 0: return "HOKI Fragment #1 + Ice Cavern Key (Boss 2 Path Unlocked!)";
                case 1: return "HOKI Fragment #2 + SIXSEVEN Ultimate Unlocked + Poison Forest Key (Boss 3 Path Unlocked!)";
                case 2: return "HOKI Fragment #3 + Shadow Castle Key (Boss 4 Path Unlocked!)";
                case 3: return "HOKI Fragment #4 (All Fragments Collected! Return to Eldoria Altar!)";
                default: return "HOKI Fragment";
            }
        }
    }
}
