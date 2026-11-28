using System.Collections;
using UnityEngine;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// StartMenu 常驻背景音乐，独立于菜单悬停音效，负责循环播放和淡入。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class StartMenuBgm : MonoBehaviour
    {
        private const string ResourcePath = "StartMenu/StartMenuBgm";
        private const float TargetVolume = 0.65f;
        private const float FadeInDuration = 1.2f;

        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip clip;

        private Coroutine fadeRoutine;

        public AudioSource Source => source;
        public AudioClip Clip => clip;
        public float ConfiguredVolume => TargetVolume;

        private void Awake()
        {
            EnsureConfigured();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                Play();
            }
        }

        public void EnsureConfigured()
        {
            if (source == null)
            {
                source = GetComponent<AudioSource>();
            }

            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }

            if (clip == null)
            {
                clip = Resources.Load<AudioClip>(ResourcePath);
            }

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = TargetVolume;
            source.clip = clip;
        }

        public void Play()
        {
            EnsureConfigured();
            if (!Application.isPlaying ||
                source == null ||
                clip == null ||
                (source.isPlaying && source.clip == clip))
            {
                return;
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            source.Stop();
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.Play();
            fadeRoutine = StartCoroutine(FadeInRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            float elapsed = 0f;
            while (elapsed < FadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(
                    0f,
                    TargetVolume,
                    Mathf.Clamp01(elapsed / FadeInDuration));
                yield return null;
            }

            source.volume = TargetVolume;
            fadeRoutine = null;
        }

        private void OnDisable()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            if (source != null)
            {
                source.Stop();
            }
        }
    }
}
