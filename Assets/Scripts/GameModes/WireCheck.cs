using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class WireCheck : MonoBehaviour
{
    [SerializeField] UnityEvent wireFound;
    int defenderTeam = 1;
    GlobalManager gm;
    private void Start()
    {
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
    }

    public void SetDefendingTeam(int set)
    {
        defenderTeam = set;
    }

    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponent<Player>();
        if (player != null)
        {
            Wire.WirePoint wire = player.GetWirePoint();
            if(wire != null)
            {
                if (player.GetTeam() != gm.GetTeams()[defenderTeam].teamColor)
                {
                    wireFound.Invoke();
                }
            }
        }
    }
}
