using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

public class StartGameFlow : MonoBehaviour
{
    [Header("UI按钮")]
    public Button startGameBtn;
    [Header("闪电特效")]
    public GameObject lightningPrefab;
    public Transform lightningSpawnPoint;
    [Header("打雷音效（一次性，PlayOneShot）")]
    public AudioClip thunderAudio;
    private AudioSource _audioSource;
    [Header("循环环境音")]
    public AudioClip rainAmbientClip;
    private AudioSource _rainAudioSource;
    public float rainFadeOutTime = 2f;
    private bool _rainIsFading;
    [Header("Timeline开场CG相关")]
    public PlayableDirector openingTimeline;
    public GameObject cgCamera;
    public CinemachineCamera gameVCam;
    [Header("角色引用")]
    public GameObject playerObject;
    [Header("直升机/降落伞")]
    public ChopperController helicopter;
    public ParachuteSpawner parachuteSpawner;
    [Header("下雨VFX（含角色脚步声）")]
    public GameObject rainVfxObject;
    private AudioSource _rainVfxAudioSource;
    private bool _rainVfxAudioPlayed;
    [Header("游戏UI根节点（Canvas下所有游戏UI的父物体，CG字幕放在此节点外）")]
    public GameObject gameUIRoot;
    [Header("UI血条画布")]
    public GameObject HpBarCanvas;
    private bool _hasPlayedOpeningCG;
    private const string PerformanceLayerName = "Performance";
    [Header("CG结束后播放的对话序列（复用VoiceSubtitlePlayer）")]
    public VoiceSubtitleItem[] postOpeningDialogueSequence;

    //【替换为新字典：仅保存CG根物体的原始Layer，不存子物体】
    private static Dictionary<GameObject, int> _cgActorRootLayerDict = new Dictionary<GameObject, int>();

    private static Dictionary<Camera, int> _cameraOriginalMaskMap = new Dictionary<Camera, int>();

    // ===== 战斗暂停计数器（静态，支持多CG嵌套） =====
    private static int _fightPauseCounter;

    // ===== 游戏尚未启动（未点击开始按钮）的锁定计数器 =====
    private static int _gameNotStartedCounter;
    // 游戏开始事件：点击开始按钮或新游戏确认后触发，供UI隐藏开始界面按钮
    public static System.Action OnGameStarted;

    /// <summary>游戏还未点击开始按钮，锁定暂停等系统（仿CG机制）</summary>
    public static void LockBeforeGameStart()
    {
        _gameNotStartedCounter++;
    }
    /// <summary>点击开始游戏后，解除“未启动”锁定</summary>
    public static void UnlockAfterGameStart()
    {
        _gameNotStartedCounter--;
        if (_gameNotStartedCounter < 0) _gameNotStartedCounter = 0;
    }
    public static bool IsSystemLocked => (_cgPlayingCounter > 0) || (_gameNotStartedCounter > 0);
    /// <summary>请求暂停战斗（CG开始时调用）</summary>
    public static void PauseFight()
    {
        _fightPauseCounter++;
    }
    /// <summary>恢复战斗（CG结束时调用，计数器归零才真正解冻）</summary>
    public static void ResumeFight()
    {
        _fightPauseCounter--;
        if (_fightPauseCounter < 0)
            _fightPauseCounter = 0;
    }
    /// <summary>外部脚本/怪物脚本读取是否应冻结战斗</summary>
    public static bool IsFightFrozen => _fightPauseCounter > 0;

