using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletProjectile : MonoBehaviour
{

    [SerializeField] private Transform vfxHitYellow;
    [SerializeField] private Transform vfxHitRed;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float maxDistance = 100f;
    [SerializeField] private float speed = 40f;

    private Rigidbody bulletRigidbody;
    private Vector3 startPos;

    private void Awake()
    {
        bulletRigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        startPos = transform.position;
        bulletRigidbody.velocity = transform.forward * speed;
    }

    private void Update()
    {
        float distance = Vector3.Distance(transform.position, startPos);
        if (distance > maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        ContactPoint contact = collision.contacts[0];
        IDamageable damageable = collision.collider.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage, contact.point);
            Instantiate(vfxHitRed, contact.point, Quaternion.LookRotation(contact.normal));
        }
        else if (collision.collider.GetComponentInParent<BulletTarget>() != null)
        {
            Instantiate(vfxHitRed, contact.point, Quaternion.LookRotation(contact.normal));
        }
        else
        {
            Instantiate(vfxHitYellow, contact.point, Quaternion.LookRotation(contact.normal));
        }
        Destroy(gameObject);
    }

}
