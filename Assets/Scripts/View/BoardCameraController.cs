using UnityEngine;

namespace ProjectArtOfAll.View
{
    [DisallowMultipleComponent]
    public sealed class BoardCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera boardCamera;
        [SerializeField] private Transform followTarget;

        [Header("Follow")]
        [SerializeField, Min(0.001f)] private float movementThreshold = 0.002f;
        [SerializeField, Min(0f)] private float followLingerSeconds = 0.35f;
        [SerializeField, Min(0.01f)] private float followSmoothTime = 0.12f;

        [Header("Manual View")]
        [SerializeField, Min(1f)] private float minimumZoom = 6f;
        [SerializeField, Min(1f)] private float maximumZoom = 30f;
        [SerializeField, Min(0.1f)] private float zoomStep = 1.1f;
        [SerializeField, Min(1f)] private float panLimit = 70f;

        private Vector3 previousTargetPosition;
        private Vector3 followVelocity;
        private Vector3 previousMousePosition;
        private bool hasTargetPosition;
        private bool isDragging;
        private bool explicitMovement;
        private float lastMovementTime = float.NegativeInfinity;

        private void Awake()
        {
            if (boardCamera == null)
            {
                boardCamera = GetComponent<Camera>();
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
            bool shouldFollow =
                explicitMovement ||
                Time.unscaledTime - lastMovementTime <= followLingerSeconds;

            if (shouldFollow && followTarget != null)
            {
                FollowTarget();
            }
            else
            {
                HandleManualView();
            }
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            CaptureTargetPosition();
        }

        public void SetPlayerMoving(bool moving)
        {
            explicitMovement = moving;
            if (moving)
            {
                lastMovementTime = Time.unscaledTime;
                CaptureTargetPosition();
            }
        }

        public void NotifyPlayerMoved(Transform target = null)
        {
            if (target != null)
            {
                followTarget = target;
            }

            lastMovementTime = Time.unscaledTime;
            CaptureTargetPosition();
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
                lastMovementTime = Time.unscaledTime;
            }

            previousTargetPosition = current;
        }

        private void FollowTarget()
        {
            Vector3 targetFocus = followTarget.position;
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

        private void HandleManualView()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                boardCamera.orthographicSize = Mathf.Clamp(
                    boardCamera.orthographicSize - scroll * zoomStep,
                    minimumZoom,
                    maximumZoom);
            }

            bool dragHeld = Input.GetMouseButton(2) || Input.GetMouseButton(1);
            if (dragHeld && !isDragging)
            {
                isDragging = true;
                previousMousePosition = Input.mousePosition;
                return;
            }

            if (!dragHeld)
            {
                isDragging = false;
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
