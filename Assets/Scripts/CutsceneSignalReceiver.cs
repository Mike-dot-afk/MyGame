using UnityEngine;
using UnityEngine.Playables;
using System.Collections;

public class CutsceneSignalReceiver : MonoBehaviour
{
    [Header("CG内部要播放的台词")]
    public AudioClip cgVoiceClip;
    public string cgSubtitleText;

    [Header("CG战斗背景BGM配置")]
    public AudioClip combatLoopBgmClip;
    [Tooltip("专门用于战斗循环BGM的AudioSource，2D，设置Loop=true，优先级高")]
    public AudioSource combatBgmAudioSource;

    // 静态实例，供NoteInteractable访问控制BGM
    public static CutsceneSignalReceiver Instance { get; private set; }

    private Coroutine _bgmFadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 注册CG战斗BGM到统一音频控制器，UI打开时压低
        if (AudioManager.Instance != null && combatBgmAudioSource != null)
            AudioManager.Instance.RegisterBGM(combatBgmAudioSource);
    }

    // 把这个函数拖进Signal Emitter的事件槽，信号触发就执行
    public void OnPlayCgVoice()
    {
        if (VoiceSubtitlePlayer.Instance == null || cgVoiceClip == null) return;
        VoiceSubtitlePlayer.Instance.PlaySingleVoice(cgVoiceClip, cgSubtitleText);

        // 同时启动循环战斗BGM
        StartCombatBGM();
    }

    /// <summary>启动战斗循环BGM</summary>
    public void StartCombatBGM()
    {
        if (combatBgmAudioSource == null || combatLoopBgmClip == null)
        {
            Debug.LogWarning("战斗BGM：AudioSource或Clip未赋值");
            return;
        }
        // 如果正在跑淡出协程，先终止
        if (_bgmFadeCoroutine != null)
        {
            StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = null;
        }
        combatBgmAudioSource.clip = combatLoopBgmClip;
        combatBgmAudioSource.loop = true;
        combatBgmAudioSource.volume = 1f;
        if (!combatBgmAudioSource.isPlaying)
        {
            combatBgmAudioSource.Play();
        }
    }

    /// <summary>开始BGM淡出：在duration秒内音量降到0，停止播放</summary>
    public void StartBgmFadeOut(float duration)
    {
        if (combatBgmAudioSource == null || !combatBgmAudioSource.isPlaying) return;
        if (_bgmFadeCoroutine != null)
        {
            StopCoroutine(_bgmFadeCoroutine);
        }
        _bgmFadeCoroutine = StartCoroutine(Coroutine_BgmFadeOut(duration));
    }

    private IEnumerator Coroutine_BgmFadeOut(float duration)
    {
        float startVol = combatBgmAudioSource.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);
            combatBgmAudioSource.volume = Mathf.Lerp(startVol, 0f, progress);
            yield return null;
        }
        combatBgmAudioSource.Stop();
        combatBgmAudioSource.volume = 0f;
        _bgmFadeCoroutine = null;
    }

    /// <summary>强制立刻停止BGM，读档/重置场景调用</summary>
    public void ForceStopCombatBGM()
    {
        if (_bgmFadeCoroutine != null)
        {
            StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = null;
        }
        if (combatBgmAudioSource != null)
        {
            combatBgmAudioSource.Stop();
            combatBgmAudioSource.volume = 0f;
        }
    }

    private void OnDisable()
    {
        ForceStopCombatBGM();
    }
}
