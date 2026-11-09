using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;

namespace Wattshift
{
    public enum GamePhase { Menu, Playing, Results }

    /// <summary>Campaign/scene adapter. Time and decisions feed the existing deterministic simulation.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        public MissionDefinition Mission;
        public MissionDefinition[] Missions = Array.Empty<MissionDefinition>();
        public MissionDefinition[] LegacyMissions = Array.Empty<MissionDefinition>();
        public CityAtmosphere Atmosphere;
        public CitySimulationService Simulation { get; private set; }
        public SimulationSnapshot Snapshot { get; private set; }
        public GamePhase Phase { get; private set; } = GamePhase.Menu;
        public int MissionIndex { get; private set; }
        public bool Paused { get; private set; } = true;
        public bool ScannerEnabled { get; private set; }
        public bool Ready => Simulation != null;
        public bool Success => Snapshot != null && Snapshot.Completed;
        public double GameHour => Ready && Mission.UseTimeline ? Simulation.CurrentHour : Mission != null ? Mission.EvaluationHour : 17;
        public int Stars { get; private set; }
        public int Score { get; private set; }
        public int CampaignCredits => progress?.Credits ?? 0;
        public int TotalStars => progress?.TotalStars ?? 0;
        public MissionEvaluation Rating { get; private set; }
        public float SpeedMultiplier { get; private set; } = 1;
        bool tutorialDone;
        string tutorialKey;
        public bool TutorialActive => Ready && Mission.UseDecisionRating && MissionIndex == 0 && Phase == GamePhase.Playing && !tutorialDone;
        public string TutorialText => !IsInvestigated("school") ? "Нажми на ! над школой — там лишняя нагрузка." : Snapshot.PurchaseCount == 0 ? "Выбери безопасную меру. Одного полезного решения уже достаточно для первой звезды." : "Мера установлена. Сравни результат или рассчитай смену до конца.";
        public string TutorialAction => !IsInvestigated("school") ? "Показать проблему" : Snapshot.PurchaseCount == 0 ? "Посмотреть решения" : "Рассчитать до 23:00";
        public int GetBestScore(int index) => progress?.BestScore(index) ?? 0;
        public double GetBestSavingPercent(int index) => progress?.BestSavingPercent(index) ?? 0;
        public string LastMessage { get; private set; } = "Выберите смену. Изучайте город и проверяйте последствия решений.";
        public string CurrentEvent { get; private set; } = "";
        public bool LastActionFailed { get; private set; }
        public string[] InvestigatedBuildingIds => investigated.ToArray();
        public string[] TriggeredEventIds => firedEvents.ToArray();
        public event Action Changed;
        public event Action<string> Feedback;

        CampaignProgress progress;
        CampaignSaveService saveService;
        BuildingEnergyView[] views;
        CityBuilding[] cityBuildings;
        CityOverlay selection;
        readonly HashSet<string> investigated = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> firedEvents = new HashSet<string>(StringComparer.Ordinal);
        double pendingRealTime, autoSaveTime;
        long lastClockStamp;
        static readonly ProfilerMarker TickMarker = new ProfilerMarker("WATTSHIFT.GameClockTick");
#if UNITY_EDITOR
        public static string EditorTestSaveDirectory;
#endif

