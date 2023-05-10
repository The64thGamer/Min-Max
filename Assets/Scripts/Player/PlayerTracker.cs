using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTracker : MonoBehaviour
{
    [Header("Raw Positions")]
    [SerializeField] Transform headset;
    [SerializeField] Transform rightController;
    [SerializeField] Transform leftController;
    [SerializeField] Transform forwardRoot;
    [SerializeField] Transform trackerScale;

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
    [SerializeField] Rigidbody rigidBody;
    [SerializeField] Player player;

    ButtonState triggerR;
    ButtonState triggerL;

    Vector3 currentSpeed;

    public enum ButtonState
    {
        off,
        started,
        on,
        cancelled,
    }

    void LateUpdate()
    {
        UpdateTriggers();
        if (animController != null)
        {
            animController.SetFloat("HandX", CalcLerpVector3(centerPos.position, rightPos.position, rightController.position, false) - CalcLerpVector3(centerPos.position, leftPos.position, rightController.position, false));
            animController.SetFloat("HandY", CalcLerpVector3(centerPos.position, upPos.position, rightController.position, true) - CalcLerpVector3(centerPos.position, downPos.position, rightController.position, true));
            playerRHand.rotation = rightController.rotation;
            playerRHand.Rotate(new Vector3(-90, 180, 0));
            playerRHand.Rotate(new Vector3(9.99f, 27.48f, 0));
            playerHead.rotation = headset.rotation;
            modelRoot.rotation = Quaternion.Lerp(modelRoot.rotation, playerHead.rotation, Time.deltaTime);
            modelRoot.eulerAngles = new Vector3(0, modelRoot.eulerAngles.y, 0);
            modelRoot.position = trackerScale.position;
        }
    }
    void OnEnable()
    {
        if (moveAxis.action != null) moveAxis.action.Enable();
    }


    public void SetNewClientPosition(Vector3 pos)
    {
        transform.position = pos;
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
        trackerScale.localScale = Vector3.one * scale;
    }

    public void TriggerPressed(bool left)
    {
        if (!left)
        {
            if (triggerR == ButtonState.off)
            {
                triggerR = ButtonState.started;
            }
        }
        else
        {
            if (triggerL == ButtonState.off)
            {
                triggerL = ButtonState.started;
            }
        }
    }
    public void TriggerReleased(bool left)
    {
        if (!left)
        {
            if (triggerR == ButtonState.on)
            {
                triggerR = ButtonState.cancelled;
            }
        }
        else
        {
            if (triggerL == ButtonState.on)
            {
                triggerL = ButtonState.cancelled;
            }
        }
    }

    void UpdateTriggers()
    {
        switch (triggerR)
        {
            case ButtonState.started:
                triggerR = ButtonState.on;
                break;
            case ButtonState.cancelled:
                triggerR = ButtonState.off;
                break;
            default:
                break;
        }
        switch (triggerL)
        {
            case ButtonState.started:
                triggerL = ButtonState.on;
                break;
            case ButtonState.cancelled:
                triggerL = ButtonState.off;
                break;
            default:
                break;
        }
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

    public ButtonState GetTriggerR()
    {
        return triggerR;
    }
    public ButtonState GetTriggerL()
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

    public Transform GetForwardRoot()
    {
        return forwardRoot;
    }

    public Vector2 GetMoveAxis()
    {
        return moveAxis.action.ReadValue<Vector2>();
    }    

    public Transform GetModelRoot()
    {
        return modelRoot;
    }

    public void MovePlayer(Vector2 axis)
    {
        Vector3 newAxis = headset.TransformDirection(new Vector3(axis.x, 0, axis.y));
        newAxis.y = 0;
        newAxis.Normalize();
        float speed = player.GetClassStats().baseSpeed;
        float accel = player.GetClassStats().baseAccel;
        currentSpeed = new Vector3(
            Mathf.Abs(newAxis.x) * Mathf.Clamp(currentSpeed.x + (newAxis.x * accel), -speed, speed),
            rigidBody.velocity.y,
            Mathf.Abs(newAxis.z) * Mathf.Clamp(currentSpeed.y + (newAxis.z * accel), -speed, speed)
            );
        rigidBody.velocity = currentSpeed;
        if (player.GetAutoHand().enabled)
        {
            player.GetAutoHand().SyncBodyHead();
        }
    }

}
