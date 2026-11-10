using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wattshift
{
    public enum ProblemMarkerState { Hidden, Problem, Urgent, Resolved }

    /// <summary>Screen-sized controls anchored to the actual city colliders. No simulation decisions live here.</summary>
    public sealed class ProblemMarkers : MonoBehaviour
    {
        public static ProblemMarkers Instance { get; private set; }
        public VisualElement Root => root;

        sealed class Anchor
        {
            public CityBuilding Building;
            public Vector3 Top;
        }

        sealed class Connector : VisualElement
        {
            public Vector2 From, To;
            public Connector()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = style.right = style.top = style.bottom = 0;
                generateVisualContent += Draw;
            }
            void Draw(MeshGenerationContext context)
            {
                var painter = context.painter2D;
                painter.strokeColor = new Color(.98f, .97f, .88f, .85f);
                painter.lineWidth = 2;
                painter.BeginPath(); painter.MoveTo(From); painter.LineTo(To); painter.Stroke();
            }
        }

        sealed class Marker
        {
            public string Id;
            public readonly List<Anchor> Anchors = new List<Anchor>();
            public Button Button;
            public Connector Line;
            public ProblemMarkerState State;
            public Rect ScreenRect;
            public bool Visible, Present, AnchorVisible;
            public Vector2 ProjectedAnchor;
            public float LastPulse = 1;
            public float SuccessAt = -1;
        }

        readonly List<Marker> markers = new List<Marker>();
        readonly StringBuilder tooltip = new StringBuilder(160);
        GameSession session;
        Camera sceneCamera;
        VisualElement root;
        PanelSettings panel;
        ThemeStyleSheet theme;
        GameObject documentObject;
        VisualElement hudRoot, hudCard, contextRoot;
        bool placementDirty = true, projectionDirty = true, wasActive;
        int screenWidth, screenHeight;
        Rect safeArea;
        Matrix4x4 viewMatrix, projectionMatrix;
        float lastScale;

        void GeometryChanged(GeometryChangedEvent change) { placementDirty = true; }
        void PreferencesChanged() { placementDirty = true; }

        // HUD Start order is unspecified: attach once its public tree exists, including the
        // card whose local geometry changes without changing the full-screen root geometry.
        void BindGeometry()
        {
            var nextHud = GameplayHud.Instance != null ? GameplayHud.Instance.Root : null;
            if (nextHud != hudRoot)
            {
                if (hudRoot != null) hudRoot.UnregisterCallback<GeometryChangedEvent>(GeometryChanged);
                if (hudCard != null) hudCard.UnregisterCallback<GeometryChangedEvent>(GeometryChanged);
                hudRoot = nextHud; hudCard = hudRoot?.Q<VisualElement>("mission-card");
                hudRoot?.RegisterCallback<GeometryChangedEvent>(GeometryChanged);
                hudCard?.RegisterCallback<GeometryChangedEvent>(GeometryChanged);
                placementDirty = true;
            }
            var nextContext = CityOverlay.Instance != null ? CityOverlay.Instance.ContextRoot : null;
            if (nextContext != contextRoot)
            {
                contextRoot?.UnregisterCallback<GeometryChangedEvent>(GeometryChanged);
                contextRoot = nextContext;
                contextRoot?.RegisterCallback<GeometryChangedEvent>(GeometryChanged);
                placementDirty = true;
            }
        }

        void Start()
        {
            Instance = this;
            session = GameSession.Instance;
            sceneCamera = CityOverlay.Instance != null ? CityOverlay.Instance.SceneCamera : Camera.main;
            if (session == null || sceneCamera == null) return;
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "WATTSHIFT problem markers";
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.sortingOrder = 22;
            var sourceTheme = Resources.Load<ThemeStyleSheet>("WattshiftRuntimeTheme");
            if (sourceTheme != null) { theme = Instantiate(sourceTheme); panel.themeStyleSheet = theme; }
            documentObject = new GameObject("WATTSHIFT world problem controls");
            var document = documentObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            root = document.rootVisualElement;
            root.name = "problem-markers";
            root.style.display = DisplayStyle.None;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            root.style.unityFontDefinition = RuntimeUiFont.Definition;

            // This is a static diorama: source colliders and roof anchors are cached once.
            var buildings = FindObjectsByType<CityBuilding>(FindObjectsSortMode.None);
            Array.Sort(buildings, (a, b) => string.CompareOrdinal(a.SimulationId, b.SimulationId));
            foreach (var building in buildings)
            {
                if (string.IsNullOrEmpty(building.SimulationId)) continue;
                var colliders = building.GetComponentsInChildren<Collider>();
                bool found = false;
                Bounds bounds = default;
                foreach (var collider in colliders)
                {
                    if (!collider.enabled || collider.isTrigger) continue;
                    if (found) bounds.Encapsulate(collider.bounds);
                    else { bounds = collider.bounds; found = true; }
                }
                if (!found) continue;
                var marker = Find(building.SimulationId);
                if (marker == null)
                {
                    marker = new Marker { Id = building.SimulationId };
                    marker.Line = new Connector(); root.Add(marker.Line);
                    string id = marker.Id;
                    marker.Button = new Button(() => Open(id)) { name = "problem-marker-" + id, text = "!", tabIndex = 0 };
                    StyleButton(marker.Button);
                    root.Add(marker.Button);
                    marker.Button.style.display = DisplayStyle.None;
                    marker.Line.style.display = DisplayStyle.None;
                    markers.Add(marker);
                }
                marker.Anchors.Add(new Anchor { Building = building, Top = new Vector3(bounds.center.x, bounds.max.y + .18f, bounds.center.z) });
            }
            session.Changed += Refresh;
            UserExperienceSettings.Changed += PreferencesChanged;
            Refresh();
        }

        static void StyleButton(Button button)
        {
            button.style.position = Position.Absolute;
            button.style.marginLeft = button.style.marginRight = button.style.marginTop = button.style.marginBottom = 0;
            button.style.paddingLeft = button.style.paddingRight = button.style.paddingTop = button.style.paddingBottom = 0;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 50;
            button.style.borderTopWidth = button.style.borderRightWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = 2;
            button.style.borderTopColor = button.style.borderRightColor = button.style.borderBottomColor = button.style.borderLeftColor = new Color(.99f, .98f, .90f);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        Marker Find(string buildingId)
        {
            foreach (var marker in markers) if (marker.Id == buildingId) return marker;
            return null;
        }

        public ProblemMarkerState GetMarkerState(string buildingId) => Find(buildingId)?.State ?? ProblemMarkerState.Hidden;

        void Open(string buildingId)
        {
            if (session != null && session.Phase == GamePhase.Playing && !(GameplayHud.Instance != null && GameplayHud.Instance.HasBlockingModal))
                session.OpenProblem(buildingId);
        }

        void Refresh()
        {
            if (root == null) return;
            var problems = session.Rating?.Problems;
            foreach (var marker in markers)
            {
                int count = 0, resolved = 0;
                bool urgent = false;
                tooltip.Clear();
                if (problems != null) foreach (var problem in problems)
                {
                    if (problem.BuildingId != marker.Id) continue;
                    if (count++ > 0) tooltip.Append("\n");
                    tooltip.Append(problem.Title).Append(problem.Resolved ? " — решено" : problem.Urgent ? " — срочно" : " — требует решения");
                    if (problem.Resolved) resolved++;
                    else urgent |= problem.Urgent;
                }
                if (session.AdventureMode)
                {
                    var taskState = session.TaskMarker(marker.Id);
                    count = taskState == ProblemMarkerState.Hidden ? 0 : 1;
                    resolved = taskState == ProblemMarkerState.Resolved ? count : 0;
                    urgent = false; tooltip.Clear(); tooltip.Append(resolved > 0 ? "Отличная работа!" : session.ActiveTask?.Title);
                }
                if (marker.Present != (count > 0)) placementDirty = true;
                marker.Present = count > 0;
                var state = count == 0 ? ProblemMarkerState.Hidden : resolved == count ? ProblemMarkerState.Resolved : urgent ? ProblemMarkerState.Urgent : ProblemMarkerState.Problem;
                if (state == ProblemMarkerState.Resolved && marker.State != state && marker.State != ProblemMarkerState.Hidden)
                    marker.SuccessAt = Time.unscaledTime;
                bool styleChanged = marker.State != state;
                marker.State = state;
                if (styleChanged) marker.Button.text = state == ProblemMarkerState.Resolved ? "✓" : "!";
                marker.Button.tooltip = tooltip.Append("\nОткрыть объект (Enter / пробел)").ToString();
                if (styleChanged) marker.Button.style.backgroundColor = state == ProblemMarkerState.Resolved ? new Color(.18f, .58f, .38f) : state == ProblemMarkerState.Urgent ? new Color(.79f, .20f, .16f) : new Color(.98f, .74f, .20f);
                if (styleChanged) marker.Button.style.color = state == ProblemMarkerState.Problem ? new Color(.20f, .17f, .08f) : Color.white;
            }
        }

        void LateUpdate()
        {
            if (root == null || panel == null || sceneCamera == null) return;
            BindGeometry();
            bool active = session.Phase == GamePhase.Playing && !(GameplayHud.Instance != null && GameplayHud.Instance.HasBlockingModal);
            if (active != wasActive)
            {
                root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                wasActive = active; placementDirty = true;
            }
            if (!active) { foreach (var marker in markers) marker.Visible = false; return; }
            float scale = Mathf.Max(.1f, UserExperienceSettings.ScaleFactor);
            if (scale != lastScale) { panel.scale = scale; lastScale = scale; placementDirty = true; }
            if (screenWidth != Screen.width || screenHeight != Screen.height || safeArea != Screen.safeArea
                || viewMatrix != sceneCamera.worldToCameraMatrix || projectionMatrix != sceneCamera.projectionMatrix)
            {
                screenWidth = Screen.width; screenHeight = Screen.height; safeArea = Screen.safeArea;
                viewMatrix = sceneCamera.worldToCameraMatrix; projectionMatrix = sceneCamera.projectionMatrix;
                projectionDirty = true; placementDirty = true;
            }
            // Collider anchors belong to the static diorama. Reproject/raycast only when the
            // camera or screen changes; geometry/session notifications invalidate placement.
            if (placementDirty)
            {
                float pixels = Mathf.Clamp(Screen.width * .045f, 44, 56);
                float size = pixels / scale;
                foreach (var marker in markers) marker.Visible = false;
                foreach (var marker in markers)
                {
                    if (projectionDirty) marker.AnchorVisible = ChooseAnchor(marker, out marker.ProjectedAnchor);
                    Vector2 anchor = marker.ProjectedAnchor;
                    if (!marker.Present || !marker.AnchorVisible || !Place(marker, anchor, pixels, out Rect rect)) { Hide(marker); continue; }
                    marker.Visible = true; marker.ScreenRect = rect;
                    marker.Button.style.display = DisplayStyle.Flex;
                    marker.Button.style.left = rect.x / scale;
                    marker.Button.style.top = (Screen.height - rect.yMax) / scale;
                    marker.Button.style.width = marker.Button.style.height = size;
                    marker.Button.style.fontSize = size * .59f;
                    Vector2 center = rect.center;
                    bool displaced = (center - anchor).sqrMagnitude > 16;
                    marker.Line.style.display = displaced ? DisplayStyle.Flex : DisplayStyle.None;
                    if (displaced)
                    {
                        Vector2 from = new Vector2(anchor.x, Screen.height - anchor.y) / scale;
                        Vector2 to = new Vector2(center.x, Screen.height - center.y) / scale;
                        if (marker.Line.From != from || marker.Line.To != to)
                        { marker.Line.From = from; marker.Line.To = to; marker.Line.MarkDirtyRepaint(); }
                    }
                }
                projectionDirty = false; placementDirty = false;
            }
            foreach (var marker in markers)
            {
                float elapsed = Time.unscaledTime - marker.SuccessAt;
                float pulse = marker.SuccessAt >= 0 && elapsed < .38f ? 1 + .13f * Mathf.Sin(elapsed / .38f * Mathf.PI) : 1;
                if (pulse == marker.LastPulse) continue;
                marker.LastPulse = pulse;
                marker.Button.style.scale = new Scale(new Vector3(pulse, pulse, 1));
            }
        }

        bool ChooseAnchor(Marker marker, out Vector2 position)
        {
            position = default;
            float best = float.MaxValue;
            foreach (var anchor in marker.Anchors)
            {
                if (anchor.Building == null) continue;
                Vector3 projected = sceneCamera.WorldToScreenPoint(anchor.Top);
                if (projected.z <= sceneCamera.nearClipPlane || projected.x < 0 || projected.x > Screen.width || projected.y < 0 || projected.y > Screen.height) continue;
                Vector3 ray = anchor.Top - sceneCamera.transform.position;
                bool occluded = Physics.Raycast(sceneCamera.transform.position, ray.normalized, out RaycastHit hit, ray.magnitude - .08f, ~0, QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(anchor.Building.transform);
                float rank = ray.sqrMagnitude + (occluded ? 1000000 : 0);
                // Identical-depth source objects receive a stable world-coordinate tie break.
                rank += anchor.Top.x * .0001f + anchor.Top.z * .00001f;
                if (rank >= best) continue;
                best = rank; position = new Vector2(projected.x, projected.y);
            }
            return best < float.MaxValue;
        }

        bool Place(Marker marker, Vector2 anchor, float size, out Rect rect)
        {
            Rect safe = Screen.safeArea;
            float gap = size + 8;
            // Stable concentric slots preserve a short, explicit connection to the source roof.
            for (int candidate = 0; candidate < 41; candidate++)
            {
                Vector2 center = anchor;
                if (candidate > 0)
                {
                    int spoke = (candidate - 1) % 8;
                    int ring = (candidate - 1) / 8 + 1;
                    float angle = (90 + spoke * 45) * Mathf.Deg2Rad;
                    center += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * gap * ring;
                }
                rect = new Rect(center.x - size / 2, center.y - size / 2, size, size);
                if (rect.xMin < safe.xMin + 3 || rect.xMax > safe.xMax - 3 || rect.yMin < safe.yMin + 3 || rect.yMax > safe.yMax - 3) continue;
                if (GameplayHud.Instance != null && GameplayHud.Instance.IsMarkerObstructed(rect)) continue;
                bool overlaps = false;
                Rect padded = new Rect(rect.x - 4, rect.y - 4, rect.width + 8, rect.height + 8);
                foreach (var other in markers)
                    if (other != marker && other.Visible && padded.Overlaps(other.ScreenRect)) { overlaps = true; break; }
                if (!overlaps) return true;
            }
            rect = default; return false;
        }

        static void Hide(Marker marker)
        {
            marker.Visible = false;
            marker.Button.style.display = marker.Line.style.display = DisplayStyle.None;
        }

        /// <param name="screenPosition">Pointer in Unity screen pixels, with bottom-left origin.</param>
        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            foreach (var marker in markers) if (marker.Visible && marker.ScreenRect.Contains(screenPosition)) return true;
            return false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (session != null) session.Changed -= Refresh;
            UserExperienceSettings.Changed -= PreferencesChanged;
            hudRoot?.UnregisterCallback<GeometryChangedEvent>(GeometryChanged);
            hudCard?.UnregisterCallback<GeometryChangedEvent>(GeometryChanged);
            contextRoot?.UnregisterCallback<GeometryChangedEvent>(GeometryChanged);
            if (documentObject != null) Destroy(documentObject);
            if (panel != null) Destroy(panel);
            if (theme != null) Destroy(theme);
        }
    }
}
