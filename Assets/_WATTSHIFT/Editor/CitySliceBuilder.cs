using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wattshift.Editor
{
    /// <summary>Reproducible G1 assembly. All visible geometry comes from licensed Kenney FBX files.</summary>
    public static class CitySliceBuilder
    {
        const string Root = "Assets/_WATTSHIFT/";
        public const string ScenePath = Root + "Scenes/CitySlice.unity";
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        static readonly List<string> Imports = new List<string>();
        static Transform district;

        [MenuItem("WATTSHIFT/Build visual slice")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            Materials.Clear(); Imports.Clear();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            district = new GameObject("KENNEY • городской квартал").transform;
            foreach (string pack in new[] { "Suburban", "Commercial", "Roads", "Industrial" })
                Materials[pack] = Palette(pack);
            Materials["Grass"] = Solid("Park grass", new Color(.56f, .70f, .46f));
            Materials["Base"] = Solid("Diorama edge", new Color(.19f, .32f, .31f));
            Materials["Paving"] = Solid("Limestone pavement", new Color(.76f, .79f, .69f));

            // The base, grass and plaza all retain the source tile-low mesh; no primitive substitutes.
            Place("Roads", "tile-low", new Vector3(0, -.43f, 0), new Vector3(16, 20, 16), 0, "Base");
            Place("Roads", "tile-low", new Vector3(0, -.035f, 0), new Vector3(15.85f, 1.75f, 15.85f), 0, "Grass");

            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                string model = Mathf.Abs(i) == 3 ? "road-end" : Mathf.Abs(i) == 1 ? "road-crossing" : "road-straight";
                Place("Roads", model, new Vector3(i * 2, .002f, 0), Vector3.one * 2, Mathf.Abs(i) == 3 && i > 0 ? 180 : 0);
                Place("Roads", model, new Vector3(0, .002f, i * 2), Vector3.one * 2, Mathf.Abs(i) == 3 && i < 0 ? 270 : 90);
            }
            Place("Roads", "road-crossroad", new Vector3(0, .002f, 0), Vector3.one * 2);

            Building("Suburban", "building-type-a", -5.3f, 2.8f, 1.6f, "Дом у лип", "ЖИЛОЙ ДОМ", "Тёплый семейный дом с небольшим садом. В следующих миссиях здесь можно будет сравнить варианты энергосбережения.");
            Building("Suburban", "building-type-b", -2.55f, 2.8f, 1.6f, "Солнечный дом", "ЖИЛОЙ ДОМ", "Компактный дом с мансардой. Выбор здания открывает его карточку; расчёты энергии появятся после утверждения визуальной сцены.");
            Building("Suburban", "building-type-c", 2.55f, 2.8f, 1.6f, "Дом на бульваре", "ЖИЛОЙ ДОМ", "Один из шести разных жилых домов квартала. Модель, материалы и палитра — Kenney City Kit.");
            Building("Suburban", "building-type-d", 5.35f, 2.8f, 1.6f, "Зелёная крыша", "ЖИЛОЙ ДОМ", "Дом рядом с пешеходным переходом. Освещение улиц и безопасные маршруты станут частью следующих миссий.");
            Building("Suburban", "building-type-e", 2.65f, -4.3f, 1.7f, "Тихий двор", "ЖИЛОЙ ДОМ", "Небольшой жилой двор с деревьями и дорожкой к бульвару.");
            Building("Suburban", "building-type-f", 5.5f, -4.3f, 1.7f, "Дом у парка", "ЖИЛОЙ ДОМ", "Жилой дом напротив городского сквера. Управление потреблением будет доступно на следующем этапе.");
            Building("Commercial", "building-e", -4.15f, 5.85f, 2.15f, "Школа № 1", "ОБРАЗОВАНИЕ", "Школьный корпус представлен подходящей моделью Kenney Commercial. Здесь начнётся миссия об освещении и технике после занятий.");
            Building("Commercial", "building-c", 4.15f, 5.85f, 2.15f, "Поликлиника", "ЗДРАВООХРАНЕНИЕ", "Важный городской объект. Медицинские службы должны сохранять необходимое питание во всех будущих энергетических решениях.", true);

            // Civic forecourts and pedestrian routes are source meshes, with a restrained shared palette.
            foreach (float x in new[] { -4.15f, 4.15f })
                Place("Roads", "tile-low", new Vector3(x, .004f, 5.7f), new Vector3(4.3f, .5f, 3.45f), 0, "Paving");
            Place("Roads", "tile-low", new Vector3(-4.2f, .004f, -4.25f), new Vector3(5.4f, .5f, 5.2f), 0, "Grass");
            Place("Suburban", "path-long", new Vector3(-4.2f, .03f, -2.75f), new Vector3(3.5f, 1, 7.5f));
            Place("Suburban", "path-long", new Vector3(-4.2f, .03f, -4.6f), new Vector3(3.5f, 1, 12), 90);
            float[,] trees = { { -6.3f,-2.6f }, { -6.2f,-5.9f }, { -2.1f,-6.1f }, { -2.2f,-2.5f }, { -5.8f,-4.5f }, { -3.0f,-5.5f }, { -6.9f,5.9f }, { 6.8f,6.1f }, { -6.9f,1.6f }, { 6.8f,1.7f }, { 2.0f,-6.3f }, { 6.6f,-6.2f }, { -2.2f,6.9f }, { 2.2f,6.9f } };
            for (int i = 0; i < trees.GetLength(0); i++)
                Place("Suburban", i % 3 == 0 ? "tree-small" : "tree-large", new Vector3(trees[i, 0], .03f, trees[i, 1]), Vector3.one * (i % 2 == 0 ? 1.9f : 1.6f), i * 39);
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -1.1f : 1.1f;
                float z = i % 2 == 0 ? -3.5f : 3.7f;
                Place("Roads", "light-curved", new Vector3(x, .045f, z), Vector3.one * 2.1f, i < 2 ? 90 : -90);
            }
            foreach (float x in new[] { -5.5f, -2.5f, 2.5f, 5.5f })
                Place("Suburban", "planter", new Vector3(x, .04f, 1.45f), Vector3.one * 1.6f);

            ConfigureRendering();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("docs/evidence/g1");
            File.WriteAllLines("docs/evidence/g1/unity-import-metrics.tsv", new[] { "pack\tmodel\tmesh_triangles\tmesh_vertices\timported_bounds\tmaterial_shader" }.Concat(Imports.Distinct()));
            Debug.Log("WATTSHIFT_G1_SCENE_BUILT " + ScenePath + " buildings=8 unique_buildings=8 streetlights=4");
        }

        static Material Palette(string pack)
        {
            string texPath = Root + "Art/Imported/" + pack + "/Textures/colormap.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.mipmapEnabled = true; importer.maxTextureSize = 512; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            Material mat = Solid(pack + " palette", Color.white);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            return mat;
        }

        static Material Solid(string name, Color color)
        {
            string path = Root + "Art/Materials/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", .12f); mat.SetFloat("_Metallic", 0); mat.enableInstancing = true;
            return mat;
        }

        static GameObject Place(string pack, string model, Vector3 position, Vector3 scale, float yaw = 0, string material = null)
        {
            string path = Root + "Art/Imported/" + pack + "/" + model + ".fbx";
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new InvalidOperationException("Missing genuine source mesh: " + path);
            string prefabPath = Root + "Art/Prefabs/" + pack + "_" + model + (material == null ? "" : "_" + material) + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                GameObject wrapper = new GameObject(pack + "/" + model);
                GameObject mesh = (GameObject)PrefabUtility.InstantiatePrefab(source); mesh.transform.SetParent(wrapper.transform, false);
                foreach (Renderer r in mesh.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterials = Enumerable.Repeat(Materials[material ?? pack], r.sharedMaterials.Length).ToArray();
                    r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
                }
                prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath); UnityEngine.Object.DestroyImmediate(wrapper);
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(district); instance.transform.localScale = scale; instance.transform.rotation = Quaternion.Euler(0, yaw, 0);
            instance.transform.position = Vector3.zero;
            Bounds bounds = BoundsOf(instance);
            // Source bottom pivot becomes the placement height, including FBX axis conversion.
            instance.transform.position = position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            int triangles = 0, vertices = 0;
            foreach (var mf in instance.GetComponentsInChildren<MeshFilter>()) { triangles += mf.sharedMesh.triangles.Length / 3; vertices += mf.sharedMesh.vertexCount; }
            Imports.Add(pack + "\t" + model + "\t" + triangles + "\t" + vertices + "\t" + BoundsOf(instance).size.ToString("F3") + "\tUniversal Render Pipeline/Lit");
            return instance;
        }

        static Bounds BoundsOf(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderers: " + obj.name);
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static void Building(string pack, string model, float x, float z, float scale, string title, string category, string description, bool essential = false)
        {
            GameObject go = Place(pack, model, new Vector3(x, .04f, z), Vector3.one * scale);
            go.name = title + " • " + model;
            var info = go.AddComponent<CityBuilding>(); info.Title = title; info.Category = category; info.Description = description; info.Essential = essential;
            Bounds bounds = BoundsOf(go);
            var collider = go.AddComponent<BoxCollider>(); collider.center = go.transform.InverseTransformPoint(bounds.center); collider.size = bounds.size / scale;
        }

        static void ConfigureRendering()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (pipeline == null) throw new InvalidOperationException("Official URP template pipeline missing");
            pipeline.renderScale = 1; pipeline.msaaSampleCount = 4; pipeline.shadowDistance = 60;
            pipeline.mainLightShadowmapResolution = 2048;
            pipeline.shadowCascadeCount = 2; pipeline.shadowDepthBias = .04f; pipeline.shadowNormalBias = .1f;
            // Forward rendering supports MSAA and avoids unnecessary deferred costs for this small diorama.
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            rendererData.renderingMode = RenderingMode.Forward;
            foreach (var feature in rendererData.rendererFeatures) feature.SetActive(false);
            EditorUtility.SetDirty(rendererData); EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All; QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
            QualitySettings.vSyncCount = 1; QualitySettings.antiAliasing = 4;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;
            PlayerSettings.companyName = "WATTSHIFT"; PlayerSettings.productName = "WATTSHIFT";
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.47f, .58f, .68f);
            RenderSettings.ambientEquatorColor = new Color(.43f, .52f, .49f);
            RenderSettings.ambientGroundColor = new Color(.22f, .29f, .26f);
            RenderSettings.ambientIntensity = 1;
            var sunObject = new GameObject("Late afternoon • directional sun");
            var sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional;
            sun.color = new Color(1, .91f, .77f); sun.intensity = 2.4f;
            sun.shadows = LightShadows.Soft; sun.shadowBias = .025f; sun.shadowNormalBias = .3f;
            sun.transform.rotation = Quaternion.Euler(38, -130, 0); RenderSettings.sun = sun;
            sunObject.AddComponent<UniversalAdditionalLightData>();

            var cameraObject = new GameObject("Isometric camera"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            camera.orthographicSize = 11.5f; camera.nearClipPlane = .1f; camera.farClipPlane = 110;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.77f, .84f, .81f);
            camera.transform.rotation = Quaternion.Euler(36, -38, 0);
            camera.transform.position = new Vector3(0, .7f, 0) - camera.transform.forward * 30;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>(); cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraObject.AddComponent<AudioListener>();
            var controls = cameraObject.AddComponent<IsoCameraController>(); controls.MinimumSize = 7; controls.MaximumSize = 16; controls.PanHalfExtent = new Vector2(4, 4);
            var overlay = new GameObject("City selection interface").AddComponent<CityOverlay>(); overlay.SceneCamera = camera;

            var volume = new GameObject("Restrained colour grading").AddComponent<Volume>(); volume.isGlobal = true;
            string profilePath = Root + "Art/Materials/CityGrade.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            profile.components.Clear();
            var tone = profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(); color.postExposure.Override(.25f); color.contrast.Override(7); color.saturation.Override(6);
            EditorUtility.SetDirty(profile); volume.sharedProfile = profile;
        }
    }
}
