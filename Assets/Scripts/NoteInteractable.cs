using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.Animations.Rigging;
using SunTemple;

public class NoteInteractable : SaveableEntity, IInteractable
{
    [Header("引用：挂UI面板上的NoteReader组件")]
    public NoteReader noteReader;
    [Header("演出配置（窗外光源闪电）")]
    public Light windowFlashLight;
    public AudioSource sfxAudioSource;
    public AudioClip thunderClip;
    public Animator zombieAnimator;
    public Door targetDoor;
    public Vector3 zombieTargetPos;
    public float moveDuration = 1.2f;
    [Header("CG触发器与真正战斗丧尸配置")]
    public GameObject cgTriggerGameObject;
    public Transform playerTeleportPos;
    [Tooltip("丧尸根父物体，检查器必须保持勾选Active=true，不要关闭父")]
    public GameObject realZombieActor;
    [Tooltip("丧尸子物体（带NavMeshAgent、AI、Animator），【编辑器必须保持勾选Active=true！！】由Awake代码运行时SetActive(false)隐藏，禁止编辑器手动取消勾选")]
    public GameObject zombieChildComponentObj;
    public Transform zombieTeleportPos;
    [Header("Timeline CG")]
    public PlayableDirector cutsceneDirector;
    [Header("门CG配置：完全打开的本地欧拉角(编辑器把门摆到最终打开姿态复制数值填这里)")]
    public Vector3 doorFullyOpenLocalEuler;
    [Header("交互提示设置")]
    public string interactPromptText = "阅读纸条";
    public Vector3 uiOffset = new Vector3(0, 1.2f, 0);
    public Vector3 uiRotation;
    public float uiScale = 0.01f;
    [Header("读完纸条闪电闪现后播放的旁白配音")]
    public AudioClip flashEndVoiceClip;
    public string flashEndSubtitleText;

    //====存档状态（父类SaveableEntity自带saveableID，编辑器右键菜单生成GUID）====
    [HideInInspector] public bool hasReadNote;
    [HideInInspector] public bool hasPlayedCG;
    [Header("当前纸条专属文本（多页，一页一条）")]
    public List<string> notePages = new List<string>();
    [Header("CG(Timeline)全部结束后播放的单句旁白")]
    public AudioClip cutsceneEndVoiceClip;
    public string cutsceneEndSubtitleText;

    private readonly Color flashColor = new Color(0.85f, 0.9f, 1f);
    private NavMeshAgent _zombieNavAgent;
    private ZombieControlOld _zombieAiController;
    private Animator _realZombieAnimator;
    private MultiAimConstraint _zombieMultiAim;
    private List<EnemyAIBase> _currentCinematicAiList;

    //====B方案：目标丧尸血量组件引用====
    private EnemyHealthBase _zombieHealth;
    //====BGM安全超时协程====
    private Coroutine _bgmSafetyTimeoutCoroutine;

    #region IInteractable 接口实现
    public void Interact()
    {
        OnInteractNote();
    }
    public string GetInteractPrompt()
    {
        return interactPromptText;
    }
    public InteractUIParams? GetUIParams()
    {
        return new InteractUIParams
        {
            offset = uiOffset,
            rotation = uiRotation,
            scale = uiScale
        };
    }
    #endregion

    //====重写存档抽象方法（来自SaveableEntity）====
    public override void SaveData()
    {
        SaveSystem.SaveEntity(saveableID, nameof(hasReadNote), hasReadNote);
        SaveSystem.SaveEntity(saveableID, nameof(hasPlayedCG), hasPlayedCG);
    }
    public override void LoadData()
    {
        hasReadNote = SaveSystem.LoadEntity(saveableID, nameof(hasReadNote), false);
        hasPlayedCG = SaveSystem.LoadEntity(saveableID, nameof(hasPlayedCG), false);
        //读档只控制CG触发器，丧尸由自身存档脚本管理，本脚本不处理丧尸对象
        if (hasReadNote)
        {
            if (hasPlayedCG)
            {
                //CG已经完整跑完，关闭触发器，禁止再次触发CG
                if (cgTriggerGameObject != null)
                    cgTriggerGameObject.SetActive(false);
            }
            else
            {
                //纸条读过，但CG尚未执行，保持触发器可用
                if (cgTriggerGameObject != null)
                    cgTriggerGameObject.SetActive(true);
            }
        }
    }

    private void Awake()
    {
        // Awake不再执行隐藏，只做预留，不操作物体
    }

