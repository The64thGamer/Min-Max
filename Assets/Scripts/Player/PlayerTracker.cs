using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using static GlobalManager;

public class PlayerTracker : NetworkBehaviour
{
    [Header("Raw Positions")]
    [SerializeField] Transform headset;
    [SerializeField] Transform rightController;
    [SerializeField] Transform leftController;
    [SerializeField] Transform forwardRoot;
    [SerializeField] Transform camOffset;


    [Header("Animator")]
    [SerializeField] Animator animController;
    [SerializeField] Transform modelRoot;
    [SerializeField] Transform playerRHand;
    [SerializeField] Transform playerLHand;
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
    [SerializeField] InputActionProperty pressRStickAction;
    [SerializeField] Player player;
    [SerializeField] CharacterController charController;

    //other
    bool isInFirstPerson;
    bool alreadyPressed;
    float height;


    //Modified by either the player or the server
    Vector2 movementAxis;
    bool triggerR;
    bool triggerL;
    bool rhandAButton;
    bool pressRstick;

    //Wackness
    Vector3 prevRHandPos;
    Vector3 prevRHandForward;
    Vector3 prevRHandUp;
    Vector3 prevRHandRight;


    //Prediction Values
    float predictionTime;
    Vector3 predictedVelocity;
    Vector3 predictedPos;

    //Const
    const float crouchMinHeight = -0.3f;

    public enum ButtonState
    {
        off,
        started,
        on,
        cancelled,
    }

    private void Start()
    {
        height = PlayerPrefs.GetFloat("PlayerHeight") - 0.127f;
    }

    private void Update()
    {
        if (IsOwner)
        {
            movementAxis = moveAxis.action.ReadValue<Vector2>();
            rhandAButton = jump.action.IsPressed();
            triggerR = triggerRAction.action.IsPressed();
            triggerL = triggerLAction.action.IsPressed();

            if (pressRStickAction.action.IsPressed() && !alreadyPressed)
            {
                alreadyPressed = true;
                pressRstick = !pressRstick;
            }
            if(!pressRStickAction.action.IsPressed())
            {
                alreadyPressed = false;
            }
        }
    }

    void LateUpdate()
    {
        //For proper syncing
        prevRHandPos = rightController.position;
        prevRHandForward = rightController.forward;
        prevRHandUp = rightController.up;
        prevRHandRight = rightController.right;

        //Animations
        if (animController != null)
        {
            animController.SetFloat("HandX", CalcLerpVector3(centerPos.position, rightPos.position, rightController.position, false) - CalcLerpVector3(centerPos.position, leftPos.position, rightController.position, false));
            animController.SetFloat("HandY", CalcLerpVector3(centerPos.position, upPos.position, rightController.position, true) - CalcLerpVector3(centerPos.position, downPos.position, rightController.position, true));
            playerHead.rotation = headset.rotation;
            modelRoot.rotation = Quaternion.Lerp(modelRoot.rotation, playerHead.rotation, Time.deltaTime * 3);
            modelRoot.eulerAngles = new Vector3(0, modelRoot.eulerAngles.y, 0);
            modelRoot.position = forwardRoot.position;
            playerRHand.rotation = rightController.rotation;
            playerRHand.Rotate(new Vector3(-90, 180, 0));
            playerRHand.Rotate(new Vector3(9.99f, 27.48f, 0));
            if (isInFirstPerson)
            {
                playerRHand.position = rightController.position + (rightController.up * -0.2f) + (rightController.forward * -0.2f);

                playerLHand.position = leftController.position + (leftController.up * -0.2f) + (leftController.forward * -0.2f);
                playerLHand.rotation = leftController.rotation;
                playerLHand.Rotate(new Vector3(-90, 180, 0));
                playerLHand.Rotate(new Vector3(9.99f, 27.48f, 0));
            }
        }

        if (!IsHost)
        {
            if(IsOwner)
            {
                //Lerping client-side movement with server positions for a smoother experience
                transform.position = Vector3.Lerp(transform.position, predictedPos, Mathf.Clamp01((Vector3.Distance(transform.position, predictedPos) - 1) - charController.velocity.magnitude));
            }
            else
            {
                transform.position = predictedPos;
            }
        }
    }
    void OnEnable()
    {
        if (moveAxis.action != null) moveAxis.action.Enable();
        if (jump.action != null) jump.action.Enable();
        if (triggerRAction.action != null) triggerRAction.action.Enable();
        if (triggerLAction.action != null) triggerLAction.action.Enable();
        if (pressRStickAction.action != null) pressRStickAction.action.Enable();
    }

