using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    Vector3 initialScale;
    private void Start()
    {
        initialScale = transform.localScale;
    }
    void Update()
    {
        this.transform.LookAt(Camera.main.transform);
        if (Vector3.Distance(transform.position, Camera.main.transform.position) < 10)
        {
            transform.localScale = initialScale;
        }
        else
        {
            transform.localScale = Vector3.zero;
        }
    }
}
