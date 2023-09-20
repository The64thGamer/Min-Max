using StarterAssets;
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
    [SerializeField] InputActionProperty menuAction;
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
    bool pressMenu;

    //Wackness
    Vector3 prevRHandPos;
    Vector3 prevRHandForward;
    Vector3 prevRHandUp;
    Vector3 prevRHandRight;

    //Const
    const float crouchMinHeight = -0.3f;
    const ulong botID = 64646464646464;

    public enum ButtonState
    {
        off,
        started,
        on,
        cancelled,
    }

    private void Start()
    {
        height = PlayerPrefs.GetFloat("Settings: PlayerHeight") - 0.127f;
        UpdateFOV();
    }

    private void Update()
    {
        if (IsOwner && player.GetPlayerID() < botID)
        {
            movementAxis = moveAxis.action.ReadValue<Vector2>();
            rhandAButton = jump.action.IsPressed();
            triggerR = triggerRAction.action.IsPressed();
            triggerL = triggerLAction.action.IsPressed();
            pressMenu = menuAction.action.IsPressed();

            if (pressRStickAction.action.IsPressed() && !alreadyPressed)
            {
                alreadyPressed = true;
                pressRstick = !pressRstick;
            }
            if (!pressRStickAction.action.IsPressed())
            {
                alreadyPressed = false;
            }
        }
    }

    public void ResetInputs()
    {
        pressRstick = false;
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
            animController.SetFloat("VelX", Vector3.Dot(GetVelocity(), Vector3.ProjectOnPlane(headset.transform.right, Vector3.up).normalized) / (player.GetClassStats().baseSpeed / 25.0f));
            animController.SetFloat("VelZ", Vector3.Dot(GetVelocity(), Vector3.ProjectOnPlane(headset.transform.forward, Vector3.up).normalized) / (player.GetClassStats().baseSpeed / 25.0f));

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
    }
    void OnEnable()
    {
        if (moveAxis.action != null) moveAxis.action.Enable();
        if (jump.action != null) jump.action.Enable();
        if (triggerRAction.action != null) triggerRAction.action.Enable();
        if (triggerLAction.action != null) triggerLAction.action.Enable();
        if (pressRStickAction.action != null) pressRStickAction.action.Enable();
        if (menuAction.action != null) { menuAction.action.Enable(); }
    }

    public void UpdateFOV()
    {
        headset.GetComponent<Camera>().fieldOfView = PlayerPrefs.GetFloat("Settings: FOV");
    }


    public void ForceNewPosition(Vector3 pos)
    {
        charController.enabled = false;
        transform.position = pos;
        charController.enabled = true;
    }

    public PlayerInputData GetPlayerInputData()
    {
        return new PlayerInputData()
        {
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
            menu = pressMenu,
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
        camOffset.localPosition = new Vector3(0, Mathf.Lerp(0, crouchMinHeight, crouchHeight), 0);
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawLine(headset.position, headset.position + headset.forward);
        Gizmos.DrawLine(leftController.position, leftController.position + leftController.forward);
        Gizmos.DrawLine(rightController.position, rightController.position + rightController.forward);
    }
}
