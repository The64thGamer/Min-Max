using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] GameObject destroyParticle;
    float speed;
    float decaytimer = 15;
    bool canDamageNow;
    Vector3 originalPos;

    public void SetProjectile(Vector3 pos, Vector3 forw, float setSpeed, int layer)
    {
        originalPos = pos;
        transform.position = pos;
        transform.forward = forw;
        speed = setSpeed;
        transform.localScale = new Vector3(1, 1, speed / 4.0f);
        this.gameObject.layer = layer;
    }
    void Update()
    {
        decaytimer -= Time.deltaTime;
        if (decaytimer <= 0)
        {
            Destroy(this.gameObject);
        }
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    void OnCollisionEnter(Collision collision)
    {
        //Once you introduce arching projectiles, dont use findnearestpoint.
        //Also this function doesn't work...? Particles still offset around meshes.
        GameObject.Instantiate(destroyParticle, FindNearestPointOnLine(originalPos, transform.forward, collision.GetContact(0).point), transform.rotation);
        Destroy(this.gameObject);
    }

    Vector3 FindNearestPointOnLine(Vector3 origin, Vector3 direction, Vector3 point)
    {
        direction.Normalize();
        Vector3 lhs = point - origin;

        float dotP = Vector3.Dot(lhs, direction);
        return origin + direction * dotP;
    }
}
