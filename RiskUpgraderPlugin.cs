using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

namespace RiskUpgrader
{
    [BepInPlugin("local.sana.ror2.riskupgrader", "Risk Upgrader", "0.3.0")]
    public sealed class RiskUpgraderPlugin : BaseUnityPlugin
    {
        private sealed class ItemChoice
        {
            public ItemIndex Index;
            public ItemDef Def;
            public string Name;
            public string Tier;
            public double Value;
            public int Owned;
        }

        private sealed class IconBinding
        {
            public UnityEngine.UI.Image Image;
            public ItemChoice Choice;
        }

        private ConfigEntry<KeyCode> toggleKey;
        private ConfigEntry<bool> allowLunar;
        private ConfigEntry<bool> allowVoid;
        private ConfigEntry<bool> protectWhileOpen;
        private ConfigEntry<float> exitProtectionSeconds;
        private ConfigEntry<float> exitRepelRadius;
        private ConfigEntry<float> exitRepelForce;
        private ConfigEntry<float> spinSeconds;
        private bool open;
        private GameObject canvasObject;
        private RectTransform panel;
        private RectTransform browserContent;
        private UnityEngine.UI.Text inputTitle;
        private UnityEngine.UI.Text targetTitle;
        private UnityEngine.UI.Text chanceText;
        private UnityEngine.UI.Text statusText;
        private UnityEngine.UI.Text browserTitle;
        private UnityEngine.UI.Text browserPageText;
        private UnityEngine.UI.Image inputIcon;
        private UnityEngine.UI.Image targetIcon;
        private UnityEngine.UI.Button rollButton;
        private UnityEngine.UI.RawImage wheelImage;
        private RectTransform marker;
        private Texture2D wheelTexture;
        private Texture2D honeycombTexture;
        private Font font;
        private readonly List<ItemChoice> inputs = new List<ItemChoice>();
        private readonly List<ItemChoice> targets = new List<ItemChoice>();
        private ItemChoice selectedInput;
        private ItemChoice selectedTarget;
        private int inputCount = 1;
        private int outputCount = 1;
        private bool browsingTargets;
        private int browserPage;
        private const int BrowserPageSize = 30;
        private CharacterBody protectedBody;
        private bool protectionAdded;
        private readonly UpgraderIconManager iconManager = new UpgraderIconManager();
        private readonly List<IconBinding> visibleIconBindings = new List<IconBinding>();
        private float nextIconRefresh;
        private MPEventSystem cursorOwner;
        private bool cursorAcquired;
        private CursorLockMode savedLock;
        private bool savedCursorVisible;
        private bool spinning;
        private float spinStart;
        private float spinDuration;
        private float spinEndTurns;
        private bool pendingSuccess;
        private CharacterMaster pendingMaster;
        private ItemIndex pendingTarget;
        private int pendingOutputCount;
        private readonly Color background = new Color(0.035f, 0.045f, 0.07f, 0.98f);
        private readonly Color surface = new Color(0.08f, 0.105f, 0.15f, 1f);
        private readonly Color raised = new Color(0.13f, 0.17f, 0.24f, 1f);
        private readonly Color gold = new Color(1f, 0.68f, 0.12f, 1f);
        private readonly Color accent = new Color(0.32f, 0.9f, 0.78f, 1f);

