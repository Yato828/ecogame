using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Wattshift.Presentation;

namespace Wattshift
{
    /// <summary>City-first presentation: investigate, act, observe and review the shift.</summary>
    public sealed class GameplayHud : MonoBehaviour
    {
        public static GameplayHud Instance { get; private set; }
        public VisualElement Root => root;
        public VisualElement UpgradeRoot => upgrades;
        public bool IsSettingsOpen => settingsOpen;
        public Button CompleteButton { get; private set; }
        public Button SaveButton { get; private set; }
        public Button LoadButton { get; private set; }
        public Button ResetButton { get; private set; }

        GameSession session;
        CityOverlay overlay;
        GameObject documentObject;
        PanelSettings panel;
        VisualElement root, safeRoot, card, detail, toolbar, modal, modalCard, missionList, resultsBody;
        VisualElement contextToolbar;
        VisualElement settingsOverlay, settingsCard;
        ScrollView settingsScroll;
        ScrollView upgrades;
        Label title, clock, totals, goal, message, eventLabel, controlHint, buildingReadout, deviceReadout, modalTitle, modalText, resultStars;
        Button scanner, pause, detailToggle, contextToggle, inspect;
        Button mute;
        Button[] qualityButtons;
        Slider volume;
        Label menuError, volumeText;
        bool settingsOpen;
        bool expanded = true, contextExpanded = true, previousPortrait;
        int width, height;
        Rect safeArea;
        string offersKey = "", menuKey = "";
        GamePhase lastPhase = (GamePhase)(-1);
        readonly CultureInfo numberFormat = CultureInfo.GetCultureInfo("ru-RU");
        static readonly Color Ink = new Color(.075f, .16f, .17f);
        static readonly Color Muted = new Color(.35f, .43f, .43f);
        static readonly Color Accent = new Color(.16f, .40f, .32f);

        void Start()
        {
            session = GameSession.Instance; overlay = CityOverlay.Instance;
            if (session == null || overlay == null) return;
            Instance = this;
            Build();
            overlay.SetGameplayMode(true);
            overlay.SelectionChanged += SelectionChanged;
            session.Changed += Refresh;
            Refresh();
        }

