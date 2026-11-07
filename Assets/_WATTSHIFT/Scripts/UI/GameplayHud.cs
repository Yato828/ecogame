using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wattshift
{
    /// <summary>Event-driven presentation of the actual service snapshot and configured upgrades.</summary>
    public sealed class GameplayHud : MonoBehaviour
    {
        public static GameplayHud Instance { get; private set; }
        public VisualElement Root => root;
        public VisualElement UpgradeRoot => upgrades;
        public Button CompleteButton { get; private set; }
        public Button SaveButton { get; private set; }
        public Button LoadButton { get; private set; }
        public Button ResetButton { get; private set; }

        GameSession session;
        CityOverlay overlay;
        UIDocument document;
        GameObject documentObject;
        PanelSettings panel;
        VisualElement root, safeRoot, card, actions;
        ScrollView upgrades;
        Label missionTitle, totals, timeWindow, goal, message, assumptions;
        int width, height;
        Rect safeArea;
        readonly Color ink = new Color(.075f, .16f, .17f);
        readonly Color muted = new Color(.35f, .43f, .43f);
        readonly Color accent = new Color(.16f, .40f, .32f);
        readonly CultureInfo numberFormat = CultureInfo.GetCultureInfo("ru-RU");

        void Start()
        {
            session = GameSession.Instance;
            overlay = CityOverlay.Instance;
            if (session == null || !session.Ready || overlay == null) return;
            Instance = this;
            Build();
            overlay.SetGameplayMode(true);
            overlay.SelectionChanged += OnSelection;
            session.Changed += Refresh;
            Refresh();
        }

        void Build()
        {
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "WATTSHIFT gameplay panel";
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.sortingOrder = 21;
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("WattshiftRuntimeTheme");
            // Keep a separate top-level document: nested UIDocuments inherit the parent's panel.
            documentObject = new GameObject("Gameplay HUD document");
            document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            root.style.unityFontDefinition = FontDefinition.FromFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            safeRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            safeRoot.style.position = Position.Absolute;
            root.Add(safeRoot);
            card = new VisualElement { name = "mission-card" };
            card.style.position = Position.Absolute;
            card.style.backgroundColor = new Color(.97f, .97f, .93f, .98f);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = 15;
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 15;
            card.style.paddingLeft = card.style.paddingRight = 18;
            card.style.paddingTop = card.style.paddingBottom = 12;
            safeRoot.Add(card);
            missionTitle = Text("", 17, ink, true); missionTitle.name = "mission-title"; card.Add(missionTitle);
            timeWindow = Text("", 10, muted); timeWindow.style.marginTop = 4; card.Add(timeWindow);
            totals = Text("", 14, ink, true); totals.name = "energy-totals"; totals.style.marginTop = 7; card.Add(totals);
            goal = Text("", 11, accent); goal.style.marginTop = 5; card.Add(goal);
            actions = new VisualElement { name = "mission-actions" };
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 9;
            CompleteButton = Action("Проверить цель", () => session.CompleteMission(), "complete-mission");
            SaveButton = Action("Сохранить", () => session.Save(), "save-mission");
            LoadButton = Action("Загрузить", () => session.Load(), "load-mission");
            ResetButton = Action("Заново", () => session.ResetMission(), "reset-mission");
            card.Add(actions);
            message = Text("", 11, accent); message.name = "action-message"; message.style.marginTop = 7; card.Add(message);
            assumptions = Text("", 9, muted); assumptions.style.marginTop = 4; card.Add(assumptions);

            upgrades = new ScrollView(ScrollViewMode.Vertical) { name = "building-upgrades" };
            upgrades.style.marginTop = 12;
            upgrades.style.flexShrink = 1;
            upgrades.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            upgrades.verticalScroller.style.width = 8;
            VisualElement thumb = upgrades.verticalScroller.Q("unity-dragger");
            if (thumb != null)
            {
                thumb.style.backgroundColor = new Color(accent.r, accent.g, accent.b, .55f);
                thumb.style.borderTopLeftRadius = thumb.style.borderTopRightRadius = 4;
                thumb.style.borderBottomLeftRadius = thumb.style.borderBottomRightRadius = 4;
            }
            overlay.ContextRoot.Add(upgrades);
            UpdateLayout();
        }

        Button Action(string text, Action clicked, string name)
        {
            var button = new Button(clicked) { text = text, name = name };
            button.style.flexGrow = 1;
            button.style.flexBasis = 0;
            button.style.marginLeft = 0;
            button.style.marginRight = 5;
            StyleButton(button);
            actions.Add(button);
            return button;
        }

        void StyleButton(Button button)
        {
            button.style.backgroundColor = accent;
            button.style.color = new Color(.98f, .98f, .94f);
            button.style.borderTopWidth = button.style.borderRightWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = 0;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 8;
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 8;
            button.style.minHeight = 38;
            button.style.fontSize = 12;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.paddingLeft = button.style.paddingRight = 8;
        }

        Label Text(string text, int size, Color color, bool bold = false)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.color = color; label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            return label;
        }

        void OnSelection(CityBuilding building) => RefreshUpgrades();

        void Refresh()
        {
            if (session == null || !session.Ready || root == null) return;
            SimulationSnapshot value = session.Snapshot;
            MissionDefinition mission = session.Mission;
            missionTitle.text = value.Completed ? "✓ " + mission.Title + " — выполнено" : mission.Title;
            timeWindow.text = "Мощность в " + Hour(value.Hour) + " · Энергия: план на " + Hour(mission.StartHour) + "–" + Hour(mission.EndHour);
            timeWindow.tooltip = mission.Assumptions ?? "";
            totals.text = TotalsText(value, Screen.width < Screen.height);
            goal.text = "Цель: ≥ " + Number(mission.TargetSavingPercent, 0) + "% экономии · комфорт ≥ " + Number(mission.MinComfort, 0) + "% · "
                + (value.SafetySatisfied ? "питание важных служб сохранено" : "нарушено питание важных служб");
            message.text = session.LastMessage ?? "";
            assumptions.text = mission.Assumptions ?? "";
            CompleteButton.text = value.Completed ? "Выполнено" : "Проверить цель";
            CompleteButton.SetEnabled(!value.Completed);
            RefreshUpgrades();
            UpdateLayout();
        }

        void RefreshUpgrades()
        {
            if (upgrades == null || session == null || !session.Ready) return;
            upgrades.Clear();
            CityBuilding selected = overlay.SelectedBuilding;
            if (selected == null) return;
            BuildingDefinition definition = session.Simulation.GetBuildingDefinition(selected.SimulationId);
            if (definition == null) { upgrades.Add(Text("Для этого объекта нет конфигурации нагрузок.", 12, muted)); return; }
            BuildingMetrics metrics = Array.Find(session.Snapshot.Buildings, b => b.BuildingId == selected.SimulationId);
            if (metrics != null)
            {
                upgrades.Add(Text(Number(metrics.CurrentPowerKW, 1) + " кВт  ·  " + Number(metrics.CurrentEnergyKWh, 1) + " кВт·ч за период", 12, ink, true));
                var baseline = Text("До изменений: " + Number(metrics.BaselineEnergyKWh, 1) + " кВт·ч · комфорт " + Number(metrics.Comfort, 0) + "%", 10, muted);
                baseline.style.marginTop = 3; baseline.style.marginBottom = 10; upgrades.Add(baseline);
            }
            if (definition.Upgrades == null || definition.Upgrades.Length == 0) { upgrades.Add(Text("В этой миссии изменения не требуются.", 12, muted)); return; }
            foreach (UpgradeDefinition upgrade in definition.Upgrades)
            {
                if (upgrade == null) continue;
                string buildingId = selected.SimulationId, upgradeId = upgrade.Id;
                bool purchased = session.Simulation.IsPurchased(buildingId, upgradeId);
                var button = new Button(() => session.Purchase(buildingId, upgradeId)) { name = "upgrade-" + upgradeId };
                button.text = purchased ? "✓ " + upgrade.Title + " · установлено" : upgrade.Title + " · " + upgrade.Cost.ToString(numberFormat) + " уч. ед.";
                button.tooltip = upgrade.Description;
                StyleButton(button); button.style.marginLeft = button.style.marginRight = 0;
                button.style.marginBottom = 4;
                button.style.minHeight = Mathf.Max(44, 44 / Mathf.Max(panel.scale, .1f));
                button.SetEnabled(!purchased && !session.Snapshot.Completed);
                upgrades.Add(button);
                var explanation = Text(upgrade.Description ?? "", 10, muted);
                explanation.style.marginBottom = 10;
                upgrades.Add(explanation);
            }
        }

        string Number(double value, int places) => value.ToString("F" + places, numberFormat);
        string TotalsText(SimulationSnapshot value, bool portrait)
        {
            string loads = Number(value.CurrentPowerKW, 1) + " кВт · " + Number(value.CurrentEnergyKWh, 1) + " кВт·ч (было " + Number(value.BaselineEnergyKWh, 1) + ")";
            string saved = "Экономия: " + Number(value.SavedEnergyKWh, 1) + " кВт·ч / " + Number(value.SavingPercent, 1) + "%";
            string resources = "Бюджет: " + value.Budget.ToString(numberFormat) + " уч. ед. · Комфорт: " + Number(value.Comfort, 0) + "%";
            return loads + "\n" + saved + (portrait ? "\n" : " · ") + resources;
        }
        static string Hour(double hour)
        {
            int minutes = (int)Math.Round(hour * 60);
            return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
        }

        void Update()
        {
            if (root != null && (width != Screen.width || height != Screen.height || safeArea != Screen.safeArea)) UpdateLayout();
        }

        void UpdateLayout()
        {
            width = Screen.width; height = Screen.height; safeArea = Screen.safeArea;
            bool portrait = width < height;
            panel.scale = portrait ? width / 600f : Mathf.Clamp(width / 1600f, .7f, 2f);
            float scale = Mathf.Max(.1f, panel.scale);
            safeRoot.style.left = safeArea.xMin / scale;
            safeRoot.style.right = (width - safeArea.xMax) / scale;
            safeRoot.style.top = (height - safeArea.yMax) / scale;
            safeRoot.style.bottom = safeArea.yMin / scale;
            card.style.top = portrait ? 84 : 145;
            card.style.left = 26;
            card.style.right = portrait ? new StyleLength(26) : new StyleLength(StyleKeyword.Auto);
            card.style.width = portrait ? new StyleLength(StyleKeyword.Auto) : new StyleLength(330);
            missionTitle.style.fontSize = portrait ? 14 : 17;
            totals.style.fontSize = portrait ? 12 : 14;
            message.style.fontSize = portrait ? 10 : 11;
            assumptions.style.display = portrait ? DisplayStyle.None : DisplayStyle.Flex;
            goal.style.display = DisplayStyle.Flex;
            goal.style.fontSize = portrait ? 10 : 11;
            if (session != null && session.Ready)
            {
                totals.text = TotalsText(session.Snapshot, portrait);
                if (portrait)
                    goal.text = "Цель ≥ " + Number(session.Mission.TargetSavingPercent, 0) + "% · комфорт ≥ " + Number(session.Mission.MinComfort, 0) + "% · " + (session.Snapshot.SafetySatisfied ? "важные службы включены" : "питание служб нарушено");
                timeWindow.text = (portrait ? "Учебная модель · " : "Мощность в ") + Hour(session.Snapshot.Hour) + " · план " + Hour(session.Mission.StartHour) + "–" + Hour(session.Mission.EndHour);
            }
            upgrades.style.height = portrait ? 142 : 210;
            float minButton = Mathf.Max(36, 44 / scale);
            actions.style.flexWrap = portrait ? Wrap.NoWrap : Wrap.Wrap;
            foreach (var child in actions.Children())
            {
                child.style.minHeight = minButton;
                child.style.flexBasis = portrait ? new StyleLength(0f) : new StyleLength(Length.Percent(45));
                child.style.minWidth = portrait ? 0 : 120;
                child.style.marginBottom = portrait ? 0 : 5;
            }
            if (portrait) timeWindow.style.fontSize = 9;
            else timeWindow.style.fontSize = 10;
        }

        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (root == null || root.panel == null || card == null) return false;
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return card.worldBound.Contains(p);
        }

        void OnDestroy()
        {
            if (session != null) session.Changed -= Refresh;
            if (overlay != null)
            {
                overlay.SelectionChanged -= OnSelection;
                overlay.SetGameplayMode(false);
            }
            if (Instance == this) Instance = null;
            if (documentObject != null) Destroy(documentObject);
            if (panel != null) Destroy(panel);
        }
    }
}
