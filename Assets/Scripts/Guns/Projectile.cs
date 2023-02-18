using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    float speed;
    float decaytimer = 20;
    public void SetProjectile(Vector3 pos, Vector3 forw, float setSpeed)
    {
        transform.position = pos;
        transform.forward = forw;
        speed = setSpeed;
        transform.localScale = new Vector3(1, 1, speed / 4.0f);
    }
    void Update()
    {
        decaytimer -= Time.deltaTime;
        if(decaytimer <= 0 )
        {
            Destroy(this.gameObject);
        }

        transform.position += transform.forward * speed * Time.deltaTime;
    }
}
