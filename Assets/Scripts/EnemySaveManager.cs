using UnityEngine;
using System.Collections.Generic;

public class EnemySaveManager : SaveableEntity
{
    public static EnemySaveManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    // 自动找到所有带 Enemy 脚本的怪物（包括 inactive）
    private List<Enemy> GetAllEnemies()
    {
        List<Enemy> list = new List<Enemy>();
        foreach (var e in UnityEngine.Object.FindObjectsOfType<Enemy>(includeInactive: true))
        {
            list.Add(e);
        }
        return list;
    }

    public override void SaveData()
    {
        var enemies = GetAllEnemies();
        foreach (Enemy enemy in enemies)
        {
            if (string.IsNullOrWhiteSpace(enemy.enemySaveID))
            {
                Debug.LogWarning($"敌人 {enemy.gameObject.name} enemySaveID为空，跳过存档", enemy);
                continue;
            }
            string enemyID = $"{saveableID}_Enemy_{enemy.enemySaveID}";
            SaveSystem.SaveEntity(enemyID, "Position", enemy.transform.position);
            SaveSystem.SaveEntity(enemyID, "Active", enemy.gameObject.activeSelf);

            if (enemy.TryGetComponent(out IEnemyHealth health))
            {
                SaveSystem.SaveEntity(enemyID, "HP", Mathf.RoundToInt(health.currentHp));
            }
        }
    }

    public override void LoadData()
    {
        var enemies = GetAllEnemies();
        foreach (Enemy enemy in enemies)
        {
            if (string.IsNullOrWhiteSpace(enemy.enemySaveID))
            {
                Debug.LogWarning($"敌人 {enemy.gameObject.name} enemySaveID为空，跳过读档", enemy);
                continue;
            }

            string enemyID = $"{saveableID}_Enemy_{enemy.enemySaveID}";

            Vector3 pos = SaveSystem.LoadEntity(enemyID, "Position", enemy.transform.position);
            bool savedActive = SaveSystem.LoadEntity(enemyID, "Active", true);
            int hp = SaveSystem.LoadEntity(enemyID, "HP", 100);

            enemy.transform.position = pos;

            bool isDead = hp <= 0;
            bool currentGameObjectActive = enemy.gameObject.activeSelf;

            // 条件：隐藏并且未死亡
            if (!savedActive && !isDead)
            {
                // 先激活物体，执行血量逻辑
                enemy.gameObject.SetActive(true);

                if (enemy.TryGetComponent(out IEnemyHealth health))
                {
                    if (hp > 0)
                    {
                        health.Revive();
                    }
                    health.currentHp = hp;
                }

                // 处理完血量之后，再隐藏回去
                enemy.gameObject.SetActive(false);
            }
            else
            {
                // 死亡怪：读档后强制隐藏（修复：5秒延迟隐藏前存档导致读档显示尸体）
                if (hp <= 0)
                {
                    enemy.gameObject.SetActive(false);
                }
                else
                {
                    enemy.gameObject.SetActive(savedActive);
                    if (enemy.TryGetComponent(out IEnemyHealth health))
                    {
                        health.Revive();
                        health.currentHp = hp;
                    }
                }
            }
            // 新增：读档后重置AI为待机，清除战斗遗留的追击/警报状态
            if (enemy.TryGetComponent(out EnemyAIBase ai))
            {
                ai.ResetToPatrolState();
            }
        }
    }
}