        void Build()
        {
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "WATTSHIFT gameplay";
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.sortingOrder = 21;
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("WattshiftRuntimeTheme");
            documentObject = new GameObject("WATTSHIFT mission interface");
            var document = documentObject.AddComponent<UIDocument>(); document.panelSettings = panel;
            root = document.rootVisualElement; root.pickingMode = PickingMode.Ignore;
            Fill(root);
            root.style.unityFontDefinition = FontDefinition.FromFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            safeRoot = new VisualElement { pickingMode = PickingMode.Ignore }; Fill(safeRoot); root.Add(safeRoot);

            card = Card("mission-card"); card.style.position = Position.Absolute; safeRoot.Add(card);
            var heading = Row(); card.Add(heading);
            title = Text("", 17, Ink, true); title.name = "mission-title"; title.style.flexGrow = 1; heading.Add(title);
            detailToggle = Button("−", () => { expanded = !expanded; UpdateLayout(); }, "mission-details");
            detailToggle.style.flexGrow = 0; detailToggle.style.flexShrink = 0;
            detailToggle.style.width = 44; heading.Add(detailToggle);
            clock = Text("", 20, Ink, true); clock.name = "game-clock"; clock.style.marginTop = 6; card.Add(clock);
            toolbar = Row(); toolbar.style.marginTop = 8; card.Add(toolbar);
            scanner = Button("Сканер", () => session.SetScanner(!session.ScannerEnabled), "scanner-toggle"); toolbar.Add(scanner);
            pause = Button("Пауза", () => session.TogglePause(), "pause-toggle"); toolbar.Add(pause);
            eventLabel = Text("", 12, Accent, true); eventLabel.name = "city-event"; eventLabel.style.marginTop = 8; card.Add(eventLabel);
            detail = new VisualElement(); card.Add(detail);
            totals = Text("", 13, Ink, true); totals.name = "energy-totals"; totals.style.marginTop = 10; detail.Add(totals);
            goal = Text("", 11, Muted); goal.name = "mission-goal"; goal.style.marginTop = 8; detail.Add(goal);
            var actions = Row(); actions.style.flexWrap = Wrap.Wrap; actions.style.marginTop = 10; detail.Add(actions);
            CompleteButton = Button("Итоги смены", () => session.CompleteMission(), "complete-mission"); actions.Add(CompleteButton);
            SaveButton = Button("Сохранить", () => session.Save(), "save-mission"); actions.Add(SaveButton);
            LoadButton = Button("Загрузить", () => session.Load(), "load-mission"); actions.Add(LoadButton);
            ResetButton = Button("Заново", () => session.ResetMission(), "reset-mission"); actions.Add(ResetButton);
            actions.Add(Button("В меню", () => session.ReturnToMenu(), "return-menu"));
            actions.Add(Button("Настройки", OpenSettings, "settings-playing"));
            foreach (VisualElement child in actions.Children()) child.style.flexBasis = Length.Percent(45);
            message = Text("", 11, Accent); message.name = "action-message"; message.style.marginTop = 8; card.Add(message);
            controlHint = Text("Перетаскивание — обзор · колесо / + − — масштаб\nWASD / стрелки — движение · Home — центр · Tab — объекты\nТелефон: один палец — обзор, два — масштаб", 10, Muted);
            controlHint.style.marginTop = 9; detail.Add(controlHint);

            contextToolbar = Row(); contextToolbar.style.marginTop = 7; overlay.ContextRoot.Add(contextToolbar);
            contextToggle = Button("Свернуть", () => { contextExpanded = !contextExpanded; UpdateLayout(); }, "context-details");
            contextToolbar.Add(contextToggle);
            upgrades = new ScrollView(ScrollViewMode.Vertical) { name = "building-upgrades" };
            upgrades.style.flexShrink = 1; upgrades.style.marginTop = 8;
            upgrades.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            upgrades.verticalScroller.style.width = 8;
            VisualElement thumb = upgrades.verticalScroller.Q("unity-dragger");
            if (thumb != null) thumb.style.backgroundColor = new Color(Accent.r, Accent.g, Accent.b, .5f);
            overlay.ContextRoot.Add(upgrades);

            modal = new VisualElement { name = "game-modal" }; Fill(modal);
            modal.style.backgroundColor = new Color(.035f, .08f, .075f, .28f);
            modal.style.alignItems = Align.Center; modal.style.justifyContent = Justify.Center;
            safeRoot.Add(modal);
            modalCard = Card("menu-card"); modalCard.style.width = 510; modalCard.style.maxWidth = Length.Percent(92);
            modalCard.style.maxHeight = Length.Percent(92); modal.Add(modalCard);
            modalTitle = Text("WATTSHIFT", 29, Ink, true); modalCard.Add(modalTitle);
            modalText = Text("", 14, Muted); modalText.style.marginTop = 9; modalCard.Add(modalText);
            resultStars = Text("", 31, Accent, true); resultStars.name = "result-stars"; resultStars.style.marginTop = 14; modalCard.Add(resultStars);
            missionList = new ScrollView(ScrollViewMode.Vertical) { name = "mission-list" }; missionList.style.maxHeight = 365; missionList.style.marginTop = 17; modalCard.Add(missionList);
            resultsBody = new VisualElement(); resultsBody.style.marginTop = 15; modalCard.Add(resultsBody);
            menuError = Text("", 12, new Color(.66f, .20f, .15f), true); menuError.name = "menu-error";
            menuError.style.marginTop = 10; modalCard.Add(menuError);
            modalCard.Add(Button("Настройки", OpenSettings, "settings-menu"));
            BuildSettings();
            UpdateLayout();
        }

