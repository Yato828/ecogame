using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Wattshift
{
    /// <summary>Ground-plane camera control with latched gesture ownership and bounded smoothing.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class IsoCameraController : MonoBehaviour
    {
        // Kept for existing scene serialization: limits the point being inspected, not camera position.
        public Vector2 PanHalfExtent = new Vector2(10, 10);
        public float MinimumSize = 8;
        public float MaximumSize = 24;
        public float PanSpeed = 10;
        public Vector2 CityHalfExtent = new Vector2(8, 8);
        public float ZoomSensitivity = .0015f;
        public float Smoothing = 12;

        Camera sceneCamera;
        Vector3 originalFocus, targetFocus, cameraOffset;
        float desiredSize, initialSize, framingScale = 1, effectiveMinimum, effectiveMaximum, previousAspect;
        bool mouseDragging, mouseBlocked, touchBlocked, touchWasActive;
        Vector2 previousMouse, previousTouchCenter;
        int previousTouchCount, firstTouchId, secondTouchId;
        float previousPinchDistance;
        readonly Plane ground = new Plane(Vector3.up, Vector3.zero);

        public Vector3 FocusPoint => transform.position - cameraOffset;
        public Vector3 TargetFocusPoint => targetFocus;
        public float TargetOrthographicSize => desiredSize;

        void Awake()
        {
            sceneCamera = GetComponent<Camera>();
            Ray centerRay = sceneCamera.ViewportPointToRay(new Vector3(.5f, .5f));
            originalFocus = ground.Raycast(centerRay, out float distance) ? centerRay.GetPoint(distance) : Vector3.zero;
            originalFocus.y = 0;
            targetFocus = originalFocus;
            cameraOffset = transform.position - originalFocus;
            initialSize = desiredSize = sceneCamera.orthographicSize;
            UpdateFraming();
        }

        bool OverUI(Vector2 p) => CityOverlay.Instance != null && CityOverlay.Instance.IsPointerOverPanel(p);

        void Update()
        {
            if (!Mathf.Approximately(previousAspect, sceneCamera.aspect))
            {
                UpdateFraming();
                // Resizing changes screen-to-world conversion: restart gesture baselines.
                previousTouchCount = touchWasActive ? -1 : 0;
                mouseDragging = false;
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            bool touchOwnsFrame = HandleTouch();
            if (!touchOwnsFrame) HandleMouse();
            else mouseDragging = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !touchOwnsFrame)
            {
                if (keyboard.homeKey.wasPressedThisFrame) ResetView();
                float horizontal = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                float vertical = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
                Vector3 move = Vector3.ClampMagnitude(right * horizontal + forward * vertical, 1);
                targetFocus += move * (PanSpeed * dt * desiredSize / 16f);
                if (keyboard.minusKey.isPressed) desiredSize *= Mathf.Exp(dt);
                if (keyboard.equalsKey.isPressed) desiredSize *= Mathf.Exp(-dt);
            }
            desiredSize = Mathf.Clamp(desiredSize, effectiveMinimum, effectiveMaximum);
            ClampFocus();
            float smoothing = 1 - Mathf.Exp(-Mathf.Max(1, Smoothing) * dt);
            transform.position = Vector3.Lerp(transform.position, targetFocus + cameraOffset, smoothing);
            sceneCamera.orthographicSize = Mathf.Lerp(sceneCamera.orthographicSize, desiredSize, smoothing);
        }

        void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) { mouseDragging = false; return; }
            Vector2 position = mouse.position.ReadValue();
            if (!OverUI(position))
            {
                float scroll = Mathf.Clamp(mouse.scroll.ReadValue().y, -1200, 1200);
                desiredSize *= Mathf.Exp(-scroll * ZoomSensitivity);
            }
            bool pressed = mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame;
            bool held = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            if (pressed)
            {
                mouseDragging = true;
                mouseBlocked = OverUI(position);
                previousMouse = position;
                CityOverlay.Instance?.CancelWorldPress();
            }
            else if (held && mouseDragging)
            {
                if (!mouseBlocked) PanScreenPoints(previousMouse, position);
                previousMouse = position;
            }
            if (!held) mouseDragging = false;
        }

        bool HandleTouch()
        {
            Touchscreen screen = Touchscreen.current;
            TouchControl first = null, second = null;
            int count = 0;
            if (screen != null)
            {
                foreach (TouchControl touch in screen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    if (count == 0) first = touch;
                    else if (count == 1) second = touch;
                    count++;
                }
            }
            bool ownsFrame = count > 0 || touchWasActive;
            touchWasActive = count > 0;
            if (count == 0)
            {
                previousTouchCount = 0;
                touchBlocked = false;
                return ownsFrame;
            }
            Vector2 p1 = first.position.ReadValue();
            Vector2 p2 = second != null ? second.position.ReadValue() : p1;
            Vector2 center = second != null ? (p1 + p2) * .5f : p1;
            float distance = second != null ? Vector2.Distance(p1, p2) : 0;
            int id1 = first.touchId.ReadValue(), id2 = second != null ? second.touchId.ReadValue() : -1;
            bool topologyChanged = count != previousTouchCount || id1 != firstTouchId || id2 != secondTouchId;
            if (previousTouchCount == 0) touchBlocked = OverUI(p1);
            if (topologyChanged)
            {
                if (second != null && OverUI(p2)) touchBlocked = true;
                previousTouchCenter = center;
                previousPinchDistance = distance;
                previousTouchCount = count;
                firstTouchId = id1; secondTouchId = id2;
            }
            else if (!touchBlocked)
            {
                PanScreenPoints(previousTouchCenter, center);
                if (second != null && distance >= 20 && previousPinchDistance >= 20)
                {
                    // Bound a single sample to avoid teleporting on a malformed/lost touch event.
                    desiredSize *= Mathf.Clamp(previousPinchDistance / distance, .8f, 1.25f);
                }
                previousTouchCenter = center;
                previousPinchDistance = distance;
            }
            if (count > 1) CityOverlay.Instance?.CancelWorldPress();
            return true;
        }

        void PanScreenPoints(Vector2 before, Vector2 after)
        {
            // Project both points through the same camera; this compensates for isometric tilt.
            // A cursor warp outside the window is not a meaningful drag sample.
            if ((after - before).sqrMagnitude > Mathf.Pow(Mathf.Max(Screen.width, Screen.height) * .5f, 2)) return;
            Ray rayBefore = sceneCamera.ScreenPointToRay(before);
            Ray rayAfter = sceneCamera.ScreenPointToRay(after);
            if (ground.Raycast(rayBefore, out float d1) && ground.Raycast(rayAfter, out float d2))
                targetFocus += rayBefore.GetPoint(d1) - rayAfter.GetPoint(d2);
        }

        void ClampFocus()
        {
            float x = Mathf.Max(0, Mathf.Min(Mathf.Abs(PanHalfExtent.x), Mathf.Abs(CityHalfExtent.x)));
            float z = Mathf.Max(0, Mathf.Min(Mathf.Abs(PanHalfExtent.y), Mathf.Abs(CityHalfExtent.y)));
            targetFocus.x = Mathf.Clamp(targetFocus.x, -x, x);
            targetFocus.z = Mathf.Clamp(targetFocus.z, -z, z);
            targetFocus.y = 0;
        }

        void UpdateFraming()
        {
            float zoomRatio = desiredSize / Mathf.Max(initialSize * framingScale, .01f);
            previousAspect = sceneCamera.aspect;
            float baseline = Mathf.Min(28, initialSize / Mathf.Min(1, Mathf.Max(previousAspect, .1f)));
            framingScale = baseline / Mathf.Max(initialSize, .01f);
            effectiveMinimum = Mathf.Max(.1f, MinimumSize * framingScale);
            effectiveMaximum = Mathf.Max(effectiveMinimum, MaximumSize * framingScale);
            desiredSize = Mathf.Clamp(baseline * zoomRatio, effectiveMinimum, effectiveMaximum);
        }

        public void ResetView()
        {
            targetFocus = originalFocus;
            ClampFocus();
            transform.position = targetFocus + cameraOffset;
            desiredSize = initialSize * framingScale;
            sceneCamera.orthographicSize = desiredSize;
            mouseDragging = false;
            previousTouchCount = 0;
        }

        void OnDisable()
        {
            mouseDragging = false;
            previousTouchCount = 0;
            touchWasActive = false;
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            mouseDragging = false;
            previousTouchCount = touchWasActive ? -1 : 0;
            touchBlocked = touchWasActive;
            CityOverlay.Instance?.CancelWorldPress();
        }
    }
}
