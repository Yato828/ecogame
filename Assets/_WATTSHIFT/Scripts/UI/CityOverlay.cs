using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Wattshift
{
    /// <summary>Runtime UI Toolkit presentation and building selection for the visual gate.</summary>
    public sealed class CityOverlay : MonoBehaviour
    {
        public Camera SceneCamera;
        public static CityOverlay Instance { get; private set; }
        public CityBuilding SelectedBuilding { get; private set; }
        public event System.Action<CityBuilding> SelectionChanged;
        public VisualElement ContextRoot => context;
        public bool GameplayMode { get; private set; }
        bool contextCompact;
        bool expandedCard;
        public bool ContextExpanded => expandedCard;
        public Rect ContextScreenBounds
        {
            get
            {
                if (context == null || SelectedBuilding == null) return Rect.zero;
                Rect bounds = context.worldBound;
                float scale = settings != null ? settings.scale : 1;
                return new Rect(bounds.x * scale, Screen.height - bounds.yMax * scale, bounds.width * scale, bounds.height * scale);
            }
        }

        UIDocument document;
        PanelSettings settings;
        VisualElement root, header, badge, context, instructions, safeRoot;
        Label title, category, description, protection, countLabel, brandTitle, brandSubtitle, contextEyebrow, contextFooter;
        VisualElement contextBar;
        Button closeButton;
        Vector2 pressPosition;
        bool pressedOverPanel, trackingPress;
        bool touchSelectionGesture, touchSelectionCancelled;
        int selectionTouchId;
        Vector2 lastTouchPosition;
        CityBuilding[] keyboardBuildings;
        int previousWidth, previousHeight;
        Rect previousSafeArea;
        readonly Color ink = new Color(0.075f, 0.16f, 0.17f);
        readonly Color muted = new Color(0.35f, 0.43f, 0.43f);
        readonly Color accent = new Color(0.16f, 0.40f, 0.32f);

        void OnEnable()
        {
            Instance = this;
            if (SceneCamera == null) SceneCamera = Camera.main;
            expandedCard = UserExperienceSettings.ExpandedCard;
            BuildInterface();
            UserExperienceSettings.Changed += PreferencesChanged;
        }

        void OnDisable()
        {
            UserExperienceSettings.Changed -= PreferencesChanged;
            if (Instance == this) Instance = null;
            if (SelectedBuilding != null) SelectedBuilding.SetSelected(false);
            SelectedBuilding = null;
            if (document != null) Destroy(document);
            if (settings != null) Destroy(settings);
        }

        void BuildInterface()
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "WATTSHIFT Runtime Panel";
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.sortingOrder = 20;
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("WattshiftRuntimeTheme");
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            root.style.color = ink;
            root.style.unityFontDefinition = FontDefinition.FromFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                // Tab belongs to world inspection; Escape remains available to close the card.
                if (evt.keyCode != KeyCode.Tab) return;
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            safeRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            safeRoot.style.position = Position.Absolute;
            root.Add(safeRoot);

            header = Card();
            header.style.position = Position.Absolute;
            header.style.left = 26;
            header.style.top = 24;
            header.style.paddingLeft = header.style.paddingRight = 23;
            header.style.paddingTop = 17;
            header.style.paddingBottom = 17;
            brandTitle = Text("WATTSHIFT", 26, ink, true);
            header.Add(brandTitle);
            Label subtitle = Text("ЭНЕРГИЯ ГОРОДА", 10, muted, true);
            brandSubtitle = subtitle;
            subtitle.style.letterSpacing = 2.6f;
            subtitle.style.marginTop = 3;
            header.Add(subtitle);
            safeRoot.Add(header);

            badge = Card();
            badge.pickingMode = PickingMode.Ignore;
            badge.style.position = Position.Absolute;
            badge.style.right = 26;
            badge.style.top = 24;
            badge.style.paddingLeft = badge.style.paddingRight = 18;
            badge.style.paddingTop = badge.style.paddingBottom = 14;
            badge.Add(Text("01  /  ГОРОДСКОЙ КВАРТАЛ", 10, accent, true));
            countLabel = Text("Визуальный срез", 13, ink, false);
            countLabel.style.marginTop = 4;
            badge.Add(countLabel);
            safeRoot.Add(badge);

            instructions = Card();
            instructions.style.position = Position.Absolute;
            instructions.style.left = 26;
            instructions.style.bottom = 24;
            instructions.style.paddingLeft = instructions.style.paddingRight = 20;
            instructions.style.paddingTop = instructions.style.paddingBottom = 16;
            instructions.Add(Text("ИССЛЕДУЙТЕ КВАРТАЛ", 10, accent, true));
            Label hint = Text("Выберите здание, чтобы узнать его роль", 14, ink, false);
            hint.style.marginTop = 6;
            instructions.Add(hint);
            Label controls = Text("Колесо — масштаб · WASD — обзор · Tab — здания\nКасание — выбор · жесты — обзор и масштаб", 11, muted, false);
            controls.style.marginTop = 9;
            controls.style.whiteSpace = WhiteSpace.Normal;
            controls.style.maxWidth = 345;
            instructions.Add(controls);
            safeRoot.Add(instructions);

            context = Card();
            context.style.position = Position.Absolute;
            context.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName>
            {
                new StylePropertyName("width"), new StylePropertyName("height")
            };
            context.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { new TimeValue(.16f, TimeUnit.Second) };
            context.style.transitionTimingFunction = new System.Collections.Generic.List<EasingFunction> { new EasingFunction(EasingMode.EaseOut) };
            context.style.right = 26;
            context.style.bottom = 24;
            context.style.width = 350;
            context.style.paddingLeft = context.style.paddingRight = 24;
            context.style.paddingTop = context.style.paddingBottom = 22;
            context.style.display = DisplayStyle.None;
            VisualElement bar = new VisualElement();
            contextBar = bar;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.style.alignItems = Align.Center;
            contextEyebrow = Text("ОБЪЕКТ КВАРТАЛА", 10, accent, true);
            bar.Add(contextEyebrow);
            Button close = new Button(() => Select(null)) { text = "×", tooltip = "Закрыть карточку (Esc)", tabIndex = 0 };
            closeButton = close;
            close.style.width = close.style.height = 44;
            close.style.marginTop = -10;
            close.style.marginRight = -12;
            close.style.fontSize = 26;
            close.style.color = muted;
            close.style.backgroundColor = Color.clear;
            close.style.borderTopWidth = close.style.borderRightWidth = close.style.borderBottomWidth = close.style.borderLeftWidth = 0;
            bar.Add(close);
            bar.style.flexShrink = 0;
            context.Add(bar);
            category = Text("", 12, muted, false);
            category.style.marginTop = 8;
            context.Add(category);
            title = Text("", 27, ink, true);
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.marginTop = 3;
            context.Add(title);
            protection = Text("", 11, accent, true);
            protection.style.marginTop = 15;
            protection.style.whiteSpace = WhiteSpace.Normal;
            context.Add(protection);
            description = Text("", 14, muted, false);
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginTop = 14;
            context.Add(description);
            Label stage = Text("Осмотрите здания, парк и безопасные пешеходные маршруты.", 11, muted, false);
            contextFooter = stage;
            stage.style.whiteSpace = WhiteSpace.Normal;
            stage.style.marginTop = 20;
            stage.style.borderTopWidth = 1;
            stage.style.borderTopColor = new Color(0.8f, 0.84f, 0.80f);
            stage.style.paddingTop = 14;
            context.Add(stage);
            safeRoot.Add(context);
            UpdateLayout();
        }

        VisualElement Card()
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.97f, 0.97f, 0.93f, 0.97f);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = 15;
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 15;
            card.style.borderTopWidth = card.style.borderRightWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = 1;
            Color edge = new Color(1, 1, 1, 0.65f);
            card.style.borderTopColor = card.style.borderRightColor = card.style.borderBottomColor = card.style.borderLeftColor = edge;
            return card;
        }

        static Label Text(string text, int size, Color color, bool bold)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.marginLeft = label.style.marginRight = 0;
            label.style.marginTop = label.style.marginBottom = 0;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            return label;
        }

        void UpdateLayout()
        {
            previousWidth = Screen.width;
            previousHeight = Screen.height;
            previousSafeArea = Screen.safeArea;
            bool portrait = Screen.width < Screen.height;
            settings.scale = UserExperienceSettings.EffectiveScale;
            float scale = Mathf.Max(settings.scale, 0.1f);
            Rect safe = Screen.safeArea;
            safeRoot.style.left = safe.xMin / scale;
            safeRoot.style.right = (Screen.width - safe.xMax) / scale;
            safeRoot.style.top = (Screen.height - safe.yMax) / scale;
            safeRoot.style.bottom = safe.yMin / scale;
            context.style.left = portrait ? new StyleLength(12) : new StyleLength(StyleKeyword.Auto);
            float availableWidth = safe.width / scale;
            float availableHeight = safe.height / scale;
            context.style.right = portrait ? 12 : 26;
            context.style.width = portrait ? availableWidth - 24 : Mathf.Min(expandedCard ? 540 : 350, Mathf.Max(280, availableWidth - 390));
            context.style.bottom = portrait ? 12 : 24;
            context.style.top = new StyleLength(StyleKeyword.Auto);
            context.style.maxHeight = portrait ? (expandedCard ? availableHeight - 24 : availableHeight * .44f) : availableHeight * .75f;
            context.style.height = expandedCard ? (portrait ? availableHeight - 24 : availableHeight * .75f) : Mathf.Min(portrait ? 208 : 260, availableHeight * .44f);
            context.style.paddingLeft = context.style.paddingRight = portrait ? 16 : 24;
            context.style.paddingTop = context.style.paddingBottom = portrait ? 16 : 22;
            contextBar.style.height = portrait ? 0 : new StyleLength(StyleKeyword.Auto);
            contextEyebrow.style.display = portrait ? DisplayStyle.None : DisplayStyle.Flex;
            closeButton.style.position = portrait ? Position.Absolute : Position.Relative;
            closeButton.style.top = portrait ? new StyleLength(0f) : new StyleLength(StyleKeyword.Auto);
            closeButton.style.right = portrait ? new StyleLength(0f) : new StyleLength(StyleKeyword.Auto);
            closeButton.style.marginTop = portrait ? 0 : -10;
            closeButton.style.marginRight = portrait ? 0 : -12;
            title.style.fontSize = portrait ? 22 : 27;
            title.style.paddingRight = portrait ? 46 : 0;
            category.style.fontSize = portrait ? 10 : 12;
            category.style.marginTop = portrait ? 0 : 8;
            protection.style.fontSize = portrait ? 10 : 11;
            protection.style.marginTop = portrait ? 8 : 15;
            description.style.fontSize = portrait ? 12 : 14;
            description.style.display = GameplayMode || contextCompact ? DisplayStyle.None : DisplayStyle.Flex;
            description.style.marginTop = portrait ? 8 : 14;
            contextFooter.style.display = portrait || GameplayMode ? DisplayStyle.None : DisplayStyle.Flex;
            if (SelectedBuilding != null) description.text = portrait ? CompactDescription(SelectedBuilding.Description) : SelectedBuilding.Description;
            instructions.style.display = GameplayMode || (portrait && SelectedBuilding != null) ? DisplayStyle.None : DisplayStyle.Flex;
            instructions.style.right = portrait ? new StyleLength(26) : new StyleLength(StyleKeyword.Auto);
            instructions.style.maxWidth = portrait ? new StyleLength(StyleKeyword.None) : new StyleLength(410);
            badge.style.display = portrait || GameplayMode ? DisplayStyle.None : DisplayStyle.Flex;
            brandTitle.style.fontSize = portrait ? 18 : 26;
            brandSubtitle.style.display = portrait ? DisplayStyle.None : DisplayStyle.Flex;
            header.style.display = GameplayMode ? DisplayStyle.None : DisplayStyle.Flex;
            header.style.paddingTop = header.style.paddingBottom = portrait ? 12 : 17;
            header.style.paddingLeft = header.style.paddingRight = portrait ? 16 : 23;
            float closeSize = Mathf.Max(44, 44 / scale);
            closeButton.style.width = closeButton.style.height = closeSize;
            category.style.display = contextCompact ? DisplayStyle.None : DisplayStyle.Flex;
            protection.style.display = contextCompact ? DisplayStyle.None : DisplayStyle.Flex;
            contextEyebrow.style.display = portrait || contextCompact ? DisplayStyle.None : DisplayStyle.Flex;
        }

        static string CompactDescription(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int sentence = text.IndexOf('.');
            if (sentence > 0) text = text.Substring(0, sentence + 1);
            if (text.Length <= 120) return text;
            int end = text.LastIndexOf(' ', 116);
            return text.Substring(0, end > 0 ? end : 116) + "…";
        }

        public bool IsPointerOverPanel(Vector2 screenPosition)
        {
            if (ProblemMarkers.Instance != null && ProblemMarkers.Instance.IsPointerOverUI(screenPosition)) return true;
            if (GameplayHud.Instance != null && GameplayHud.Instance.IsPointerOverUI(screenPosition)) return true;
            if (root == null || root.panel == null) return false;
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return (header.style.display == DisplayStyle.Flex && header.worldBound.Contains(p))
                || (badge.style.display == DisplayStyle.Flex && badge.worldBound.Contains(p))
                || (instructions.style.display == DisplayStyle.Flex && instructions.worldBound.Contains(p))
                || (context.style.display == DisplayStyle.Flex && context.worldBound.Contains(p));
        }

        public void Select(CityBuilding building)
        {
            if (SelectedBuilding != null) SelectedBuilding.SetSelected(false);
            SelectedBuilding = building;
            if (context == null) return;
            context.style.display = building != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (building == null) { UpdateLayout(); SelectionChanged?.Invoke(null); return; }
            building.SetSelected(true);
            title.text = building.Title;
            category.text = building.Category;
            description.text = building.Description;
            protection.text = building.Essential ? "●  ЗАЩИЩЁННЫЙ ПОТРЕБИТЕЛЬ" : "●  ОБЪЕКТ ГОРОДСКОЙ СРЕДЫ";
            UpdateLayout();
            SelectionChanged?.Invoke(building);
        }

        void Update()
        {
            if (previousWidth != Screen.width || previousHeight != Screen.height || previousSafeArea != Screen.safeArea) UpdateLayout();
            if (GameSession.Instance != null && GameSession.Instance.Phase != GamePhase.Playing) return;
            if (GameplayHud.Instance != null && GameplayHud.Instance.HasBlockingModal) { CancelWorldPress(); return; }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Select(null);
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                CycleSelection(Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed ? -1 : 1);
            Touchscreen touchscreen = Touchscreen.current;
            int activeTouches = 0;
            UnityEngine.InputSystem.Controls.TouchControl first = null, tracked = null;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (touchSelectionGesture && touch.touchId.ReadValue() == selectionTouchId) tracked = touch;
                    if (!touch.press.isPressed) continue;
                    if (first == null) first = touch;
                    activeTouches++;
                }
            }
            if (activeTouches > 0)
            {
                if (!touchSelectionGesture)
                {
                    touchSelectionGesture = true;
                    touchSelectionCancelled = false;
                    selectionTouchId = first.touchId.ReadValue();
                    lastTouchPosition = first.position.ReadValue();
                    tracked = first;
                    BeginPress(lastTouchPosition);
                }
                if (activeTouches > 1 || tracked == null || !tracked.press.isPressed)
                {
                    touchSelectionCancelled = true;
                    CancelWorldPress();
                }
                if (tracked != null)
                {
                    lastTouchPosition = tracked.position.ReadValue();
                    CancelDraggedPress(lastTouchPosition);
                }
                return; // A touch gesture owns this frame; ignore synthetic mouse events.
            }
            if (touchSelectionGesture)
            {
                if (tracked != null) lastTouchPosition = tracked.position.ReadValue();
                if (!touchSelectionCancelled && tracked != null && tracked.phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.Canceled)
                    EndPress(lastTouchPosition);
                else CancelWorldPress();
                touchSelectionGesture = false;
                return; // Release events must not fall through into mouse handling.
            }
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
            {
                CancelWorldPress();
                return;
            }
            if (mouse.leftButton.wasPressedThisFrame) BeginPress(mouse.position.ReadValue());
            if (mouse.leftButton.isPressed) CancelDraggedPress(mouse.position.ReadValue());
            if (mouse.leftButton.wasReleasedThisFrame) EndPress(mouse.position.ReadValue());
        }

        public void SetGameplayMode(bool enabled)
        {
            GameplayMode = enabled;
            if (root != null) UpdateLayout();
        }

        void PreferencesChanged()
        {
            expandedCard = UserExperienceSettings.ExpandedCard;
            if (root != null) UpdateLayout();
        }

        public void SetExpandedCard(bool expanded)
        {
            if (expandedCard == expanded) return;
            expandedCard = expanded;
            UserExperienceSettings.ExpandedCard = expanded;
            if (root != null) UpdateLayout();
        }

        public void SetContextCompact(bool compact)
        {
            if (contextCompact == compact) return;
            contextCompact = compact;
            if (root != null) UpdateLayout();
        }

        public void CancelWorldPress() => trackingPress = false;

        void CycleSelection(int direction)
        {
            if (keyboardBuildings == null)
            {
                keyboardBuildings = FindObjectsByType<CityBuilding>(FindObjectsSortMode.None);
                System.Array.Sort(keyboardBuildings, (a, b) => string.Compare(a.Title, b.Title, System.StringComparison.Ordinal));
            }
            if (keyboardBuildings.Length == 0) return;
            int index = System.Array.IndexOf(keyboardBuildings, SelectedBuilding);
            if (index < 0) index = direction > 0 ? 0 : keyboardBuildings.Length - 1;
            else index = (index + direction + keyboardBuildings.Length) % keyboardBuildings.Length;
            Select(keyboardBuildings[index]);
        }

        void CancelDraggedPress(Vector2 p)
        {
            if (trackingPress && Vector2.Distance(p, pressPosition) > Mathf.Max(12, 8 * settings.scale)) trackingPress = false;
        }

        void BeginPress(Vector2 p)
        {
            pressPosition = p;
            pressedOverPanel = IsPointerOverPanel(p);
            trackingPress = true;
        }

        void EndPress(Vector2 p)
        {
            if (!trackingPress) return;
            trackingPress = false;
            if (pressedOverPanel || IsPointerOverPanel(p) || Vector2.Distance(p, pressPosition) > Mathf.Max(12, 8 * settings.scale) || SceneCamera == null) return;
            if (Physics.Raycast(SceneCamera.ScreenPointToRay(p), out RaycastHit hit, 500)) Select(hit.collider.GetComponentInParent<CityBuilding>());
            else Select(null);
        }
    }
}