    protected override void Start()
    {
        // LoadData由外部SaveSystem.LoadGame全局调用，此处不要写base.Start();
        // 读档完成后hasPlayedCG已经是存档真实状态
        if (zombieChildComponentObj != null && !hasPlayedCG)
        {
            zombieChildComponentObj.SetActive(false);
        }
    }

    public void OnInteractNote()
    {
        if (notePages == null || notePages.Count == 0)
        {
            Debug.LogError($"{gameObject.name} 纸条没有填写文本！");
            return;
        }
        if (hasReadNote)
        {
            noteReader.OpenNote(notePages);
            return;
        }
        noteReader.OpenNote(notePages);
        noteReader.OnNoteClosed += OnPlayerFinishReadNote;
    }

    void OnPlayerFinishReadNote()
    {
        noteReader.OnNoteClosed -= OnPlayerFinishReadNote;
        hasReadNote = true;
        if (targetDoor != null)
        {
            if (!targetDoor.DoorClosed)
            {
                targetDoor.Activate();
            }
        }
        if (cgTriggerGameObject != null)
        {
            cgTriggerGameObject.SetActive(true);
        }
        StartCoroutine(PlayNoteCutscene());
    }

    public void OnPlayerEnterCGTrigger()
    {
        if (!hasReadNote) return;
        if (cgTriggerGameObject == null) return;
        cgTriggerGameObject.SetActive(false);
        StartCoroutine(TriggerRealZombieCGCoroutine());
    }

