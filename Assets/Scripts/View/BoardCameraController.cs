using UnityEngine;

namespace ProjectArtOfAll.View
{
    [DisallowMultipleComponent]
    public sealed class BoardCameraController : MonoBehaviour
    {
        private const float InitialOverviewFraction = 0.2f;
        private const float OverviewFocusBlendStart = 0.78f;

        [Header("References")]
        [SerializeField] private Camera boardCamera;
        [SerializeField] private Transform followTarget;

        [Header("Follow")]
        [SerializeField, Min(0.001f)] private float movementThreshold = 0.002f;
        [SerializeField, Min(0.01f)] private float followSmoothTime = 0.12f;

        [Header("Zoom")]
        [SerializeField, Min(1f)] private float minimumZoom = 6f;
        [SerializeField, Min(1f)] private float maximumZoom = 30f;
        [SerializeField, Min(0.1f)] private float zoomStep = 1.1f;
        [SerializeField, Min(1f)] private float fullMapPadding = 1.08f;

        [Header("Manual View")]
        [SerializeField, Min(1f)] private float panLimit = 70f;

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
                boardCamera.orthographicSize = minimumZoom;
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
            else if (manualView)
            {
                ClampCameraPosition();
            }
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

            if (!viewInitialized)
            {
                boardCamera.orthographicSize = Mathf.Lerp(
                    minimumZoom,
                    fullMapOrthographicSize,
                    InitialOverviewFraction);
                viewInitialized = true;
            }
            else
            {
                boardCamera.orthographicSize = Mathf.Clamp(
                    boardCamera.orthographicSize,
                    minimumZoom,
                    fullMapOrthographicSize);
            }
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
            Vector3 groundFocus = GetGroundFocusPoint();
            Vector3 delta = new Vector3(
                targetFocus.x - groundFocus.x,
                0f,
                targetFocus.z - groundFocus.z);
            Vector3 desiredPosition = boardCamera.transform.position + delta;
            desiredPosition.y = boardCamera.transform.position.y;

            boardCamera.transform.position = Vector3.SmoothDamp(
                boardCamera.transform.position,
                desiredPosition,
                ref followVelocity,
                followSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            ClampCameraPosition();
        }

        private Vector3 ResolveFollowFocus()
        {
            Vector3 targetFocus = followTarget.position;
            if (!hasMapBounds || fullMapOrthographicSize <= minimumZoom + 0.001f)
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

            // Wheel up magnifies (smaller orthographic size); wheel down zooms out.
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
                2f * boardCamera.orthographicSize / Mathf.Max(1f, Screen.height);
            Vector3 right = Vector3.ProjectOnPlane(
                boardCamera.transform.right,
                Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(
                boardCamera.transform.forward,
                Vector3.up).normalized;
            Vector3 position = boardCamera.transform.position -
                               right * (mouseDelta.x * worldPerPixel) -
                               forward * (mouseDelta.y * worldPerPixel);
            boardCamera.transform.position = position;
            ClampCameraPosition();
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
            Transform cameraTransform = boardCamera.transform;
            Vector3 right = cameraTransform.right;
            Vector3 up = cameraTransform.up;
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
            float fittedHalfHeight = Mathf.Max(maximumUp, maximumRight / aspect);
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

        private void ClampCameraPosition()
        {
            Vector3 position = boardCamera.transform.position;
            position.x = Mathf.Clamp(position.x, -panLimit, panLimit);
            position.z = Mathf.Clamp(position.z, -panLimit, panLimit);
            boardCamera.transform.position = position;
        }
    }
}
