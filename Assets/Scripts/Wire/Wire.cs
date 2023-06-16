using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class Wire : MonoBehaviour
{
    TeamList currentTeam;
    [SerializeField] Texture2D palette;
    [SerializeField] Material wireMat;
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
        int index = 0;
        for (int i = 0; i < startingWire.children.Count; i++)
        {
            index = RecursiveDraw(startingWire.children[i], index);
        }
    }

    public WirePoint RequestForWire(Vector3 playerPos)
    {
        //This should only be called by the host
        WirePoint closest = RecursiveWireSearch(playerPos, startingWire, float.PositiveInfinity);
        if (closest != null)
        {
            if (Vector3.Distance(playerPos, closest.point) <= minWireGrabDistance
                //&& !Physics.Raycast(playerPos + raycastYOffset, (closest.point + raycastYOffset) - (playerPos + raycastYOffset))
                )
            {
                WirePoint final = new WirePoint() { parent = closest, isOn = true, point = playerPos, wireID = lastID + 1 };
                lastID++;
                closest.children.Add(final);
                Debug.Log(closest.children.Count);
                AddNewLineRenderer(final);
                return final;
            }
        }
        return null;
    }

    WirePoint RecursiveWireSearch(Vector3 playerPos, WirePoint parent, float distance)
    {
        WirePoint bestChoice = null;
        if (Vector3.Distance(parent.point, playerPos) < distance)
        {
            distance = Vector3.Distance(parent.point, playerPos);
            bestChoice = parent;
        }
        for (int i = 0; i < parent.children.Count; i++)
        {
            WirePoint child = RecursiveWireSearch(playerPos, parent.children[i], distance);
            if (child != null)
            {
                if (Vector3.Distance(child.point, playerPos) < distance)
                {
                    distance = Vector3.Distance(child.point, playerPos);
                    bestChoice = child;
                }
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
        lr.material = wireMat;
        lr.material.color = palette.GetPixel((int)currentTeam, 7);
        lr.startColor = palette.GetPixel((int)currentTeam, 7);
        lr.endColor = lr.startColor;
        lr.startWidth = 0.1f;
        lr.endWidth = lr.startWidth;
        meshes.Add(lr);
    }

    int RecursiveDraw(WirePoint parent, int index)
    {
        meshes[index].SetPosition(0, parent.point);
        meshes[index].SetPosition(1, parent.parent.point);
        index++;
        for (int i = 0; i < parent.children.Count; i++)
        {
            index = RecursiveDraw(parent.children[i],index);
        }
        return index;
    }

    WirePoint RecursiveIDSearch(uint id, WirePoint parent)
    {
        WirePoint bestChoice = null;
        if (parent.wireID == id)
        {
            return parent;
        }
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

    public void SetTeam(TeamList team)
    {
        currentTeam = team;
    }


    public WirePoint CreateNewClientWire(uint id, uint parentId)
    {
        WirePoint parent = FindWireFromID(parentId);
        WirePoint final = new WirePoint() { parent = parent, isOn = true, wireID = id };
        parent.children.Add(final);
        AddNewLineRenderer(final);
        return final;
    }

    public void CreateNewClientWire(WirePointData data)
    {
        WirePoint parent = null;
        if(data.parent != -1)
        {
            parent = FindWireFromID((uint)data.parent);
        }
        WirePoint final = new WirePoint() { parent = parent, isOn = true, wireID = data.wireID,point = data.point};
        parent.children.Add(final);
        AddNewLineRenderer(final);
    }

    public List<WirePointData> ConvertWiresToDataArray(int teamNumber)
    {
        List<WirePointData> data = new List<WirePointData>();
        RecursiveConvertWires(data,startingWire, teamNumber);
        return data;
    }

    void RecursiveConvertWires(List<WirePointData> data, WirePoint parent, int teamNumber)
    {
        int parentID = -1;
        if(parent.parent != null)
        {
            parentID = (int)parent.parent.wireID;
        }
        data.Add(new WirePointData() { isOn = parent.isOn, parent = parentID, wireID = parent.wireID, point = parent.point, teamNum = teamNumber });
        for (int i = 0; i < parent.children.Count; i++)
        {
            RecursiveConvertWires(data, parent.children[i], teamNumber);
        }
    }

    public class WirePoint
    {
        public bool isOn = true;
        public Vector3 point;
        public List<WirePoint> children = new List<WirePoint>();
        public WirePoint parent;
        public uint wireID;
    }

    [System.Serializable]
    public struct WirePointData : INetworkSerializable
    {
        public bool isOn;
        public Vector3 point;
        public int parent;
        public uint wireID;
        public int teamNum;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref isOn);
            serializer.SerializeValue(ref point);
            serializer.SerializeValue(ref parent);
            serializer.SerializeValue(ref wireID);
            serializer.SerializeValue(ref teamNum);
        }
    }
}
