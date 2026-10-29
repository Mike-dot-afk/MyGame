using UnityEngine;

public interface IEnemyHealth
{
    float currentHp { get; set; }
    bool isDead();
    void Revive();
}