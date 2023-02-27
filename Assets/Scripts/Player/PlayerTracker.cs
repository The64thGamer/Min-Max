using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerTracker : MonoBehaviour
{
    [Header("Anim Points")]
    [SerializeField] Transform upPos;
    [SerializeField] Transform downPos;
    [SerializeField] Transform rightPos;
    [SerializeField] Transform leftPos;
    [SerializeField] Transform centerPos;

    [Header("Raw Positions")]
    [SerializeField] Transform headset;
    [SerializeField] Transform rightController;
    [SerializeField] Transform leftController;

    [Header("Player Bones")]
    [SerializeField] Transform playerRHand;
    [SerializeField] Transform playerHead;


    [Header("Force Mirror")]
    [SerializeField] bool forceHostMirror;
    [Range(1.0f, 10.0f)]
    [SerializeField] float forceHostScale = 10;
    [SerializeField] Transform Hhead;
    [SerializeField] Transform HrightCont;
    [SerializeField] Transform HleftCont;
    [SerializeField] Transform Hroot;
    [SerializeField] Transform forwardRoot;
    [SerializeField] Vector3 adjustRotation;

    [Header("Animator")]
    [SerializeField] Animator animController;
    [SerializeField] bool forceValue;
    [Range(-1.0f, 1.0f)]
    [SerializeField] float forceX;
    [Range(-1.0f, 1.0f)]
    [SerializeField] float forceY;

    void Update()
    {
        if(forceValue)
        {
            animController.SetFloat("HandX",forceX);
            animController.SetFloat("HandY", forceY);
            return;
        }
        if (forceHostMirror)
        {
            forwardRoot.position = Hroot.position;
            transform.LookAt(forwardRoot);
            transform.localEulerAngles = new Vector3(0,transform.localEulerAngles.y,0);

            headset.localPosition = forwardRoot.InverseTransformPoint(Hhead.transform.position);
            rightController.localPosition = forwardRoot.InverseTransformPoint(HrightCont.transform.position);
            leftController.localPosition = forwardRoot.InverseTransformPoint(HleftCont.transform.position);

            ScaleAround(headset, headset.transform.parent.InverseTransformPoint(transform.position), Vector3.one * forceHostScale);
            ScaleAround(rightController, rightController.transform.parent.InverseTransformPoint(transform.position), Vector3.one * forceHostScale);
            ScaleAround(leftController, leftController.transform.parent.InverseTransformPoint(transform.position), Vector3.one * forceHostScale);
        }

        animController.SetFloat("HandX", CalcLerpVector3(centerPos.localPosition, rightPos.localPosition, rightController.localPosition,false) - CalcLerpVector3(centerPos.localPosition, leftPos.localPosition, rightController.localPosition, false));
        animController.SetFloat("HandY", CalcLerpVector3(centerPos.localPosition, upPos.localPosition, rightController.localPosition, true) - CalcLerpVector3(centerPos.localPosition, downPos.localPosition, rightController.localPosition, true));
    }

    void LateUpdate()
    {
        playerRHand.rotation = HrightCont.rotation;
        playerRHand.eulerAngles = new Vector3(-playerRHand.eulerAngles.x, playerRHand.eulerAngles.y, -playerRHand.eulerAngles.z);
        playerRHand.Rotate(forwardRoot.localEulerAngles);
        playerRHand.Rotate(new Vector3(-90, 180,0));
        playerRHand.Rotate(adjustRotation);


        playerHead.rotation = Hhead.rotation;
        playerHead.eulerAngles = new Vector3(-playerHead.eulerAngles.x, playerHead.eulerAngles.y, -playerHead.eulerAngles.z);
        playerHead.Rotate(forwardRoot.localEulerAngles);
    }

    float CalcLerpVector3(Vector3 a, Vector3 b, Vector3 t, bool vertical)
    {
        if (!vertical)
        {
            return Mathf.Clamp01((t.x - a.x) / (b.x - a.x));

        }
        else
        {
            return Mathf.Clamp01((t.y - a.y) / (b.y - a.y));
        }
    }

    public void ScaleAround(Transform target, Vector3 pivot, Vector3 newScale)
    {
        Vector3 A = target.localPosition;
        Vector3 B = pivot;

        Vector3 C = A - B;

        float RS = newScale.x / target.localScale.x;

        Vector3 FP = B + C * RS;

        //target.localScale = newScale;
        target.localPosition = FP;
    }
}
