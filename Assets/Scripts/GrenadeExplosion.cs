using UnityEngine;

public class GrenadeExplosion : MonoBehaviour
{
    [Header("爆炸核心参数")]
    public float fuseTime = 3f;          // 手雷落地后几秒爆炸
    public float explosionRadius = 5f;  // 爆炸伤害半径
    public float explosionDamage = 50f; // 爆炸伤害值

    [Header("特效与音效")]
    public ParticleSystem explosionVFX; // 爆炸粒子特效（拖你的Explosion预制体）
    public AudioClip explosionSFX;      // 爆炸音效（拖你的Explosion音频）
    public float sfxVolume = 1f;        // 音效音量

    private Rigidbody rb;
    private AudioSource audioSource;
    private bool hasExploded = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();

        // 自动补全AudioSource（如果没挂就自动加）
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (rb != null && !rb.isKinematic)
        {
            // 手雷扔出后，倒计时爆炸
            Invoke(nameof(Explode), fuseTime);
        }
    }

    public void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // 1. 播放爆炸特效
        if (explosionVFX != null)
        {
            Instantiate(explosionVFX, transform.position, Quaternion.identity);
        }

        // 2. 独立播放音效（不会被中途掐断）
        if (explosionSFX != null)
        {
            GameObject soundObj = new GameObject("ExplosionSound");
            soundObj.transform.position = transform.position;
            AudioSource newAudio = soundObj.AddComponent<AudioSource>();
            newAudio.PlayOneShot(explosionSFX, sfxVolume);
            Destroy(soundObj, explosionSFX.length);
        }

        // 3. 伤害检测
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider col in colliders)
        {
            if (col.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(explosionDamage, transform.position);
            }
        }

        // 4. 立刻销毁手雷，不影响音效
        Destroy(gameObject);
    }
}