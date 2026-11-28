using ProjectArtOfAll.Board;
using UnityEngine;

namespace ProjectArtOfAll.View
{
    [DisallowMultipleComponent]
    public sealed class BoardCameraController : MonoBehaviour
    {
        public const float BaseFullMapPadding = 1.08f;
        public const float MaximumViewHeightMultiplier = 1.2f;
        public const float MinimumZoom = 2.8f;
        public const float CloseUpCameraHeight = 8f;
        public const float CloseUpCameraPitch = 30f;
        public const float CloseUpFocusHeight = 2f;
        private const float OverviewFocusBlendStart = 0.78f;

        [Header("References")]
        [SerializeField] private Camera boardCamera;
        [SerializeField] private Transform followTarget;

        [Header("Follow")]
        [SerializeField, Min(0.001f)] private float movementThreshold = 0.002f;
        [SerializeField, Min(0.01f)] private float followSmoothTime = 0.12f;

        [Header("Zoom")]
        [SerializeField, Min(1f)]
        private float minimumZoom = MinimumZoom;
        [SerializeField, Min(1f)]
        private float maximumZoom =
            30f * BoardMapBuilder.TileScaleMultiplier * MaximumViewHeightMultiplier;
        [SerializeField, Min(0.1f)] private float zoomStep = 1.1f;
        [SerializeField, Min(1f)]
        private float fullMapPadding =
            BaseFullMapPadding * MaximumViewHeightMultiplier;

        [Header("Close-up Camera")]
        [SerializeField, Min(0.1f)]
        private float minimumCameraHeight = CloseUpCameraHeight;
        [SerializeField, Range(5f, 85f)]
        private float minimumCameraPitch = CloseUpCameraPitch;
        [SerializeField, Min(0f)]
        private float minimumFocusHeight = CloseUpFocusHeight;
        [SerializeField, Min(0.01f)] private float closeUpSmoothTime = 0.18f;

        [Header("Manual View")]
        [SerializeField, Min(1f)]
        private float panLimit =
            70f * BoardMapBuilder.TileScaleMultiplier * MaximumViewHeightMultiplier;

        private Vector3 previousTargetPosition;
        private Vector3 followVelocity;
        private Vector3 previousMousePosition;
        private Bounds mapBounds;
        private bool hasTargetPosition;
        private bool hasMapBounds;
        private bool isDragging;
        private bool manualView;
        private bool viewInitialized;
        private float fullMapOrthographicSize;
        private float overviewHeight;
        private float overviewPitch;
        private float overviewYaw;
        private float closeUpBlend;
        private float closeUpVelocity;
        private Vector3 groundFocus;

        private void Awake()
        {
            if (boardCamera == null)
            {
                boardCamera = GetComponent<Camera>();
            }

            minimumZoom = Mathf.Max(0.1f, minimumZoom);
            zoomStep = Mathf.Max(1.01f, zoomStep);
            fullMapOrthographicSize = Mathf.Max(minimumZoom, maximumZoom);
            if (boardCamera != null)
            {
                Vector3 overviewEuler = boardCamera.transform.eulerAngles;
                overviewHeight = boardCamera.transform.position.y;
                overviewPitch = overviewEuler.x;
                overviewYaw = overviewEuler.y;
                minimumCameraHeight = Mathf.Clamp(
                    minimumCameraHeight,
                    0.1f,
                    Mathf.Max(0.1f, overviewHeight));
                minimumCameraPitch = Mathf.Clamp(
                    minimumCameraPitch,
                    5f,
                    Mathf.Max(5f, overviewPitch));
                boardCamera.orthographic = true;
                boardCamera.orthographicSize = minimumZoom;
                groundFocus = GetGroundFocusPoint();
            }

            CaptureTargetPosition();
        }

        private void LateUpdate()
        {
            if (boardCamera == null)
            {
                return;
            }

            DetectTargetMovement();
            RefreshZoomLimits();
            HandleZoomInput();
            HandleManualDrag();

            if (!manualView && followTarget != null)
            {
                FollowTarget();
            }

            ApplyZoomView();
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            manualView = target == null;
            CaptureTargetPosition();
        }