        void BuildSettings()
        {
            settingsOverlay = new VisualElement { name = "settings-overlay" }; Fill(settingsOverlay);
            settingsOverlay.style.backgroundColor = new Color(.035f, .08f, .075f, .65f);
            settingsOverlay.style.alignItems = Align.Center; settingsOverlay.style.justifyContent = Justify.Center;
            settingsOverlay.style.display = DisplayStyle.None; safeRoot.Add(settingsOverlay);
            settingsCard = Card("settings-card"); settingsCard.style.maxWidth = Length.Percent(92);
            settingsCard.style.maxHeight = Length.Percent(92); settingsOverlay.Add(settingsCard);
            settingsScroll = new ScrollView(ScrollViewMode.Vertical); settingsCard.Add(settingsScroll);
            settingsScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            settingsScroll.Add(Text("Настройки", 25, Ink, true));
            var pauseHint = Text("Во время настройки смена стоит на паузе. Закройте панель и нажмите «Продолжить», когда будете готовы.", 12, Muted);
            pauseHint.style.marginTop = 10; settingsScroll.Add(pauseHint);
            var audioTitle = Text("Звук", 16, Ink, true); audioTitle.style.marginTop = 22; settingsScroll.Add(audioTitle);
            volumeText = Text("Громкость", 12, Muted); volumeText.style.marginTop = 10; settingsScroll.Add(volumeText);
            volume = new Slider(0, 1) { name = "audio-volume" }; volume.style.marginTop = 8;
            volume.RegisterValueChangedCallback(change =>
            {
                if (CampaignAudio.Instance != null) CampaignAudio.Instance.Volume = change.newValue;
                RefreshSettings();
            });
            settingsScroll.Add(volume);
            mute = Button("Выключить звук", () =>
            {
                if (CampaignAudio.Instance != null) CampaignAudio.Instance.Muted = !CampaignAudio.Instance.Muted;
                RefreshSettings();
            }, "audio-mute"); mute.style.marginTop = 10; settingsScroll.Add(mute);
            var qualityTitle = Text("Качество изображения", 16, Ink, true); qualityTitle.style.marginTop = 22; settingsScroll.Add(qualityTitle);
            var qualityHint = Text("Если движение теряет плавность, выберите более низкое качество. Настройка сохраняется на этом устройстве.", 12, Muted);
            qualityHint.style.marginTop = 8; qualityHint.style.marginBottom = 12; settingsScroll.Add(qualityHint);
            qualityButtons = new Button[3]; string[] tiers = { "Низкое", "Среднее", "Высокое" };
            for (int i = 0; i < qualityButtons.Length; i++)
            {
                int tier = i;
                qualityButtons[i] = Button(tiers[i], () =>
                {
                    if (QualityController.Instance != null) QualityController.Instance.Tier = tier;
                    RefreshSettings();
                }, "quality-" + i);
                settingsScroll.Add(qualityButtons[i]);
            }
            var close = Button("Готово", CloseSettings, "settings-close");
            close.style.marginTop = 20; settingsScroll.Add(close);
        }

        void OpenSettings()
        {
            if (session.Phase == GamePhase.Playing && !session.Paused) session.TogglePause();
            settingsOpen = true; settingsOverlay.style.display = DisplayStyle.Flex;
            card.SetEnabled(false); modal.SetEnabled(false); overlay.ContextRoot.SetEnabled(false);
            RefreshSettings(); UpdateLayout();
            volume.Focus();
        }

        void CloseSettings()
        {
            settingsOpen = false; settingsOverlay.style.display = DisplayStyle.None;
            card.SetEnabled(true); modal.SetEnabled(true); overlay.ContextRoot.SetEnabled(true);
            root.Q<Button>(session.Phase == GamePhase.Playing ? "settings-playing" : "settings-menu")?.Focus();
        }

        void RefreshSettings()
        {
            var audio = CampaignAudio.Instance;
            if (audio != null)
            {
                volume.SetValueWithoutNotify(audio.Volume);
                volumeText.text = "Громкость · " + Number(audio.Volume * 100, 0) + "%";
                mute.text = audio.Muted ? "Включить звук" : "Выключить звук";
            }
            int activeTier = QualityController.Instance != null ? QualityController.Instance.Tier : -1;
            string[] tiers = { "Низкое", "Среднее", "Высокое" };
            for (int i = 0; i < qualityButtons.Length; i++)
            {
                qualityButtons[i].text = (i == activeTier ? "✓ " : "") + tiers[i];
                qualityButtons[i].style.backgroundColor = i == activeTier ? Accent : new Color(.83f, .88f, .84f);
                qualityButtons[i].style.color = i == activeTier ? new Color(.98f, .98f, .94f) : Ink;
            }
        }

