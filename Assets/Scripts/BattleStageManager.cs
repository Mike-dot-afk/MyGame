using SunTemple;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.Animations.Rigging;

[RequireComponent(typeof(TriggerEnemyActivator))]
public class BattleStageManager : SaveableEntity
{
    [Header("精英怪【场景现成对象，放触发器球体外面，编辑器保持Active=true】")]
    public GameObject eliteSceneActor;
    public Transform eliteSpawnPoint;
    [Header("精英CG设置")]
    public OneWayDoubleDoor eliteSpawnDoor;
    public Transform eliteCGDoorPoint;     // CG演出点位：门内侧
    [Header("CG配置")]
    public PlayableDirector eliteSpawnCutscene;
    public Camera eliteCGCamera;
    public PlayableDirector battleWinCutscene;
    public Camera winCGCamera;
    [Header("战斗参数")]
    public float battleTotalTime = 360f;      //6分钟通关
    public float eliteTriggerTime = 240f;    //4分钟刷精英
    public int winKillNormal = 15;           //杀15只通关
    public int eliteSpawnKillThreshold = 10; //杀10只刷精英
    [Header("精英CG Rig约束(拖拽赋值，不要运行时查找)")]
    public TwoBoneIKConstraint eliteTwoBoneIKRightArm;
    public TwoBoneIKConstraint eliteTwoBoneIKLeftArm;
    public MultiAimConstraint eliteMultiAimHead;
    [Header("战斗BGM")]
    public AudioClip battleBgm;
    private AudioSource _bgmAudioSource;

    //===== 存档持久化字段 =====
    [HideInInspector] public bool battleStarted;
    [HideInInspector] public float battleElapsedTime;
    [HideInInspector] public int normalKillCount;
    [HideInInspector] public bool eliteSpawned;
    [HideInInspector] public bool eliteDead;
    [HideInInspector] public bool battleComplete;

    //运行时
    private TriggerEnemyActivator _trigger;
    private GameObject _eliteInstance;
    private HashSet<GameObject> _countedEnemySet = new HashSet<GameObject>();
    private bool _bgmPausedByTimeScale;
    private bool _killSetInitialized;

    private void Awake()
    {
        _trigger = GetComponent<TriggerEnemyActivator>();
        //初始化BGM音源
        _bgmAudioSource = GetComponent<AudioSource>();
        if (_bgmAudioSource == null)
        {
            _bgmAudioSource = gameObject.AddComponent<AudioSource>();
        }
        _bgmAudioSource.loop = true;          // 开启循环播放
        _bgmAudioSource.spatialBlend = 0f;    //2D全局音乐
        _bgmAudioSource.playOnAwake = false;
    }

    protected override void Start()
    {
        // 注册战斗BGM到统一音频控制器，UI打开时压低
        if (AudioManager.Instance != null && _bgmAudioSource != null)
            AudioManager.Instance.RegisterBGM(_bgmAudioSource);
        // LoadData由外部SaveSystem.LoadGame调用，此处不手动调用
        if (battleComplete)
            return;
        // 存档未生成精英，则初始隐藏场景精英；精英Awake已经先执行完毕
        if (!eliteSpawned && eliteSceneActor != null)
        {
            eliteSceneActor.SetActive(false);
        }
        // 读档后精英已经生成，直接赋值实例引用
        if (eliteSpawned && eliteSceneActor != null)
        {
            _eliteInstance = eliteSceneActor;
        }
    }

    private void Update()
    {
        if (!_killSetInitialized)
        {
            _countedEnemySet.Clear();
            if (_trigger.EnemiesInTrigger != null)
            {
                foreach (var go in _trigger.EnemiesInTrigger)
                {
                    if (go != null && go.TryGetComponent(out IEnemyHealth hp) && hp.isDead())
                    {
                        _countedEnemySet.Add(go);
                    }
                }
            }
            _killSetInitialized = true;
        }
        // ==========方案A：根据Time.timeScale自动暂停/恢复BGM==========
        if (_bgmAudioSource != null)
        {
            if (AudioManager.UIAudioPaused)
            {
                if (_bgmPausedByTimeScale)
                {
                    _bgmAudioSource.UnPause();
                    _bgmPausedByTimeScale = false;
                }
            }
            else if (Time.timeScale < 0.01f)
            {
                if (!_bgmPausedByTimeScale && _bgmAudioSource.isPlaying)
                {
                    _bgmAudioSource.Pause();
                    _bgmPausedByTimeScale = true;
                }
            }
            else
            {
                if (_bgmPausedByTimeScale)
                {
                    _bgmAudioSource.UnPause();
                    _bgmPausedByTimeScale = false;
                }
            }
        }
        // ==========================================================

        if (battleComplete) return;
        if (!battleStarted)
        {
            CheckBattleStartCondition();
            return;
        }
        // ========= 对齐StartGameFlow：CG播放期间停止战斗计时 =========
        // Time.deltaTime自动受timeScale=0(游戏暂停/打开UI)影响，暂停时不会累加战斗时间
        if (StartGameFlow.IsCGPlaying || StartGameFlow.IsFightFrozen)
            return;
        battleElapsedTime += Time.deltaTime;
        ScanNormalEnemyKills();
        if (!eliteSpawned)
        {
            bool condition1 = battleElapsedTime >= eliteTriggerTime;
            bool condition2 = normalKillCount >= eliteSpawnKillThreshold;
            if (condition1 || condition2)
            {
                SpawnEliteWithCutscene();
            }
        }
        if (eliteSpawned && !eliteDead && _eliteInstance != null)
        {
            if (_eliteInstance.TryGetComponent(out IEnemyHealth hp) && hp.isDead())
            {
                eliteDead = true;
            }
        }
        CheckWinCondition();
    }

