using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音效池设置")]
    public int poolSize = 10;
    public int maxConcurrent3DSounds = 20;

    private List<AudioSource> audioSourcePool;
    private int currentIndex = 0;
    private int active3DSoundCount = 0;

    // ===== UI音频统一管理 =====
    // BGM源注册表，UI打开时压低音量而不是暂停
    private List<AudioSource> _bgmSources = new List<AudioSource>();
    // 记录进入UI前各BGM源的原始音量，用于关闭UI后恢复
    private Dictionary<AudioSource, float> _bgmOriginalVolumes = new Dictionary<AudioSource, float>();
    // UI打开时被暂停的音效源，用于关闭UI后继续播放
    private HashSet<AudioSource> _pausedSfx = new HashSet<AudioSource>();
    // UI打开状态标记，供BattleStageManager和字幕计时判断
    public static bool UIAudioPaused { get; private set; }

    [Header("全局设置")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;

    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioPool();
    }

    private void InitializeAudioPool()
    {
        audioSourcePool = new List<AudioSource>();

        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.maxDistance = 50f;
            audioSourcePool.Add(source);
        }
    }

    public void PlaySFX2D(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableSource();
        if (source == null) return;

        source.spatialBlend = 0f;
        source.volume = Mathf.Clamp01(volume * sfxVolume * masterVolume);
        source.clip = clip;
        source.Play();
    }

    public void PlaySFX3D(AudioClip clip, Vector3 position, float volume = 1f, float minDistance = 1f, float maxDistance = 50f)
    {
        if (clip == null) return;

        if (active3DSoundCount >= maxConcurrent3DSounds)
            return;

        AudioSource source = GetAvailableSource();
        if (source == null) return;

        source.spatialBlend = 1f;
        source.transform.position = position;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = Mathf.Clamp01(volume * sfxVolume * masterVolume);
        source.clip = clip;
        source.Play();

        active3DSoundCount++;
        StartCoroutine(DecrementActiveSoundCount(source));
    }

    private IEnumerator DecrementActiveSoundCount(AudioSource source)
    {
        yield return new WaitWhile(() => source.isPlaying);
        active3DSoundCount--;
        source.spatialBlend = 1f;
    }

    private AudioSource GetAvailableSource()
    {
        if (audioSourcePool == null || audioSourcePool.Count == 0)
            return null;

        int startIndex = currentIndex;
        int attempts = 0;

        while (attempts < audioSourcePool.Count)
        {
            AudioSource source = audioSourcePool[currentIndex];
            currentIndex = (currentIndex + 1) % audioSourcePool.Count;

            if (!source.isPlaying)
            {
                return source;
            }
            attempts++;
        }

        return audioSourcePool[startIndex];
    }

    /// <summary>注册BGM源，UI打开时它被压低而不是暂停</summary>
    public void RegisterBGM(AudioSource src)
    {
        if (src != null && !_bgmSources.Contains(src))
            _bgmSources.Add(src);
    }

    /// <summary>UI打开：压低所有BGM，暂停场景中其余全部音效源</summary>
    public void PauseAllForUI()
    {
        if (UIAudioPaused) return;
        UIAudioPaused = true;

        // 1. 压低BGM音量
        _bgmOriginalVolumes.Clear();
        foreach (AudioSource src in _bgmSources)
        {
            if (src == null) continue;
            _bgmOriginalVolumes[src] = src.volume;
            src.volume = 0.3f;
        }

        // 2. 暂停所有非BGM的音效源，记录暂停列表
        _pausedSfx.Clear();
        AudioSource[] allSources = FindObjectsOfType<AudioSource>(true);
        foreach (AudioSource src in allSources)
        {
            if (src == null) continue;
            if (_bgmSources.Contains(src)) continue;
            if (src.isPlaying)
            {
                src.Pause();
                _pausedSfx.Add(src);
            }
        }
    }

    /// <summary>UI关闭：恢复BGM音量，继续播放被暂停的音效</summary>
    public void ResumeAllForUI()
    {
        if (!UIAudioPaused) return;
        UIAudioPaused = false;

        // 1. 恢复BGM原始音量
        foreach (AudioSource src in _bgmSources)
        {
            if (src == null) continue;
            float vol;
            if (_bgmOriginalVolumes.TryGetValue(src, out vol))
                src.volume = vol;
        }
        _bgmOriginalVolumes.Clear();

        // 2. 继续播放被暂停的音效
        foreach (AudioSource src in _pausedSfx)
        {
            if (src != null)
                src.UnPause();
        }
        _pausedSfx.Clear();
    }

    public void StopAllSFX()
    {
        foreach (AudioSource source in audioSourcePool)
        {
            if (source.isPlaying)
                source.Stop();
        }
        active3DSoundCount = 0;
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }
}