        static void SetButtonEnabled(Button button, bool enabled)
        {
            button.SetEnabled(enabled);
            button.style.opacity = 1;
            button.style.backgroundColor = enabled ? Accent : new Color(.79f, .83f, .79f);
            button.style.color = enabled ? new Color(.98f, .98f, .94f) : new Color(.30f, .37f, .33f);
        }

        static void Fill(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = element.style.right = element.style.top = element.style.bottom = 0;
        }
        static VisualElement Row()
        {
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; return row;
        }
        static VisualElement Card(string name)
        {
            var element = new VisualElement { name = name };
            element.style.backgroundColor = new Color(.97f, .97f, .93f, .98f);
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius = 15;
            element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = 15;
            element.style.paddingLeft = element.style.paddingRight = 18;
            element.style.paddingTop = element.style.paddingBottom = 15;
            return element;
        }
        static Label Text(string text, int size, Color color, bool bold = false)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.color = color; label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            return label;
        }
        static Button Button(string text, Action clicked, string name)
        {
            var button = new Button(clicked) { text = text, name = name };
            button.style.backgroundColor = Accent; button.style.color = new Color(.98f, .98f, .94f);
            button.style.borderTopWidth = button.style.borderRightWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = 0;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 8;
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 8;
            button.style.minHeight = 44; button.style.fontSize = 12; button.style.whiteSpace = WhiteSpace.Normal;
            button.style.paddingLeft = button.style.paddingRight = 10;
            button.style.marginLeft = 0; button.style.marginRight = button.style.marginBottom = 5;
            button.style.flexGrow = 1; button.style.flexShrink = 1;
            return button;
        }

        void SelectionChanged(CityBuilding building)
        {
            contextExpanded = true;
            offersKey = "";
            RefreshInspector(); UpdateLayout();
        }

        void Refresh()
        {
            if (root == null || session == null) return;
            GamePhase phase = session.Phase;
            bool changedPhase = phase != lastPhase;
            if (changedPhase)
            {
                lastPhase = phase;
                if (phase != GamePhase.Playing) overlay.Select(null);
                offersKey = ""; menuKey = "";
            }
            bool playing = phase == GamePhase.Playing;
            card.style.display = playing ? DisplayStyle.Flex : DisplayStyle.None;
            modal.style.display = playing ? DisplayStyle.None : DisplayStyle.Flex;
            if (!playing) { RefreshModal(); UpdateLayout(); return; }
            if (!session.Ready || session.Snapshot == null) return;
            var snapshot = session.Snapshot;
            title.text = session.Mission.Title;
            clock.text = Hour(session.GameHour) + (session.Paused ? "  ·  ПАУЗА" : "  ·  СМЕНА");
            pause.text = session.Paused ? "Продолжить" : "Пауза";
            scanner.text = session.ScannerEnabled ? "Сканер: вкл." : "Сканер";
            scanner.style.backgroundColor = session.ScannerEnabled ? new Color(.67f, .36f, .13f) : Accent;
            eventLabel.text = session.CurrentEvent ?? "";
            eventLabel.tooltip = eventLabel.text;
            eventLabel.style.display = string.IsNullOrWhiteSpace(eventLabel.text) ? DisplayStyle.None : DisplayStyle.Flex;
            totals.text = Number(snapshot.CurrentPowerKW, 1) + " кВт сейчас\n"
                + Number(snapshot.AccumulatedEnergyKWh, 1) + " кВт·ч за смену · экономия " + Number(snapshot.ObservedSavingPercent, 1) + "%\n"
                + "Бюджет " + snapshot.Budget + " уч. ед. · комфорт " + Number(snapshot.Comfort, 0) + "%";
            goal.text = session.Mission.Description + "\nЦель: ≥ " + Number(session.Mission.TargetSavingPercent, 0)
                + "% экономии при безопасной работе. Наблюдайте город до " + Hour(session.Mission.EndHour) + ".";
            goal.tooltip = session.Mission.Assumptions;
            message.text = session.LastMessage ?? "";
            message.style.color = session.LastActionFailed ? new Color(.66f, .20f, .15f) : Accent;
            SetButtonEnabled(CompleteButton, true);
            RefreshInspector(); UpdateLayout();
        }

