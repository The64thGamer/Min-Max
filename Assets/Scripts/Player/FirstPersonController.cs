using Unity.Netcode;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;

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
        Menu menu;

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

        //Menu
        bool holdingMenuButton;
        bool menuIsOpen;

        //Mouselook
        bool usingMouse;
        float height;
        const float sensitivity = 10f;
        const float maxYAngle = 80f;
        const ulong botID = 64646464646464;
        Vector2 currentRotation;


        public override void OnNetworkSpawn()
        {
            gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
            tracker = this.GetComponent<PlayerTracker>();
            _controller = GetComponent<CharacterController>();
            player = GetComponent<Player>();
            menu = player.GetMenu();

            // reset our timeouts on start
            _fallTimeoutDelta = FallTimeout;

            if (PlayerPrefs.GetInt("IsVREnabled") == 0 && IsOwner && player.GetPlayerID() < botID)
            {
                usingMouse = true;
                height = PlayerPrefs.GetFloat("Settings: PlayerHeight") - 0.127f;
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
                Destroy(_mainCamera.GetComponent<TrackedPoseDriver>());
                Destroy(tracker.GetRightHand().GetComponent<ActionBasedController>());
                Destroy(tracker.GetLeftHand().GetComponent<ActionBasedController>());
            }
        }

        public void MovePlayer()
        {
            if (_controller == null) { return; }

            PlayerDataSentToServer data = player.GetTracker().GetPlayerNetworkData();

            //Menu Stuff
            if (menu != null)
            {
                if (data.menu && !holdingMenuButton)
                {
                    holdingMenuButton = true;
                    menu.gameObject.SetActive(!menu.gameObject.activeSelf);
                    menuIsOpen = menu.gameObject.activeSelf;
                    if(menuIsOpen)
                    {
                        UnityEngine.Cursor.lockState = CursorLockMode.None;
                    }
                    else
                    {
                        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    }
                }
                if (!data.menu && holdingMenuButton)
                {
                    holdingMenuButton = false;
                }
            }

            if (menuIsOpen)
            {
                data.rightJoystick = Vector2.zero;
                data.crouch = false;
                data.shoot = false;
                data.jump = false;
                
                if (usingMouse)
                {
                    menu.transform.position = _mainCamera.transform.position + (_mainCamera.transform.forward * 0.1f);
                    menu.transform.LookAt(_mainCamera.transform.position);
                    menu.transform.localScale = 3.5f * Vector3.Distance(_mainCamera.transform.position, menu.transform.position) * Mathf.Tan((PlayerPrefs.GetFloat("Settings: FOV") * Mathf.Deg2Rad) / 2) * Vector3.one;
                }
                else
                {

                }
            }

            //Mouselook
            if (usingMouse && !menuIsOpen)
            {
                currentRotation.x += Input.GetAxis("Mouse X") * sensitivity;
                currentRotation.y -= Input.GetAxis("Mouse Y") * sensitivity;
                currentRotation.x = Mathf.Repeat(currentRotation.x, 360);
                currentRotation.y = Mathf.Clamp(currentRotation.y, -maxYAngle, maxYAngle);
                Quaternion rot = Quaternion.Euler(currentRotation.y, currentRotation.x, 0);
                _mainCamera.transform.rotation = rot;
                _mainCamera.transform.localPosition = Vector3.zero;
                tracker.GetRightHand().rotation = rot;
                tracker.GetLeftHand().rotation = rot;
                tracker.GetRightHand().localPosition = new Vector3(0, height - 0.5f, 0) + (_mainCamera.transform.right * 0.35f);
                tracker.GetLeftHand().localPosition = new Vector3(0, height - 0.5f, 0) + (_mainCamera.transform.right * -0.35f);
                _mainCamera.transform.localPosition = new Vector3(0, height, 0);
            }

            if (data.shoot)
            {
                player.GetCurrentGun().Fire();
            }

            Vector3 forward = _mainCamera.transform.forward;
            Vector3 right = _mainCamera.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            Vector3 newAxis = forward * data.rightJoystick.y + right * data.rightJoystick.x;
            Vector3 targetSpeed = new Vector3(newAxis.x, 0.0f, newAxis.z).normalized * player.GetClassStats().baseSpeed / 25.0f;

            heldWire = player.GetWirePoint();

            //Crouch
            if (data.crouch && _controller.isGrounded)
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
                            gm.UpdateMatchFocalPoint(player.GetTeam());
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
                        gm.UpdateMatchFocalPoint(player.GetTeam());
                        gm.RemoveClientWireClientRpc(player.GetPlayerID(), heldWire.point, true);
                    }
                    hasBeenCrouched = false;
                }
                currentCrouchLerp = Mathf.Clamp01(currentCrouchLerp - (Time.deltaTime * crouchSpeed));
            }
            if (data.jump)
            {
                if (IsHost && heldWire != null)
                {
                    gm.UpdateMatchFocalPoint(player.GetTeam());
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
                else if (distance < _controller.radius)
                {
                    directionDecided = false;
                }
                if (directionDecided)
                {
                    //Extremely cheap and fast collision for wires, using the player's current hitbox
                    if (Vector3.Distance((wireCollisionVector * distance) + heldWire.parent.point, transform.position) > _controller.radius)
                    {
                        directionDecided = false;

                        player.SetWirePoint(gm.GetWire(player.GetTeam()).RequestForWire(transform.position), false);
                        heldWire = player.GetWirePoint();
                        if (heldWire != null)
                        {
                            gm.UpdateMatchFocalPoint(player.GetTeam());
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
                    oldInput = data.rightJoystick;
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
                            oldInput = data.rightJoystick;
                        }
                        else if (Vector2.Dot(oldInput, data.rightJoystick) < -0.5f)
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
            JumpAndGravity(data.jump);

            if (data.rightJoystick.magnitude == 0)
            {
                _hasBeenMovingDelta = Mathf.Lerp(_hasBeenMovingDelta, 0, Time.deltaTime * hasMovedDeltaTimeout);
            }
            else
            {
                _hasBeenMovingDelta = Mathf.Lerp(_hasBeenMovingDelta, 1, Time.deltaTime * hasMovedDeltaTimeout);
            }


            // move the player
            Vector3 finalVelocity = (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime;

            Vector3 oldPos = transform.position;

            _controller.Move(finalVelocity);

            //Wire
            if (heldWire != null)
            {
                heldWire.point = transform.position;
            }

            //Achievements
            if (IsOwner)
            {
                if (_controller.isGrounded)
                {
                    gm.GetAchievements().AddToValue("Achievement: Total Walking Distance", Vector3.Distance(oldPos, transform.position));
                }
                else
                {
                    gm.GetAchievements().AddToValue("Achievement: Total Air Travel", Vector3.Distance(oldPos, transform.position));
                    gm.GetAchievements().AddToValue("Achievement: Total Air-Time", Time.deltaTime);
                }
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