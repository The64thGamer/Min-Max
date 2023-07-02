using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Projectile : MonoBehaviour
{
    [SerializeField] GameObject destroyParticle;
    float speed;
    float t;
    float volume;
    Vector3 originalPos;
    Vector3 hitPoint;
    [SerializeField] AudioClip hitSound;

    public void SetProjectile(Vector3 pos, Vector3 forw, float setSpeed, int layer, Vector3 hit, float volumeScale)
    {
        hitPoint = hit;
        originalPos = pos;
        transform.position = pos;
        transform.forward = forw;
        speed = setSpeed;
        transform.localScale = new Vector3(1, 1, speed / 4.0f);
        this.gameObject.layer = layer;
        volume = volumeScale;
    }
    void Update()
    {
        t += Time.deltaTime * speed / Vector3.Distance(originalPos,hitPoint);
        if (t > 1)
        {
            Destroying();
        }
        transform.position = Vector3.Lerp(originalPos, hitPoint ,t);
    }

    void Destroying()
    {
        GameObject dsp = GameObject.Instantiate(destroyParticle, hitPoint, transform.rotation, transform.parent);
        dsp.name = "Projectile Particle";
        dsp.layer = this.gameObject.layer;
        dsp.GetComponent<AudioSource>().PlayOneShot(hitSound, volume);
        Destroy(this.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        Destroying();
    }
}
