using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MagicMissile : MonoBehaviour
{

    public Collider2D collider;
    public Enemy enemy;
    public float moveSpeed = 0.4f, collisionOffset = 0.01f;
    public Player player;
    public Rigidbody2D rb;
    public ContactFilter2D movementFilter;
    public List<RaycastHit2D> castCollisions = new List<RaycastHit2D>();
    public GameObject explosion;

    float aliveTimer = 0;
    public float damage = 1;
    public float knockbackDistance = 6f;
    public float delay = 0.15f, delayStep;
    
    void FixedUpdate()
    {
        aliveTimer++;
        if (enemy.enemySensor.PlayerDetected)
        {
            player = enemy.enemySensor.Player.GetComponent<Player>();
            Vector3 playerPos = new Vector3(player.transform.position.x, player.transform.position.y - 0.15f, player.transform.position.z);
            Vector2 dir = playerPos - transform.position;
            dir = dir.normalized;
            TryMove(dir);
        }
        if (!GetComponent<ParticleSystem>().IsAlive())
            Destroy(gameObject);

        Destroy(this.gameObject, 4f);
    }


    private bool TryMove(Vector2 dir)
    {
        int count = rb.Cast(dir, movementFilter, castCollisions, moveSpeed * Time.fixedDeltaTime + collisionOffset);

        if (count == 0)
        {
            rb.MovePosition(rb.position + dir * moveSpeed * Time.fixedDeltaTime);
            return true;
        }
        else
        {
            var p = Instantiate(explosion, transform.position, transform.rotation);
            Destroy(gameObject);
            return false;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            Player triggerPlayer = collision.GetComponent<Player>();

            if (triggerPlayer != null)
            {
                if (!triggerPlayer.damageImmune)
                {
                    //var p = Instantiate(attackImpact.gameObject, player.transform.position, transform.rotation);
                    player.pC.Knockback(knockbackDistance, transform.position, delay);
                    triggerPlayer.Damage(damage);
                    Destroy(gameObject);
                    var p = Instantiate(explosion, transform.position, transform.rotation);
                }
            }

        }

        if (collision.tag == "Enemy" && aliveTimer >= 30)
        {
            Enemy enemyTarget = collision.GetComponent<Enemy>();

            if (enemyTarget != null)
            {
                if (!enemyTarget.damageImmune)
                {
                    //var p = Instantiate(attackImpact.gameObject, player.transform.position, transform.rotation);
                    enemyTarget.Damage(damage);
                    var p = Instantiate(explosion, transform.position, transform.rotation);
                    Destroy(gameObject);
                }
            }

        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            Player triggerPlayer = collision.GetComponent<Player>();

            if (triggerPlayer != null)
            {
                if (!triggerPlayer.damageImmune)
                {
                    //var p = Instantiate(attackImpact.gameObject, player.transform.position, transform.rotation);
                    player.pC.Knockback(knockbackDistance, transform.position, delay);
                    triggerPlayer.Damage(damage);
                    Destroy(gameObject);
                    var p = Instantiate(explosion, transform.position, transform.rotation);

                }
            }

        }

        if (collision.tag == "Enemy" && aliveTimer >= 30)
        {
            Enemy enemyTarget = collision.GetComponent<Enemy>();

            if (enemyTarget != null)
            {
                if (!enemyTarget.damageImmune)
                {
                    //var p = Instantiate(attackImpact.gameObject, player.transform.position, transform.rotation);
                    enemyTarget.Damage(damage);
                    var p = Instantiate(explosion, transform.position, transform.rotation);
                    Destroy(gameObject);

                }
            }

        }
    }
}
