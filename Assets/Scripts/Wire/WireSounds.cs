using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WireSounds : MonoBehaviour
{
    AudioSource au;
    void Start()
    {
        au = GetComponent<AudioSource>();
    }

    public void AddWire()
    {
        au.Play();
        au.PlayOneShot((AudioClip)Resources.Load("Sounds/Surge/Surge Pickup", typeof(AudioClip)));
    }

    public void RemoveWire()
    {
        au.Stop(); 
        au.PlayOneShot((AudioClip)Resources.Load("Sounds/Surge/Surge Drop", typeof(AudioClip)),1);
    }

    public void PauseWire()
    {
        au.Stop();
    }

    public void ResumeWire()
    {
        au.Play();
    }
}