        public void SetPlayerMoving(bool moving)
        {
            if (moving)
            {
                manualView = false;
                CaptureTargetPosition();
            }
        }

        public void NotifyPlayerMoved(Transform target = null)
        {
            if (target != null)
            {
                followTarget = target;
            }

            manualView = followTarget == null;
            CaptureTargetPosition();
        }

        /// <summary>
        /// Binds the visible board's world-space bounds. The controller uses them to derive
        /// the zoom-out limit, so Map1 and Map2 can have different footprints without
        /// hard-coded orthographic sizes.
        /// </summary>
        public void SetMapBounds(Bounds bounds)
        {
            mapBounds = bounds;
            hasMapBounds = bounds.size.sqrMagnitude > 0.000001f;
            RefreshZoomLimits();

            if (boardCamera == null || !hasMapBounds)
            {
                return;
            }

            boardCamera.orthographicSize = viewInitialized
                ? Mathf.Clamp(
                    boardCamera.orthographicSize,
                    minimumZoom,
                    fullMapOrthographicSize)
                : fullMapOrthographicSize;
            viewInitialized = true;
        }

        private void DetectTargetMovement()
        {
            if (followTarget == null)
            {
                hasTargetPosition = false;
                return;
            }

            if (!hasTargetPosition)
            {
                CaptureTargetPosition();
                return;
            }

            Vector3 current = followTarget.position;
            Vector3 delta = current - previousTargetPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude >= movementThreshold * movementThreshold)
            {
                // Moving the piece takes back control from a temporary manual pan.
                manualView = false;
            }

            previousTargetPosition = current;
        }

        private void FollowTarget()
        {
            Vector3 targetFocus = ResolveFollowFocus();
            MoveGroundFocus(targetFocus);
        }

        private void MoveGroundFocus(Vector3 targetFocus)
        {
            Vector3 desiredFocus = new Vector3(
                targetFocus.x,
                0f,
                targetFocus.z);
            groundFocus = Vector3.SmoothDamp(
                groundFocus,
                desiredFocus,
                ref followVelocity,
                followSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            ClampGroundFocus();
        }

        private Vector3 ResolveFollowFocus()
        {
            Vector3 targetFocus = followTarget.position;
            if (!hasMapBounds ||
                fullMapOrthographicSize <= minimumZoom + 0.001f)
            {
                return targetFocus;
            }

            float zoomProgress = Mathf.InverseLerp(
                minimumZoom,
                fullMapOrthographicSize,
                boardCamera.orthographicSize);
            float overviewWeight = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(OverviewFocusBlendStart, 1f, zoomProgress));
            targetFocus.x = Mathf.Lerp(targetFocus.x, mapBounds.center.x, overviewWeight);
            targetFocus.z = Mathf.Lerp(targetFocus.z, mapBounds.center.z, overviewWeight);
            return targetFocus;
        }

