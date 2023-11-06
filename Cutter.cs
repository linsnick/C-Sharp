using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cutter : MonoBehaviour
{

    public AttackImpact attackImpact;
    public float damage = 1;
    public float knockbackDistance = 6f;
    public float rotVal = 1;
    public float delay = 0.15f, delayStep;

    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        Vector3 rotVec = new Vector3(0, 0, rotVal);
        transform.Rotate(rotVec, Space.Self);

        Destroy(gameObject, 6f);
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            Player player = collision.GetComponent<Player>();

            if (player != null)
            {
                if (!player.damageImmune)
                {
                    //var p = Instantiate(attackImpact.gameObject, player.transform.position, transform.rotation);
                    player.pC.Knockback(knockbackDistance, transform.position, delay);
                    player.Damage(damage);
                    var p = Instantiate(attackImpact, player.transform.position, player.transform.rotation);
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
                    //var p = Instantiate(attackImpact.gameObject, player.transform.position, transform.rotation);
                    player.pC.Knockback(knockbackDistance, transform.position, delay);
                    player.Damage(damage);
                    var p = Instantiate(attackImpact, player.transform.position, player.transform.rotation);

                }
            }

        }
    }
}
