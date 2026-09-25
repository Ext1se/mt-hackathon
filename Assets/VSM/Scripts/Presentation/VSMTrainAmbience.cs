namespace VSM.Presentation
{
    using UnityEngine;
    /// <summary>Воспроизводит фоновый шум поезда с плавным началом и перекрытием стыка аудиопетли.</summary>
    public sealed class VSMTrainAmbience : MonoBehaviour
    {
        [Tooltip("Запись внутреннего шума вагона.")] public AudioClip clip;
        [Range(0, 1), Tooltip("Громкость фонового шума.")] public float volume = .45f;
        [Tooltip("Продолжительность перекрытия конца и начала записи в секундах.")] public float crossfade = 1.5f;
        AudioSource[] sources;
        int current;
        float elapsed;
        bool blending;
        /// <summary>Создаёт два источника для бесшовного перекрытия записи.</summary>
        void Awake()
        {
            sources = new AudioSource[2];
            for (int i = 0; i < 2; i++) { sources[i] = gameObject.AddComponent<AudioSource>(); sources[i].clip = clip; sources[i].spatialBlend = 0; sources[i].playOnAwake = false; sources[i].volume = 0; sources[i].priority = 64; }
        }
        /// <summary>Запускает фоновую запись при включении сцены.</summary>
        void OnEnable() { if (clip && sources != null) { current = 0; elapsed = 0; blending = false; sources[0].Play(); } }
        /// <summary>Плавно смешивает конец текущей записи с началом следующей.</summary>
        void Update()
        {
            if (!clip) return;
            elapsed += Time.unscaledDeltaTime;
            float fade = Mathf.Min(crossfade, clip.length * .2f);
            float start = clip.length - fade;
            if (elapsed < start) { sources[current].volume = volume * Mathf.Clamp01(elapsed / Mathf.Max(.1f, fade)); return; }
            int next = 1 - current;
            if (!blending) { sources[next].time = 0; sources[next].Play(); blending = true; }
            float a = Mathf.Clamp01((elapsed - start) / Mathf.Max(.1f, fade));
            sources[current].volume = volume * (1 - a); sources[next].volume = volume * a;
            if (a >= 1) { sources[current].Stop(); current = next; elapsed = fade; blending = false; }
        }
        /// <summary>Останавливает оба источника при отключении окружения.</summary>
        void OnDisable() { if (sources != null) foreach (var source in sources) if (source) source.Stop(); }
    }
}