        private void HandleZoomInput()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) <= 0.01f)
            {
                return;
            }

            // Wheel up magnifies by reducing the orthographic size.
            float requestedSize = boardCamera.orthographicSize *
                                  Mathf.Pow(zoomStep, -scroll);
            boardCamera.orthographicSize = Mathf.Clamp(
                requestedSize,
                minimumZoom,
                fullMapOrthographicSize);
        }

        private void HandleManualDrag()
        {
            bool dragHeld = Input.GetMouseButton(2) || Input.GetMouseButton(1);
            if (dragHeld && !isDragging)
            {
                isDragging = true;
                manualView = true;
                previousMousePosition = Input.mousePosition;
                return;
            }

            if (!dragHeld)
            {
                isDragging = false;
                return;
            }

            if (!manualView)
            {
                return;
            }

            Vector3 currentMouse = Input.mousePosition;
            Vector3 mouseDelta = currentMouse - previousMousePosition;
            previousMousePosition = currentMouse;

            float worldPerPixel =
                2f * boardCamera.orthographicSize /
                Mathf.Max(1f, Screen.height);
            Vector3 right = Vector3.ProjectOnPlane(
                boardCamera.transform.right,
                Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(
                boardCamera.transform.forward,
                Vector3.up).normalized;
            groundFocus -= right * (mouseDelta.x * worldPerPixel);
            groundFocus -= forward * (mouseDelta.y * worldPerPixel);
            groundFocus.y = 0f;
            ClampGroundFocus();
        }

        private void RefreshZoomLimits()
        {
            if (boardCamera == null)
            {
                return;
            }

            if (!hasMapBounds)
            {
                fullMapOrthographicSize = Mathf.Max(minimumZoom, maximumZoom);
            }
            else
            {
                fullMapOrthographicSize = CalculateFullMapOrthographicSize();
            }

            boardCamera.orthographicSize = Mathf.Clamp(
                boardCamera.orthographicSize,
                minimumZoom,
                fullMapOrthographicSize);
        }

        private float CalculateFullMapOrthographicSize()
        {
            Quaternion overviewRotation = Quaternion.Euler(
                overviewPitch,
                overviewYaw,
                0f);
            Vector3 right = overviewRotation * Vector3.right;
            Vector3 up = overviewRotation * Vector3.up;
            Vector3 center = mapBounds.center;
            float maximumRight = 0f;
            float maximumUp = 0f;

            Vector3 min = mapBounds.min;
            Vector3 max = mapBounds.max;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 offset = corner - center;
                        maximumRight = Mathf.Max(
                            maximumRight,
                            Mathf.Abs(Vector3.Dot(offset, right)));
                        maximumUp = Mathf.Max(
                            maximumUp,
                            Mathf.Abs(Vector3.Dot(offset, up)));
                    }
                }
            }

            float aspect = Mathf.Max(0.1f, boardCamera.aspect);
            float fittedHalfHeight = Mathf.Max(
                maximumUp,
                maximumRight / aspect);
            return Mathf.Max(
                minimumZoom,
                fittedHalfHeight * Mathf.Max(1f, fullMapPadding));
        }

        private void CaptureTargetPosition()
        {
            if (followTarget == null)
            {
                hasTargetPosition = false;
                return;
            }

            previousTargetPosition = followTarget.position;
            hasTargetPosition = true;
        }

        private void ApplyZoomView()
        {
            if (!hasMapBounds ||
                fullMapOrthographicSize <= minimumZoom + 0.001f)
            {
                closeUpBlend = 0f;
                closeUpVelocity = 0f;
                return;
            }

            float targetBlend = 1f - Mathf.InverseLerp(
                minimumZoom,
                fullMapOrthographicSize,
                boardCamera.orthographicSize);
            closeUpBlend = Mathf.SmoothDamp(
                closeUpBlend,
                targetBlend,
                ref closeUpVelocity,
                closeUpSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            float easedBlend = Mathf.SmoothStep(0f, 1f, closeUpBlend);

            float cameraHeight = Mathf.Lerp(
                overviewHeight,
                minimumCameraHeight,
                easedBlend);
            float cameraPitch = Mathf.Lerp(
                overviewPitch,
                minimumCameraPitch,
                easedBlend);
            Quaternion rotation = Quaternion.Euler(
                cameraPitch,
                overviewYaw,
                0f);
            Vector3 focus = groundFocus;
            focus.y = Mathf.Lerp(0f, minimumFocusHeight, easedBlend);

            Vector3 forward = rotation * Vector3.forward;
            float downward = Mathf.Max(0.001f, -forward.y);
            float distance = (cameraHeight - focus.y) / downward;
            boardCamera.transform.SetPositionAndRotation(
                focus - forward * distance,
                rotation);
        }

        private Vector3 GetGroundFocusPoint()
        {
            Transform cameraTransform = boardCamera.transform;
            float forwardY = cameraTransform.forward.y;
            if (forwardY >= -0.0001f)
            {
                return cameraTransform.position +
                       Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            }

            float distanceToGround = cameraTransform.position.y / -forwardY;
            Vector3 focus = cameraTransform.position +
                            cameraTransform.forward * distanceToGround;
            focus.y = 0f;
            return focus;
        }

        private void ClampGroundFocus()
        {
            groundFocus.x = Mathf.Clamp(groundFocus.x, -panLimit, panLimit);
            groundFocus.z = Mathf.Clamp(groundFocus.z, -panLimit, panLimit);
            groundFocus.y = 0f;
        }
    }
}
