using UnityEngine;

namespace ProjectArtOfAll.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class ReadyRoomPlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform head;
        [SerializeField] private Camera playerCamera;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 3f;

        [Header("View")]
        [SerializeField, Min(0.1f)] private float eyeHeight = 1.7f;
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 2f;
        [SerializeField, Range(1f, 89f)] private float maxLookAngle = 80f;

        private CharacterController characterController;
        private float yaw;
        private float pitch;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

            if (head == null)
            {
                head = transform.Find("Head");
            }

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (head != null)
            {
                head.localPosition = new Vector3(0f, eyeHeight, 0f);
            }

            yaw = transform.eulerAngles.y;
            pitch = 0f;
            LockCursor();
        }

        private void Update()
        {
            if (head == null || playerCamera == null)
            {
                return;
            }

            UpdateView();
            UpdateMovement();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                LockCursor();
            }
        }

        private void UpdateView()
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector3 input = new Vector3(horizontal, 0f, vertical);

            if (input.sqrMagnitude < 0.0001f)
            {
                return;
            }

            input = Vector3.ClampMagnitude(input, 1f);
            Vector3 forward = Vector3.ProjectOnPlane(
                playerCamera.transform.forward,
                Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(
                playerCamera.transform.right,
                Vector3.up).normalized;
            Vector3 movement = forward * input.z + right * input.x;

            characterController.Move(movement * (moveSpeed * Time.deltaTime));

            Vector3 position = transform.position;
            position.y = 0f;
            transform.position = position;
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