        void RefreshInspector()
        {
            if (upgrades == null || session == null || !session.Ready) return;
            CityBuilding selected = overlay.SelectedBuilding;
            if (selected == null) { upgrades.Clear(); offersKey = ""; return; }
            BuildingDefinition definition = session.Simulation.GetBuildingDefinition(selected.SimulationId);
            if (definition == null)
            {
                if (offersKey != selected.SimulationId) { upgrades.Clear(); upgrades.Add(Text("У этого объекта нет активной задачи в текущей смене.", 12, Muted)); offersKey = selected.SimulationId; }
                return;
            }
            BuildingMetrics metrics = Array.Find(session.Snapshot.Buildings, b => b.BuildingId == selected.SimulationId);
            bool investigated = session.IsInvestigated(selected.SimulationId);
            string key = selected.SimulationId + ":" + investigated + ":" + session.ScannerEnabled + ":" + session.Phase;
            foreach (var offer in definition.Upgrades ?? Array.Empty<UpgradeDefinition>())
                key += ":" + offer.Id + session.Simulation.IsPurchased(selected.SimulationId, offer.Id) + (session.GameHour >= offer.RequiredAfterHour);
            if (offersKey == key)
            {
                if (buildingReadout != null && metrics != null) buildingReadout.text = BuildingNumbers(metrics);
                if (deviceReadout != null && metrics != null) deviceReadout.text = DeviceNumbers(metrics);
                return;
            }
            offersKey = key; upgrades.Clear(); buildingReadout = null; deviceReadout = null;
            if (!investigated)
            {
                upgrades.Add(Text(session.ScannerEnabled ? "Сканер отмечает нагрузку. Обследуйте объект, чтобы выяснить причины." : "Включите сканер и обследуйте объект: сначала информация, затем решение.", 12, Ink));
                inspect = Button("Обследовать объект", () => session.InspectBuilding(selected.SimulationId), "inspect-building");
                inspect.style.marginTop = 10; SetButtonEnabled(inspect, session.ScannerEnabled); upgrades.Add(inspect);
                return;
            }
            if (metrics != null)
            {
                buildingReadout = Text(BuildingNumbers(metrics), 12, Ink, true); upgrades.Add(buildingReadout);
                buildingReadout.style.marginBottom = 10;
            }
            if (!string.IsNullOrWhiteSpace(definition.Description))
            {
                var description = Text(definition.Description, 11, Muted);
                description.style.marginBottom = 10; upgrades.Add(description);
            }
            if (metrics != null)
            {
                deviceReadout = Text(DeviceNumbers(metrics), 11, Ink);
                deviceReadout.name = "building-devices";
                upgrades.Add(deviceReadout);
            }
            foreach (var offer in definition.Upgrades ?? Array.Empty<UpgradeDefinition>())
            {
                string buildingId = selected.SimulationId, offerId = offer.Id;
                bool installed = session.Simulation.IsPurchased(buildingId, offerId);
                bool alternativeInstalled = !string.IsNullOrEmpty(offer.ExclusiveGroup) && definition.Upgrades.Any(other => other.Id != offer.Id && other.ExclusiveGroup == offer.ExclusiveGroup && session.Simulation.IsPurchased(buildingId, other.Id));
                bool availableTime = offer.RequiredAfterHour < 0 || session.GameHour >= offer.RequiredAfterHour;
                var button = Button(installed ? "✓ " + offer.Title : offer.Title + " · " + offer.Cost + " уч. ед.", () => session.Purchase(buildingId, offerId), "upgrade-" + offerId);
                button.style.marginTop = 10; button.style.marginRight = 0;
                SetButtonEnabled(button, !installed && !alternativeInstalled && availableTime && session.Phase == GamePhase.Playing);
                button.tooltip = offer.Description;
                upgrades.Add(button);
                string note = installed ? "Установлено. Наблюдайте за работой объекта." : alternativeInstalled ? "Уже выбран другой вариант для этой системы." : !availableTime ? "Доступно с " + Hour(offer.RequiredAfterHour) + "." : offer.Description;
                upgrades.Add(Text(note ?? "", 10, Muted));
            }
        }

        string BuildingNumbers(BuildingMetrics metrics) => Number(metrics.CurrentPowerKW, 1) + " кВт сейчас · комфорт " + Number(metrics.Comfort, 0) + "%";

