using UnityEngine;

public class Grenade : MonoBehaviour
{
    private Rigidbody _rb;
    private bool _hasLanded;

    [Tooltip("判定为地面的最大角度，小于该角度视为地面，单位度")]
    public float maxGroundAngle = 45f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasLanded) return;

        ContactPoint contact = collision.contacts[0];
        float angle = Vector3.Angle(contact.normal, Vector3.up);

        // 是地面/缓斜坡：消除水平滚动，保留垂直下坠速度
        if (angle <= maxGroundAngle)
        {
            _hasLanded = true;
            Vector3 vel = _rb.velocity;
            // XZ水平清零，Y垂直保留，防止满地乱滚
            vel.x = 0;
            vel.z = 0;
            _rb.velocity = vel;
            _rb.angularVelocity = Vector3.zero;
        }
        // 是墙壁、垂直墙面：不修改速度，交给物理材质自动反弹，直接return
        else
        {
            return;
        }
    }
}