        void Awake()
        {
            Instance = this;
            if (Missions == null || Missions.Length == 0) Missions = Mission != null ? new[] { Mission } : Array.Empty<MissionDefinition>();
            if (Missions.Length == 0) { LastMessage = "Не задана конфигурация кампании."; return; }
            progress = new CampaignProgress(Missions);
            views = FindObjectsByType<BuildingEnergyView>(FindObjectsSortMode.None);
            cityBuildings = FindObjectsByType<CityBuilding>(FindObjectsSortMode.None);
            string directory = Application.persistentDataPath;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-wattshiftEvidence") >= 0)
                directory = Path.Combine(Application.temporaryCachePath, "WattshiftValidationG4");
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorTestSaveDirectory)) directory = EditorTestSaveDirectory;
#endif
            bool decisionRating = Missions[0].UseDecisionRating;
            saveService = new CampaignSaveService(Path.Combine(directory, decisionRating ? "wattshift-campaign-v4.json" : "wattshift-campaign-v3.json"));
            tutorialKey = Wattshift.Presentation.PresentationSettingsScope.Key("tutorial-done");
            tutorialDone = PlayerPrefs.GetInt(tutorialKey, 0) != 0;
            if (decisionRating && !saveService.Exists && LegacyMissions.Length == Missions.Length)
            {
                string oldPath=Path.Combine(directory,"wattshift-campaign-v3.json");
                if(File.Exists(oldPath) && !CampaignUpgradeMigration.TryMigrate(oldPath,saveService,LegacyMissions,Missions,out string migrationMessage))
                { LastActionFailed=true;LastMessage="Предыдущий прогресс не перенесён. Старый файл сохранён. "+migrationMessage; }
            }
            CreateSimulation(0);
        }

        void Start()
        {
            selection = CityOverlay.Instance;
            if (selection != null) selection.SelectionChanged += OnSelection;
            if (Atmosphere != null) Atmosphere.Bind(cityBuildings, views);
            if (saveService != null && saveService.Exists)
            {
                if (saveService.Load(out CampaignSave saved, out string message)
                    && ValidateSave(saved, out _, out CampaignProgress restored, out _)) progress = restored;
                else { LastActionFailed = true; LastMessage = "Сохранение не применено. Файл оставлен без изменений; доступна новая смена."; }
            }
            Refresh();
        }

        void CreateSimulation(int index)
        {
            if (Simulation != null) Simulation.Changed -= Refresh;
            MissionIndex = index; Mission = Missions[index];
            Simulation = new CitySimulationService(Mission); Simulation.Changed += Refresh;
            investigated.Clear(); firedEvents.Clear(); SpeedMultiplier = 1; ScannerEnabled = false; Stars = Score = 0;
            pendingRealTime = autoSaveTime = 0; CurrentEvent = ""; LastActionFailed = false;
        }

        public int GetBestStars(int index) => progress?.BestStars(index) ?? 0;
        public bool IsMissionUnlocked(int index) => progress != null && progress.IsUnlocked(index);
        public bool IsInvestigated(string id) => id != null && investigated.Contains(id);

        public void BeginMission(int index)
        {
            if (!IsMissionUnlocked(index)) { Inform("Сначала завершите предыдущую смену.", "error"); return; }
            CreateSimulation(index); Phase = GamePhase.Playing; Paused = false;
            LastMessage = "Исследуйте район. Можно поставить время на паузу и обдумать решение.";
            selection?.Select(null); FireEvents(Mission.StartHour - .00001, Mission.StartHour); Refresh(); Persist();
        }

        public void ReturnToMenu()
        {
            if (Phase != GamePhase.Menu) Persist();
            Phase = GamePhase.Menu; Paused = true; ScannerEnabled = false; selection?.Select(null); Refresh();
        }

        public void TogglePause()
        {
            if (Phase != GamePhase.Playing) return;
            if (Mission.UseTimeline && GameHour >= Mission.EndHour) { Inform("Смена закончилась. Откройте итоги.", "event"); return; }
            LastActionFailed = false; Paused = !Paused; LastMessage = Paused ? "Время остановлено: обследование и решения доступны." : "Смена продолжается. Наблюдайте за городом.";
            pendingRealTime = 0; Refresh(); Persist();
        }

        public void SetScanner(bool enabled)
        {
            if (Phase != GamePhase.Playing) return;
            LastActionFailed = false; ScannerEnabled = enabled;
            LastMessage = enabled ? (Mission.UseDecisionRating ? "Синий — обследовать; янтарный — есть нерешённые проблемы; зелёный — все проблемы объекта решены." : "Синий — обследовать; янтарный — проблема найдена; зелёный — нагрузка снижена.") : "Обычный вид города.";
            Feedback?.Invoke("scanner"); Refresh();
        }

        public void InspectBuilding(string id)
        {
            if (Phase != GamePhase.Playing || (!Mission.UseDecisionRating && !ScannerEnabled)) { Inform("Сначала включите энергетический сканер.", "error"); return; }
            if (!Mission.RequiredBuildingIds.Contains(id)) { Inform("Здесь нет проблемы текущего задания.", "event"); return; }
            if (GameHour + .000001 < Mission.InvestigationAvailableHour) { Inform("Пока нагрузка соответствует работе объекта. Дождитесь события смены.", "event"); return; }
            LastActionFailed = false; LastMessage = investigated.Add(id) ? "Обследование завершено. Сопоставьте расписание, важность нагрузок и доступные решения." : "Объект уже обследован. Проверьте последствия мер.";
            Feedback?.Invoke("inspect"); Refresh(); Persist();
        }

        public void Purchase(string buildingId, string upgradeId)
        {
            if (!Ready || Phase != GamePhase.Playing) return;
            if (Mission.UseTimeline && !IsInvestigated(buildingId)) { Inform("Сначала обследуйте объект сканером. Решение требует информации.", "error"); return; }
            ActionResult result = Simulation.TryPurchase(buildingId, upgradeId); LastMessage = result.Message; LastActionFailed = !result.Success;
            if (result.Success)
            {
                var offer = Simulation.GetBuildingDefinition(buildingId)?.Upgrades.FirstOrDefault(u => u.Id == upgradeId);
                LastMessage = "Установлено. " + (offer?.Description ?? "Наблюдайте за работой объекта.");
                foreach (var building in cityBuildings)
                    if (building.SimulationId == buildingId)
                    {
                        var highlight = building.GetComponent<SourceBuildingHighlight>();
                        if (highlight == null) highlight = building.gameObject.AddComponent<SourceBuildingHighlight>();
                        highlight.PulseSuccess();
                    }
                Feedback?.Invoke("purchase:" + buildingId); Persist();
            }
            else Feedback?.Invoke("error");
            Refresh();
        }

        public void CompleteMission()
        {
            if (!Ready || Phase != GamePhase.Playing) return;
            if (!Mission.UseDecisionRating && Mission.UseTimeline && Mission.RequiredBuildingIds.Any(id => !IsInvestigated(id)))
            {
                if (GameHour >= Mission.EndHour) { Phase = GamePhase.Results; Paused = true; Stars = Score = 0; Persist(); }
                Inform("Обследуйте отмеченные объекты и проверьте их работу в течение смены. Без обследования задача не решена.", "error"); return;
            }
            ActionResult result = Simulation.TryCompleteMission(); LastMessage = result.Message; LastActionFailed = !result.Success;
            if (result.Success)
            {
                var evaluation = MissionEvaluator.Evaluate(Mission, Simulation.Evaluate(GameHour));
                Stars = evaluation.Stars; Score = evaluation.Score;
                int reward = progress.Record(MissionIndex, Simulation.ExportState());
                LastMessage += " Новая награда: " + reward + ". Повторное прохождение не дублирует награды.";
                Phase = GamePhase.Results; Paused = true; ScannerEnabled = false; Feedback?.Invoke("success"); Persist();
            }
            else
            {
                if (Mission.UseTimeline && GameHour >= Mission.EndHour) { Phase = GamePhase.Results; Paused = true; Stars = Score = 0; Persist(); }
                if (Mission.UseDecisionRating) Score = MissionEvaluator.Evaluate(Mission, Simulation.Evaluate(GameHour)).Score;
                Feedback?.Invoke("error");
            }
            if (Mission.UseDecisionRating && Phase == GamePhase.Results) SkipTutorial();
            Refresh();
        }

        public void SetSpeed(float speed)
        {
            if (speed != 1 && speed != 2 && speed != 4) return;
            SpeedMultiplier = speed; pendingRealTime = 0; Refresh(); Persist();
        }
        public void FinishShiftEarly()
        {
            if (!Ready || Phase != GamePhase.Playing) return;
            double before = GameHour;
            var result = Simulation.AdvanceTo(Mission.EndHour);
            if (!result.Success) { Inform(result.Message, "error"); return; }
            FireEvents(before, Mission.EndHour); Paused = true; Refresh(); CompleteMission();
        }
        public void OpenProblem(string id)
        {
            if (!Ready || Phase != GamePhase.Playing) return;
            var building = Array.Find(cityBuildings, b => b.SimulationId == id);
            if (building == null) return;
            selection?.Select(building);
            Camera.main?.GetComponent<IsoCameraController>()?.FocusOn(building.GetComponent<Collider>().bounds.center);
            InspectBuilding(id);
        }
        public void SkipTutorial() { tutorialDone = true; if (tutorialKey != null) { PlayerPrefs.SetInt(tutorialKey,1); PlayerPrefs.Save(); } Changed?.Invoke(); }
        public void RepeatTutorial() { tutorialDone = false; PlayerPrefs.SetInt(tutorialKey,0); BeginMission(0); }
        public void PerformTutorialAction()
        {
            if (!TutorialActive) return;
            if (!IsInvestigated("school")) OpenProblem("school");
            else if (Snapshot.PurchaseCount == 0) { CityOverlay.Instance?.SetExpandedCard(true); GameplayHud.Instance?.ShowSolutions(); }
            else FinishShiftEarly();
        }

        void Update()
        {
            long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
            double delta = lastClockStamp == 0 ? 0 : (stamp - lastClockStamp) / (double)System.Diagnostics.Stopwatch.Frequency;
            lastClockStamp = stamp;
            if (Phase != GamePhase.Playing || Paused || !Ready || !Mission.UseTimeline) return;
            pendingRealTime += delta;
            if (pendingRealTime < .2) return;
            double elapsed = pendingRealTime; pendingRealTime = 0; Tick(elapsed);
        }
        void OnEnable() { lastClockStamp = System.Diagnostics.Stopwatch.GetTimestamp(); }

        public void Tick(double realSeconds)
        {
            if (!Ready || Phase != GamePhase.Playing || Paused || !Mission.UseTimeline || realSeconds <= 0 || double.IsNaN(realSeconds) || double.IsInfinity(realSeconds)) return;
            using (TickMarker.Auto())
            {
                double before = GameHour;
                double hour = Math.Min(Mission.EndHour, before + realSeconds * SpeedMultiplier * (Mission.EndHour - Mission.StartHour) / Mission.RealDurationSeconds);
                var result = Simulation.AdvanceTo(hour);
                if (!result.Success) { Inform(result.Message, "error"); Paused = true; return; }
                FireEvents(before, hour);
                if (hour >= Mission.EndHour) { Paused = true; LastMessage = "Смена завершена. Откройте итоги: учитывается фактическая энергия."; }
                autoSaveTime += realSeconds;
                if (autoSaveTime >= 10 || hour >= Mission.EndHour) { autoSaveTime = 0; Persist(); }
                Refresh();
            }
        }

        void FireEvents(double before, double after)
        {
            foreach (var item in Mission.Events ?? Array.Empty<MissionEventDefinition>())
                if (item.Hour > before && item.Hour <= after && firedEvents.Add(item.Id)) { CurrentEvent = item.Text; Feedback?.Invoke("event"); }
        }

        void Refresh()
        {
            if (!Ready) return;
            Snapshot = Simulation.Evaluate(GameHour);
            Rating = MissionEvaluator.Evaluate(Mission, Snapshot);
            if (views != null)
                foreach (var view in views)
                    foreach (var metrics in Snapshot.Buildings)
                        if (view.BuildingId == metrics.BuildingId) { view.Apply(metrics); break; }
            if (Atmosphere != null)
            {
                Atmosphere.TargetBuildingIds = GameHour >= Mission.InvestigationAvailableHour ? Mission.RequiredBuildingIds : Array.Empty<string>();
                Atmosphere.Present(GameHour, ScannerEnabled && Phase == GamePhase.Playing, Snapshot, InvestigatedBuildingIds, Mission.UseDecisionRating ? Rating.Problems : null);
            }
            Changed?.Invoke();
        }

        void Inform(string message, string feedback) { LastActionFailed = feedback == "error"; LastMessage = message; Feedback?.Invoke(feedback); Refresh(); }
        void OnSelection(CityBuilding selected) { if (selected != null) Feedback?.Invoke("select"); }
        CampaignSave Export() => new CampaignSave { MissionIndex = MissionIndex, City = Simulation.ExportState(), Records = progress.Export(), InvestigatedIds = InvestigatedBuildingIds, ScannerEnabled = ScannerEnabled, Results = Phase == GamePhase.Results, SpeedMultiplier = SpeedMultiplier };
        void Persist() { if (Ready && saveService != null && !saveService.Save(Export(), out string error)) { LastActionFailed = true; LastMessage += " " + error; } }
        public void Save() { if (!Ready) return; LastActionFailed = !saveService.Save(Export(), out string message); LastMessage = !LastActionFailed ? "Смена и кампания сохранены." : message; Refresh(); }

        public void Load()
        {
            if (saveService == null || !saveService.Load(out CampaignSave saved, out string message)) { Inform("Сохранение не прочитано. Проверьте наличие и версию файла.", "error"); return; }
            if (!ValidateSave(saved, out var candidate, out var restoredProgress, out string error)) { Inform("Сохранение не применено: " + error, "error"); return; }
            Simulation.Changed -= Refresh; Simulation = candidate; Simulation.Changed += Refresh;
            progress = restoredProgress; MissionIndex = saved.MissionIndex; Mission = Missions[MissionIndex];
            investigated.Clear(); foreach (string id in saved.InvestigatedIds) investigated.Add(id);
            firedEvents.Clear(); foreach (var item in Mission.Events) if (item.Hour <= GameHour) { firedEvents.Add(item.Id); CurrentEvent = item.Text; }
            Phase = saved.Results || saved.City.Completed ? GamePhase.Results : GamePhase.Playing;
            Paused = true; ScannerEnabled = saved.ScannerEnabled; SpeedMultiplier = saved.SpeedMultiplier == 2 || saved.SpeedMultiplier == 4 ? saved.SpeedMultiplier : 1; pendingRealTime = autoSaveTime = 0;
            var evaluation = MissionEvaluator.Evaluate(Mission, Simulation.Evaluate(GameHour));
            Stars = saved.City.Completed ? evaluation.Stars : 0; Score = saved.City.Completed ? evaluation.Score : 0;
            LastActionFailed = false; LastMessage = "Прогресс восстановлен. Время на паузе."; selection?.Select(null); Refresh();
        }

        bool ValidateSave(CampaignSave saved, out CitySimulationService candidate, out CampaignProgress restoredProgress, out string error)
        {
            candidate = null; restoredProgress = null; error = null;
            if (saved == null || saved.Version != 3 || saved.MissionIndex < 0 || saved.MissionIndex >= Missions.Length || saved.City == null || saved.InvestigatedIds == null)
            { error = "Повреждён заголовок кампании."; return false; }
            var nextProgress = new CampaignProgress(Missions);
            if (!nextProgress.TryRestore(saved.Records, out error) || !nextProgress.IsUnlocked(saved.MissionIndex)) { error = error ?? "Миссия ещё не открыта."; return false; }
            var mission = Missions[saved.MissionIndex]; var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in saved.InvestigatedIds)
                if (id == null || !mission.RequiredBuildingIds.Contains(id) || !ids.Add(id)) { error = "Повреждены данные обследования."; return false; }
            if (ids.Count > 0 && saved.City.CurrentHour < mission.InvestigationAvailableHour) { error = "Обследование записано до появления проблемы."; return false; }
            foreach (var purchase in saved.City.History ?? Array.Empty<UpgradePurchaseRecord>())
                if (purchase == null || !ids.Contains(purchase.BuildingId)) { error = "Покупка не подтверждена обследованием."; return false; }
            var nextSimulation = new CitySimulationService(mission);
            var result = nextSimulation.TryRestore(saved.City);
            if (!result.Success) { error = result.Message; return false; }
            if (saved.Results && !saved.City.Completed && saved.City.CurrentHour < mission.EndHour) { error = "Итоги записаны до окончания смены."; return false; }
            candidate = nextSimulation; restoredProgress = nextProgress; return true;
        }

        public void ResetMission() => BeginMission(MissionIndex);
        void OnApplicationPause(bool paused) { if (paused && Phase == GamePhase.Playing) { Paused = true; Persist(); Refresh(); } }
        void OnApplicationFocus(bool focused)
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorTestSaveDirectory)) return;
#endif
            if (!focused && Phase == GamePhase.Playing)
            { Paused = true; pendingRealTime = 0; LastMessage = "Смена на паузе после потери фокуса. Продолжите, когда будете готовы."; Persist(); Refresh(); }
        }
        void OnDestroy()
        {
            if (Simulation != null) Simulation.Changed -= Refresh;
            if (selection != null) selection.SelectionChanged -= OnSelection;
            if (Instance == this) Instance = null;
        }
    }
}
