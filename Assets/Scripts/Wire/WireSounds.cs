using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WireSounds : MonoBehaviour
{
    AudioSource au;
    void Start()
    {
        au = GetComponent<AudioSource>();
        au.Pause();
    }

    public void AddWire()
    {
        au.PlayOneShot((AudioClip)Resources.Load("Sounds/Surge/Surge Pickup", typeof(AudioClip)));
        au.Play();
    }

    public void RemoveWire()
    {
        au.Pause();
        au.PlayOneShot((AudioClip)Resources.Load("Sounds/Surge/Surge Pickup", typeof(AudioClip)));
    }
}
