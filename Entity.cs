using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Entity : MonoBehaviour
{

    public Animator anim;
    public Rigidbody2D rb;
    public Collider2D collider;
    public SpriteRenderer spriteRenderer;


    public bool damageImmune = false, canImmune = false, knockbackable = false;
    public float damageImmunity = 0.5f, parryImmunity = 1f;
    [SerializeField]private float _health = 3;
    [SerializeField]private int _healthMax = 3;
    public virtual int HealthMax
    {
        get
        {
            return _healthMax;
        }
    }

    public virtual float Health
    {
        get => Mathf.Min(_health, HealthMax);
        set => _health = Mathf.Clamp(value, 0, HealthMax);
    }

    public void Damage(float damage)
    {
        if (!damageImmune)
        {
            Health -= damage;
            if (canImmune)
                StartCoroutine(ImmunityCR());

        }
    }

    public void Heal()
    {
        Health += 1;
    }


    IEnumerator ImmunityCR()
    {
        spriteRenderer.color = Color.red;
        BecomeImmune();
        yield return new WaitForSeconds(damageImmunity);
        CancelImmune();
        spriteRenderer.color = Color.white;

    }

    public void BecomeImmune()
    {
        damageImmune = true;
    }

    public void CancelImmune()
    {
        damageImmune = false;
    }

    public IEnumerator ParryImmune()
    {
        spriteRenderer.color = Color.yellow;
        BecomeImmune();
        yield return new WaitForSeconds(parryImmunity);
        CancelImmune();
        spriteRenderer.color = Color.white;
    }

}
