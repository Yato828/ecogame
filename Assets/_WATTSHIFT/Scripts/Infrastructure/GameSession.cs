using System;
using System.IO;
using UnityEngine;

namespace Wattshift
{
    /// <summary>Scene adapter for a pure, deterministic service. No simulation arithmetic lives in the UI.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        public MissionDefinition Mission;
        public CitySimulationService Simulation { get; private set; }
        public SimulationSnapshot Snapshot { get; private set; }
        public string LastMessage { get; private set; } = "Выберите школу и найдите ненужные нагрузки после занятий.";
        public bool Ready => Simulation != null;
        public event Action Changed;
        SaveService saveService;
        BuildingEnergyView[] views;

        void Awake()
        {
            Instance = this;
            string saveDirectory = Application.persistentDataPath;
#if UNITY_EDITOR
            // Explicit CLI test runs must not read or overwrite a player's real progress.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-wattshiftEvidence") >= 0)
                saveDirectory = Path.Combine(Application.temporaryCachePath, "WattshiftValidation");
#endif
            saveService = new SaveService(Path.Combine(saveDirectory, "wattshift-mission1-v1.json"));
            views = FindObjectsByType<BuildingEnergyView>(FindObjectsSortMode.None);
            if (Mission == null) { LastMessage = "Не задана конфигурация миссии."; return; }
            CreateSimulation();
        }

        void Start()
        {
            // A malformed file never overwrites the fresh state or disappears silently.
            if (File.Exists(saveService.FilePath)) Load();
            else Refresh();
        }

        void CreateSimulation()
        {
            if (Simulation != null) Simulation.Changed -= Refresh;
            Simulation = new CitySimulationService(Mission);
            Simulation.Changed += Refresh;
            Refresh();
        }

        void Refresh()
        {
            if (!Ready) return;
            Snapshot = Simulation.Evaluate(Mission.EvaluationHour);
            if (views != null)
                foreach (BuildingEnergyView view in views)
                    foreach (BuildingMetrics metrics in Snapshot.Buildings)
                        if (view.BuildingId == metrics.BuildingId) { view.Apply(metrics); break; }
            Changed?.Invoke();
        }

        public void Purchase(string buildingId, string upgradeId)
        {
            if (!Ready) return;
            ActionResult result = Simulation.TryPurchase(buildingId, upgradeId);
            LastMessage = result.Message;
            if (result.Success) PersistWithoutReplacingMessage();
            Refresh();
        }

        public void CompleteMission()
        {
            if (!Ready) return;
            ActionResult result = Simulation.TryCompleteMission();
            LastMessage = result.Message;
            if (result.Success) PersistWithoutReplacingMessage();
            Refresh();
        }

        void PersistWithoutReplacingMessage()
        {
            if (!saveService.TrySave(Simulation.ExportState(), out string message)) LastMessage += " " + message;
        }

        public void Save()
        {
            if (!Ready) return;
            saveService.TrySave(Simulation.ExportState(), out string message); LastMessage = message; Refresh();
        }

        public void Load()
        {
            if (!Ready) return;
            if (!saveService.TryLoad(out CityState state, out string message)) { LastMessage = message; Refresh(); return; }
            ActionResult result = Simulation.TryRestore(state);
            LastMessage = result.Success ? "Прогресс восстановлен." : "Сохранение не применено: " + result.Message;
            Refresh();
        }

        public void ResetMission()
        {
            CreateSimulation(); LastMessage = "Миссия начата заново. Бюджет и нагрузки восстановлены.";
            PersistWithoutReplacingMessage(); Refresh();
        }

        void OnDestroy()
        {
            if (Simulation != null) Simulation.Changed -= Refresh;
            if (Instance == this) Instance = null;
        }
    }
}
