using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class WireCheck : MonoBehaviour
{
    [SerializeField] UnityEvent wireFound; 
    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponent<Player>();
        if (player != null)
        {
            Wire.WirePoint wire = player.GetWirePoint();
            if(wire != null)
            {
                wireFound.Invoke();
            }
        }
    }
}
