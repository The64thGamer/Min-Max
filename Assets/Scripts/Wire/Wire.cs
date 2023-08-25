using System.Collections.Generic;
using System.Drawing;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Wire : NetworkBehaviour
{
    TeamList currentTeam;
    [SerializeField] Texture2D palette;
    [SerializeField] Material wireMat;
    [SerializeField] Transform startPoint;
    [SerializeField] Transform parentPoint;
    WirePoint startingWire = new WirePoint();
    const float minWireGrabDistance = 0.5f;
    Vector3 raycastYOffset = new Vector3(0, 0.2f, 0);
    uint lastID = 0;
    List<LineRenderer> meshes = new List<LineRenderer>();
    GlobalManager gm;

    private void Start()
    {
        gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
        startingWire.point = parentPoint.position;
        if(IsHost)
        {
            WirePoint final = new WirePoint() { parent = startingWire, point = startPoint.position, wireID = lastID + 1 };
            lastID++;
            startingWire.children.Add(final);
            AddNewLineRenderer(final);
        }
    }

    private void Update()
    {
        //Wait isn't this like super ineffecient?
        //I have to do this for the constantly updating wires but
        //This doesn't check if points have changed.
        //Then again, would caching all previous points work?
        //Maybe have moved wires ping this script to change a bool hmmmm
        //Later, gotta get this update out first.
        int index = 0;
        for (int i = 0; i < startingWire.children.Count; i++)
        {
            index = RecursiveDraw(startingWire.children[i], index);
        }
    }

    //There is a potential bug that if someone times out long enough after this reset
    //and someone else creates a wire from the starting point, and then again,
    //that it would error out. Error wire creation should have a backup to re-download
    //the server's wiring.
    public void RemoveAllWires()
    {
        for (int i = 0; i < meshes.Count; i++)
        {
            GameObject.Destroy(meshes[i].gameObject);
        }
        meshes = new List<LineRenderer>();
        startingWire.children = new List<WirePoint>();
    }

    public WirePoint RequestForWire(Vector3 playerPos)
    {
        //This should only be called by the host
        WirePoint closest = RecursiveWireSearch(playerPos, startingWire, float.PositiveInfinity);
        if (closest == startingWire)
        {
            return null;
        }
        if (closest != null)
        {
            if (Vector3.Distance(playerPos, closest.point) <= minWireGrabDistance
                //&& !Physics.Raycast(playerPos + raycastYOffset, (closest.point + raycastYOffset) - (playerPos + raycastYOffset))
                )
            {
                WirePoint final = new WirePoint() { parent = closest, point = playerPos, wireID = lastID + 1 };
                lastID++;
                closest.children.Add(final);
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
        bruh.name = "WirePoint";
        bruh.transform.parent = startPoint;
        LineRenderer lr = bruh.AddComponent<LineRenderer>();
        UnityEngine.Color c = palette.GetPixel((int)currentTeam, 7);
        lr.material = wireMat;
        lr.material.color = c;
        lr.startColor = c;
        lr.endColor = c;
        lr.startWidth = 0.1f;
        lr.endWidth = lr.startWidth;
        lr.numCapVertices = 5;
        point.lineRenderer = lr;
        meshes.Add(lr);
    }

    int RecursiveDraw(WirePoint parent, int index)
    {
        meshes[index].SetPosition(0, parent.point);
        meshes[index].SetPosition(1, parent.parent.point);
        index++;
        for (int i = 0; i < parent.children.Count; i++)
        {
            index = RecursiveDraw(parent.children[i], index);
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
                if (bestChoice != null)
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
        WirePoint final = new WirePoint() { parent = parent, wireID = id };
        parent.children.Add(final);
        AddNewLineRenderer(final);
        return final;
    }

    public void CreateNewClientWire(WirePointData data)
    {
        WirePoint parent = null;
        if (data.parent != -1)
        {
            parent = FindWireFromID((uint)data.parent);
        }
        WirePoint final = new WirePoint() { parent = parent, wireID = data.wireID, point = data.point };
        if (parent != null)
        {
            parent.children.Add(final);
        }
        AddNewLineRenderer(final);
    }

    public List<WirePointData> ConvertWiresToDataArray(int teamNumber)
    {
        List<WirePointData> data = new List<WirePointData>();
        for (int i = 0; i < startingWire.children.Count; i++)
        {
            RecursiveConvertWires(data, startingWire.children[i], teamNumber);
        }
        return data;
    }

    void RecursiveConvertWires(List<WirePointData> data, WirePoint parent, int teamNumber)
    {
        int parentID = -1;
        if (parent.parent != null)
        {
            parentID = (int)parent.parent.wireID;
        }
        data.Add(new WirePointData() {parent = parentID, wireID = parent.wireID, point = parent.point, teamNum = teamNumber });
        for (int i = 0; i < parent.children.Count; i++)
        {
            RecursiveConvertWires(data, parent.children[i], teamNumber);
        }
    }

    public WirePoint FindClosestWireToGoal(List<Vector3> point)
    {
        WirePoint finalWire = RecursiveGoalSearch(point, float.PositiveInfinity, startingWire);
        WirePoint returnwire = finalWire;
        if (finalWire != null)
        {
            UnityEngine.Color brightPalette = palette.GetPixel((int)currentTeam, 2);
            while (true)
            {
                if (finalWire.parent == null)
                {
                    break;
                }
                finalWire.lineRenderer.material.color = brightPalette;
                finalWire.lineRenderer.startColor = brightPalette;
                finalWire.lineRenderer.endColor = brightPalette;
                finalWire = finalWire.parent;
            }
        }
        return returnwire;
    }

    WirePoint RecursiveGoalSearch(List<Vector3> point, float currentDistance, WirePoint parent)
    {
        UnityEngine.Color normalPalette = palette.GetPixel((int)currentTeam, 6);
        if (parent.lineRenderer != null)
        {
            parent.lineRenderer.material.color = normalPalette;
            parent.lineRenderer.startColor = normalPalette;
            parent.lineRenderer.endColor = normalPalette;
        }
        float lng = 0.0f;
        WirePoint bestChoice = null;

        lng = CalculatePath(parent.point, point);

        if (lng < currentDistance)
        {
            bestChoice = parent;
            currentDistance = lng;
        }

        for (int i = 0; i < parent.children.Count; i++)
        {
            WirePoint bestChild = RecursiveGoalSearch(point, currentDistance, parent.children[i]);
            if (bestChild != null)
            {
                lng = CalculatePath(bestChild.point, point);

                if (lng < currentDistance)
                {
                    bestChoice = bestChild;
                    currentDistance = lng;
                }
            }
        }
        return bestChoice;
    }

    float CalculatePath(Vector3 start, List<Vector3> end)
    {
        float shortest = float.PositiveInfinity;
        NavMeshPath path = new NavMeshPath();
        for (int i = 0; i < end.Count; i++)
        {
            float lng = 0;
            NavMesh.CalculatePath(start, end[i], NavMesh.AllAreas, path);
            if (path.status != NavMeshPathStatus.PathInvalid)
            {
                for (int e = 1; e < path.corners.Length; ++e)
                {
                    lng += Vector3.Distance(path.corners[e - 1], path.corners[e]);
                }
            }
            else
            {
                lng = float.PositiveInfinity;
            }
            if(lng < shortest)
            {
                shortest = lng;
            }
        } 
        return shortest;
    }

    public class WirePoint
    {
        public Vector3 point;
        public List<WirePoint> children = new List<WirePoint>();
        public WirePoint parent;
        public uint wireID;
        public LineRenderer lineRenderer;
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
