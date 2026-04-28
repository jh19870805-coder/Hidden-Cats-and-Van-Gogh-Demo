using UnityEngine;
using HiddenCats.Interactable;
using HiddenCats.UI;

namespace HiddenCats.Core
{
    /// <summary>
    /// Global audio manager for background music and sound effects.
    /// A single instance is created at startup and kept across scenes.
    /// BGM / common SFX load from Resources/SoundEffect (see FeatureSpec 音乐配置文件).
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const string ResourceFolder = "SoundEffect";
        private const float BgmFadeOutDuration = 2f;

        [Header("Music")]
        [SerializeField] private AudioSource musicSource;

        [Tooltip("Optional fallback if Resources clips are missing.")]
        [SerializeField] private AudioClip defaultMusicClip;

        [Header("Sound Effects")]
        [SerializeField] private AudioSource sfxSource;

        [System.Serializable]
        public struct SfxEntry
        {
            [Tooltip("逻辑 ID，与 Resources 内音频文件名（无扩展名）一致，例如 Common、miao01")]
            public string id;
            public AudioClip clip;
        }

        [Tooltip("可覆盖或补充 Resources/SoundEffect 下的同名音效。")]
        [SerializeField] private SfxEntry[] sfxEntries;

        private System.Collections.Generic.Dictionary<string, AudioClip> _sfxLookup;
        private AudioClip[] _randomMiaoClips;
        private string _lastBgmResourceName = "01Main";
        private System.Collections.IEnumerator _bgmFadeOutCo;
        private float _targetMusicVolume = 1f;

        private static AudioClip _cachedClickBlockClip;
        private static AudioClip _cachedFish01Clip;
        private static AudioClip _cachedSpinGetClip;
        private static AudioClip _cachedLockedClip;
        private static AudioClip _cachedCommon02Clip;
        private static AudioSource _sfxAudioSource;

        private static readonly string[] ResourceClipNames = new[]
        {
            "01Main", "02Room",
            "Common", "Common02", "Fire", "FireFind", "spin_get", "clickBlock", "HiddenCatsFinded", "Fish01",
            "overNewRecord", "Paper01", "Paper02", "DoorOpen", "WindowOpen", "popup", "locked",
            "miao01", "miao02", "miao03", "miao04", "miao05", "miao06", "miao07", "miao08", "miao09", "miao10", "miao11"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            EnsureMusicSource();
            EnsureSfxSource();
            _sfxAudioSource = sfxSource;
            BuildSfxLookup();
            CacheRandomMiaoClips();

            // Reset to default BGM to prevent stale music from a previous session
            _lastBgmResourceName = "01Main";

            // 初始化目标音量为默认设置值
            _targetMusicVolume = 1f;
        }

        /// <summary>
        /// Apply volume and playback state based on settings.
        /// </summary>
        public void ApplySettings(SettingsData settings)
        {
            if (settings == null)
            {
                return;
            }

            EnsureMusicSource();
            EnsureSfxSource();

            float volume = Mathf.Clamp01(settings.masterVolume) * Mathf.Clamp01(settings.musicVolume);
            Debug.Log($"[AudioManager] ApplySettings: masterVolume={settings.masterVolume:F4}, musicVolume={settings.musicVolume:F4}, 最终音乐音量={volume:F4}");
            _targetMusicVolume = volume;
            musicSource.volume = volume;

            float sfxVolume = Mathf.Clamp01(settings.masterVolume) * Mathf.Clamp01(settings.sfxVolume);
            sfxSource.volume = sfxVolume;

            if (!musicSource.isPlaying || musicSource.clip == null)
            {
                PlayBackgroundMusic();
            }
        }

        /// <summary>
        /// Start or resume the last requested BGM (defaults to Main menu).
        /// </summary>
        public void PlayBackgroundMusic()
        {
            string name = string.IsNullOrEmpty(_lastBgmResourceName) ? "01Main" : _lastBgmResourceName;
            PlayBgmByResourceName(name);
        }

        /// <summary>
        /// Returns true if the music source is currently playing audio.
        /// </summary>
        public bool IsMusicPlaying()
        {
            return musicSource != null && musicSource.isPlaying && musicSource.clip != null;
        }

        /// <summary>
        /// Switch BGM by Resources/SoundEffect file name without extension (e.g. 01Main, 04Cafe).
        /// Fades out the current music over 5 seconds, then starts the new one immediately.
        /// </summary>
        public void PlayBgmByResourceName(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                return;
            }

            EnsureMusicSource();

            var clip = GetClipById(resourceName);
            if (clip == null && defaultMusicClip != null && resourceName == "01Main")
            {
                clip = defaultMusicClip;
            }

