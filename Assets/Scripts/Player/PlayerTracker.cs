using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTracker : NetworkBehaviour
{
    [Header("Raw Positions")]
    [SerializeField] Transform headset;
    [SerializeField] Transform rightController;
    [SerializeField] Transform leftController;
    [SerializeField] Transform forwardRoot;

    [Header("Animator")]
    [SerializeField] Animator animController;
    [SerializeField] Transform modelRoot;
    [SerializeField] Transform playerRHand;
    [SerializeField] Transform playerHead;


    [Header("Anim Points")]
    [SerializeField] Transform upPos;
    [SerializeField] Transform downPos;
    [SerializeField] Transform rightPos;
    [SerializeField] Transform leftPos;
    [SerializeField] Transform centerPos;

    [Header("Input")]
    [SerializeField] InputActionProperty moveAxis;
    [SerializeField] InputActionProperty jump;
    [SerializeField] InputActionProperty triggerRAction;
    [SerializeField] InputActionProperty triggerLAction;
    [SerializeField] Rigidbody rigidBody;
    [SerializeField] Player player;

    //Modified by either the player or the server
    Vector2 movementAxis;
    bool triggerR;
    bool triggerL;
    bool rhandAButton;

    public enum ButtonState
    {
        off,
        started,
        on,
        cancelled,
    }

    private void Update()
    {
        if(IsOwner)
        {
            movementAxis = moveAxis.action.ReadValue<Vector2>();
            rhandAButton = jump.action.IsPressed();
            triggerR = triggerRAction.action.IsPressed();
            triggerL = triggerLAction.action.IsPressed();
        }
    }

    void LateUpdate()
    {
        if (animController != null && animController.gameObject.activeSelf)
        {
            animController.SetFloat("HandX", CalcLerpVector3(centerPos.position, rightPos.position, rightController.position, false) - CalcLerpVector3(centerPos.position, leftPos.position, rightController.position, false));
            animController.SetFloat("HandY", CalcLerpVector3(centerPos.position, upPos.position, rightController.position, true) - CalcLerpVector3(centerPos.position, downPos.position, rightController.position, true));
            playerRHand.rotation = rightController.rotation;
            playerRHand.Rotate(new Vector3(-90, 180, 0));
            playerRHand.Rotate(new Vector3(9.99f, 27.48f, 0));
            playerHead.rotation = headset.rotation;
            modelRoot.rotation = Quaternion.Lerp(modelRoot.rotation, playerHead.rotation, Time.deltaTime);
            modelRoot.eulerAngles = new Vector3(0, modelRoot.eulerAngles.y, 0);
            modelRoot.position = forwardRoot.position;
        }
    }
    void OnEnable()
    {
        if (moveAxis.action != null) moveAxis.action.Enable();
        if (jump.action != null) jump.action.Enable();
        if (triggerRAction.action != null) triggerRAction.action.Enable();
        if (triggerLAction.action != null) triggerLAction.action.Enable();
    }


    public void SetNewClientPosition(Vector3 pos, Vector3 velocity)
    {
        transform.position = pos;
        rigidBody.velocity = velocity;
    }

    public void UpdatePlayerPositions(Transform head, Transform handR, Transform handL, Transform root, float scale)
    {
        headset.localPosition = root.InverseTransformPoint(head.transform.position);
        headset.rotation = head.rotation;
        rightController.localPosition = root.InverseTransformPoint(handR.transform.position);
        rightController.rotation = handR.rotation;
        leftController.localPosition = root.InverseTransformPoint(handL.transform.position);
        leftController.rotation = handL.rotation;
        forwardRoot.position = root.position;
        forwardRoot.localScale = Vector3.one * scale;
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

    void ScaleAround(Transform target, Vector3 pivot, Vector3 newScale)
    {
        Vector3 A = target.localPosition;
        Vector3 B = pivot;

        Vector3 C = A - B;

        float RS = newScale.x / target.localScale.x;

        Vector3 FP = B + C * RS;

        //target.localScale = newScale;
        target.localPosition = FP;
    }

    public bool GetTriggerR()
    {
        return triggerR;
    }
    public bool GetTriggerL()
    {
        return triggerL;
    }

    public Transform GetCamera()
    {
        return headset;
    }

    public Transform GetRightHand()
    {
        return rightController;
    }

    public Transform GetLeftHand()
    {
        return leftController;
    }

    public Vector3 GetRightHandFirePos(Vector3 firePosition)
    {
        Vector3 oldPos = rightController.localPosition;
        rightController.localPosition += rightController.right * firePosition.x;
        rightController.localPosition += rightController.up * firePosition.y;
        rightController.localPosition += rightController.forward * firePosition.z;
        Vector3 newPos = rightController.position;
        rightController.localPosition = oldPos;
        return newPos;
    }

    public Vector3 GetVelocity()
    {
        return rigidBody.velocity;
    }

    public Transform GetForwardRoot()
    {
        return forwardRoot;
    }

    public Vector2 GetMoveAxis()
    {
        return movementAxis;
    }

    public bool GetRHandAButton()
    {
        return rhandAButton;
    }

    public Transform GetModelRoot()
    {
        return modelRoot;
    }


}
