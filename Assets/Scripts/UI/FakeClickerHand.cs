using System.Collections;
using System.Collections.Generic;
using UnityEditor.Presets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class FakeClickerHand : StandaloneInputModule
{
    WorldSpaceUIDocument wsuid;
    void OnCollisionEnter(Collision collision)
    {
        Input.simulateMouseWithTouches = true;
        var pointerData = GetTouchPointerEventData(new Touch()
        {
        }, out bool b, out bool bb); ;
        ProcessTouchPress(pointerData, true, false);
        /*
        wsuid = this.GetComponent<WorldSpaceUIDocument>();
        Vector2 screenPos = GetColliderVertexPositions(collision.GetContact(0).point);
        Debug.Log(screenPos);
        screenPos *= new Vector2(1000, 700);
        Input.simulateMouseWithTouches = true;
        var pointerData = GetTouchPointerEventData(new Touch()
        {
            position = screenPos
        }, out bool b, out bool bb); ;
        ProcessTouchPress(pointerData, true, false);
        */
    }

    void OnCollisionExit(Collision collision)
    {
        Input.simulateMouseWithTouches = true;
        var pointerData = GetTouchPointerEventData(new Touch()
        {
            position = Vector2.zero,
        }, out bool b, out bool bb);
        //ProcessTouchPress(pointerData, false, true);
    }

    //This always assumes the UI plane is not upside down or completely flat, and that its Y rotation is between -89 and 89.
    //Only use this for menus and follow these rules.
    //also also y is swapped for Z because of WorldSpaceUIDocument
    Vector2 GetColliderVertexPositions(Vector3 point)
    {
        //Calculate Extents
        Vector3[] vertices = new Vector3[2];
        BoxCollider b = this.GetComponent<BoxCollider>();
        vertices[0] = b.center + new Vector3(-b.size.x, b.size.z, b.size.y) * 0.5f;
        vertices[1] = b.center + new Vector3(b.size.x, -b.size.z, b.size.y) * 0.5f;
        //Calculate Pointer
        point = transform.InverseTransformPoint(point);
        return new Vector2(
            (RemapClamped(
                point.x, vertices[0].x, 0, vertices[1].x, 1)-0.5f)*1, 
            (RemapClamped(
                -point.y, -vertices[0].y, 0, -vertices[1].y, 1)-0.5f)*1);
    }

    //Oh my beloved remap function
    float RemapClamped(float aValue, float aIn1, float aIn2, float aOut1, float aOut2)
    {
        float t = (aValue - aIn1) / (aIn2 - aIn1);
        t = Mathf.Clamp(t,0,2);
        return aOut1 + (aOut2 - aOut1) * t;
    }
}
