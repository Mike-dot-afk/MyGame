using UnityEngine;
[RequireComponent(typeof(Collider))]
public class TriggerEnemyActivator : SaveableEntity
{
    [Header("怪物所在层级")]
    public LayerMask enemyLayer;
    [Header("玩家Layer")]
    public LayerMask playerLayer;
    [HideInInspector] public bool _hasActivated = false;
    private GameObject[] _enemiesInTrigger;
    private SphereCollider _sphereCollider;

    //对外暴露数组，供BattleStageManager读取
    public GameObject[] EnemiesInTrigger => _enemiesInTrigger;

    private void Awake()
    {
        _sphereCollider = GetComponent<SphereCollider>();
        Collider col = _sphereCollider;
        if (!col.isTrigger)
        {
            Debug.LogError($"{gameObject.name} 的碰撞体没有勾选 Is Trigger！");
        }
        //收集敌人，只做收集，不做隐藏
        CollectEnemiesInsideSphere();
    }

    protected override void Start()
    {
        // LoadData由外部SaveSystem.LoadGame调用，此处不要写base.Start();（父类为空）
        //此时：如果是重载场景，LoadData已经执行完毕，_hasActivated是存档真实值
        if (_enemiesInTrigger != null && !_hasActivated)
        {
            foreach (var enemy in _enemiesInTrigger)
            {
                if (enemy != null)
                    enemy.SetActive(false);
            }
        }
    }

    void CollectEnemiesInsideSphere()
    {
        if (_sphereCollider == null) return;
        Vector3 center = transform.TransformPoint(_sphereCollider.center);
        float radius = _sphereCollider.radius;
        Collider[] hits = Physics.OverlapSphere(center, radius, enemyLayer);
        _enemiesInTrigger = new GameObject[hits.Length];
        for (int i = 0; i < hits.Length; i++)
        {
            _enemiesInTrigger[i] = hits[i].gameObject;
        }
    }

    public override void SaveData()
    {
        SaveSystem.SaveEntity(saveableID, nameof(_hasActivated), _hasActivated);
    }

    public override void LoadData()
    {
        _hasActivated = SaveSystem.LoadEntity(saveableID, nameof(_hasActivated), false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasActivated) return;
        if (((1 << other.gameObject.layer) & playerLayer) == 0)
            return;
        TryActivateAllEnemies();
    }

    void TryActivateAllEnemies()
    {
        if (_hasActivated) return;
        if (_enemiesInTrigger == null || _enemiesInTrigger.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} 触发器范围内没有找到Enemy层级敌人");
            _hasActivated = true;
            return;
        }
        bool anyEnemyAlive = false;
        foreach (var enemyGo in _enemiesInTrigger)
        {
            if (enemyGo == null) continue;
            bool enemyIsDead = false;
            if (enemyGo.TryGetComponent(out IEnemyHealth health))
            {
                enemyIsDead = health.isDead();
            }
            if (enemyIsDead)
            {
                Debug.Log($"怪物 {enemyGo.name} 已经死亡，跳过激活");
                continue;
            }
            enemyGo.SetActive(true);
            anyEnemyAlive = true;
        }
        _hasActivated = true;
        if (!anyEnemyAlive)
        {
            Debug.Log("范围内所有怪物均已死亡");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null)
        {
            Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius);
        }
    }
}
