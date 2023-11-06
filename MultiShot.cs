using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiShot : MonoBehaviour
{
    public Player player;
    public Enemy enemy;
    public float moveSpeed = 0.3f;
    public float collisionDamage = 1, knockbackDistance = 5, delay = 0.15f, collisionOffset = 0.01f;
    public float damage = 1f;
    Vector2 dir = Vector2.zero;
    ParticleSystem ps;
    public Vector3 direction;
    public float count = 0;
    private float projectileSpread = 40f;
    Rigidbody2D rb;
    public ContactFilter2D movementFilter;
    public List<RaycastHit2D> castCollisions = new List<RaycastHit2D>();
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        ps = GetComponent<ParticleSystem>();

        //Set up the direction at which the projectile will be shot based on the float count which is set when it gets called
        float faceRot = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float startRot = faceRot + projectileSpread / 2f;
        float angleInc = projectileSpread / (2f);

        float tempRot = startRot - angleInc * count;
        Vector3 finalDir = new Vector2(Mathf.Cos(tempRot * Mathf.Deg2Rad), Mathf.Sin(tempRot * Mathf.Deg2Rad));
        if (count != 1)
            dir = (finalDir - enemy.transform.position).normalized;
        if (count == 1)
            dir = (direction - enemy.transform.position).normalized;
    }

    private bool TryMove(Vector2 dir)
    {
        int count = rb.Cast(dir, movementFilter, castCollisions, moveSpeed * Time.fixedDeltaTime + collisionOffset);

        //Will check if the projectile will hit anything and will only move if it has the space
        if (count == 0)
        {
            rb.MovePosition(rb.position + dir * moveSpeed * Time.fixedDeltaTime);
            return true;
        }
        else
            return false;
    }

    //FixedUpdate ensures consistency no matter the user's framerate
    void FixedUpdate()
    {
        TryMove(dir);

        if (!TryMove(dir))
        {
            ps.Pause();
        }

        //Destroys the GameObject after 2 seconds OR when the particle system stops
        if (!GetComponent<ParticleSystem>().IsAlive())
            Destroy(gameObject);
        Destroy(gameObject, 2f);
    }


    #region ColliderTriggers
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            Player player = collision.GetComponent<Player>();

            if (player != null)
            {
                if (!player.damageImmune)
                {
                    player.pC.Knockback(knockbackDistance, transform.position, delay);
                    player.Damage(damage);
                }
            }

        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            Player player = collision.GetComponent<Player>();

            if (player != null)
            {
                if (!player.damageImmune)
                {
                    player.pC.Knockback(knockbackDistance, transform.position, delay);
                    player.Damage(damage);
                }
            }

        }
    }
    #endregion ColliderTriggers
}
