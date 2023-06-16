using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wire : MonoBehaviour
{
    [SerializeField] Transform startPoint;
    WirePoint startingWire = new WirePoint();
    const float minWireGrabDistance = 0.1f;
    Vector3 raycastYOffset = new Vector3(0, 0.2f, 0);
    uint lastID = 0;

    private void Start()
    {
        startingWire.point = startPoint.position;
    }

    private void OnDrawGizmos()
    {
        RecursiveDraw(startingWire);
    }

    public WirePoint RequestForWire(Vector3 playerPos)
    {
        //This should only be called by the host
        WirePoint closest = RecursiveWireSearch(playerPos, startingWire, float.PositiveInfinity);
        if(Vector3.Distance(playerPos,closest.point) <= minWireGrabDistance 
            && !Physics.Raycast(playerPos + raycastYOffset, (closest.point + raycastYOffset) - (playerPos + raycastYOffset)))
        {
            WirePoint final = new WirePoint() { parent = closest, isOn = true, point = playerPos, wireID = lastID + 1};
            lastID++;
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

    public WirePoint FindWireFromID(uint id)
    {
        return RecursiveIDSearch(id, startingWire);
    }

    WirePoint RecursiveIDSearch(uint id, WirePoint parent)
    {
        WirePoint bestChoice = null;
        for (int i = 0; i < parent.children.Count; i++)
        {
            if (parent.children[i].wireID == id)
            {
                bestChoice = parent.children[i];
                break;
            }
            else
            {
                bestChoice = RecursiveIDSearch(id, parent.children[i]);
                if(bestChoice != null)
                {
                    break;
                }
            }
        }
        return bestChoice;
    }

    void RecursiveDraw(WirePoint parent)
    {
        Gizmos.DrawWireSphere(parent.point, 0.1f);
        for (int i = 0; i < parent.children.Count; i++)
        {
            Gizmos.DrawLine(parent.point, parent.children[i].point);
            RecursiveDraw(parent.children[i]);
        }
    }

    public WirePoint CreateNewClientWire(uint id, uint parentId)
    {
        WirePoint parent = FindWireFromID(parentId);
        WirePoint final = new WirePoint() { parent = parent, isOn = true, wireID = id };
        parent.children.Add(final);
        return final;
    }

    public class WirePoint
    {
        public bool isOn = true;
        public Vector3 point;
        public List<WirePoint> children = new List<WirePoint>();
        public WirePoint parent;
        public uint wireID;
    }
}
