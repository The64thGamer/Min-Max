using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wire : MonoBehaviour
{
    [SerializeField] Transform startPoint;
    WirePoint startingWire = new WirePoint();
    const float minWireGrabDistance = 0.1f;
    Vector3 raycastYOffset = new Vector3(0, 0.2f, 0) ;

    private void Start()
    {
        startingWire.point = startPoint.position;
    }

    public WirePoint RequestForWire(Vector3 playerPos)
    {
        WirePoint closest = RecursiveWireSearch(playerPos, startingWire, float.PositiveInfinity);
        if(Vector3.Distance(playerPos,closest.point) <= minWireGrabDistance 
            && !Physics.Raycast(playerPos + raycastYOffset, (closest.point + raycastYOffset) - (playerPos + raycastYOffset)))
        {
            WirePoint final = new WirePoint() { parent = closest, isOn = true, point = playerPos };
            closest.children.Add(final);
            return final;
        }
        else
        {
            return null;
        }
    }

    WirePoint RecursiveWireSearch(Vector3 playerPos, WirePoint parent, float distance)
    {
        WirePoint bestChoice = parent;
        for (int i = 0; i < parent.children.Count; i++)
        {
            if (Vector3.Distance(parent.children[i].point, playerPos) < distance)
            {
                distance = Vector3.Distance(parent.children[i].point, playerPos);
                bestChoice = parent.children[i];
            }
            if (parent.children[i].children.Count > 0)
            {
                bestChoice = RecursiveWireSearch(playerPos, parent.children[i], distance);
            }
        }
        return bestChoice;
    }

    [System.Serializable]
    public class WirePoint
    {
        public bool isOn = true;
        public Vector3 point;
        public List<WirePoint> children;
        public WirePoint parent;
    }
}