    IEnumerator TriggerRealZombieCGCoroutine()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        //【时序修复：先锁定CG控制器，再传送玩家，避免朝向被ThirdPersonController覆盖】
        StartGameFlow.BeginCG();
        //====新增：标记本CG所有Timeline绑定参演怪物，CG冻结时不关闭其Animator====
        if (cutsceneDirector != null)
        {
            _currentCinematicAiList = TimelineCinematicHelper.MarkCinematicActors(cutsceneDirector);
        }
        //传送玩家
        if (playerObj != null && playerTeleportPos != null)
        {
            CharacterController cc = playerObj.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerObj.transform.position = playerTeleportPos.position;
            playerObj.transform.rotation = playerTeleportPos.rotation;
            if (cc != null) cc.enabled = true;
        }
        if (realZombieActor != null && zombieTeleportPos != null && zombieChildComponentObj != null)
        {
            realZombieActor.transform.position = zombieTeleportPos.position;
            realZombieActor.transform.rotation = zombieTeleportPos.rotation;
            zombieChildComponentObj.SetActive(true);
            _zombieNavAgent = zombieChildComponentObj.GetComponent<NavMeshAgent>();
            _zombieAiController = zombieChildComponentObj.GetComponent<ZombieControlOld>();
            _realZombieAnimator = zombieChildComponentObj.GetComponent<Animator>();
            _zombieMultiAim = zombieChildComponentObj.GetComponentInChildren<MultiAimConstraint>();

            //====B方案：获取血量组件，订阅死亡事件====
            _zombieHealth = zombieChildComponentObj.GetComponent<EnemyHealthBase>();
            if (_zombieHealth != null)
            {
                _zombieHealth.OnEnemyDied += OnTargetZombieDied;
            }

            if (_zombieNavAgent != null) _zombieNavAgent.enabled = false;
            if (_zombieAiController != null) _zombieAiController.enabled = false;
            if (_realZombieAnimator != null) _realZombieAnimator.enabled = true;
            //==== CG开始：MultiAimConstraint权重置1，允许Timeline驱动头部看向 ====
            if (_zombieMultiAim != null)
            {
                _zombieMultiAim.weight = 1f;
            }
        }
        if (cutsceneDirector != null)
        {
            cutsceneDirector.Stop();
            cutsceneDirector.stopped += OnTimelineCutsceneFinished;
            cutsceneDirector.Play();
        }
        yield break;
    }

    //====B方案：目标丧尸死亡回调====
    void OnTargetZombieDied()
    {

        //丧尸死亡后，10秒渐淡BGM至0
        CutsceneSignalReceiver.Instance?.StartBgmFadeOut(10f);

        //丧尸已死，停止安全超时协程，防止二次淡出
        if (_bgmSafetyTimeoutCoroutine != null)
        {
            StopCoroutine(_bgmSafetyTimeoutCoroutine);
            _bgmSafetyTimeoutCoroutine = null;
        }

        //在这里写你的业务：播放CG、解锁、语音等
    }

    void OnTimelineCutsceneFinished(PlayableDirector director)
    {
        director.stopped -= OnTimelineCutsceneFinished;
        //====新增：先复原CG参演标记，再解除战斗冻结（顺序不能反）====
        TimelineCinematicHelper.RestoreCinematicActors(_currentCinematicAiList);
        //标记CG完整播放完毕，存档时持久化
        hasPlayedCG = true;
        StartGameFlow.EndCG();
        //==== CG结束：MultiAimConstraint权重置0，关闭约束，防止重置低头 ====
        if (_zombieMultiAim != null)
        {
            _zombieMultiAim.weight = 0f;
        }
        //同步门视觉与业务状态
        if (targetDoor != null)
        {
            targetDoor.transform.localEulerAngles = doorFullyOpenLocalEuler;
            targetDoor.DoorClosed = false;
            if (targetDoor.navMeshLink != null)
            {
                targetDoor.navMeshLink.enabled = true;
            }
            targetDoor.enabled = false;
            targetDoor.enabled = true;
        }
        //CG运行时恢复丧尸组件；读档丧尸状态交给EnemySaveManager
        if (realZombieActor != null && zombieChildComponentObj != null && zombieChildComponentObj.activeSelf)
        {
            if (_zombieNavAgent != null)
            {
                _zombieNavAgent.enabled = true;
                _zombieNavAgent.Warp(realZombieActor.transform.position);
            }
            if (_zombieAiController != null) _zombieAiController.enabled = true;
            if (_realZombieAnimator != null) _realZombieAnimator.enabled = true;
            // ========= 删除强制满血+Revive整套逻辑，血量状态交给EnemySaveManager =========
        }
        //===== 新增：Timeline CG全部结束后播放单句旁白字幕 =====
        if (VoiceSubtitlePlayer.Instance != null && cutsceneEndVoiceClip != null)
        {
            VoiceSubtitlePlayer.Instance.PlaySingleVoice(cutsceneEndVoiceClip, cutsceneEndSubtitleText);
        }

        //====BGM安全超时保护：CG结束后60秒强制淡出，防止丧尸卡死不死BGM无限循环====
        if (_bgmSafetyTimeoutCoroutine != null)
        {
            StopCoroutine(_bgmSafetyTimeoutCoroutine);
        }
        _bgmSafetyTimeoutCoroutine = StartCoroutine(Coroutine_BgmSafetyTimeout(60f));

        // 条件：必须完全空手（没有武器，也没有拿手雷）才执行，否则直接跳过
        if (ThirdPersonShooterController.Instance == null || !ThirdPersonShooterController.Instance.IsCompletelyUnarmed())
        {
            return;
        }
        WeaponQuickBarSlot targetQuickSlotWithPistol = null;
        WeaponQuickBarSlot[] allQuickSlots = WeaponQuickBarUI.Instance.GetComponentsInChildren<WeaponQuickBarSlot>(true);
        // ①遍历4个枪械快捷栏slotIndex:0~3，查找有没有手枪
        foreach (var qs in allQuickSlots)
        {
            if (qs.slotIndex >= 0 && qs.slotIndex <= 3) //只看1‑4对应枪格
            {
                if (qs.currentItem != null && qs.currentItem.itemName == "手枪")
                {
                    targetQuickSlotWithPistol = qs;
                    break;
                }
            }
        }
        if (targetQuickSlotWithPistol != null)
        {
            // 情况A：快捷栏枪格已经存在手枪，直接装备它
            WeaponManager.Instance.EquipWeaponByItem(targetQuickSlotWithPistol.currentItem);
            return;
        }
        // ②快捷栏枪格里没有手枪 → 去背包寻找手枪
        SlotUI bagPistolSlot = null;
        foreach (var bagSlot in Inventory.instance.slots)
        {
            if (bagSlot.currentItem != null && bagSlot.currentItem.itemName == "手枪")
            {
                bagPistolSlot = bagSlot;
                break;
            }
        }
        if (bagPistolSlot == null)
        {
            // 背包也没有手枪，什么都不做
            return;
        }
        // ③背包找到了手枪，寻找【第一个空的枪快捷栏slotIndex0‑3】
        WeaponQuickBarSlot firstEmptyGunQuickSlot = null;
        foreach (var qs in allQuickSlots)
        {
            if (qs.slotIndex >= 0 && qs.slotIndex <= 3)
            {
                if (qs.currentItem == null)
                {
                    firstEmptyGunQuickSlot = qs;
                    break;
                }
            }
        }
        if (firstEmptyGunQuickSlot == null)
        {
            //4个枪格子全部被占满，没有空位，什么也不做
            return;
        }
        // ④把背包的手枪移动到找到的空快捷栏格子，然后装备
        WeaponQuickBarUI.Instance.EquipWeaponToQuickBar(bagPistolSlot, firstEmptyGunQuickSlot);
        //移动完成后装备这个槽的手枪
        if (firstEmptyGunQuickSlot.currentItem != null)
        {
            WeaponManager.Instance.EquipWeaponByItem(firstEmptyGunQuickSlot.currentItem);
        }
    }

    /// <summary>BGM安全超时：超过maxWait秒还没有触发丧尸死亡，则强制淡出BGM</summary>
    IEnumerator Coroutine_BgmSafetyTimeout(float maxWait)
    {
        yield return new WaitForSeconds(maxWait);
        Debug.LogWarning("CG战斗BGM安全超时触发：丧尸长时间未死亡，强制淡出战斗BGM");
        CutsceneSignalReceiver.Instance?.StartBgmFadeOut(10f);
        _bgmSafetyTimeoutCoroutine = null;
    }

    IEnumerator DoWindowFlash()
    {
        if (windowFlashLight == null) yield break;
        if (sfxAudioSource != null && thunderClip != null)
        {
            sfxAudioSource.PlayOneShot(thunderClip);
        }
        windowFlashLight.color = flashColor;
        windowFlashLight.enabled = true;
        float t = 0;
        float totalTime = 1.0f;
        while (t < totalTime)
        {
            t += Time.deltaTime;
            float progress = t / totalTime;
            if (progress < 0.25f)
            {
                windowFlashLight.intensity = Mathf.Lerp(0, 2, progress / 0.25f);
            }
            else if (progress < 0.5f)
            {
                float p = (progress - 0.25f) / 0.25f;
                windowFlashLight.intensity = Mathf.Lerp(2, 1, p);
            }
            else if (progress < 0.75f)
            {
                float p = (progress - 0.5f) / 0.25f;
                windowFlashLight.intensity = Mathf.Lerp(1, 2, p);
            }
            else
            {
                float p = (progress - 0.75f) / 0.25f;
                windowFlashLight.intensity = Mathf.Lerp(2, 0, p);
            }
            yield return null;
        }
        windowFlashLight.intensity = 0;
        windowFlashLight.enabled = false;
    }

    /// <summary>读完纸条瞬间打雷闪现演出：复用真实丧尸，先开子，再移动父</summary>
    IEnumerator PlayNoteCutscene()
    {
        if (realZombieActor == null || zombieChildComponentObj == null)
            yield break;
        // =====新增：打雷一瞬间设置丧尸父物体Y轴旋转 -123.902=====
        Vector3 rot = realZombieActor.transform.localEulerAngles;
        rot.y = -123.902f;
        realZombieActor.transform.localEulerAngles = rot;
        StartCoroutine(DoWindowFlash());
        //激活丧尸子物体
        zombieChildComponentObj.SetActive(true);
        //激活后立刻禁用NavMeshAgent，防止演出期间AI启动以及贴地拖拽
        NavMeshAgent agent = zombieChildComponentObj.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }
        float t = 0;
        Vector3 startPos = realZombieActor.transform.position;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            realZombieActor.transform.position = Vector3.Lerp(startPos, zombieTargetPos, t / moveDuration);
            yield return null;
        }
        zombieChildComponentObj.SetActive(false);
        if (VoiceSubtitlePlayer.Instance != null && flashEndVoiceClip != null)
        {
            VoiceSubtitlePlayer.Instance.PlaySingleVoice(flashEndVoiceClip, flashEndSubtitleText);
        }
        yield break;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (noteReader != null)
        {
            noteReader.OnNoteClosed -= OnPlayerFinishReadNote;
        }

        //退订丧尸死亡事件，防止内存泄漏
        if (_zombieHealth != null)
        {
            _zombieHealth.OnEnemyDied -= OnTargetZombieDied;
            _zombieHealth = null;
        }

        //清理BGM超时协程，强制停止战斗BGM
        if (_bgmSafetyTimeoutCoroutine != null)
        {
            StopCoroutine(_bgmSafetyTimeoutCoroutine);
            _bgmSafetyTimeoutCoroutine = null;
        }
        CutsceneSignalReceiver.Instance?.ForceStopCombatBGM();
    }
}