    /// <summary>
    /// 战斗开启判定：玩家进入触发器区域前提下
    /// 满足【任意敌人发现玩家】 OR 【任意敌人受到过伤害】，战斗正式开始
    /// </summary>
    void CheckBattleStartCondition()
    {
        if (_trigger.EnemiesInTrigger == null) return;
        foreach (var enemyGo in _trigger.EnemiesInTrigger)
        {
            if (enemyGo == null || !enemyGo.activeSelf) continue;
            EnemyAIBase ai = enemyGo.GetComponent<EnemyAIBase>();
            IEnemyHealth health = enemyGo.GetComponent<IEnemyHealth>();
            //条件1：怪物已经发现玩家
            bool alertPlayer = (ai != null && ai.HasAlertPlayer);
            //条件2：怪物血量小于最大血量 = 受过伤害（排除已经死亡的）
            bool gotDamage = false;
            if (health != null && !health.isDead())
            {
                //注意IEnemyHealth没有暴露maxHp，这里强转实际实现类EnemyHealthBase
                if (health is EnemyHealthBase eh)
                {
                    gotDamage = eh.currentHp < eh.maxHealth;
                }
            }
            if (alertPlayer || gotDamage)
            {
                battleStarted = true;
                PlayBattleBGM();
                Debug.Log($"[BattleStage] 战斗开始");
                break;
            }
        }
    }

    void ScanNormalEnemyKills()
    {
        if (_trigger.EnemiesInTrigger == null) return;
        foreach (var enemyGo in _trigger.EnemiesInTrigger)
        {
            if (enemyGo == null) continue;
            if (_eliteInstance != null && enemyGo == _eliteInstance) continue;
            if (_countedEnemySet.Contains(enemyGo)) continue;
            if (enemyGo.TryGetComponent(out IEnemyHealth hp) && hp.isDead())
            {
                _countedEnemySet.Add(enemyGo);
                normalKillCount++;
            }
        }
    }

