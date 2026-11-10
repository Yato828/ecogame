using System;
using System.Linq;
using UnityEngine;

namespace Wattshift
{
    [Serializable] public sealed class CityTask
    {
        public string Id, BuildingId, Title, Situation, Kind, UpgradeId, Effect, Explanation;
        public int Chapter, Best, SourceBest;
        public string[] Choices, Explanations, Sources;
    }

    /// <summary>One choice/source/safety template; no independent energy calculations.</summary>
    [Serializable] public sealed class CityTaskCampaign
    {
        public CityTask[] Tasks;
        public static CityTaskCampaign Load() => JsonUtility.FromJson<CityTaskCampaign>(Resources.Load<TextAsset>("CityTasks").text);
        public static readonly string[] Chapters = { "Помоги жителям", "Освети город", "Умные решения", "Город в твоих руках" };
        public bool Valid(TaskProgress state)
        {
            if (state == null || state.Completed == null || state.Errors == null || state.Found == null
                || state.Index < 0 || state.Index > Tasks.Length || state.Completed.Length > Tasks.Length) return false;
            // Sequential completion prevents a corrupted save from skipping lessons.
            if (!state.Completed.SequenceEqual(Tasks.Take(state.Completed.Length).Select(t => t.Id))) return false;
            if (state.Index != state.Completed.Length && state.Index != state.Completed.Length - 1) return false;
            return state.Found.Distinct().Count() == state.Found.Length
                && state.Found.All(id => Tasks.Any(t => t.Id == id && t.Kind == "find"))
                && state.Errors.Distinct().Count() == state.Errors.Length
                && state.Errors.All(id => Tasks.Any(t => t.Id == id));
        }
        public int ChapterStars(TaskProgress state, int chapter)
        {
            var tasks = Tasks.Where(t => t.Chapter == chapter).ToArray();
            if (tasks.Any(t => !state.Completed.Contains(t.Id))) return 0;
            int mistakes = tasks.Count(t => state.Errors.Contains(t.Id));
            return mistakes == 0 ? 3 : mistakes <= 2 ? 2 : 1;
        }
    }

    [Serializable] public sealed class TaskProgress
    {
        public int Index;
        public bool TutorialDone;
        public string[] Completed = Array.Empty<string>(), Errors = Array.Empty<string>(), Found = Array.Empty<string>();
    }

    public sealed partial class GameSession
    {
        public bool AdventureMode { get; private set; } = true;
        public CityTaskCampaign TaskCampaign { get; private set; }
        public TaskProgress Tasks { get; private set; } = new TaskProgress();
        public CityTask ActiveTask => Tasks.Index < TaskCampaign.Tasks.Length ? TaskCampaign.Tasks[Tasks.Index] : null;
        public bool TaskSolved => ActiveTask != null && Tasks.Completed.Contains(ActiveTask.Id);
        public bool FindingSource => ActiveTask?.Kind == "find" && !Tasks.Found.Contains(ActiveTask.Id);
        public string TaskFeedback { get; private set; } = "";
        public int TaskStars => Enumerable.Range(0, 4).Sum(c => TaskCampaign.ChapterStars(Tasks, c));
        public bool TaskWelcome => !Tasks.TutorialDone && Tasks.Completed.Length == 0;
        CampaignSaveService taskSaveService;
        MissionDefinition taskMission;
#if UNITY_EDITOR
        public static bool EditorLegacyGameplay;
#endif

        void InitializeTasks(string directory)
        {
            TaskCampaign = CityTaskCampaign.Load();
            taskSaveService = new CampaignSaveService(System.IO.Path.Combine(directory, "wattshift-player-campaign.json"));
            if (taskSaveService.Exists)
            {
                if (taskSaveService.Load(out var save, out string error) && TaskCampaign.Valid(save.Tasks)) Tasks = save.Tasks;
                else { LastActionFailed = true; LastMessage = "Не удалось прочитать задания. Исходный файл сохранён; можно начать заново."; }
            }
#if UNITY_EDITOR
            AdventureMode = !EditorLegacyGameplay;
#endif
        }

        public void BeginAdventure(bool skipTutorial = false)
        {
            AdventureMode = true;
            if (skipTutorial) Tasks.TutorialDone = true;
            BuildTaskCity();
            Phase = ActiveTask == null ? GamePhase.Results : GamePhase.Playing;
            Paused = true; ScannerEnabled = false;
            TaskFeedback = TaskSolved ? "✓ " + ActiveTask.Explanation : "";
            selection?.Select(null);
            if (ActiveTask != null) FocusTask(false);
            SaveTasks(); Refresh();
        }

        void BuildTaskCity()
        {
            if (Simulation != null) Simulation.Changed -= Refresh;
            if (taskMission != null) Destroy(taskMission);
            taskMission = Instantiate(Missions[0]);
            // Reuse the authored seven upgrades and all existing constraints in one city.
            taskMission.Buildings = Missions[0].Buildings.Select(b =>
                b.Id == "clinic" ? Missions[2].Buildings.First(x => x.Id == b.Id)
                : b.Id == "crossings" || b.Id == "walkways" ? Missions[1].Buildings.First(x => x.Id == b.Id) : b).ToArray();
            taskMission.InitialBudget = taskMission.Buildings.Sum(b => b.Upgrades.Sum(u => u.Cost)) + 1;
            Mission = taskMission; MissionIndex = 0;
            Simulation = new CitySimulationService(taskMission); Simulation.Changed += Refresh;
            Simulation.AdvanceTo(20); // Fixed evening presentation; no waiting and no running clock.
            foreach (var task in TaskCampaign.Tasks.Where(t => Tasks.Completed.Contains(t.Id) && !string.IsNullOrEmpty(t.UpgradeId)))
            {
                var result = Simulation.TryPurchase(task.BuildingId, task.UpgradeId);
                if (!result.Success) throw new InvalidOperationException("Task upgrade cannot restore: " + task.Id + ": " + result.Message);
            }
        }