        string DeviceNumbers(BuildingMetrics metrics)
        {
            var devices = metrics.Devices ?? Array.Empty<DeviceMetrics>();
            var lines = new System.Collections.Generic.List<string>();
            foreach (LoadPriority priority in new[] { LoadPriority.Critical, LoadPriority.Important, LoadPriority.NonCritical })
            {
                var group = devices.Where(device => (device.Essential ? LoadPriority.Critical : device.Priority) == priority).ToArray();
                if (group.Length == 0) continue;
                if (lines.Count > 0) lines.Add("");
                lines.Add(priority == LoadPriority.Critical ? "Критические" : priority == LoadPriority.Important ? "Рабочие" : "Некритичные");
                foreach (var device in group)
                {
                    string deviceTitle = string.IsNullOrWhiteSpace(device.Title) ? "Оборудование" : device.Title;
                    lines.Add(deviceTitle + " · " + Number(device.CurrentPowerKW, 1) + " кВт · работа " + Number(device.ServiceLevel * 100, 0) + "%");
                }
            }
            return string.Join("\n", lines);
        }

        void RefreshModal()
        {
            menuError.text = session.LastActionFailed ? session.LastMessage ?? "" : "";
            menuError.style.display = string.IsNullOrWhiteSpace(menuError.text) ? DisplayStyle.None : DisplayStyle.Flex;
            if (session.Phase == GamePhase.Results)
            {
                modalTitle.text = session.Stars > 0 ? "Смена завершена" : "Нужен другой подход";
                modalText.text = session.LastMessage ?? "";
                resultStars.style.display = DisplayStyle.Flex;
                resultStars.text = session.Stars > 0 ? new string('★', session.Stars) + new string('☆', Mathf.Max(0, 3 - session.Stars)) : "Попробуйте ещё раз";
                missionList.style.display = DisplayStyle.None;
                string key = "results:" + session.MissionIndex + ":" + session.Stars + ":" + session.Score;
                if (menuKey == key) return;
                menuKey = key; resultsBody.Clear();
                if (session.Snapshot != null)
                    resultsBody.Add(Text("За смену: " + Number(session.Snapshot.AccumulatedEnergyKWh, 1) + " кВт·ч\nЭкономия: " + Number(session.Snapshot.ObservedSavingPercent, 1) + "% · комфорт " + Number(session.Snapshot.Comfort, 0) + "%\nОценка: " + session.Score + " · баллы кампании: " + session.CampaignCredits, 14, Ink));
                resultsBody.Add(Button("Переиграть смену", () => session.ResetMission(), "reset-mission-result"));
                resultsBody.Add(Button("Выбрать миссию", () => session.ReturnToMenu(), "return-menu-result"));
                return;
            }
            modalTitle.text = "Исследуйте. Решайте. Наблюдайте.";
            modalText.text = "Город расходует энергию по своим расписаниям. Найдите лишнюю нагрузку и сохраните важные службы.\nБаллы кампании: " + session.CampaignCredits;
            resultStars.style.display = DisplayStyle.None;
            missionList.style.display = DisplayStyle.Flex;
            resultsBody.Clear();
            string signature = "menu:" + (session.Missions?.Length ?? 0) + ":" + session.CampaignCredits;
            for (int i = 0; i < (session.Missions?.Length ?? 0); i++) signature += ":" + session.GetBestStars(i) + session.IsMissionUnlocked(i);
            if (signature == menuKey) return;
            menuKey = signature; missionList.Clear();
            for (int i = 0; i < (session.Missions?.Length ?? 0); i++)
            {
                int index = i; MissionDefinition definition = session.Missions[i];
                if (definition == null) continue;
                int stars = session.GetBestStars(i); bool unlocked = session.IsMissionUnlocked(i);
                var button = Button((i + 1) + ". " + definition.Title + (stars > 0 ? "  " + new string('★', stars) : unlocked ? "" : "  · закрыто"), () => session.BeginMission(index), "start-mission-" + i);
                SetButtonEnabled(button, unlocked); missionList.Add(button);
                Label hint = Text(unlocked ? definition.Description : "Сначала завершите предыдущую смену.", 12, Muted);
                hint.style.marginBottom = 14; missionList.Add(hint);
            }
            missionList.Add(Button("Загрузить сохранение", () => session.Load(), "load-campaign"));
        }

