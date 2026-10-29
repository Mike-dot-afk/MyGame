using UnityEngine;

public interface IDamageable
{
    // 统一受击方法，所有可受击对象都要实现
    void TakeDamage(float damage, Vector3 hitPoint);
}