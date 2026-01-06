using UnityEngine;
using UnityEngine.UI;

namespace ChogZombies.Game
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Music")]
        [SerializeField] AudioSource musicSource;
        [SerializeField] AudioClip[] playlist;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.5f;
        [SerializeField] bool playOnStart = true;
        [SerializeField] bool loopPlaylist = true;

        int _currentTrackIndex;
        bool _musicMuted;
        bool _initialized;

        [Header("SFX")]
        [SerializeField] bool sfxEnabledOnStart = true;

        bool _sfxMuted;

        [Header("UI")]
        [SerializeField] Button musicToggleButton;
        [SerializeField] Button sfxToggleButton;
        [SerializeField] Graphic musicButtonGraphic;
        [SerializeField] Graphic sfxButtonGraphic;
        [SerializeField] Color enabledButtonColor = Color.white;
        [SerializeField] Color disabledButtonColor = new Color(1f, 1f, 1f, 0.5f);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (musicSource == null)
                musicSource = GetComponent<AudioSource>();

            if (musicSource != null)
            {
                musicSource.playOnAwake = false;
                musicSource.loop = false;
                musicSource.ignoreListenerPause = true;
                musicSource.ignoreListenerVolume = true;
                musicSource.volume = Mathf.Clamp01(musicVolume);
            }

            if (musicButtonGraphic == null && musicToggleButton != null)
                musicButtonGraphic = musicToggleButton.targetGraphic;
            if (sfxButtonGraphic == null && sfxToggleButton != null)
                sfxButtonGraphic = sfxToggleButton.targetGraphic;

            _musicMuted = false;
            _sfxMuted = !sfxEnabledOnStart;
            AudioListener.pause = _sfxMuted;
            RegisterButtonCallbacks();
            RefreshButtonVisuals();
            _initialized = true;
        }

        void Start()
        {
            if (!_initialized)
                return;

            if (playOnStart)
                StartPlaylistIfNeeded();
        }

        void Update()
        {
            if (musicSource == null)
                return;

            if (!_musicMuted && !musicSource.isPlaying && playlist != null && playlist.Length > 0)
            {
                PlayNextTrack();
            }
        }

        void StartPlaylistIfNeeded()
        {
            if (musicSource == null)
                return;

            if (playlist == null || playlist.Length == 0)
                return;

            if (!musicSource.isPlaying)
            {
                _currentTrackIndex = Mathf.Clamp(_currentTrackIndex, 0, playlist.Length - 1);
                PlayTrackAtIndex(_currentTrackIndex);
            }
        }

        void PlayNextTrack()
        {
            if (playlist == null || playlist.Length == 0)
                return;

            _currentTrackIndex++;

            if (_currentTrackIndex >= playlist.Length)
            {
                if (!loopPlaylist)
                    return;

                _currentTrackIndex = 0;
            }

            PlayTrackAtIndex(_currentTrackIndex);
        }

        void PlayTrackAtIndex(int index)
        {
            if (musicSource == null)
                return;
            if (playlist == null || playlist.Length == 0)
                return;
            if (index < 0 || index >= playlist.Length)
                return;

            var clip = playlist[index];
            if (clip == null)
                return;

            musicSource.clip = clip;
            if (!_musicMuted)
            {
                musicSource.volume = Mathf.Clamp01(musicVolume);
                musicSource.Play();
            }
        }

        public void SetMusicVolume01(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            if (musicSource != null && !_musicMuted)
            {
                musicSource.volume = musicVolume;
            }
        }

        public void ToggleMusic()
        {
            SetMusicEnabled(_musicMuted);
        }

        public void SetMusicEnabled(bool enabled)
        {
            _musicMuted = !enabled;

            if (musicSource == null)
                return;

            if (_musicMuted)
            {
                if (musicSource.isPlaying)
                    musicSource.Pause();
                musicSource.volume = 0f;
            }
            else
            {
                musicSource.volume = Mathf.Clamp01(musicVolume);
                if (!musicSource.isPlaying)
                {
                    if (musicSource.clip == null)
                    {
                        StartPlaylistIfNeeded();
                    }
                    else
                    {
                        musicSource.Play();
                    }
                }
            }

            RefreshButtonVisuals();
        }

        public void ToggleSfx()
        {
            SetSfxEnabled(_sfxMuted);
        }

        public void SetSfxEnabled(bool enabled)
        {
            _sfxMuted = !enabled;
            AudioListener.pause = _sfxMuted;
            RefreshButtonVisuals();
        }

        void RegisterButtonCallbacks()
        {
            if (musicToggleButton != null)
            {
                musicToggleButton.onClick.RemoveListener(ToggleMusic);
                musicToggleButton.onClick.AddListener(ToggleMusic);
            }

            if (sfxToggleButton != null)
            {
                sfxToggleButton.onClick.RemoveListener(ToggleSfx);
                sfxToggleButton.onClick.AddListener(ToggleSfx);
            }
        }

        void RefreshButtonVisuals()
        {
            if (musicButtonGraphic != null)
                musicButtonGraphic.color = _musicMuted ? disabledButtonColor : enabledButtonColor;

            if (sfxButtonGraphic != null)
                sfxButtonGraphic.color = _sfxMuted ? disabledButtonColor : enabledButtonColor;
        }

        public bool IsMusicMuted => _musicMuted;
        public bool IsSfxMuted => _sfxMuted;
    }
}