        string Number(double value, int places) => value.ToString("F" + places, numberFormat);
        static string Hour(double hour)
        {
            int minutes = (int)Math.Floor(hour * 60 + .00001);
            return ((minutes / 60) % 24).ToString("00") + ":" + (minutes % 60).ToString("00");
        }
        void Update()
        {
            if (root != null && (width != Screen.width || height != Screen.height || safeArea != Screen.safeArea)) UpdateLayout();
        }
        void UpdateLayout()
        {
            if (panel == null) return;
            width = Screen.width; height = Screen.height; safeArea = Screen.safeArea;
            bool portrait = width < height;
            if (portrait != previousPortrait) { previousPortrait = portrait; expanded = !portrait; }
            panel.scale = portrait ? width / 600f : Mathf.Clamp(width / 1600f, .7f, 2f);
            float scale = Mathf.Max(.1f, panel.scale);
            safeRoot.style.left = safeArea.xMin / scale; safeRoot.style.right = (width - safeArea.xMax) / scale;
            safeRoot.style.top = (height - safeArea.yMax) / scale; safeRoot.style.bottom = safeArea.yMin / scale;
            card.style.top = portrait ? 84 : 138; card.style.left = 26;
            card.style.right = portrait ? new StyleLength(26) : new StyleLength(StyleKeyword.Auto);
            card.style.width = portrait ? new StyleLength(StyleKeyword.Auto) : new StyleLength(300);
            detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            detailToggle.text = expanded ? "−" : "+";
            bool compactMobile = portrait && !expanded;
            message.style.display = (compactMobile && !session.LastActionFailed) || string.IsNullOrWhiteSpace(message.text) ? DisplayStyle.None : DisplayStyle.Flex;
            controlHint.style.display = compactMobile ? DisplayStyle.None : DisplayStyle.Flex;
            eventLabel.style.display = string.IsNullOrWhiteSpace(eventLabel.text) ? DisplayStyle.None : DisplayStyle.Flex;
            eventLabel.style.whiteSpace = compactMobile ? WhiteSpace.NoWrap : WhiteSpace.Normal;
            eventLabel.style.overflow = compactMobile ? Overflow.Hidden : Overflow.Visible;
            eventLabel.style.textOverflow = compactMobile ? TextOverflow.Ellipsis : TextOverflow.Clip;
            title.style.fontSize = portrait ? 14 : 17; clock.style.fontSize = portrait ? 16 : 20;
            contextToggle.text = contextExpanded ? "Свернуть карточку" : "Развернуть карточку";
            upgrades.style.display = contextExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            upgrades.style.height = portrait ? 135 : 210;
            contextToolbar.style.display = overlay.SelectedBuilding == null ? DisplayStyle.None : DisplayStyle.Flex;
            overlay.SetContextCompact(!contextExpanded);
            modalCard.style.width = portrait ? 548 : 510;
            settingsCard.style.width = portrait ? 548 : 480;
            settingsScroll.style.maxHeight = height / scale * .78f;
            modalTitle.style.fontSize = portrait ? 21 : 29;
            missionList.style.maxHeight = Mathf.Max(150, height / scale * .5f);
            float buttonHeight = Mathf.Max(44, 44 / scale);
            volume.style.minHeight = buttonHeight;
            contextToggle.style.minHeight = buttonHeight;
            detailToggle.style.minWidth = detailToggle.style.width = buttonHeight;
            foreach (var button in root.Query<Button>().ToList()) button.style.minHeight = buttonHeight;
            if (upgrades != null) foreach (var button in upgrades.Query<Button>().ToList()) button.style.minHeight = buttonHeight;
        }
        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (root == null || root.panel == null) return false;
            if (settingsOpen) return true;
            if (session != null && session.Phase != GamePhase.Playing) return true;
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return card.worldBound.Contains(p);
        }
        void OnDestroy()
        {
            if (session != null) session.Changed -= Refresh;
            if (overlay != null)
            {
                overlay.ContextRoot?.SetEnabled(true);
                overlay.SelectionChanged -= SelectionChanged; overlay.SetGameplayMode(false);
            }
            if (Instance == this) Instance = null;
            if (documentObject != null) Destroy(documentObject);
            if (panel != null) Destroy(panel);
        }
    }
}
