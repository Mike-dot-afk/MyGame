using UnityEngine;
public class ChopperController : MonoBehaviour
{
    [Header("直升机移动参数")]
    public float moveSpeed = 100f;       // 飞行速度 m/s
    public float followCamSpeed = 40f;   // 飞过400米后，和镜头二同步的飞行速度
    public float delaySeconds = 4f;     // 点击开始后延迟4秒启动
    public float soundRampStartOffset = 2f; // 调用后第2秒才开始声音渐强
    public float soundRampDuration = 2f;    // 渐强持续2秒(2s→4s刚好拉满)
    public float targetDistance = 400f; // 需要向前飞行400米
    [Header("直升机音效")]
    public AudioSource heliAudioSource;
    public float maxVolume = 0.7f;
    [Header("【新增】镜头3锚点空物体")]
    public Transform camera3Anchor;

    [Header("引擎8秒衰减配置")]
    public float engineFadeTotalTime = 8f;

    private Rigidbody _heliRb;
    private bool _isStartFly = false;
    private bool _isFollowCamState = false;
    private float _flyDistance;
    private Vector3 _flyForwardDir;
    private float _timer;
    private bool _isCountingDown;
    // 新增：防止Timeline信号重复触发
    private bool _hasSnapToCam3Anchor = false;

    //音量衰减到0
    private bool _doEngineFadeToZero;
    private float _engineFadeTimer;
    private float _fadeStartVolume; //记录触发瞬间的音量

    public Vector3 FlyForwardDir => _flyForwardDir;
    public bool IsFollowCamState => _isFollowCamState;

    void Awake()
    {
        _heliRb = GetComponent<Rigidbody>();
        if (_heliRb == null)
        {
            _heliRb = gameObject.AddComponent<Rigidbody>();
        }
        _heliRb.useGravity = false;
        _heliRb.isKinematic = false;
        if (heliAudioSource != null)
        {
            heliAudioSource.Stop();
            heliAudioSource.volume = 0f;
        }
        _timer = 0f;
        _isCountingDown = false;
        _hasSnapToCam3Anchor = false;

        //初始化衰减变量
        _doEngineFadeToZero = false;
        _engineFadeTimer = 0f;
        _fadeStartVolume = 0f;
    }

    public void StartFlyCountDown()
    {
        _timer = 0f;
        _isCountingDown = true;
        _isFollowCamState = false;
        _hasSnapToCam3Anchor = false;

        //重置音量衰减状态，防止上一局残留
        _doEngineFadeToZero = false;
        _engineFadeTimer = 0f;

        if (heliAudioSource != null)
        {
            heliAudioSource.volume = 0f;
            heliAudioSource.Play();
        }
    }

    void Update()
    {
        // 最开头！音量衰减逻辑
        if (_doEngineFadeToZero && heliAudioSource != null)
        {
            _engineFadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_engineFadeTimer / engineFadeTotalTime);
            heliAudioSource.volume = Mathf.Lerp(_fadeStartVolume, 0f, t);
            if (t >= 1f)
            {
                _doEngineFadeToZero = false;
            }
        }

        if (!_isCountingDown) return;
        _timer += Time.deltaTime;
        if (_timer >= soundRampStartOffset && _timer < delaySeconds)
        {
            float rampElapsed = _timer - soundRampStartOffset;
            float t = Mathf.Clamp01(rampElapsed / soundRampDuration);
            if (heliAudioSource != null)
            {
                heliAudioSource.volume = Mathf.Lerp(0, maxVolume, t);
            }
        }
        if (_timer >= delaySeconds)
        {
            _isCountingDown = false;
            BeginFly();
        }
    }

    void BeginFly()
    {
        _flyForwardDir = transform.TransformDirection(Vector3.forward);
        _flyDistance = 0f;
        _isStartFly = true;
        _isFollowCamState = false;
        if (heliAudioSource != null)
        {
            heliAudioSource.volume = maxVolume;
        }
    }

    void FixedUpdate()
    {
        if (!_isStartFly) return;
        if (_isFollowCamState)
        {
            _heliRb.velocity = _flyForwardDir * followCamSpeed;
        }
        else
        {
            float delta = Time.fixedDeltaTime;
            float moveStep = moveSpeed * delta;
            _flyDistance += moveStep;
            _heliRb.velocity = _flyForwardDir * moveSpeed;
            if (_flyDistance >= targetDistance)
            {
                _isFollowCamState = true;
                //正在衰减的时候，不执行降到0.2倍，避免冲突
                if (heliAudioSource != null && !_doEngineFadeToZero)
                {
                    heliAudioSource.volume = maxVolume * 0.2f;
                }
            }
        }
    }

    /// <summary>
    /// 供 Timeline Signal 信号直接调用（Public，无参数）
    /// 在Timeline执行时机(PostLateUpdate)执行，与相机激活轨道同一批次，渲染前完成
    /// </summary>
    public void SwitchToCamera3Anchor()
    {
        if (_hasSnapToCam3Anchor) return;
        if (camera3Anchor == null)
        {
            Debug.LogError("ChopperController: camera3Anchor 锚点为空！");
            return;
        }
        _hasSnapToCam3Anchor = true;
        // 停止飞行逻辑
        _isStartFly = false;
        _isFollowCamState = false;
        // 清除刚体运动，消除物理惯性
        _heliRb.velocity = Vector3.zero;
        _heliRb.angularVelocity = Vector3.zero;
        // 挂载锚点，本地归0
        transform.SetParent(camera3Anchor);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        // 挂载锚点后，刚体改为运动学，完全由Transform父物体驱动，防止物理继续干扰位置
        _heliRb.isKinematic = true;
    }

    /// <summary>外部调用：立刻启动8秒引擎音量平滑降到0</summary>
    public void StartEngineVolumeFadeToZero()
    {
        if (heliAudioSource == null) return;
        _doEngineFadeToZero = true;
        _engineFadeTimer = 0f;
        _fadeStartVolume = heliAudioSource.volume;
    }

    private void OnDestroy()
    {
        if (heliAudioSource != null)
        {
            heliAudioSource.Stop();
        }
    }
}