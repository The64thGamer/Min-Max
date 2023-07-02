using Unity.Netcode;
using UnityEngine;

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : NetworkBehaviour
    {
        [Header("Player")]
        public float acceleration = 100.0f;
        public float deceleration = 1.0f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;
        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.1f;
        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        //Objects
        CharacterController _controller;
        [SerializeField] GameObject _mainCamera;
        PlayerTracker tracker;
        GlobalManager gm;
        Player player;

        //Consts
        const float crouchSpeed = 7;

        // player
        Vector3 _speed;
        float _verticalVelocity;
        float _terminalVelocity = 53.0f;
        float _jumpTimeoutDelta;
        float _fallTimeoutDelta;

        //Midair Movement
        Vector3 oldAxis;
        Vector2 oldInput;
        bool hasBeenGrounded;
        bool hasBeenStopped;

        //Crouch
        float currentCrouchLerp;
        bool hasBeenCrouched;


        public override void OnNetworkSpawn()
        {
            gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
            tracker = this.GetComponent<PlayerTracker>();
            _controller = GetComponent<CharacterController>();
            player = GetComponent<Player>();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        public void MovePlayer(Vector2 _input, bool jump, bool crouch)
        {
            if (_controller == null) { return; }

            Vector3 forward = _mainCamera.transform.forward;
            Vector3 right = _mainCamera.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            Vector3 newAxis = forward * _input.y + right * _input.x;
            Vector3 targetSpeed = new Vector3(newAxis.x, 0.0f, newAxis.z).normalized * player.GetClassStats().baseSpeed / 25.0f;

            Wire.WirePoint heldWire = player.GetWirePoint();

            //Crouch
            if (crouch && _controller.isGrounded)
            {
                if (!hasBeenCrouched)
                {
                    hasBeenCrouched = true;
                    if (IsHost)
                    {
                        player.SetWirePoint(gm.GetWire(player.GetTeam()).RequestForWire(transform.position));
                        heldWire = player.GetWirePoint();
                        if (heldWire != null)
                        {
                            gm.GiveClientWireClientRpc(player.GetPlayerID(), heldWire.wireID, heldWire.parent.wireID, player.GetTeam());
                        }
                    }
                }
                currentCrouchLerp = Mathf.Clamp01(currentCrouchLerp + (Time.deltaTime * crouchSpeed));
            }
            else
            {
                if (hasBeenCrouched)
                {
                    if (IsHost && heldWire != null)
                    {
                        gm.RemoveClientWireClientRpc(player.GetPlayerID(), heldWire.point);
                        player.RemoveHeldWire(heldWire.point);
                    }
                    hasBeenCrouched = false;
                }
                currentCrouchLerp = Mathf.Clamp01(currentCrouchLerp - (Time.deltaTime * crouchSpeed));
            }
            if (jump)
            {
                if (IsHost && heldWire != null)
                {
                    gm.RemoveClientWireClientRpc(player.GetPlayerID(), heldWire.point);
                    player.RemoveHeldWire(heldWire.point);
                }
            }
            targetSpeed *= ((1 - currentCrouchLerp) / 2.0f) + 0.5f;
            tracker.ModifyPlayerHeight(currentCrouchLerp);

            //Movement rotation halted in midair
            if (!_controller.isGrounded)
            {
                if (!hasBeenGrounded)
                {
                    oldAxis = newAxis;
                    oldInput = _input;
                    hasBeenGrounded = true;
                }
                else
                {
                    if (!hasBeenStopped)
                    {
                        //No Starting Input In Air
                        if (oldAxis == Vector3.zero)
                        {
                            oldAxis = newAxis;
                            oldInput = _input;
                        }
                        else if (Vector2.Dot(oldInput, _input) < -0.5f)
                        {
                            //Stop Mid-air if holding opposite direction
                            newAxis = Vector3.zero;
                            hasBeenStopped = true;
                        }
                        else
                        {
                            newAxis = oldAxis;
                        }
                    }
                    else
                    {
                        newAxis = Vector3.zero;
                    }
                }
            }
            else
            {
                hasBeenGrounded = false;
                hasBeenStopped = false;

            }

            //Jump
            JumpAndGravity(jump);

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (newAxis == Vector3.zero) targetSpeed = Vector2.zero;


            // accelerate or decelerate to target speed

            if (targetSpeed != Vector3.zero)
            {
                _speed.x = Mathf.Lerp(_controller.velocity.x, targetSpeed.x, Time.deltaTime * acceleration);
                _speed.z = Mathf.Lerp(_controller.velocity.z, targetSpeed.z, Time.deltaTime * acceleration);
            }
            else
            {
                _speed.x = Mathf.Lerp(_controller.velocity.x, targetSpeed.x, Time.deltaTime * deceleration);
                _speed.z = Mathf.Lerp(_controller.velocity.z, targetSpeed.z, Time.deltaTime * deceleration);
            }

            // move the player
            Vector3 finalVelocity = (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime;

            _controller.Move(finalVelocity);

            //Wire
            if (heldWire != null)
            {
                heldWire.point = transform.position;
            }
        }

        void JumpAndGravity(bool jump)
        {
            if (_controller.isGrounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }

                // if we are not grounded, do not jump
                jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

    }
}