            if (clip == null)
            {
                clip = Resources.Load<AudioClip>($"{ResourceFolder}/{resourceName}");
            }

            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] BGM '{resourceName}' not found under Resources/{ResourceFolder}.");
                return;
            }

            _lastBgmResourceName = resourceName;

            // 停止旧的 fade-out 协程必须在判断 hasOldMusic 之前执行，
            // 否则旧协程已把 musicSource.clip 换成新 clip，导致 hasOldMusic 判断错误，
            // 引起双重淡出叠加（音量被压到极低）。
            if (_bgmFadeOutCo != null)
            {
                StopCoroutine(_bgmFadeOutCo);
            }

            bool hasOldMusic = musicSource.clip != null && musicSource.isPlaying;
            Debug.Log($"[AudioManager] PlayBgmByResourceName → {resourceName}, 当前音量={musicSource.volume:F4}, 有旧音乐={hasOldMusic}");

            _bgmFadeOutCo = CoFadeOutThenPlayNew(clip, hasOldMusic);
            StartCoroutine(_bgmFadeOutCo);
        }

        private System.Collections.IEnumerator CoFadeOutThenPlayNew(AudioClip newClip, bool hasOldMusic)
        {
            float originalVolume = musicSource.volume;

            // 如果当前音量接近 0，先设置正确音量再播放，避免新 BGM 以 0 音量无声播放
            if (Mathf.Approximately(originalVolume, 0f))
            {
                if (Mathf.Approximately(musicSource.volume, 0f))
                {
                    musicSource.volume = _targetMusicVolume;
                }
                musicSource.clip = newClip;
                musicSource.loop = true;
                musicSource.Play();
                Debug.Log($"[AudioManager] 淡入新BGM={newClip.name}, 初始音量={musicSource.volume:F4}");
                _bgmFadeOutCo = null;
                yield break;
            }

            if (hasOldMusic)
            {
                float elapsed = 0f;
                while (elapsed < BgmFadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    musicSource.volume = Mathf.Lerp(originalVolume, 0f, elapsed / BgmFadeOutDuration);
                    yield return null;
                }

                musicSource.volume = 0f;
                Debug.Log($"[AudioManager] 淡出完成, 当前音量={musicSource.volume:F4}");
            }

            musicSource.clip = newClip;
            musicSource.loop = true;
            musicSource.Play();

            // 切换完成后恢复到目标音量（而不是 originalVolume），
            // 避免在快速切换时音量被旧值覆盖导致越来越小
            musicSource.volume = _targetMusicVolume;
            Debug.Log($"[AudioManager] 切换BGM完成, 新BGM={newClip.name}, 当前音量={musicSource.volume:F4}");

            _bgmFadeOutCo = null;
        }

        /// <summary>
        /// Call after switching top-level windows (prefab base name, with or without "(Clone)").
        /// </summary>
        public void ApplyBgmForWindowPrefab(string prefabOrInstanceName)
        {
            if (string.IsNullOrEmpty(prefabOrInstanceName))
            {
                return;
            }

            string baseName = StripCloneSuffix(prefabOrInstanceName);
            float currentVolume = musicSource != null ? musicSource.volume : 0f;

            switch (baseName)
            {
                case "MainWnd":
                    Debug.Log($"[AudioManager] 界面切换 → {baseName}, 切换前音量={currentVolume:F4}");
                    PlayBgmByResourceName("01Main");
                    break;
                case "RoomWnd":
                    Debug.Log($"[AudioManager] 界面切换 → {baseName}, 切换前音量={currentVolume:F4}");
                    PlayBgmByResourceName("02Room");
                    break;
                default:
                    Debug.Log($"[AudioManager] 界面切换 → {baseName}, 切换前音量={currentVolume:F4}");
                    break;
            }
        }

        private static string StripCloneSuffix(string name)
        {
            const string suffix = "(Clone)";
            if (name.EndsWith(suffix))
            {
                return name.Substring(0, name.Length - suffix.Length).TrimEnd();
            }

            return name;
        }

        public void PlayRandomCatMeow()
        {
            if (_randomMiaoClips == null || _randomMiaoClips.Length == 0)
            {
                CacheRandomMiaoClips();
            }

            if (_randomMiaoClips == null || _randomMiaoClips.Length == 0)
            {
                PlaySfx("miao01");
                return;
            }

            int i = Random.Range(0, _randomMiaoClips.Length);
            var clip = _randomMiaoClips[i];
            if (clip == null)
            {
                return;
            }

            EnsureSfxSource();
            sfxSource.PlayOneShot(clip);
        }

        public void PlayMainCatPopMeow()
        {
            PlaySfx("miao01");
        }

        private void CacheRandomMiaoClips()
        {
            var list = new System.Collections.Generic.List<AudioClip>(11);
            for (int i = 1; i <= 11; i++)
            {
                string id = $"miao{i:00}";
                var c = GetClipById(id);
                if (c == null)
                {
                    c = Resources.Load<AudioClip>($"{ResourceFolder}/{id}");
                }

                if (c != null)
                {
                    list.Add(c);
                }
            }

            _randomMiaoClips = list.ToArray();
        }

        private void EnsureMusicSource()
        {
            if (musicSource != null)
            {
                return;
            }

            musicSource = GetComponent<AudioSource>();
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
            }
        }

        private void EnsureSfxSource()
        {
            if (sfxSource != null)
            {
                return;
            }

            var sources = GetComponents<AudioSource>();
            foreach (var src in sources)
            {
                if (src != musicSource)
                {
                    sfxSource = src;
                    break;
                }
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
            }

            _sfxAudioSource = sfxSource;
        }

        private void BuildSfxLookup()
        {
            _sfxLookup = new System.Collections.Generic.Dictionary<string, AudioClip>();

            for (int i = 0; i < ResourceClipNames.Length; i++)
            {
                string id = ResourceClipNames[i];
                var clip = Resources.Load<AudioClip>($"{ResourceFolder}/{id}");
                if (clip != null)
                {
                    _sfxLookup[id] = clip;
                }
            }

            if (sfxEntries == null)
            {
                return;
            }

            foreach (var entry in sfxEntries)
            {
                if (string.IsNullOrEmpty(entry.id) || entry.clip == null)
                {
                    continue;
                }

                _sfxLookup[entry.id] = entry.clip;
            }
        }

        internal AudioClip GetClipById(string id)
        {
            if (_sfxLookup == null || _sfxLookup.Count == 0)
            {
                BuildSfxLookup();
            }

            return _sfxLookup != null && _sfxLookup.TryGetValue(id, out var clip) ? clip : null;
        }

        /// <summary>
        /// Play a sound effect by id (Resources file name without extension, or SfxEntry id).
        /// </summary>
        public void PlaySfx(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            EnsureSfxSource();

            if (_sfxLookup == null || _sfxLookup.Count == 0)
            {
                BuildSfxLookup();
            }

            var clip = GetClipById(id);
            if (clip == null)
            {
                clip = Resources.Load<AudioClip>($"{ResourceFolder}/{id}");
                if (clip != null && _sfxLookup != null)
                {
                    _sfxLookup[id] = clip;
                }
            }

            if (clip != null)
            {
                sfxSource.PlayOneShot(clip);
            }
            else
            {
                Debug.LogWarning($"[AudioManager] 未找到音效 id: {id}（Resources/{ResourceFolder} 或 sfxEntries）。");
            }
        }

        /// <summary>Plays clickBlock SFX with zero overhead (reuses _sfxLookup, direct AudioSource call).</summary>
        public static void PlayClickBlock()
        {
            if (_sfxAudioSource == null) return;

            AudioClip clip = _cachedClickBlockClip;
            if (clip == null && Instance != null)
            {
                clip = Instance.GetClipById("clickBlock");
                _cachedClickBlockClip = clip;
            }

            if (clip != null)
            {
                _sfxAudioSource.PlayOneShot(clip);
            }
        }

        /// <summary>Plays Fish01 SFX with zero overhead (reuses _sfxLookup, direct AudioSource call).</summary>
        public static void PlayFishCollect()
        {
            if (_sfxAudioSource == null) return;

            AudioClip clip = _cachedFish01Clip;
            if (clip == null && Instance != null)
            {
                clip = Instance.GetClipById("Fish01");
                _cachedFish01Clip = clip;
            }

            if (clip != null)
            {
                _sfxAudioSource.PlayOneShot(clip);
            }
        }

        /// <summary>Plays spin_get SFX with zero overhead (reuses _sfxLookup, direct AudioSource call).</summary>
        public static void PlaySpinGet()
        {
            if (_sfxAudioSource == null) return;

            AudioClip clip = _cachedSpinGetClip;
            if (clip == null && Instance != null)
            {
                clip = Instance.GetClipById("spin_get");
                _cachedSpinGetClip = clip;
            }

            if (clip != null)
            {
                _sfxAudioSource.PlayOneShot(clip);
            }
        }

        /// <summary>Plays locked SFX with zero overhead (reuses _sfxLookup, direct AudioSource call).</summary>
        public static void PlayLocked()
        {
            if (_sfxAudioSource == null) return;

            AudioClip clip = _cachedLockedClip;
            if (clip == null && Instance != null)
            {
                clip = Instance.GetClipById("locked");
                _cachedLockedClip = clip;
            }

            if (clip != null)
            {
                _sfxAudioSource.PlayOneShot(clip);
            }
        }

        /// <summary>Plays Common02 SFX with zero overhead (reuses _sfxLookup, direct AudioSource call).</summary>
        public static void PlayCommon02()
        {
            if (_sfxAudioSource == null) return;

            AudioClip clip = _cachedCommon02Clip;
            if (clip == null && Instance != null)
            {
                clip = Instance.GetClipById("Common02");
                _cachedCommon02Clip = clip;
            }

            if (clip != null)
            {
                _sfxAudioSource.PlayOneShot(clip);
            }
        }
    }
}
