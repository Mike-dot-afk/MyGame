using UnityEngine;
using System.Collections.Generic;

public class RagdollHelper : MonoBehaviour
{
    [Header("把人形所有骨骼的Rigidbody拖入列表")]
    public List<Rigidbody> ragdollRbs;
    [Header("自动填充：所有骨骼身上的Collider，Awake自动收集")]
    public List<Collider> ragdollColliders;

    private Animator _anim;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        ragdollColliders.Clear();
        foreach (var rb in ragdollRbs)
        {
            Collider col = rb.GetComponent<Collider>();
            if (col != null)
            {
                ragdollColliders.Add(col);
            }
        }
        SetRagdollActive(false);
    }

    public void SetRagdollActive(bool enable, Vector3 impulse = default, float impulseScale = 1f)
    {
        foreach (var rb in ragdollRbs)
        {
            rb.isKinematic = !enable;
            rb.useGravity = enable;
            if (enable && impulse.magnitude > 0.01f)
            {
                rb.AddForce(impulse * impulseScale, ForceMode.Impulse);
            }
        }

        foreach (var col in ragdollColliders)
        {
            col.enabled = enable;
        }

        if (enable)
        {
            _anim.enabled = false;
        }
    }

    public bool IsLyingFaceUp()
    {
        Transform hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null) return true;
        return hips.up.y > -0.2f;
    }
}
