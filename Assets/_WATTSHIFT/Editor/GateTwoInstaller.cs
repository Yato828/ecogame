using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wattshift.Editor
{
    /// <summary>Adds G2 data and adapters to the approved scene, without rebuilding or moving its geometry.</summary>
    public static class GateTwoInstaller
    {
        const string Data = "Assets/_WATTSHIFT/Data/Mission1/";
        const string Feedback = "Assets/_WATTSHIFT/Art/Feedback/";
        [MenuItem("WATTSHIFT/Install first mission")]
        public static void Install()
        {
            Directory.CreateDirectory(Data); Directory.CreateDirectory(Feedback); AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(CitySliceBuilder.ScenePath);
            CityBuilding[] views = UnityEngine.Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None);
            var definitions = new List<BuildingDefinition>();
            foreach (CityBuilding view in views.OrderBy(v => v.name, StringComparer.Ordinal))
            {
                string model = view.name.Substring(view.name.LastIndexOf(" • ", StringComparison.Ordinal) + 3);
                bool home = model.StartsWith("building-type-", StringComparison.Ordinal);
                bool clinic = view.Essential;
                string id = home ? "home-" + model.Substring(model.Length - 1) : clinic ? "clinic" : "school";
                view.SimulationId = id;
                BuildingDefinition definition = Get<BuildingDefinition>(id);
                definition.Id = id; definition.Title = view.Title; definition.BaseComfort = 100;
                if (home)
                {
                    definition.Devices = new[] { Device("fridge", .12f, 0, 24, true), Device("standby", .2f, 0, 24), Device("lights", .6f, 18, 24, false, true) };
                    definition.Upgrades = new[] { Upgrade(id + "-led", "Эффективные лампы", "Та же освещённость при меньшей мощности в учебной модели.", 140, 0, Effect("lights", .45f)) };
                }
                else if (clinic)
                {
                    definition.Devices = new[] { Device("medical", 8, 0, 24, true), Device("lights", 2, 0, 24, false, true) };
                    definition.Upgrades = new[] { Upgrade("clinic-led", "Эффективное освещение", "Сохраняет освещение и питание медицинских систем.", 220, 0, Effect("lights", .5f)), Upgrade("clinic-off", "Отключить медицинские системы", "Недопустимое действие: критические системы должны оставаться включёнными.", 0, 0, Effect("medical", 0)) };
                }
                else
                {
                    definition.Devices = new[] { Device("security", .5f, 0, 24, true), Device("lights", 4, 8, 23, false, true), Device("computers", 6, 8, 23) };
                    definition.Upgrades = new[] {
                        Upgrade("school-lights", "Свет по расписанию", "Выключать коридорный свет после 18:00. Охранные системы сохраняют питание.", 180, 0, Schedule("lights", 8, 18)),
                        Upgrade("school-computers", "Компьютеры после занятий", "Завершать работу компьютеров в 18:00 вместо 23:00.", 260, 0, Schedule("computers", 8, 18)),
                        Upgrade("school-dark", "Отключить свет без условий", "Нарушает минимальный комфорт: действие будет отклонено.", 70, -40, Effect("lights", 0)),
                        Upgrade("school-expensive", "Полная замена компьютеров", "Альтернативная модернизация, превышающая начальный бюджет.", 1200, 0, Effect("computers", .5f)) };
                }
                definitions.Add(definition); EditorUtility.SetDirty(definition);
                var feedback = view.GetComponent<BuildingEnergyView>() ?? view.gameObject.AddComponent<BuildingEnergyView>();
                feedback.BuildingId = id;
                feedback.WindowRenderer = WindowOverlay(view, id);
                EditorUtility.SetDirty(view); EditorUtility.SetDirty(feedback);
            }
            var mission = Get<MissionDefinition>("after-hours");
            mission.Id = "after-hours-v1"; mission.Title = "После занятий";
            mission.Description = "Снизьте вечернее потребление школы, сохранив питание поликлиники и комфорт квартала.";
            mission.Assumptions = "Учебные примерные нагрузки; расходы — условные единицы. кВт: мощность в 20:00; кВт·ч: энергия по расписаниям за 17:00–23:00.";
            mission.StartHour = 17; mission.EndHour = 23; mission.EvaluationHour = 20;
            mission.InitialBudget = 650; mission.TargetSavingPercent = 30; mission.MinComfort = 80; mission.Buildings = definitions.ToArray();
            EditorUtility.SetDirty(mission);
            var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            if (session == null) session = new GameObject("Mission 1 • data-driven gameplay").AddComponent<GameSession>();
            session.Mission = mission; EditorUtility.SetDirty(session);
            CityOverlay overlay = UnityEngine.Object.FindFirstObjectByType<CityOverlay>();
            if (overlay.GetComponent<GameplayHud>() == null) overlay.gameObject.AddComponent<GameplayHud>();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("WATTSHIFT_G2_INSTALLED buildings=" + views.Length + "; original art transforms/materials preserved");
        }

        static T Get<T>(string name) where T : ScriptableObject
        {
            string path = Data + name + ".asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
        static DeviceDefinition Device(string id, float power, float start, float end, bool essential = false, bool lighting = false)
            => new DeviceDefinition { Id = id, PowerKW = power, StartHour = start, EndHour = end, Essential = essential, IsLighting = lighting };
        static UpgradeEffect Effect(string device, float multiplier) => new UpgradeEffect { DeviceId = device, PowerMultiplier = multiplier };
        static UpgradeEffect Schedule(string device, float start, float end) => new UpgradeEffect { DeviceId = device, PowerMultiplier = 1, OverrideSchedule = true, StartHour = start, EndHour = end };
        static UpgradeDefinition Upgrade(string id, string title, string description, int cost, float comfort, params UpgradeEffect[] effects)
        {
            var result = Get<UpgradeDefinition>(id);
            result.Id = id; result.Title = title; result.Description = description; result.Cost = cost; result.ComfortDelta = comfort; result.Effects = effects;
            EditorUtility.SetDirty(result); return result;
        }

        static Renderer WindowOverlay(CityBuilding building, string id)
        {
            Transform existing = building.transform.Find("Source window feedback");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            var diagnostic = new Dictionary<string, int>();
            foreach (var mf in building.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = mf.sharedMesh; var renderer = mf.GetComponent<Renderer>();
                Texture2D source = renderer.sharedMaterial.GetTexture("_BaseMap") as Texture2D;
                if (source == null) continue;
                var texture = new Texture2D(2, 2); texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source)));
                Vector3[] pos = mesh.vertices; Vector2[] uv = mesh.uv; int[] indices = mesh.triangles;
                Matrix4x4 local = building.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    Vector3 v0 = local.MultiplyPoint3x4(pos[a]), v1 = local.MultiplyPoint3x4(pos[b]), v2 = local.MultiplyPoint3x4(pos[c]);
                    Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                    if (Mathf.Abs(normal.y) > .3f) continue;
                    Vector2 sample = (uv[a] + uv[b] + uv[c]) / 3;
                    Color color = texture.GetPixelBilinear(sample.x, sample.y);
                    string rgb = ColorUtility.ToHtmlStringRGB(color); diagnostic[rgb] = diagnostic.TryGetValue(rgb, out int n) ? n + 1 : 1;
                    // Bright blue glass entries only. Slate frames/fascias share a blue hue
                    // but must keep their approved original material and must never glow.
                    if (!(color.r > .4f && color.g > .5f && color.b > .7f && color.b > color.r * 1.3f && color.b > color.g * 1.15f)) continue;
                    int first = vertices.Count;
                    vertices.Add(v0 + normal * .0015f); vertices.Add(v1 + normal * .0015f); vertices.Add(v2 + normal * .0015f);
                    triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                }
                UnityEngine.Object.DestroyImmediate(texture);
            }
            if (triangles.Count == 0) throw new InvalidOperationException("No genuine source window faces found: " + id + " colors=" + string.Join(",", diagnostic.Keys));
            string meshPath = Feedback + id + "-windows.asset";
            Mesh overlayMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (overlayMesh == null) { overlayMesh = new Mesh(); AssetDatabase.CreateAsset(overlayMesh, meshPath); }
            overlayMesh.Clear(); overlayMesh.name = id + " • original Kenney window faces";
            overlayMesh.SetVertices(vertices); overlayMesh.SetTriangles(triangles, 0); overlayMesh.RecalculateNormals(); overlayMesh.RecalculateBounds(); EditorUtility.SetDirty(overlayMesh);
            string materialPath = Feedback + "Window feedback.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetColor("_BaseColor", new Color(1, .78f, .35f));
                material.SetFloat("_Cull", 0); AssetDatabase.CreateAsset(material, materialPath);
            }
            var go = new GameObject("Source window feedback"); go.transform.SetParent(building.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = overlayMesh;
            var output = go.AddComponent<MeshRenderer>(); output.sharedMaterial = material;
            output.shadowCastingMode = ShadowCastingMode.Off; output.receiveShadows = false;
            Debug.Log("G2_SOURCE_WINDOWS " + id + " triangles=" + triangles.Count / 3);
            return output;
        }
    }
}
