using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] GameObject destroyParticle;
    float speed;
    float t;
    Vector3 originalPos;
    Vector3 hitPoint;
    AudioClip hitSound;

    public void SetProjectile(Vector3 pos, Vector3 forw, float setSpeed, int layer, Vector3 hit, AudioClip audC)
    {
        hitSound = audC;
        hitPoint = hit;
        originalPos = pos;
        transform.position = pos;
        transform.forward = forw;
        speed = setSpeed;
        transform.localScale = new Vector3(1, 1, speed / 4.0f);
        this.gameObject.layer = layer;
    }
    void Update()
    {
        t += Time.deltaTime * speed / Vector3.Distance(originalPos,hitPoint);
        if (t > 1)
        {
            GameObject.Instantiate(destroyParticle, hitPoint, transform.rotation);
            destroyParticle.GetComponent<AudioSource>().PlayOneShot(hitSound);
            Destroy(this.gameObject);
        }
        transform.position = Vector3.Lerp(originalPos, hitPoint ,t);
    }
}
