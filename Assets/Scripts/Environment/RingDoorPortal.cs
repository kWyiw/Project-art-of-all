using ProjectArtOfAll.Player;
using ProjectArtOfAll.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectArtOfAll.Environment
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class RingDoorPortal : MonoBehaviour
    {
        [Header("Ring Binding")]
        [SerializeField] private ScreenCenteredRing ring;
        [SerializeField, Range(0f, 359f)] private float angleDegrees;

        [Header("Frame")]
        [SerializeField, Min(0.1f)] private float frameWidth = 3f;
        [SerializeField, Min(0.1f)] private float frameHeight = 2f;

        [Header("Transition")]
        [SerializeField, Min(0.05f)] private float crossingDepth = 0.5f;
        [SerializeField] private string targetScene = "MainGame";

        private ReadyRoomPlayerController player;
        private float previousSide;
        private bool hasPreviousSide;
        private bool isTransitioning;

        private void OnEnable()
        {
            ApplyRingBinding();
        }

        private void OnValidate()
        {
            ApplyRingBinding();
        }

        private void Update()
        {
            ApplyRingBinding();

            if (!Application.isPlaying || isTransitioning)
            {
                return;
            }

            if (player == null)
            {
                player = FindObjectOfType<ReadyRoomPlayerController>();
            }

            if (player == null)
            {
                return;
            }

            Vector3 localPosition = transform.InverseTransformPoint(
                player.transform.position + Vector3.up * 0.9f);
            bool insideOpening =
                Mathf.Abs(localPosition.x) <= frameWidth * 0.5f &&
                localPosition.y >= 0f &&
                localPosition.y <= frameHeight;

            if (!insideOpening || Mathf.Abs(localPosition.z) > crossingDepth)
            {
                hasPreviousSide = false;
                return;
            }

            float side = localPosition.z >= 0f ? 1f : -1f;
            if (hasPreviousSide && !Mathf.Approximately(side, previousSide))
            {
                isTransitioning = true;
                GameEntryContext.SetEntryAngle(angleDegrees);
                SceneManager.LoadScene(targetScene);
                return;
            }

            previousSide = side;
            hasPreviousSide = true;
        }

        public void ApplyRingBinding()
        {
            if (ring == null)
            {
                return;
            }

            float radians = angleDegrees * Mathf.Deg2Rad;
            float radius = ring.InnerRadius;

            transform.position = new Vector3(
                Mathf.Cos(radians) * radius,
                ring.transform.position.y,
                Mathf.Sin(radians) * radius);
            transform.rotation = Quaternion.Euler(
                0f,
                90f - angleDegrees,
                0f);
        }
    }
}