        private void Awake()
        {
            toggleKey = Config.Bind("Controls", "ToggleKey", KeyCode.F7, "Open Risk Upgrader.");
            allowLunar = Config.Bind("Items", "AllowLunar", true, "Allow lunar items as inputs and targets.");
            allowVoid = Config.Bind("Items", "AllowVoid", true, "Allow void items as inputs and targets.");
            protectWhileOpen = Config.Bind("Game", "ProtectWhileOpen", true, "Protect only the local host while the upgrader window is open.");
            exitProtectionSeconds = Config.Bind("Game", "ExitProtectionSeconds", 2.25f, "Invulnerability duration after closing the upgrader.");
            exitRepelRadius = Config.Bind("Game", "ExitRepelRadius", 18f, "Radius of the non-damaging enemy push when closing the upgrader.");
            exitRepelForce = Config.Bind("Game", "ExitRepelForce", 1800f, "Strength of the non-damaging enemy push when closing the upgrader.");
            spinSeconds = Config.Bind("Game", "SpinSeconds", 2.2f, "Wheel animation duration.");
            Logger.LogInfo("Risk Upgrader 0.3.0 loaded. Safe exit, Hallowed tiers and asynchronous DLC icons enabled.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey.Value)) SetOpen(!open);
            if (open && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
            if (open) MaintainProtection();
            if (open && Time.unscaledTime >= nextIconRefresh)
                RefreshVisibleIcons();
            if (spinning) UpdateSpin();
        }

        private void LateUpdate()
        {
            if (open) AcquireCursor();
        }

        private void SetOpen(bool value)
        {
            if (value && !RoR2Application.loadFinished) return;
            open = value;
            if (open)
            {
                EnsureUi();
                canvasObject.SetActive(true);
                RefreshCatalog();
                AcquireCursor();
                MaintainProtection();
            }
            else
            {
                if (canvasObject != null) canvasObject.SetActive(false);
                LocalUser localUser = LocalUserManager.GetFirstLocalUser();
                CharacterBody body = protectedBody != null ? protectedBody : (localUser == null ? null : localUser.cachedBody);
                RemoveProtection();
                ApplyExitSafety(body);
                ReleaseCursor();
            }
        }

        private void EnsureUi()
        {
            if (canvasObject != null) return;
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            canvasObject = new GameObject("RiskUpgraderCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            DontDestroyOnLoad(canvasObject);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = CreatePanel(canvasObject.transform, "Panel", new Vector2(1240f, 900f), background);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            CreateText(panel, "RISK UPGRADER", 38, FontStyle.Bold, new Vector2(0f, 405f), new Vector2(1200f, 54f), Color.white, TextAnchor.MiddleCenter);
            CreateText(panel, "F7 / ESC — закрыть    •    защита в окне + щит и импульс при выходе", 17, FontStyle.Normal, new Vector2(0f, 369f), new Vector2(1100f, 28f), new Color(0.7f, 0.75f, 0.82f), TextAnchor.MiddleCenter);

            honeycombTexture = BuildHoneycombTexture();
            RectTransform inputCard = CreateCard(panel, "InputCard", new Vector2(-420f, 205f), accent);
            RectTransform targetCard = CreateCard(panel, "TargetCard", new Vector2(420f, 205f), gold);
            UnityEngine.UI.Button inputCardButton = inputCard.gameObject.AddComponent<UnityEngine.UI.Button>();
            inputCardButton.targetGraphic = inputCard.GetComponent<UnityEngine.UI.Image>();
            inputCardButton.onClick.AddListener(delegate { browsingTargets = false; browserPage = 0; PopulateBrowser(); });
            UnityEngine.UI.Button targetCardButton = targetCard.gameObject.AddComponent<UnityEngine.UI.Button>();
            targetCardButton.targetGraphic = targetCard.GetComponent<UnityEngine.UI.Image>();
            targetCardButton.onClick.AddListener(delegate { browsingTargets = true; browserPage = 0; PopulateBrowser(); });
            inputIcon = CreateImage(inputCard, "InputIcon", new Vector2(0f, 54f), new Vector2(92f, 92f), Color.white);
            targetIcon = CreateImage(targetCard, "TargetIcon", new Vector2(0f, 54f), new Vector2(92f, 92f), Color.white);
            inputTitle = CreateText(inputCard, "ВЛОЖЕНИЕ", 18, FontStyle.Bold, new Vector2(0f, -36f), new Vector2(310f, 62f), Color.white, TextAnchor.MiddleCenter);
            targetTitle = CreateText(targetCard, "ЦЕЛЬ", 18, FontStyle.Bold, new Vector2(0f, -36f), new Vector2(310f, 62f), Color.white, TextAnchor.MiddleCenter);
            CreateButton(inputCard, "InputMinus", "−", new Vector2(-38f, -116f), new Vector2(62f, 38f), surface,
                delegate { inputCount = Math.Max(1, inputCount - 1); RefreshSelection(); });
            CreateButton(inputCard, "InputPlus", "+", new Vector2(38f, -116f), new Vector2(62f, 38f), surface,
                delegate { inputCount = Math.Min(MaxOwned(), inputCount + 1); RefreshSelection(); });

            wheelImage = CreateRawImage(panel, "Wheel", new Vector2(0f, 220f), new Vector2(300f, 300f));
            marker = CreatePanel(wheelImage.transform, "Marker", new Vector2(14f, 34f), Color.white);
            marker.anchorMin = marker.anchorMax = marker.pivot = new Vector2(0.5f, 0.5f);
            chanceText = CreateText(panel, "—", 31, FontStyle.Bold, new Vector2(0f, 230f), new Vector2(220f, 88f), gold, TextAnchor.MiddleCenter);
            statusText = CreateText(panel, "Выбери вложение и цель", 16, FontStyle.Normal, new Vector2(0f, 70f), new Vector2(380f, 44f), Color.white, TextAnchor.MiddleCenter);

            rollButton = CreateButton(panel, "Roll", "▲  UPGRADE", new Vector2(-285f, 7f), new Vector2(535f, 58f), new Color(0.72f, 0.43f, 0.05f), StartRoll);

            int[] outputMultipliers = { 1, 2, 4, 8 };
            for (int i = 0; i < outputMultipliers.Length; i++)
            {
                int amount = outputMultipliers[i];
                CreateButton(panel, "Output" + amount, "x" + amount, new Vector2(28f + i * 72f, 7f), new Vector2(64f, 52f), surface,
                    delegate { outputCount = amount; RefreshSelection(); });
            }
            double[] desired = { 0.30d, 0.50d, 0.70d };
            for (int i = 0; i < desired.Length; i++)
            {
                double wanted = desired[i];
                CreateButton(panel, "Chance" + i, (wanted * 100d).ToString("0") + "%", new Vector2(316f + i * 72f, 7f), new Vector2(64f, 52f), i == 2 ? new Color(0.12f, 0.42f, 0.25f) : surface,
                    delegate { SetDesiredChance(wanted); });
            }

            CreateButton(panel, "BrowseInventory", "ИНВЕНТАРЬ", new Vector2(-150f, -64f), new Vector2(260f, 42f), surface,
                delegate { browsingTargets = false; browserPage = 0; PopulateBrowser(); });
            CreateButton(panel, "BrowseTargets", "ЦЕЛИ", new Vector2(150f, -64f), new Vector2(260f, 42f), surface,
                delegate { browsingTargets = true; browserPage = 0; PopulateBrowser(); });
            browserTitle = CreateText(panel, "ИНВЕНТАРЬ", 18, FontStyle.Bold, new Vector2(0f, -104f), new Vector2(900f, 30f), accent, TextAnchor.MiddleCenter);
            browserContent = CreatePanel(panel, "IconGrid", new Vector2(940f, 245f), new Color(0.025f, 0.033f, 0.052f, 1f));
            browserContent.anchorMin = browserContent.anchorMax = browserContent.pivot = new Vector2(0.5f, 0.5f);
            browserContent.anchoredPosition = new Vector2(0f, -242f);
            CreateButton(panel, "BrowserPrevious", "◀", new Vector2(-90f, -395f), new Vector2(58f, 38f), surface,
                delegate { browserPage = Math.Max(0, browserPage - 1); PopulateBrowser(); });
            browserPageText = CreateText(panel, "1 / 1", 15, FontStyle.Bold, new Vector2(0f, -395f), new Vector2(110f, 38f), Color.white, TextAnchor.MiddleCenter);
            CreateButton(panel, "BrowserNext", "▶", new Vector2(90f, -395f), new Vector2(58f, 38f), surface,
                delegate { browserPage = Math.Min(BrowserPageCount() - 1, browserPage + 1); PopulateBrowser(); });
            RebuildWheel(0d);
            marker.gameObject.SetActive(false);
            inputIcon.enabled = false;
            targetIcon.enabled = false;
            canvasObject.SetActive(false);
        }

        private void RefreshCatalog()
        {
            inputs.Clear();
            targets.Clear();
            Inventory inventory = LocalInventory();
            Run run = Run.instance;
            if (inventory == null || run == null)
            {
                statusText.text = "Начни забег — нужен активный инвентарь";
                PopulateBrowser();
                return;
            }
            for (int i = 0; i < ItemCatalog.itemCount; i++)
            {
                ItemIndex index = (ItemIndex)i;
                ItemDef def = ItemCatalog.GetItemDef(index);
                int owned = inventory.GetItemCountEffective(index);
                if (!Allowed(def, owned > 0)) continue;
                double value = UpgradeMath.TierValue(def.tier.ToString());
                if (value <= 0d) continue;
                ItemChoice choice = new ItemChoice();
                choice.Index = index;
                choice.Def = def;
                choice.Name = Language.GetString(def.nameToken);
                if (string.IsNullOrEmpty(choice.Name)) choice.Name = def.name;
                choice.Tier = def.tier.ToString();
                choice.Value = value;
                choice.Owned = owned;
                if (choice.Owned > 0) inputs.Add(choice);
                if (!def.hidden && (run.availableItems.Contains(index) || IsHallowedTier(choice.Tier))) targets.Add(choice);
            }
            Comparison<ItemChoice> comparison = delegate(ItemChoice a, ItemChoice b)
            {
                int valueCompare = a.Value.CompareTo(b.Value);
                return valueCompare != 0 ? valueCompare : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            };
            inputs.Sort(comparison);
            targets.Sort(comparison);
            if (inputs.Count == 0) browsingTargets = true;
            if (!Contains(inputs, selectedInput)) selectedInput = inputs.Count > 0 ? inputs[0] : null;
            if (!Contains(targets, selectedTarget) || selectedTarget == selectedInput)
            {
                selectedTarget = null;
                if (selectedInput != null)
                    for (int i = 0; i < targets.Count; i++) if (targets[i].Index != selectedInput.Index && targets[i].Value > selectedInput.Value) { selectedTarget = targets[i]; break; }
                if (selectedTarget == null && targets.Count > 0) selectedTarget = targets[0];
            }
            inputCount = Math.Min(Math.Max(1, inputCount), MaxOwned());
            browserPage = Math.Max(0, Math.Min(BrowserPageCount() - 1, browserPage));
            PopulateBrowser();
            RefreshSelection();
        }

        private bool Allowed(ItemDef def, bool owned)
        {
            if (def == null || string.IsNullOrEmpty(def.name) || def.hidden && !owned) return false;
            string tier = def.tier.ToString();
            if (!allowLunar.Value && tier.IndexOf("Lunar", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (!allowVoid.Value && tier.IndexOf("Void", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return UpgradeMath.TierValue(tier) > 0d;
        }

        private static bool IsHallowedTier(string tier)
        {
            if (string.IsNullOrEmpty(tier)) return false;
            return tier.Equals("Hex", StringComparison.OrdinalIgnoreCase)
                || tier.Equals("Page", StringComparison.OrdinalIgnoreCase)
                || tier.Equals("Cursed", StringComparison.OrdinalIgnoreCase)
                || tier.Equals("FoodTier", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(List<ItemChoice> list, ItemChoice choice)
        {
            if (choice == null) return false;
            for (int i = 0; i < list.Count; i++) if (list[i].Index == choice.Index) return true;
            return false;
        }

        private void PopulateBrowser()
        {
            visibleIconBindings.Clear();
            ClearChildren(browserContent);
            List<ItemChoice> source = browsingTargets ? targets : inputs;
            browserPage = Math.Max(0, Math.Min(BrowserPageCount() - 1, browserPage));
            int first = browserPage * BrowserPageSize;
            int last = Math.Min(source.Count, first + BrowserPageSize);
            for (int i = first; i < last; i++)
            {
                int local = i - first;
                int column = local % 10;
                int row = local / 10;
                Vector2 position = new Vector2(-414f + column * 92f, 79f - row * 78f);
                CreateItemCell(browserContent, source[i], !browsingTargets, position);
            }
            if (source.Count == 0)
                CreateText(browserContent, browsingTargets ? "Нет доступных целей в текущем забеге" : "Инвентарь пуст — сначала подбери предмет",
                    20, FontStyle.Bold, Vector2.zero, new Vector2(850f, 80f), new Color(0.65f, 0.71f, 0.80f), TextAnchor.MiddleCenter);
            if (browserTitle != null) browserTitle.text = browsingTargets ? "КАТАЛОГ ЦЕЛЕЙ • НАЖМИ ИКОНКУ" : "ИНВЕНТАРЬ • НАЖМИ ИКОНКУ";
            if (browserPageText != null) browserPageText.text = (browserPage + 1) + " / " + BrowserPageCount();
        }

        private int BrowserPageCount()
        {
            int count = browsingTargets ? targets.Count : inputs.Count;
            return Math.Max(1, (count + BrowserPageSize - 1) / BrowserPageSize);
        }

        private void CreateItemCell(Transform parent, ItemChoice choice, bool input, Vector2 position)
        {
            bool selected = input ? selectedInput != null && selectedInput.Index == choice.Index : selectedTarget != null && selectedTarget.Index == choice.Index;
            UnityEngine.UI.Button button = CreateButton(parent, "Item_" + choice.Index, "", position, new Vector2(82f, 70f), selected ? new Color(0.25f, 0.40f, 0.38f, 1f) : surface,
                delegate
                {
                    if (spinning) return;
                    if (input) { selectedInput = choice; inputCount = 1; }
                    else selectedTarget = choice;
                    PopulateBrowser();
                    RefreshSelection();
                });
            UnityEngine.UI.Image icon = CreateImage(button.transform, "Icon", new Vector2(0f, 6f), new Vector2(50f, 50f), Color.white);
            icon.sprite = GetChoiceIcon(choice);
            icon.enabled = icon.sprite != null;
            visibleIconBindings.Add(new IconBinding { Image = icon, Choice = choice });
            icon.preserveAspect = true;
            string text = input ? "x" + choice.Owned : choice.Value.ToString("0.##");
            CreateText(button.transform, text, 12, FontStyle.Bold, new Vector2(0f, -25f), new Vector2(76f, 18f), selected ? accent : Color.white, TextAnchor.MiddleCenter);
        }

        private void RefreshSelection()
        {
            if (chanceText == null) return;
            if (selectedInput == null || selectedTarget == null)
            {
                chanceText.text = "—";
                statusText.text = selectedInput == null ? "Сначала подбери предмет — каталог целей уже можно смотреть" : "Выбери цель";
                if (inputIcon != null)
                {
                    inputIcon.sprite = GetChoiceIcon(selectedInput);
                    inputIcon.enabled = inputIcon.sprite != null;
                }
                if (targetIcon != null)
                {
                    targetIcon.sprite = GetChoiceIcon(selectedTarget);
                    targetIcon.enabled = targetIcon.sprite != null;
                }
                if (inputTitle != null) inputTitle.text = selectedInput == null ? "ПУСТО\n<color=#AAB7C8>Нет предмета для ставки</color>" : selectedInput.Name;
                if (targetTitle != null) targetTitle.text = selectedTarget == null ? "ЦЕЛЬ НЕ ВЫБРАНА" : selectedTarget.Name;
                if (marker != null) marker.gameObject.SetActive(false);
                RebuildWheel(0d);
                rollButton.interactable = false;
                return;
            }
            inputCount = Math.Min(Math.Max(1, inputCount), MaxOwned());
            double chance = CurrentChance();
            chanceText.text = (chance * 100d).ToString("0.0") + "%\n<color=#AAB7C8><size=16>ШАНС</size></color>";
            inputTitle.text = selectedInput.Name + "\n<color=#AAB7C8>Value: " + selectedInput.Value.ToString("0.##") + " • x" + inputCount + " / " + MaxOwned() + "</color>";
            targetTitle.text = selectedTarget.Name + "\n<color=#AAB7C8>Value: " + selectedTarget.Value.ToString("0.##") + " • x" + outputCount + "</color>";
            inputIcon.sprite = GetChoiceIcon(selectedInput);
            targetIcon.sprite = GetChoiceIcon(selectedTarget);
            inputIcon.enabled = targetIcon.enabled = true;
            marker.gameObject.SetActive(true);
            inputIcon.preserveAspect = targetIcon.preserveAspect = true;
            statusText.text = selectedInput.Value.ToString("0.##") + " × " + inputCount + "  →  " + selectedTarget.Value.ToString("0.##") + " × " + outputCount;
            rollButton.interactable = !spinning && selectedInput.Index != selectedTarget.Index && LocalInventory() != null && MaxOwned() >= inputCount;
            RebuildWheel(chance);
        }

        private double CurrentChance()
        {
            return selectedInput == null || selectedTarget == null ? 0d : UpgradeMath.Chance(selectedInput.Value, inputCount, selectedTarget.Value, outputCount);
        }

        private int MaxOwned()
        {
            if (selectedInput == null) return 1;
            Inventory inventory = LocalInventory();
            return Math.Max(1, inventory == null ? selectedInput.Owned : inventory.GetItemCountEffective(selectedInput.Index));
        }

        private Sprite GetChoiceIcon(ItemChoice choice)
        {
            if (choice == null || choice.Def == null) return null;
            return iconManager.Get(choice.Def);
        }

        private void RefreshVisibleIcons()
        {
            bool missing = !ApplyIcon(inputIcon, selectedInput) || !ApplyIcon(targetIcon, selectedTarget);
            for (int i = 0; i < visibleIconBindings.Count; i++)
            {
                IconBinding binding = visibleIconBindings[i];
                if (binding != null && !ApplyIcon(binding.Image, binding.Choice)) missing = true;
            }
            nextIconRefresh = Time.unscaledTime + (missing ? 0.25f : 2f);
        }

        private bool ApplyIcon(UnityEngine.UI.Image image, ItemChoice choice)
        {
            if (image == null) return choice == null;
            Sprite sprite = GetChoiceIcon(choice);
            if (sprite != null) image.sprite = sprite;
            image.enabled = image.sprite != null && choice != null;
            return choice == null || image.sprite != null;
        }

        private void SetDesiredChance(double desired)
        {
            if (selectedInput == null || selectedTarget == null || spinning) return;
            int required = UpgradeMath.RequiredInput(desired, selectedInput.Value, selectedTarget.Value, outputCount);
            inputCount = Math.Min(MaxOwned(), required);
            RefreshSelection();
        }

        private void StartRoll()
        {
            if (spinning || selectedInput == null || selectedTarget == null) return;
            if (!NetworkServer.active)
            {
                statusText.text = "Только соло или хост: бросок должен выполнить сервер";
                return;
            }
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            CharacterMaster master = user == null ? null : user.cachedMaster;
            Inventory inventory = master == null ? null : master.inventory;
            if (inventory == null || inventory.GetItemCountEffective(selectedInput.Index) < inputCount)
            {
                statusText.text = "Предметов уже недостаточно — список обновлён";
                RefreshCatalog();
                return;
            }
            double chance = CurrentChance();
            float roll = UnityEngine.Random.value;
            pendingSuccess = roll < chance;
            pendingMaster = master;
            pendingTarget = selectedTarget.Index;
            pendingOutputCount = outputCount;
            inventory.RemoveItemPermanent(selectedInput.Index, inputCount);
            spinning = true;
            spinStart = Time.unscaledTime;
            spinDuration = Mathf.Clamp(spinSeconds.Value, 0.5f, 6f);
            spinEndTurns = 5f + roll;
            rollButton.interactable = false;
            statusText.text = "КРУТИМ... вложение уже принято";
        }

        private void UpdateSpin()
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - spinStart) / spinDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            PositionMarker(spinEndTurns * 360f * eased);
            if (progress < 1f) return;
            spinning = false;
            if (pendingSuccess && pendingMaster != null && pendingMaster.inventory != null)
            {
                pendingMaster.inventory.GiveItemPermanent(pendingTarget, pendingOutputCount);
                ItemDef def = ItemCatalog.GetItemDef(pendingTarget);
                statusText.text = "УСПЕХ! Получено: " + pendingOutputCount + " × " + (def == null ? pendingTarget.ToString() : Language.GetString(def.nameToken));
            }
            else statusText.text = "ПРОМАХ — вложенные предметы сгорели";
            pendingMaster = null;
            Invoke("RefreshAfterRoll", 0.75f);
        }

        private void RefreshAfterRoll()
        {
            if (open) RefreshCatalog();
        }

        private Inventory LocalInventory()
        {
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            CharacterMaster master = user == null ? null : user.cachedMaster;
            return master == null ? null : master.inventory;
        }

        private void RebuildWheel(double chance)
        {
            if (wheelTexture != null) Destroy(wheelTexture);
            const int size = 256;
            wheelTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            wheelTexture.wrapMode = TextureWrapMode.Clamp;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color centerFill = new Color(0.025f, 0.035f, 0.06f, 1f);
            Color ring = new Color(0.16f, 0.20f, 0.29f, 1f);
            Color success = gold;
            Vector2 center = new Vector2(127.5f, 127.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float radius = delta.magnitude;
                if (radius < 82f) { wheelTexture.SetPixel(x, y, centerFill); continue; }
                if (radius < 92f || radius > 122f) { wheelTexture.SetPixel(x, y, clear); continue; }
                double angle = Math.Atan2(delta.x, delta.y) / (Math.PI * 2d);
                if (angle < 0d) angle += 1d;
                wheelTexture.SetPixel(x, y, angle <= chance ? success : ring);
            }
            wheelTexture.Apply(false, false);
            wheelImage.texture = wheelTexture;
            PositionMarker(0f);
        }

        private void PositionMarker(float degrees)
        {
            if (marker == null) return;
            float radians = degrees * Mathf.Deg2Rad;
            marker.anchoredPosition = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * 137f;
            marker.localRotation = Quaternion.Euler(0f, 0f, -degrees);
        }

        private void MaintainProtection()
        {
            if (protectWhileOpen == null || !protectWhileOpen.Value || !NetworkServer.active)
            {
                RemoveProtection();
                return;
            }
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            CharacterBody body = user == null ? null : user.cachedBody;
            if (body == protectedBody)
            {
                if (body != null && RoR2Content.Buffs.HiddenInvincibility != null
                    && !body.HasBuff(RoR2Content.Buffs.HiddenInvincibility))
                {
                    body.AddBuff(RoR2Content.Buffs.HiddenInvincibility);
                    protectionAdded = true;
                }
                return;
            }
            RemoveProtection();
            protectedBody = body;
            if (protectedBody != null && RoR2Content.Buffs.HiddenInvincibility != null
                && !protectedBody.HasBuff(RoR2Content.Buffs.HiddenInvincibility))
            {
                protectedBody.AddBuff(RoR2Content.Buffs.HiddenInvincibility);
                protectionAdded = true;
            }
        }

        private void RemoveProtection()
        {
            if (protectionAdded && protectedBody != null && RoR2Content.Buffs.HiddenInvincibility != null
                && protectedBody.HasBuff(RoR2Content.Buffs.HiddenInvincibility))
                protectedBody.RemoveBuff(RoR2Content.Buffs.HiddenInvincibility);
            protectionAdded = false;
            protectedBody = null;
        }

        private void ApplyExitSafety(CharacterBody operatorBody)
        {
            if (!NetworkServer.active || operatorBody == null || operatorBody.healthComponent == null
                || !operatorBody.healthComponent.alive)
                return;

            float protectionSeconds = Mathf.Clamp(exitProtectionSeconds.Value, 0f, 10f);
            if (protectionSeconds > 0f && RoR2Content.Buffs.HiddenInvincibility != null)
                operatorBody.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, protectionSeconds);

            float radius = Mathf.Clamp(exitRepelRadius.Value, 0f, 50f);
            float force = Mathf.Clamp(exitRepelForce.Value, 0f, 10000f);
            if (radius <= 0f || force <= 0f || operatorBody.teamComponent == null)
                return;

            Vector3 origin = operatorBody.corePosition;
            TeamIndex operatorTeam = operatorBody.teamComponent.teamIndex;
            int pushed = 0;
            foreach (CharacterBody enemyBody in CharacterBody.readOnlyInstancesList)
            {
                if (enemyBody == null || enemyBody == operatorBody || enemyBody.healthComponent == null
                    || !enemyBody.healthComponent.alive || enemyBody.teamComponent == null
                    || enemyBody.teamComponent.teamIndex == operatorTeam || enemyBody.characterMotor == null)
                    continue;

                Vector3 offset = enemyBody.corePosition - origin;
                float distance = offset.magnitude;
                if (distance > radius)
                    continue;

                Vector3 direction = offset.sqrMagnitude > 0.01f ? offset.normalized : operatorBody.transform.forward;
                direction = (direction + Vector3.up * 0.3f).normalized;
                float falloff = Mathf.Lerp(0.35f, 1f, 1f - distance / radius);
                enemyBody.characterMotor.ApplyForce(direction * force * falloff, true, true);
                pushed++;
            }

            Logger.LogDebug("Exit safety: " + protectionSeconds.ToString("0.00") + "s protection, pushed " + pushed + " enemies.");
        }

        private void AcquireCursor()
        {
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            MPEventSystem owner = user == null ? null : user.eventSystem;
            if (cursorAcquired && owner != cursorOwner) ReleaseCursor();
            if (!cursorAcquired)
            {
                savedLock = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                cursorOwner = owner;
                if (cursorOwner != null) cursorOwner.cursorOpenerCount++;
                cursorAcquired = true;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ReleaseCursor()
        {
            if (!cursorAcquired) return;
            if (cursorOwner != null) cursorOwner.cursorOpenerCount = Mathf.Max(0, cursorOwner.cursorOpenerCount - 1);
            Cursor.lockState = savedLock;
            Cursor.visible = savedCursorVisible;
            cursorOwner = null;
            cursorAcquired = false;
        }

        private void OnDestroy()
        {
            RemoveProtection();
            ReleaseCursor();
            iconManager.Dispose();
            if (wheelTexture != null) Destroy(wheelTexture);
            if (honeycombTexture != null) Destroy(honeycombTexture);
            if (canvasObject != null) Destroy(canvasObject);
        }

        private RectTransform CreateCard(Transform parent, string name, Vector2 position, Color borderColor)
        {
            RectTransform card = CreatePanel(parent, name, new Vector2(340f, 300f), surface);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = position;
            UnityEngine.UI.Outline outline = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(borderColor.r, borderColor.g, borderColor.b, 0.75f);
            outline.effectDistance = new Vector2(2f, -2f);
            UnityEngine.UI.RawImage pattern = CreateRawImage(card, "Honeycomb", Vector2.zero, new Vector2(330f, 290f));
            pattern.texture = honeycombTexture;
            pattern.color = new Color(gold.r, gold.g, gold.b, 0.25f);
            return card;
        }

        private Texture2D BuildHoneycombTexture()
        {
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            Color32[] pixels = new Color32[size * size];
            texture.SetPixels32(pixels);
            Color color = new Color(0.72f, 0.55f, 0.22f, 0.7f);
            const int radius = 29;
            const int xStep = 50;
            const int yStep = 44;
            for (int row = -1; row < 7; row++)
            for (int column = -1; column < 7; column++)
            {
                int cx = column * xStep + (row % 2 == 0 ? 0 : xStep / 2);
                int cy = row * yStep;
                Vector2[] points = new Vector2[6];
                for (int i = 0; i < 6; i++)
                {
                    float angle = (60f * i + 30f) * Mathf.Deg2Rad;
                    points[i] = new Vector2(cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius);
                }
                for (int i = 0; i < 6; i++) DrawTextureLine(texture, points[i], points[(i + 1) % 6], color);
            }
            texture.Apply(false, false);
            return texture;
        }

        private static void DrawTextureLine(Texture2D texture, Vector2 from, Vector2 to, Color color)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to)));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t));
                if (x >= 0 && x < texture.width && y >= 0 && y < texture.height) texture.SetPixel(x, y, color);
            }
        }

        private RectTransform CreatePanel(Transform parent, string name, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            go.GetComponent<UnityEngine.UI.Image>().color = color;
            return rect;
        }

        private UnityEngine.UI.Text CreateText(Transform parent, string value, int fontSize, FontStyle style, Vector2 position, Vector2 size, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            UnityEngine.UI.Text text = go.GetComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private UnityEngine.UI.Image CreateImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private UnityEngine.UI.RawImage CreateRawImage(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            UnityEngine.UI.RawImage image = go.GetComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private UnityEngine.UI.Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction click)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            UnityEngine.UI.Button button = go.GetComponent<UnityEngine.UI.Button>();
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.55f);
            button.colors = colors;
            button.onClick.AddListener(click);
            UnityEngine.UI.LayoutElement layout = go.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredHeight = size.y;
            layout.minHeight = size.y;
            if (!string.IsNullOrEmpty(label)) CreateText(go.transform, label, 18, FontStyle.Bold, Vector2.zero, size, Color.white, TextAnchor.MiddleCenter);
            return button;
        }

        private RectTransform CreateScroll(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform root = CreatePanel(parent, name, size, new Color(0.025f, 0.033f, 0.052f, 1f));
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = position;
            UnityEngine.UI.ScrollRect scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            RectTransform viewport = CreatePanel(root, "Viewport", size - new Vector2(8f, 8f), new Color(0f, 0f, 0f, 0.01f));
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(4f, 4f);
            viewport.offsetMax = new Vector2(-4f, -4f);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            UnityEngine.UI.VerticalLayoutGroup layout = contentObject.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            UnityEngine.UI.ContentSizeFitter fitter = contentObject.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }
    }
}