    public void SetNewClientPosition(Vector3 pos, Vector3 velocity, float rpcPredicitonTime)
    {
        charController.enabled = false;
        predictedPos = pos;
        charController.enabled = true;
        predictedVelocity = velocity;
        predictionTime = rpcPredicitonTime;
    }

    public void ForceNewPosition(Vector3 pos)
    {
        charController.enabled = false;
        transform.position = pos;
        charController.enabled = true;
    }

    public void ClientSyncPlayerInputs(PlayerDataSentToClient data)
    {
        if (!IsOwner)
        {
            headset.localPosition = data.headsetPos;
            headset.rotation = data.headsetRot;
            rightController.localPosition = data.rHandPos;
            rightController.rotation = data.rHandRot;
            leftController.localPosition = data.lHandPos;
            leftController.rotation = data.lHandRot;
        }
    }

    public void ServerSyncPlayerInputs(PlayerDataSentToServer data)
    {
        if (data.predictionTime < predictionTime) { return; }
        headset.localPosition = data.headsetPos;
        headset.rotation = data.headsetRot;
        rightController.localPosition = data.rHandPos;
        rightController.rotation = data.rHandRot;
        leftController.localPosition = data.lHandPos;
        leftController.rotation = data.lHandRot;
        movementAxis = data.rightJoystick;
        rhandAButton = data.jump;
        triggerR = data.shoot;
        pressRstick = data.crouch;
    }

    public PlayerDataSentToClient GetPlayerPosData()
    {
        return new PlayerDataSentToClient()
        {
            id = player.GetPlayerID(),
            pos = GetPosition(),
            velocity = GetVelocity(),
            predictionTime = NetworkManager.Singleton.LocalTime.TimeAsFloat,
            headsetPos = headset.localPosition,
            headsetRot = headset.rotation,
            rHandPos = rightController.localPosition,
            rHandRot = rightController.rotation,
            lHandPos = leftController.localPosition,
            lHandRot = leftController.rotation,
        };
    }

    public PlayerDataSentToServer GetPlayerNetworkData()
    {
        return new PlayerDataSentToServer()
        {
            predictionTime = NetworkManager.Singleton.LocalTime.TimeAsFloat,
            rightJoystick = movementAxis,
            jump = rhandAButton,
            shoot = triggerR,
            headsetPos = headset.localPosition,
            headsetRot = headset.rotation,
            rHandPos = rightController.localPosition,
            rHandRot = rightController.rotation,
            lHandPos = leftController.localPosition,
            lHandRot = leftController.rotation,
            crouch = pressRstick,
        };
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

    public Vector3 GetPosition()
    {
        charController.enabled = false;
        Vector3 pos = transform.position;
        charController.enabled = true;
        return pos;
    }

    public Vector3 GetRightHandFirePos(Vector3 firePosition)
    {
        Vector3 pos = prevRHandPos;
        pos += prevRHandRight * firePosition.x;
        pos += prevRHandUp * firePosition.y;
        pos += prevRHandForward * firePosition.z;
        return pos;
    }

    public Vector3 GetRightHandSafeForward()
    {
        return prevRHandForward;
    }

    public Vector3 GetVelocity()
    {
        return charController.velocity;
    }

    public Vector3 GetPredictionVelocity()
    {
        return predictedVelocity;
    }

    public float GetPredictionVelocityTime()
    {
        return predictionTime;
    }

    public Transform GetForwardRoot()
    {
        return forwardRoot;
    }

    public Vector2 GetMoveAxis()
    {
        return movementAxis;
    }

    public bool GetRStickPress()
    {
        return pressRstick;
    }

    public bool GetRHandAButton()
    {
        return rhandAButton;
    }

    public Transform GetModelRoot()
    {
        return modelRoot;
    }

    public void SetCharacter(Animator playerAnim, Transform rootModel, Transform rHandPlayer, Transform lHandPlayer, Transform headPlayer, bool firstPerson)
    {
        animController = playerAnim;
        modelRoot = rootModel;
        playerRHand = rHandPlayer;
        playerLHand = lHandPlayer;
        playerHead = headPlayer;
        isInFirstPerson = firstPerson;
    }

    public void ModifyPlayerHeight(float crouchHeight)
    {
        camOffset.localPosition = new Vector3(0, Mathf.Lerp(0,crouchMinHeight,crouchHeight), 0);
    }


    private void OnDrawGizmos()
    {
        Gizmos.DrawLine(headset.position, headset.position + headset.forward);
        Gizmos.DrawLine(leftController.position, leftController.position + leftController.forward);
        Gizmos.DrawLine(rightController.position, rightController.position + rightController.forward);
    }
}