    void SpawnEliteWithCutscene()
    {
        eliteSpawned = true;
        if (eliteSceneActor == null || eliteSpawnPoint == null)
        {
            return;
        }
        _eliteInstance = eliteSceneActor;
        _eliteInstance.SetActive(true);
        if (eliteSpawnCutscene == null)
        {
            return;
        }
        // CG重播兜底：销毁旧Graph，避免Animator被读档Rebind后无法重新驱动
        if (eliteSpawnCutscene.playableGraph.IsValid())
        {
            eliteSpawnCutscene.playableGraph.Destroy();
        }
        CinematicPlayer.Play(
            eliteSpawnCutscene,
            eliteCGCamera,
            // onCgStart：Timeline Play之前执行
            () =>
            {
                //CG开始压低BGM音量
                if (_bgmAudioSource != null) _bgmAudioSource.volume = 0.3f;

                // 读档后死亡冻结恢复可能被错过，Animator可能保持禁用，CG前强制恢复（Timeline才能驱动）
                Animator eliteAnimator = _eliteInstance.GetComponent<Animator>();
                if (eliteAnimator != null && !eliteAnimator.enabled)
                {
                    eliteAnimator.enabled = true;
                }

                if (eliteCGDoorPoint != null)
                {
                    _eliteInstance.transform.SetPositionAndRotation(eliteCGDoorPoint.position, eliteCGDoorPoint.rotation);
                }
                NavMeshAgent agent = _eliteInstance.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = false;
                }
            },
            // onCgFinish：全部复原完成，EndCG之前执行
            () =>
            {
                //CG结束恢复BGM音量
                if (_bgmAudioSource != null) _bgmAudioSource.volume = 1f;

                //Rig兜底归零
                if (eliteTwoBoneIKRightArm != null) eliteTwoBoneIKRightArm.weight = 0f;
                if (eliteTwoBoneIKLeftArm != null) eliteTwoBoneIKLeftArm.weight = 0f;
                if (eliteMultiAimHead != null) eliteMultiAimHead.weight = 0f;
                if (eliteSpawnDoor != null)
                {
                    eliteSpawnDoor.ForceHardCloseImmediate();
                }
                if (_eliteInstance != null)
                {
                    _eliteInstance.transform.SetPositionAndRotation(eliteSpawnPoint.position, eliteSpawnPoint.rotation);
                    NavMeshAgent agent = _eliteInstance.GetComponent<NavMeshAgent>();
                    if (agent != null)
                    {
                        agent.enabled = true;
                        agent.isStopped = false;
                        agent.Warp(eliteSpawnPoint.position);
                    }
                }
            }
        );
    }

    void CheckWinCondition()
    {
        if (battleComplete) return; // 如果已经通关直接退出，不往下跑
        bool condTime = battleElapsedTime >= battleTotalTime;
        bool condKill = normalKillCount >= winKillNormal;
        bool condEliteKill = eliteDead;
        if (condTime || condKill || condEliteKill)
        {
            battleComplete = true;
            StopBattleBGM();
            Debug.Log($"[BattleStage]战斗通关。时间:{battleElapsedTime:F1} 击杀:{normalKillCount} 精英死亡:{eliteDead}");
            PlayWinCutscene();
        }
    }

    void PlayWinCutscene()
    {
        if (battleWinCutscene == null) return;
        CinematicPlayer.Play(
            battleWinCutscene,
            winCGCamera,
            () =>
            {
                //胜利CG播放前无额外业务
                //新增：取消精英怪死亡延迟隐藏，防止死亡动画播放中消失
                if (_eliteInstance != null && _eliteInstance.TryGetComponent(out EnemyHealthBase eh))
                {
                    eh.CancelDeathDispose();
                }
            },
            () =>
            {
                //========胜利CG全部播完，执行批量怪物死亡+立即隐藏========
                KillAllAliveEnemiesImmediate();
            }
        );
    }

    /// <summary>
    /// 胜利CG结束：所有存活怪物执行死亡逻辑，随后立刻隐藏，跳过延迟销毁
    /// 顺序：先跑死亡逻辑 → 再隐藏对象
    /// 额外兜底：单独处理精英实例，不受触发器集合限制
    /// </summary>
    private void KillAllAliveEnemiesImmediate()
    {
        //1.处理触发器内普通怪物
        if (_trigger == null || _trigger.EnemiesInTrigger == null)
            return;

        foreach (var enemyGo in _trigger.EnemiesInTrigger)
        {
            if (enemyGo == null || !enemyGo.activeSelf)
                continue;

            if (!enemyGo.TryGetComponent(out IEnemyHealth enemyHealth))
                continue;
            if (!enemyGo.TryGetComponent(out IDamageable damageable))
                continue;

            if (enemyHealth.isDead())
                continue;

            damageable.TakeDamage(99999f, enemyGo.transform.position);
            enemyGo.SetActive(false);
        }

        //==== 2.精英单独兜底分支：不受触发器球体是否包含的影响 ====
        if (_eliteInstance != null && _eliteInstance.activeSelf)
        {
            Debug.Log($"[BattleStage-Debug] 进入精英兜底处理: {_eliteInstance.name}");
            if (_eliteInstance.TryGetComponent(out IEnemyHealth eh)
                && _eliteInstance.TryGetComponent(out IDamageable dmg)
                && !eh.isDead())
            {
                Debug.Log($"[BattleStage-Debug] 精英存活，调用TakeDamage");
                dmg.TakeDamage(99999f, _eliteInstance.transform.position);
                Debug.Log($"[BattleStage-Debug] TakeDamage后精英 isDead={eh.isDead()}");
                _eliteInstance.SetActive(false);
                eliteDead = true;
                Debug.Log($"[BattleStage-Debug] 精英处理完成，设置eliteDead=true");
            }
            else
            {
                Debug.Log($"[BattleStage-Debug] 精英跳过处理，isDead={eh?.isDead()}");
            }
        }

        //==== 3.新增：胜利CG结束，无论精英是否提前死亡，统一隐藏，防止尸体残留 ====
        if (_eliteInstance != null)
        {
            _eliteInstance.SetActive(false);
        }

        Debug.Log("[BattleStage]胜利CG结束，全部存活怪物已执行死亡逻辑并立即隐藏");
    }

    /// <summary>Signal信号调用：打开全部Rig权重 weight=1（CG演出开启IK/Aim）</summary>
    public void OpenEliteRigWeight()
    {
        if (eliteTwoBoneIKRightArm != null) eliteTwoBoneIKRightArm.weight = 1f;
        if (eliteTwoBoneIKLeftArm != null) eliteTwoBoneIKLeftArm.weight = 1f;
        if (eliteMultiAimHead != null) eliteMultiAimHead.weight = 1f;
    }

    /// <summary>Signal信号调用：关闭全部Rig权重 weight=0（CG演出结束关闭IK/Aim）</summary>
    public void CloseEliteRigWeight()
    {
        if (eliteTwoBoneIKRightArm != null) eliteTwoBoneIKRightArm.weight = 0f;
        if (eliteTwoBoneIKLeftArm != null) eliteTwoBoneIKLeftArm.weight = 0f;
        if (eliteMultiAimHead != null) eliteMultiAimHead.weight = 0f;
    }

    private void OnDisable()
    {
        // NoteInteractable同款OnDisable保护：脚本被销毁/关闭时强制重置Rig权重
        if (eliteTwoBoneIKRightArm != null) eliteTwoBoneIKRightArm.weight = 0f;
        if (eliteTwoBoneIKLeftArm != null) eliteTwoBoneIKLeftArm.weight = 0f;
        if (eliteMultiAimHead != null) eliteMultiAimHead.weight = 0f;
    }

    #region BGM工具方法
    /// <summary>开始播放循环战斗BGM</summary>
    private void PlayBattleBGM()
    {
        if (battleBgm == null || _bgmAudioSource == null) return;
        if (_bgmAudioSource.isPlaying) return;

        _bgmAudioSource.clip = battleBgm;
        _bgmAudioSource.Play();
        _bgmPausedByTimeScale = false;
        Debug.Log("[BattleStage] 开始播放战斗BGM");
    }

    /// <summary>停止战斗BGM【public，玩家死亡脚本可以调用这个】</summary>
    public void StopBattleBGM()
    {
        if (_bgmAudioSource == null) return;
        if (!_bgmAudioSource.isPlaying) return;

        _bgmAudioSource.Stop();
        _bgmPausedByTimeScale = false;
        Debug.Log("[BattleStage] 停止战斗BGM");
    }
    #endregion

    #region ISaveable 实现，完全贴合你的SaveSystem
    public override void SaveData()
    {
        SaveSystem.SaveEntity(saveableID, nameof(battleStarted), battleStarted);
        SaveSystem.SaveEntity(saveableID, nameof(battleElapsedTime), battleElapsedTime);
        SaveSystem.SaveEntity(saveableID, nameof(normalKillCount), normalKillCount);
        SaveSystem.SaveEntity(saveableID, nameof(eliteSpawned), eliteSpawned);
        SaveSystem.SaveEntity(saveableID, nameof(eliteDead), eliteDead);
        SaveSystem.SaveEntity(saveableID, nameof(battleComplete), battleComplete);
    }

    public override void LoadData()
    {
        battleStarted = SaveSystem.LoadEntity(saveableID, nameof(battleStarted), false);
        battleElapsedTime = SaveSystem.LoadEntity(saveableID, nameof(battleElapsedTime), 0f);
        normalKillCount = SaveSystem.LoadEntity(saveableID, nameof(normalKillCount), 0);
        eliteSpawned = SaveSystem.LoadEntity(saveableID, nameof(eliteSpawned), false);
        eliteDead = SaveSystem.LoadEntity(saveableID, nameof(eliteDead), false);
        battleComplete = SaveSystem.LoadEntity(saveableID, nameof(battleComplete), false);
        // 使用场景引用，不再FindWithTag
        if (eliteSpawned && eliteSceneActor != null)
        {
            _eliteInstance = eliteSceneActor;
        }
        // ==========【读档兜底，CG已经播放完毕强制重置Rig权重】==========
        if (eliteSpawned)
        {
            if (eliteTwoBoneIKRightArm != null) eliteTwoBoneIKRightArm.weight = 0f;
            if (eliteTwoBoneIKLeftArm != null) eliteTwoBoneIKLeftArm.weight = 0f;
            if (eliteMultiAimHead != null) eliteMultiAimHead.weight = 0f;
        }

        //读档BGM恢复逻辑：读档从头播放战斗BGM
        if (battleStarted && !battleComplete)
        {
            PlayBattleBGM();
        }
        else
        {
            StopBattleBGM();
        }
        // ====================================================================
        _killSetInitialized = false;
    }
    #endregion
}
