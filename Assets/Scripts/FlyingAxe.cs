using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AxeProjectile : MonoBehaviour
{
    [Header("飞斧参数")]
    public float flySpeed = 8f;
    public float damage = 20f;
    public float rotateSpeed = 3f;
    public float destroyDelay = 3f;
    [Header("抛物线参数")]
    public float gravity = -9.81f;

    private Rigidbody rb;
    private bool isHit = false;
    private Vector3 spawnPos;

    // 抛物线专用
    private Vector3 flyDir;
    private Vector3 targetPos;      // 目标位置
    private float totalTime;        // 总飞行时间
    private Vector3 perpDir;             // 垂直于连线方向
    private float perpendicularVelocity; // 垂直于连线方向的初速度

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        spawnPos = transform.position;

        // 自行通过Tag获取玩家，计算初始朝向
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            targetPos = player.transform.position + Vector3.up * 1.5f;  // 直接使用玩家位置，不修改Y轴

            // 计算完整向量（包含Y轴）
            Vector3 delta = targetPos - transform.position;
            float totalDistance = delta.magnitude;

            // 连线方向归一化（匀速运动方向）
            if (totalDistance > 0.01f)
            {
                flyDir = delta.normalized;
            }
            else
            {
                flyDir = transform.forward;
            }

            // 计算飞行总时间
            totalTime = totalDistance / flySpeed;

            // 构建垂直于连线方向的单位向量（重力作用方向）
            perpDir = GetPerpendicularUp(flyDir);

            // 计算垂直于连线方向的初速度
            // 目标：在总时间内，垂直于连线方向的位移为0（起点和终点都在同一垂直平面上）
            // 公式: 0 = vy * totalTime + 0.5 * g * totalTime^2
            // 解出: vy = -0.5 * g * totalTime
            perpendicularVelocity = -0.5f * gravity * totalTime;
        }
        else
        {
            flyDir = transform.forward;
            targetPos = transform.position + transform.forward * 10f;
            totalTime = 1f;
            perpendicularVelocity = 0f;
            perpDir = Vector3.up;
        }

        // 关闭刚体自有速度，纯代码控制抛物线
        rb.velocity = Vector3.zero;
    }

    void Update()
    {
        // 撞击后不再执行抛物线
        if (isHit) return;

        // 飞斧自转
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);

        // 重力累加（作用于垂直于连线的方向）
        perpendicularVelocity += gravity * Time.deltaTime;

        // 位移：沿连线方向匀速 + 垂直于连线方向重力运动
        Vector3 moveStep = flyDir * flySpeed * Time.deltaTime + perpDir * perpendicularVelocity * Time.deltaTime;
        transform.position += moveStep;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isHit) return;
        isHit = true;
        rb.useGravity = true;

        ContactPoint contact = collision.contacts[0];
        // 检测可受伤目标，造成伤害
        IDamageable damageable = collision.collider.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage, contact.point);
        }

        // 撞击后3秒销毁，无特效
        Invoke(nameof(DestroyAxe), destroyDelay);

        // 可选：钉在物体上（取消注释开启）
        // rb.isKinematic = true;
        // rb.constraints = RigidbodyConstraints.FreezeAll;
    }

    void DestroyAxe()
    {
        Destroy(gameObject);
    }

    // 获取一个垂直于给定向量的单位向量
    Vector3 GetPerpendicularUp(Vector3 direction)
    {
        // 避免与 direction 平行的情况
        if (Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < 0.99f)
        {
            // 使用叉积计算垂直于 direction 和 world up 的向量
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            // 再叉积得到垂直于 direction 的"上"向量
            return Vector3.Cross(direction, right).normalized;
        }
        else
        {
            // 如果 direction 接近垂直，使用 world right 作为参考
            Vector3 right = Vector3.Cross(Vector3.right, direction).normalized;
            return Vector3.Cross(direction, right).normalized;
        }
    }
}