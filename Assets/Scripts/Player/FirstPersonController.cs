using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : NetworkBehaviour
    {

        //Objects
        CharacterController _controller;
        [SerializeField] GameObject _mainCamera;
        PlayerTracker tracker;
        GlobalManager gm;
        Player player;

        //Consts
        const float crouchSpeed = 7;
        const float acceleration = 9;
        const float deceleration = 6;
        const float hasMovedDeltaTimeout = 15;
        const float JumpHeight = 1.0f;
        const float Gravity = -15.0f;
        const float FallTimeout = 0.15f;

        // player
        Vector3 _speed;
        float _verticalVelocity;
        float _terminalVelocity = 53.0f;
        float _fallTimeoutDelta;
        float _hasBeenMovingDelta;

        //Midair Movement
        Vector3 oldAxis;
        Vector2 oldInput;
        bool hasBeenGrounded;
        bool hasBeenStopped;

        //Crouch
        float currentCrouchLerp;
        bool hasBeenCrouched;

        //Wire
        bool directionDecided;
        Vector3 wireCollisionVector;
        Wire.WirePoint heldWire;


        public override void OnNetworkSpawn()
        {
            gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
            tracker = this.GetComponent<PlayerTracker>();
            _controller = GetComponent<CharacterController>();
            player = GetComponent<Player>();

            // reset our timeouts on start

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

            heldWire = player.GetWirePoint();

            //Crouch
            if (crouch && _controller.isGrounded)
            {
                if (!hasBeenCrouched)
                {
                    hasBeenCrouched = true;
                    if (IsHost)
                    {
                        player.SetWirePoint(gm.GetWire(player.GetTeam()).RequestForWire(transform.position), true);
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
                        gm.RemoveClientWireClientRpc(player.GetPlayerID(), heldWire.point, true);
                    }
                    hasBeenCrouched = false;
                }
                currentCrouchLerp = Mathf.Clamp01(currentCrouchLerp - (Time.deltaTime * crouchSpeed));
            }
            if (jump)
            {
                if (IsHost && heldWire != null)
                {
                    gm.RemoveClientWireClientRpc(player.GetPlayerID(), heldWire.point, true);
                }
            }
            targetSpeed *= ((1 - currentCrouchLerp) / 2.0f) + 0.5f;
            tracker.ModifyPlayerHeight(currentCrouchLerp);

            //Holding Wire
            if (IsHost && heldWire != null)
            {
                float distance = Vector3.Distance(heldWire.parent.point, transform.position);
                if (distance > _controller.radius && !directionDecided)
                {
                    directionDecided = true;
                    wireCollisionVector = (transform.position - heldWire.parent.point).normalized;
                }
                else if(distance < _controller.radius)
                {
                    directionDecided = false;
                }
                if(directionDecided)
                {
                    //Extremely cheap and fast collision for wires, using the player's current hitbox
                    if(Vector3.Distance((wireCollisionVector * distance) + heldWire.parent.point, transform.position) > _controller.radius)
                    {
                        directionDecided = false;

                        player.SetWirePoint(gm.GetWire(player.GetTeam()).RequestForWire(transform.position), true);
                        heldWire = player.GetWirePoint();
                        if (heldWire != null)
                        {
                            gm.SegmentClientWireClientRpc(player.GetPlayerID(), heldWire.point, heldWire.wireID, heldWire.parent.wireID, player.GetTeam());
                        }
                    }
                }
            }

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
                // accelerate or decelerate to target speed
                if (newAxis == Vector3.zero) targetSpeed = Vector2.zero;


                if (targetSpeed != Vector3.zero)
                {
                    if (_hasBeenMovingDelta > 0.01f)
                    {
                        _speed.x = Mathf.Lerp(_controller.velocity.x, targetSpeed.x, Time.deltaTime * acceleration);
                        _speed.z = Mathf.Lerp(_controller.velocity.z, targetSpeed.z, Time.deltaTime * acceleration);
                    }
                    else
                    {
                        _speed = targetSpeed;
                    }
                }
                else
                {
                    _speed.x = Mathf.Lerp(_controller.velocity.x, targetSpeed.x, Time.deltaTime * deceleration);
                    _speed.z = Mathf.Lerp(_controller.velocity.z, targetSpeed.z, Time.deltaTime * deceleration);
                }
            }

            //Jump
            JumpAndGravity(jump);

            if (_input.magnitude == 0)
            {
                _hasBeenMovingDelta = Mathf.Lerp(_hasBeenMovingDelta, 0, Time.deltaTime * hasMovedDeltaTimeout);
            }
            else
            {
                _hasBeenMovingDelta = Mathf.Lerp(_hasBeenMovingDelta, 1, Time.deltaTime * hasMovedDeltaTimeout);
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
                if (jump)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }
            }
            else
            {


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

        private void OnDrawGizmos()
        {
            if (heldWire != null)
            {
                Gizmos.DrawLine((wireCollisionVector * Vector3.Distance(heldWire.parent.point, transform.position)) + heldWire.parent.point, transform.position);
            }
        }
    }
}