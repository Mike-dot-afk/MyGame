using UnityEngine;

public class ParachuteItem : MonoBehaviour
{
    [Header("子物体引用")]
    public GameObject parachuteBroken;
    public GameObject parachuteNormal;

    [Header("碎片模拟（仅二号降落伞触发）")]
    public GameObject[] debrisObjects;
    public float debrisFallSpeed = 15f;
    public float debrisHideDelay = 1f;

    [Header("运动参数")]
    public float fallSpeed1 = 2.0f;
    public float fallSpeed2 = 0.6f;
    public float hitFallSpeed = 3.5f;
    public float waitOpenTime = 2.0f;
    public float scaleDuration = 1.0f;
    public float scaleStart = 1f;
    public float scaleEnd = 6f;

    [Header("Z轴水平移动")]
    public float horizontalSpeedZ = 1.2f;
    public bool useHorizontalMove;

    private float timer;
    private bool isScaling;
    private bool switchFallSpeed;
    private bool isStopMove;
    private bool isHit;
    private float debrisTimer;
    private Transform parachuteTarget;

    public float CurrentFallSpeed
    {
        get
        {
            if (isStopMove) return 0f;
            if (isHit)
            {
                return hitFallSpeed;
            }
            if (timer >= waitOpenTime && switchFallSpeed)
            {
                return fallSpeed2;
            }
            return fallSpeed1;
        }
    }

    void Start()
    {
        parachuteBroken.SetActive(false);
        parachuteNormal.SetActive(false);
        timer = 0;
        isScaling = false;
        switchFallSpeed = false;
        isStopMove = false;
        isHit = false;
        debrisTimer = 0;
    }

    void Update()
    {
        if (isStopMove) return;

        timer += Time.deltaTime;
        Vector3 moveDelta = Vector3.zero;

        if (timer < waitOpenTime)
        {
            moveDelta += Vector3.down * fallSpeed1 * Time.deltaTime;
        }
        else
        {
            if (!isScaling && !isHit)
            {
                parachuteNormal.SetActive(true);
                parachuteTarget = parachuteNormal.transform;
                parachuteTarget.localScale = Vector3.one * scaleStart;
                isScaling = true;
            }
            if (isScaling && !isHit)
            {
                float scaleProgress = (timer - waitOpenTime) / scaleDuration;
                float currentScale = Mathf.Lerp(scaleStart, scaleEnd, scaleProgress);
                parachuteTarget.localScale = Vector3.one * currentScale;
                if (scaleProgress >= 1.0f)
                {
                    isScaling = false;
                    switchFallSpeed = true;
                }
            }
            if (switchFallSpeed)
            {
                if (isHit)
                {
                    moveDelta += Vector3.down * hitFallSpeed * Time.deltaTime;
                }
                else
                {
                    moveDelta += Vector3.down * fallSpeed2 * Time.deltaTime;
                }
            }
            else
            {
                moveDelta += Vector3.down * fallSpeed1 * Time.deltaTime;
            }
        }

        if (useHorizontalMove)
        {
            moveDelta += Vector3.forward * horizontalSpeedZ * Time.deltaTime;
        }

        transform.position += moveDelta;

        // 被击中 每帧强制维护伞显示状态，解决两个伞同时打开bug
        if (isHit)
        {
            parachuteNormal.SetActive(false);
            parachuteBroken.SetActive(true);

            debrisTimer += Time.deltaTime;
            foreach (var deb in debrisObjects)
            {
                if (deb != null && deb.activeSelf)
                {
                    deb.transform.position += Vector3.down * debrisFallSpeed * Time.deltaTime;
                }
            }
            if (debrisTimer >= debrisHideDelay)
            {
                foreach (var deb in debrisObjects)
                {
                    if (deb != null)
                    {
                        deb.SetActive(false);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 被碎片击中，由Spawner调用
    /// </summary>
    public void OnBeHitByDebris()
    {
        if (isHit) return;
        isHit = true;
        debrisTimer = 0;

        foreach (var deb in debrisObjects)
        {
            if (deb != null)
            {
                deb.SetActive(true);
            }
        }
        //不再在这里写SetActive，交给Update每帧强制管控
    }

    public void StopMove()
    {
        isStopMove = true;
    }
}