    // ===== 玩家输入控制（静态） =====
    private static StarterAssetsInputs _globalInput;
    private static MonoBehaviour _globalController;
    private static bool _inputLocked;
    private static MonoBehaviour _globalPlayerInput;
    /// <summary>在Start()中调用一次，缓存玩家引用</summary>
    public static void InitGlobalPlayerRef(GameObject player)
    {
        if (player == null) return;
        _globalInput = player.GetComponent<StarterAssetsInputs>();
        _globalController = player.GetComponent("ThirdPersonController") as MonoBehaviour;
        _globalPlayerInput = player.GetComponent("PlayerInput") as MonoBehaviour;
    }
    /// <summary>锁定玩家移动和键鼠输入（CG开始时调用）</summary>
    public static void LockPlayerInput()
    {
        if (_inputLocked) return;
        _inputLocked = true;
        if (_globalController != null)
            _globalController.enabled = false;
        if (_globalPlayerInput != null)
            _globalPlayerInput.enabled = false;
        if (_globalInput != null)
        {
            _globalInput.cursorLocked = false;
            _globalInput.move = Vector2.zero;
            _globalInput.look = Vector2.zero;
            _globalInput.jump = false;
            _globalInput.sprint = false;
            _globalInput.aim = false;
            _globalInput.shoot = false;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    /// <summary>解锁玩家输入（CG结束时调用）</summary>
    public static void UnlockPlayerInput()
    {
        if (!_inputLocked) return;
        _inputLocked = false;
        if (_globalController != null)
            _globalController.enabled = true;
        if (_globalPlayerInput != null)
            _globalPlayerInput.enabled = true;
        if (_globalInput != null)
        {
            // 按真实按键状态恢复瞄准（保留关闭UI时仍按着右键则继续瞄准）
            if (UnityEngine.InputSystem.Mouse.current != null)
                _globalInput.aim = UnityEngine.InputSystem.Mouse.current.rightButton.isPressed;
            _globalInput.shoot = false;
            _globalInput.look = Vector2.zero;
            _globalInput.cursorLocked = true;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        // UI/CG关闭后的防误触窗口，吃掉InputSystem恢复时补发的shoot/look
        if (ThirdPersonShooterController.Instance != null)
            ThirdPersonShooterController.Instance.BlockInputAfterUI(0.15f);
    }

    // ===== 游戏UI控制（静态，通过gameUIRoot字段初始化） =====
    private static GameObject _gameUIRootStatic;
    private static bool _uiRootWasActive;
    /// <summary>禁用游戏UI根节点（CG开始时调用，CG字幕放在根节点外不受影响）</summary>
    private static void DisableGameUI()
    {
        if (_gameUIRootStatic == null) return;
        _uiRootWasActive = _gameUIRootStatic.activeSelf;
        _gameUIRootStatic.SetActive(false);
    }
    /// <summary>恢复游戏UI根节点（按CG前状态还原）</summary>
    private static void RestoreGameUI()
    {
        if (_gameUIRootStatic == null) return;
        _gameUIRootStatic.SetActive(_uiRootWasActive);
    }

    // ===== CG统一入口/出口（静态，其他CG一行调用） =====
    private static int _cgPlayingCounter;
    /// <summary>是否正在播放CG（存档系统可检查此属性拒绝操作）</summary>
    public static bool IsCGPlaying => _cgPlayingCounter > 0;
    /// <summary>
    /// CG统一入口：暂停战斗 + 锁定玩家输入 + 禁用游戏UI + 标记禁止存档
    /// 其他CG脚本只需调用这一行
    /// </summary>
    public static void BeginCG()
    {
        PauseFight();
        LockPlayerInput();
        DisableGameUI();
        // CG播放期间隐藏鼠标
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _cgPlayingCounter++;
    }
    /// <summary>
    /// CG统一出口：恢复战斗 + 解锁玩家输入 + 恢复游戏UI + 解除存档禁止
    /// </summary>
    public static void EndCG()
    {
        _cgPlayingCounter--;
        if (_cgPlayingCounter < 0)
            _cgPlayingCounter = 0;
        RestoreGameUI();
        UnlockPlayerInput();
        ResumeFight();
        Time.timeScale = 1f;
    }

    void Start()
    {
        // 游戏启动时玩家在开始界面，暂停战斗
        _fightPauseCounter = 0;
        PauseFight();
        startGameBtn.onClick.AddListener(OnClickStartGame);
        _hasPlayedOpeningCG = PlayerPrefs.GetInt("HasPlayedOpeningCG", 0) == 1;
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _rainAudioSource = GetComponent<AudioSource>();
        if (_rainAudioSource == null)
        {
            _rainAudioSource = gameObject.AddComponent<AudioSource>();
        }
        _rainIsFading = false;
        if (rainAmbientClip != null)
        {
            _rainAudioSource.clip = rainAmbientClip;
            _rainAudioSource.loop = true;
            _rainAudioSource.spatialBlend = 0;
            _rainAudioSource.Play();
        }
        // 初始化全局玩家引用和UI根节点引用
        InitGlobalPlayerRef(playerObject);
        _gameUIRootStatic = gameUIRoot;
        // 游戏启动时锁定玩家输入 + 禁用游戏UI（开始界面阶段）
        LockPlayerInput();
        DisableGameUI();
        _rainVfxAudioPlayed = false;
        if (rainVfxObject != null)
        {
            _rainVfxAudioSource = rainVfxObject.GetComponent<AudioSource>();
        }
        LockBeforeGameStart();
    }

    void OnClickStartGame()
    {
        UnlockAfterGameStart();
        if (OnGameStarted != null)
        {
            OnGameStarted();
        }
        startGameBtn.gameObject.SetActive(false);
        if (_rainAudioSource != null && rainAmbientClip != null && !_rainIsFading)
        {
            _rainIsFading = true;
            StartCoroutine(FadeRainAudioOut(rainFadeOutTime));
        }
        bool existTempSave = HasTempSave();
        if (existTempSave)
        {
            SaveSystem.LoadGame(SaveSystem.TempSaveSlot);
            if (HpBarCanvas != null)
            {
                HpBarCanvas.SetActive(true);
            }
            if (cgCamera != null) cgCamera.SetActive(false);
            if (gameVCam != null) gameVCam.enabled = true;
            UnlockPlayerInput();
            RestoreGameUI();
            if (helicopter != null)
            {
                helicopter.gameObject.SetActive(false);
            }
            PlayCharacterRainAudio();
            StartGameFlow.ResumeFight();
            return;
        }
        StartGameFlow.ResumeFight();
        if (!_hasPlayedOpeningCG)
        {
            StartGameFlow.PauseFight();
            int lightningCount = 1;
            for (int i = 0; i < lightningCount; i++)
            {
                Vector3 spawnPos = lightningSpawnPoint.position + new Vector3(
                    Random.Range(-0.6f, 0.6f),
                    Random.Range(-0.3f, 0.3f),
                    Random.Range(-0.6f, 0.6f)
                );
                GameObject lightning = Instantiate(lightningPrefab, spawnPos, lightningSpawnPoint.rotation);
                Destroy(lightning, 1.5f);
            }
            if (thunderAudio != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(thunderAudio);
            }
            gameVCam.enabled = false;
            cgCamera.SetActive(true);
            // CG播放期间隐藏鼠标
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            openingTimeline.Play();
            helicopter?.StartFlyCountDown();
        }
        else
        {
            // 检查点档数据不存在时(存档被删除或损坏)，重建出生点档并直接开始游戏
            string checkpointTestKey = $"{SaveSystem.CheckpointSaveSlot}_43436c19-29cd-41f4-9299-0d7f5b910da6_HP";
            int checkpointHp = SaveFileManager.Instance.LoadData<int>(checkpointTestKey, -999);
            if (checkpointHp == -999)
            {
                ExecuteGameStartLogic();
                return;
            }
            if (HpBarCanvas != null)
            {
                HpBarCanvas.SetActive(true);
            }
            if (cgCamera != null) cgCamera.SetActive(false);
            if (gameVCam != null) gameVCam.enabled = true;
            UnlockPlayerInput();
            RestoreGameUI();
            if (helicopter != null)
            {
                helicopter.gameObject.SetActive(false);
            }
            PlayCharacterRainAudio();
            SaveSystem.currentSlotID = SaveSystem.CheckpointSaveSlot;
        }
    }

    private bool HasTempSave()
    {
        string testKey = $"{SaveSystem.TempSaveSlot}_43436c19-29cd-41f4-9299-0d7f5b910da6_HP";
        int hp = SaveFileManager.Instance.LoadData<int>(testKey, -999);
        return hp != -999;
    }

    public void OnCutsceneEndSignal()
    {
        _hasPlayedOpeningCG = true;
        PlayerPrefs.SetInt("HasPlayedOpeningCG", 1);
        PlayerPrefs.Save();
        // 停止播放
        openingTimeline.Stop();
        // 同步销毁底层Graph，避免残留Animation或Animator占用，导致下一帧卡
        if (openingTimeline.playableGraph.IsValid())
        {
            openingTimeline.playableGraph.Destroy();
        }
        // 直接同步执行后续逻辑，不需要协程等待帧
        ExecuteGameStartLogic();
    }

    // 已合并开场支持全一套后续逻辑，保证通过信号直接调用
    private void ExecuteGameStartLogic()
    {
        StartGameFlow.ResumeFight();
        if (HpBarCanvas != null)
        {
            HpBarCanvas.SetActive(true);
        }
        if (cgCamera != null) cgCamera.SetActive(false);
        if (gameVCam != null) gameVCam.enabled = true;
        UnlockPlayerInput();
        RestoreGameUI();
        if (helicopter != null)
        {
            helicopter.gameObject.SetActive(false);
        }
        PlayCharacterRainAudio();
        SaveSystem.currentSlotID = SaveSystem.CheckpointSaveSlot;
        foreach (var entity in UnityEngine.Object.FindObjectsOfType<SaveableEntity>(includeInactive: true))
        {
            entity.SaveData();
        }
        if (postOpeningDialogueSequence != null && postOpeningDialogueSequence.Length > 0)
        {
            // 确认是走完CG流程才播放（_hasPlayedOpeningCG在OnCutsceneEndSignal已经置true）
            if (_hasPlayedOpeningCG && VoiceSubtitlePlayer.Instance != null)
            {
                VoiceSubtitlePlayer.Instance.PlayVoiceSequence(postOpeningDialogueSequence);
            }
        }
    }

    IEnumerator FadeRainAudioOut(float duration)
    {
        float startVol = _rainAudioSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _rainAudioSource.volume = Mathf.Lerp(startVol, 0f, t);
            yield return null;
        }
        _rainAudioSource.volume = 0f;
        _rainAudioSource.Stop();
        _rainIsFading = false;
    }

    public void ResetRainSound()
    {
        if (_rainAudioSource != null && rainAmbientClip != null)
        {
            StopCoroutine(FadeRainAudioOut(rainFadeOutTime));
            _rainIsFading = false;
            _rainAudioSource.volume = 1f;
            _rainAudioSource.Play();
        }
    }

    public void PlayCharacterRainAudio()
    {
        if (_rainVfxAudioPlayed) return;
        if (_rainVfxAudioSource == null) return;
        _rainVfxAudioPlayed = true;
        _rainVfxAudioSource.Play();
    }

    public void ResetCGPlayedFlag()
    {
        _hasPlayedOpeningCG = false;
        PlayerPrefs.DeleteKey("HasPlayedOpeningCG");
        PlayerPrefs.Save();
        _rainVfxAudioPlayed = false;
        if (_rainVfxAudioSource != null)
        {
            _rainVfxAudioSource.Stop();
        }
    }

    // 新游戏按钮调用：重置开场CG标志后，直接走完整开始流程(播开场CG)
    public void StartNewGame()
    {
        ResetCGPlayedFlag();
        OnClickStartGame();
    }
    public static void ForceClearFightPauseCounter()
    {
        _fightPauseCounter = 0;
    }

    /// <summary>
    /// 将参演角色整棵树递归移到Performance层；仅保存每个根物体原始Layer
    /// 注意：要求子物体默认与根Layer一致；子物体有独立特殊层级请勿使用
    /// </summary>
    public static void PushActorsToPerformanceLayer(List<GameObject> actors)
    {
        if (actors == null) return;
        int performanceLayer = LayerMask.NameToLayer(PerformanceLayerName);
        if (performanceLayer < 0)
        {
            Debug.LogError("Layer [Performance] 未在Project Settings -> Tags and Layers中创建");
            return;
        }

        foreach (var root in actors)
        {
            if (root == null) continue;
            if (_cgActorRootLayerDict.ContainsKey(root)) continue;

            // 只记录根的原始层级
            int originalLayer = root.layer;
            _cgActorRootLayerDict.Add(root, originalLayer);

            SetWholeTreeLayer(root.transform, performanceLayer);
        }
    }

    /// <summary>
    /// 复原所有CG参演物体树：整棵树全部恢复为对应根的原始Layer，清空缓存
    /// </summary>
    public static void RestoreActorsOriginalLayer()
    {
        foreach (var kv in _cgActorRootLayerDict)
        {
            GameObject root = kv.Key;
            int restoreLayer = kv.Value;
            if (root == null) continue;
            SetWholeTreeLayer(root.transform, restoreLayer);
        }
        _cgActorRootLayerDict.Clear();
    }

    /// <summary>递归设置自身以及全部子物体layer</summary>
    private static void SetWholeTreeLayer(Transform tf, int targetLayer)
    {
        tf.gameObject.layer = targetLayer;
        foreach (Transform child in tf)
        {
            SetWholeTreeLayer(child, targetLayer);
        }
    }

    /// <summary>
    /// 设置CG相机遮罩：保存原mask，屏蔽指定层级（如Enemy、Player），并确保Performance层可见。
    /// 以后需要屏蔽更多层级，直接在调用时追加参数即可。
    /// </summary>
    public static void ApplyCGCameraCullingMask(Camera cgCamera, params string[] excludeLayers)
    {
        if (cgCamera == null) return;
        if (!_cameraOriginalMaskMap.ContainsKey(cgCamera))
        {
            _cameraOriginalMaskMap[cgCamera] = cgCamera.cullingMask;
        }
        int excludeMask = 0;
        foreach (string layerName in excludeLayers)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
                excludeMask |= (1 << layer);
        }
        int performanceLayer = LayerMask.NameToLayer(PerformanceLayerName);
        int performanceMask = performanceLayer >= 0 ? (1 << performanceLayer) : 0;
        cgCamera.cullingMask = (cgCamera.cullingMask & ~excludeMask) | performanceMask;
    }

    /// <summary>
    /// 恢复CG相机的原cullingMask。
    /// </summary>
    public static void RestoreCGCameraCullingMask(Camera cgCamera)
    {
        if (cgCamera == null) return;
        if (_cameraOriginalMaskMap.TryGetValue(cgCamera, out int originalMask))
        {
            cgCamera.cullingMask = originalMask;
            _cameraOriginalMaskMap.Remove(cgCamera);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("清除CG已播放标记(重置PlayerPrefs)")]
    private void ClearCGFlagEditor()
    {
        PlayerPrefs.DeleteKey("HasPlayedOpeningCG");
        PlayerPrefs.Save();
        if (Application.isPlaying)
        {
            _hasPlayedOpeningCG = false;
            _rainVfxAudioPlayed = false;
            if (_rainVfxAudioSource != null)
            {
                _rainVfxAudioSource.Stop();
            }
        }
    }
#endif
}
