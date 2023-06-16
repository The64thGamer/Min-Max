using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Wire : MonoBehaviour
{
    [SerializeField] Transform startPoint;
    WirePoint startingWire = new WirePoint();
    const float minWireGrabDistance = 0.5f;
    Vector3 raycastYOffset = new Vector3(0, 0.2f, 0);
    uint lastID = 0;
    List<LineRenderer> meshes = new List<LineRenderer>();

    private void Start()
    {
        startingWire.point = startPoint.position;
    }

    private void Update()
    {
        for (int i = 0; i < startingWire.children.Count; i++)
        {
            RecursiveDraw(startingWire.children[i],0);
        }
    }

    public WirePoint RequestForWire(Vector3 playerPos)
    {
        //This should only be called by the host
        WirePoint closest = RecursiveWireSearch(playerPos, startingWire, float.PositiveInfinity);
        Debug.Log("Wire Attempt: " + closest);
        if(Vector3.Distance(playerPos,closest.point) <= minWireGrabDistance 
            //&& !Physics.Raycast(playerPos + raycastYOffset, (closest.point + raycastYOffset) - (playerPos + raycastYOffset))
            )
        {
            Debug.Log("YEAAAAAAAAAAAA");
            WirePoint final = new WirePoint() { parent = closest, isOn = true, point = playerPos, wireID = lastID + 1};
            lastID++;
            closest.children.Add(final);
            AddNewLineRenderer(final);
            return final;
        }
        else
        {
            Debug.Log("awwwwwww only " + Vector3.Distance(playerPos, closest.point));
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

    void AddNewLineRenderer(WirePoint point)
    {
        GameObject bruh = new GameObject();
        bruh.transform.parent = transform;
        LineRenderer lr = bruh.AddComponent<LineRenderer>();
        lr.SetColors(Color.black, Color.black);
        lr.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        lr.SetWidth(0.1f, 0.1f);
        meshes.Add(lr);
    }

    void RecursiveDraw(WirePoint parent, int index)
    {
        meshes[index].SetPosition(0, parent.point);
        meshes[index].SetPosition(1, parent.parent.point);
        index++;
        for (int i = 0; i < parent.children.Count; i++)
        {
            RecursiveDraw(parent.children[i],index);
        }
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


    public WirePoint CreateNewClientWire(uint id, uint parentId)
    {
        WirePoint parent = FindWireFromID(parentId);
        WirePoint final = new WirePoint() { parent = parent, isOn = true, wireID = id };
        parent.children.Add(final);
        AddNewLineRenderer(final);
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
