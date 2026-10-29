using UnityEngine;
using System.Collections;

[System.Serializable]
public class VoiceSubtitleItem
{
    [Tooltip("配音音频片段")]
    public AudioClip voiceClip;
    [Tooltip("对应字幕文本")]
    [TextArea] public string subtitleText;
    [Tooltip("本条【音频结束后】到下一条开始前的停顿秒数")]
    public float afterPause = 0f;
}

public class VoiceSubtitlePlayer : MonoBehaviour
{
    public static VoiceSubtitlePlayer Instance;

    [Header("复用原有CG字幕控制器（老CG也用这个）")]
    public SubtitleController subtitleController;
    [Header("2D语音AudioSource，不要空间音，Spatial Blend=0")]
    public AudioSource voiceAudioSource;

    [Header("字幕全局设置")]
    [Tooltip("字幕最小显示秒数；仅当后面没有下一句时才强制执行该保底时间")]
    public float minSubtitleShowTime = 1.5f;

    private Coroutine _playSequenceCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>【新剧情最常用】播放单条：同时播放配音+字幕，音频结束自动关字幕（受minSubtitleShowTime限制）</summary>
    public void PlaySingleVoice(AudioClip clip, string subtitle, float afterPause = 0f)
    {
        if (clip == null) return;
        StopCurrentPlay();
        VoiceSubtitleItem[] one = new VoiceSubtitleItem[]
        {
            new VoiceSubtitleItem(){ voiceClip = clip, subtitleText = subtitle, afterPause = afterPause }
        };
        _playSequenceCoroutine = StartCoroutine(PlaySequenceCoroutine(one));
    }

    /// <summary>播放连续多段配音字幕，一句播完自动接下一句，支持每条独立句间停顿</summary>
    public void PlayVoiceSequence(VoiceSubtitleItem[] sequence)
    {
        if (sequence == null || sequence.Length == 0) return;
        StopCurrentPlay();
        _playSequenceCoroutine = StartCoroutine(PlaySequenceCoroutine(sequence));
    }

    /// <summary>强制停止当前正在播放的新系统配音字幕，清空字幕UI</summary>
    public void StopCurrentPlay()
    {
        if (_playSequenceCoroutine != null)
        {
            StopCoroutine(_playSequenceCoroutine);
            _playSequenceCoroutine = null;
        }
        voiceAudioSource.Stop();
        subtitleController.HideSubtitle();
    }

    private IEnumerator PlaySequenceCoroutine(VoiceSubtitleItem[] sequence)
    {
        int totalCount = sequence.Length;
        for (int i = 0; i < totalCount; i++)
        {
            var item = sequence[i];
            if (item.voiceClip == null) continue;

            float showStartTime = Time.unscaledTime;
            subtitleController.ShowSubtitle(item.subtitleText);

            voiceAudioSource.clip = item.voiceClip;
            voiceAudioSource.Play();

            //1、等待本条音频播放完毕
            float audioRemain = item.voiceClip.length;
            while (audioRemain > 0)
            {
                audioRemain -= Time.unscaledDeltaTime;
                yield return null;
            }
            voiceAudioSource.Stop();

            bool isLastItem = (i == totalCount - 1);
            if (!isLastItem)
            {
                // 不是最后一条：执行afterPause，跑完直接切下一句，**无视minSubtitleShowTime**
                float pauseRemain = item.afterPause;
                while (pauseRemain > 0)
                {
                    pauseRemain -= Time.unscaledDeltaTime;
                    yield return null;
                }
                // afterPause到点，立刻切换，旧字幕直接覆盖，牺牲最小显示时间
                subtitleController.HideSubtitle();
            }
            else
            {
                // 是序列【最后一句 / 单条台词】：才执行最小字幕保底时间
                float elapsed = Time.unscaledTime - showStartTime;
                float needWait = minSubtitleShowTime - elapsed;
                if (needWait > 0)
                {
                    while (needWait > 0)
                    {
                        needWait -= Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                subtitleController.HideSubtitle();
            }
        }
        _playSequenceCoroutine = null;
    }
}
