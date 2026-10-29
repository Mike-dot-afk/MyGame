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