        public void FocusTask(bool open = true)
        {
            if (ActiveTask == null) return;
            var building = Array.Find(cityBuildings, b => b.SimulationId == ActiveTask.BuildingId);
            if (building == null) return;
            var collider = building.GetComponentInChildren<Collider>();
            Camera.main?.GetComponent<IsoCameraController>()?.FocusOn(collider != null ? collider.bounds.center : building.transform.position);
            if (open) { selection?.Select(building); GameplayHud.Instance?.ShowSolutions(); }
        }

        public void ChooseTask(int index)
        {
            if (!AdventureMode || Phase != GamePhase.Playing || ActiveTask == null || TaskSolved) return;
            var task = ActiveTask;
            bool source = FindingSource;
            var choices = source ? task.Sources : task.Choices;
            if (index < 0 || index >= choices.Length) return;
            bool correct = index == (source ? task.SourceBest : task.Best);
            if (!correct)
            {
                Tasks.Errors = Tasks.Errors.Union(new[] { task.Id }).ToArray();
                TaskFeedback = source ? "Можно лучше! Важные системы не отключаем. Здесь лишнее: " + task.Sources[task.SourceBest]
                    : "Можно лучше! " + task.Explanations[index] + "\nЛучшее решение: " + task.Choices[task.Best];
                LastActionFailed = true; Feedback?.Invoke("error");
            }
            else if (source)
            {
                Tasks.Found = Tasks.Found.Union(new[] { task.Id }).ToArray();
                TaskFeedback = "Источник найден! Теперь выбери решение."; LastActionFailed = false; Feedback?.Invoke("inspect");
            }
            else
            {
                if (!string.IsNullOrEmpty(task.UpgradeId))
                {
                    var result = Simulation.TryPurchase(task.BuildingId, task.UpgradeId);
                    if (!result.Success) { TaskFeedback = result.Message; LastActionFailed = true; Refresh(); return; }
                }
                Tasks.Completed = Tasks.Completed.Concat(new[] { task.Id }).ToArray();
                TaskFeedback = "✓ " + task.Explanation + "\n★ Ещё один шаг к умному городу!";
                LastActionFailed = false;
                foreach (var building in cityBuildings.Where(b => b.SimulationId == task.BuildingId))
                {
                    var effect = building.GetComponent<SourceBuildingHighlight>() ?? building.gameObject.AddComponent<SourceBuildingHighlight>();
                    effect.PulseSuccess();
                }
                Feedback?.Invoke("purchase:" + task.BuildingId);
                if (Tasks.Index == 0) Tasks.TutorialDone = true;
            }
            SaveTasks(); Refresh();
        }

        public void NextTask()
        {
            if (!AdventureMode || !TaskSolved) return;
            if (tutorialReplay != null) { LeaveAdventure(); return; }
            Tasks.Index++; TaskFeedback = ""; LastActionFailed = false;
            Phase = ActiveTask == null ? GamePhase.Results : GamePhase.Playing;
            selection?.Select(null); FocusTask(false); SaveTasks(); Refresh();
        }

        public void RepeatTaskTutorial()
        {
            // Replay the real first task in a disposable state, without erasing campaign progress.
            if (tutorialReplay == null) tutorialReplay = JsonUtility.ToJson(Tasks);
            Tasks = new TaskProgress(); BeginAdventure();
        }
        string tutorialReplay;
        public void LeaveAdventure()
        {
            if (tutorialReplay != null) { Tasks = JsonUtility.FromJson<TaskProgress>(tutorialReplay); tutorialReplay = null; }
            SaveTasks(); Phase = GamePhase.Menu; selection?.Select(null); Refresh();
        }
        public void SkipTaskTutorial() { Tasks.TutorialDone = true; SaveTasks(); Refresh(); }
        void SaveTasks()
        {
            if (taskSaveService == null || tutorialReplay != null) return;
            if (!taskSaveService.Save(new CampaignSave { Tasks = Tasks }, out string error))
            { LastActionFailed = true; LastMessage = error; TaskFeedback = error; }
        }
        public string TaskTips => string.Join("\n\n", TaskCampaign.Tasks.Where(t => Tasks.Errors.Contains(t.Id))
            .Select(t => t.Explanation).Distinct().Take(4).DefaultIfEmpty("Выключай ненужное. Сохраняй важное. Подбирай эффективные решения без потери комфорта."));
        public ProblemMarkerState TaskMarker(string id) => ActiveTask?.BuildingId == id
            ? TaskSolved ? ProblemMarkerState.Resolved : ProblemMarkerState.Problem
            : TaskCampaign.Tasks.Any(t => t.BuildingId == id && Tasks.Completed.Contains(t.Id)) ? ProblemMarkerState.Resolved : ProblemMarkerState.Hidden;
    }
}
