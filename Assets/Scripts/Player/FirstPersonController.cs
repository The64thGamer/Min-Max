using System.Collections.Generic;
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
        const float _terminalVelocity = 53.0f;

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

        //Prediction Values
        TickValues currentTick;
        List<PlayerDataSentToServer> oldTicksClient;
        List<TickValues> oldTicksServer;

        public override void OnNetworkSpawn()
        {
            currentTick = new TickValues();
            gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
            tracker = this.GetComponent<PlayerTracker>();
            _controller = GetComponent<CharacterController>();
            player = GetComponent<Player>();
            menu = player.GetMenu();
            oldTicksClient = new List<PlayerDataSentToServer>();
            oldTicksServer = new List<TickValues>();

            // reset our timeouts on start
            currentTick._fallTimeoutDelta = FallTimeout;

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

        public void Update()
        {
            if (_controller == null) { return; }

            currentTick.inputs = player.GetTracker().GetPlayerNetworkData();
            currentTick.inputs.deltaTime = Time.deltaTime;

            //Remove inputs in situations
            if (menuIsOpen || player.GetHealth() <= 0)
            {
                currentTick.inputs.rightJoystick = Vector2.zero;
                currentTick.inputs.crouch = false;
                currentTick.inputs.shoot = false;
                currentTick.inputs.jump = false;
                tracker.ResetInputs();
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

            //Wire
            heldWire = player.GetWirePoint();
            if (IsHost)
            {
                HostWireChecking();
            }

            //Execute Movement
            Vector3 oldPos = transform.position;
            _controller.Move(MovePlayer());
            if (!IsHost)
            {
                oldTicksClient.Add(currentTick.inputs);
            }
            if(IsHost && !IsOwner)
            {
                currentTick.pos = transform.position;
                currentTick.velocity = _controller.velocity;
                oldTicksServer.Add(currentTick);
            }

            //Achievements
            if (IsOwner && player.GetPlayerID() < botID)
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

            //Menu Stuff
            if (menu != null)
            {
                if (currentTick.inputs.menu && !holdingMenuButton)
                {
                    holdingMenuButton = true;
                    menu.gameObject.SetActive(!menu.gameObject.activeSelf);
                    menuIsOpen = menu.gameObject.activeSelf;
                    if (menuIsOpen)
                    {
                        UnityEngine.Cursor.lockState = CursorLockMode.None;
                        gm.SaveAchievements();
                    }
                    else
                    {
                        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    }
                }
                if (!currentTick.inputs.menu && holdingMenuButton)
                {
                    holdingMenuButton = false;
                }
            }

            if (menuIsOpen)
            {
                if (usingMouse)
                {
                    menu.transform.position = _mainCamera.transform.position + (_mainCamera.transform.forward * 0.1f);
                    menu.transform.LookAt(_mainCamera.transform.position);
                    menu.transform.localScale = 3.5f * Vector3.Distance(_mainCamera.transform.position, menu.transform.position) * Mathf.Tan((PlayerPrefs.GetFloat("Settings: FOV") * Mathf.Deg2Rad) / 2) * Vector3.one;
                }
            }

            if (currentTick.inputs.shoot)
            {
                player.GetCurrentGun().Fire();
            }


            //Holding Wire
            if (heldWire != null)
            {
                heldWire.point = transform.position;
                if (IsHost)
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
            }
        }

        Vector3 MovePlayer()
        {
            Vector3 forward = _mainCamera.transform.forward;
            Vector3 right = _mainCamera.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            Vector3 newAxis = forward * currentTick.inputs.rightJoystick.y + right * currentTick.inputs.rightJoystick.x;
            Vector3 targetSpeed = new Vector3(newAxis.x, 0.0f, newAxis.z).normalized * player.GetClassStats().baseSpeed / 25.0f;

            //Crouch
            if (currentTick.inputs.crouch && _controller.isGrounded)
            {
                if (!currentTick.hasBeenCrouched)
                {
                    currentTick.hasBeenCrouched = true;
                }
                currentTick.currentCrouchLerp = Mathf.Clamp01(currentTick.currentCrouchLerp + (currentTick.inputs.deltaTime * crouchSpeed));
            }
            else
            {
                if (currentTick.hasBeenCrouched)
                {
                    currentTick.hasBeenCrouched = false;
                }
                currentTick.currentCrouchLerp = Mathf.Clamp01(currentTick.currentCrouchLerp - (currentTick.inputs.deltaTime * crouchSpeed));
            }
            targetSpeed *= ((1 - currentTick.currentCrouchLerp) / 2.0f) + 0.5f;
            tracker.ModifyPlayerHeight(currentTick.currentCrouchLerp);

            //Movement rotation halted in midair
            if (!_controller.isGrounded)
            {
                if (!currentTick.hasBeenGrounded)
                {
                    currentTick.oldAxis = newAxis;
                    currentTick.oldInput = currentTick.inputs.rightJoystick;
                    currentTick.hasBeenGrounded = true;
                }
                else
                {
                    if (!currentTick.hasBeenStopped)
                    {
                        //No Starting Input In Air
                        if (currentTick.oldAxis == Vector3.zero)
                        {
                            currentTick.oldAxis = newAxis;
                            currentTick.oldInput = currentTick.inputs.rightJoystick;
                        }
                        else if (Vector2.Dot(currentTick.oldInput, currentTick.inputs.rightJoystick) < -0.5f)
                        {
                            //Stop Mid-air if holding opposite direction
                            newAxis = Vector3.zero;
                            currentTick.hasBeenStopped = true;
                        }
                        else
                        {
                            newAxis = currentTick.oldAxis;
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
                currentTick.hasBeenGrounded = false;
                currentTick.hasBeenStopped = false;
                // accelerate or decelerate to target speed
                if (newAxis == Vector3.zero) targetSpeed = Vector2.zero;


                if (targetSpeed != Vector3.zero)
                {
                    if (currentTick._hasBeenMovingDelta > 0.01f)
                    {
                        currentTick._speed.x = Mathf.Lerp(_controller.velocity.x, targetSpeed.x, currentTick.inputs.deltaTime * acceleration);
                        currentTick._speed.z = Mathf.Lerp(_controller.velocity.z, targetSpeed.z, currentTick.inputs.deltaTime * acceleration);
                    }
                    else
                    {
                        currentTick._speed = targetSpeed;
                    }
                }
                else
                {
                    currentTick._speed.x = Mathf.Lerp(_controller.velocity.x, targetSpeed.x, currentTick.inputs.deltaTime * deceleration);
                    currentTick._speed.z = Mathf.Lerp(_controller.velocity.z, targetSpeed.z, currentTick.inputs.deltaTime * deceleration);
                }
            }

            //Jump
            if (_controller.isGrounded)
            {
                // reset the fall timeout timer
                currentTick._fallTimeoutDelta = FallTimeout;

                // stop our velocity dropping infinitely when grounded
                if (currentTick._verticalVelocity < 0.0f)
                {
                    currentTick._verticalVelocity = -2f;
                }

                // Jump
                if (currentTick.inputs.jump)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    currentTick._verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }
            }
            else
            {
                // fall timeout
                if (currentTick._fallTimeoutDelta >= 0.0f)
                {
                    currentTick._fallTimeoutDelta -= currentTick.inputs.deltaTime;
                }
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (currentTick._verticalVelocity < _terminalVelocity)
            {
                currentTick._verticalVelocity += Gravity * currentTick.inputs.deltaTime;
            }

            if (currentTick.inputs.rightJoystick.magnitude == 0)
            {
                currentTick._hasBeenMovingDelta = Mathf.Lerp(currentTick._hasBeenMovingDelta, 0, currentTick.inputs.deltaTime * hasMovedDeltaTimeout);
            }
            else
            {
                currentTick._hasBeenMovingDelta = Mathf.Lerp(currentTick._hasBeenMovingDelta, 1, currentTick.inputs.deltaTime * hasMovedDeltaTimeout);
            }

            // move the player
            Vector3 finalVelocity = (currentTick._speed * currentTick.inputs.deltaTime) + new Vector3(0.0f, currentTick._verticalVelocity, 0.0f) * currentTick.inputs.deltaTime;

            return finalVelocity;
        }


        public void RecalculateClientPosition(PlayerDataSentToClient data)
        {
            currentTick._speed = data._speed;
            currentTick._verticalVelocity = data._verticalVelocity;
            currentTick._fallTimeoutDelta = data._fallTimeoutDelta;
            currentTick._hasBeenMovingDelta = data._hasBeenMovingDelta;
            currentTick.oldAxis = data.oldAxis;
            currentTick.oldInput = data.oldInput;
            currentTick.hasBeenGrounded = data.hasBeenGrounded;
            currentTick.hasBeenStopped = data.hasBeenStopped;
            currentTick.currentCrouchLerp = data.currentCrouchLerp;
            currentTick.hasBeenCrouched = data.hasBeenCrouched;

            //Achieve original Pos and Vel
            tracker.ForceNewPosition(data.pos);
            _controller.SimpleMove(data.velocity);
            tracker.ForceNewPosition(data.pos);

            //Rollback Netcode
            for (int i = 0; i < oldTicksClient.Count; i++)
            {
                currentTick.inputs = oldTicksClient[i];
                //For non-owned clients
                if (!IsOwner && !IsHost)
                {
                    currentTick.inputs.rightJoystick = data.rightJoystick;
                    currentTick.inputs.jump = data.jump;
                    currentTick.inputs.shoot = data.shoot;
                    currentTick.inputs.crouch = data.crouch;
                    currentTick.inputs.menu = data.menu;
                }
                _controller.Move(MovePlayer());
            }
            oldTicksClient = new List<PlayerDataSentToServer>();
        }

        public void RecalculateServerPosition(PlayerDataSentToServer data)
        {
            //Rollback Netcode
            for (int i = 0; i < oldTicksServer.Count; i++)
            {
                if(i == 0)
                {
                    currentTick.inputs.rightJoystick = data.rightJoystick;
                    currentTick.inputs.jump = data.jump;
                    currentTick.inputs.shoot = data.shoot;
                    currentTick.inputs.crouch = data.crouch;
                    currentTick.inputs.menu = data.menu;
                    tracker.ForceNewPosition(oldTicksServer[0].pos);
                    _controller.SimpleMove(oldTicksServer[0].velocity);
                    tracker.ForceNewPosition(oldTicksServer[0].pos);
                }
                currentTick._speed = oldTicksServer[i]._speed;
                currentTick._verticalVelocity = oldTicksServer[i]._verticalVelocity;
                currentTick._fallTimeoutDelta = oldTicksServer[i]._fallTimeoutDelta;
                currentTick._hasBeenMovingDelta = oldTicksServer[i]._hasBeenMovingDelta;
                currentTick.oldAxis = oldTicksServer[i].oldAxis;
                currentTick.oldInput = oldTicksServer[i].oldInput;
                currentTick.hasBeenGrounded = oldTicksServer[i].hasBeenGrounded;
                currentTick.hasBeenStopped = oldTicksServer[i].hasBeenStopped;
                currentTick.currentCrouchLerp = oldTicksServer[i].currentCrouchLerp;
                currentTick.hasBeenCrouched = oldTicksServer[i].hasBeenCrouched;
                _controller.Move(MovePlayer());
            }
            oldTicksServer = new List<TickValues>();
        }

        void HostWireChecking()
        {
            if (currentTick.inputs.crouch && _controller.isGrounded)
            {
                if (!currentTick.hasBeenCrouched)
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
            else if (currentTick.hasBeenCrouched && heldWire != null)
            {
                gm.UpdateMatchFocalPoint(player.GetTeam());
                gm.RemoveClientWireClientRpc(player.GetPlayerID(), heldWire.point, true);
            }

            if (currentTick.inputs.jump && heldWire != null)
            {
                gm.UpdateMatchFocalPoint(player.GetTeam());
                gm.RemoveClientWireClientRpc(player.GetPlayerID(), heldWire.point, true);
            }
        }

        public TickValues GetCurrentTick()
        {
            return currentTick;
        }

        private void OnDrawGizmos()
        {
            if (heldWire != null)
            {
                Gizmos.DrawLine((wireCollisionVector * Vector3.Distance(heldWire.parent.point, transform.position)) + heldWire.parent.point, transform.position);
            }
        }

        [System.Serializable]
        public struct TickValues
        {
            //Input
            public PlayerDataSentToServer inputs;

            //Movement
            public Vector3 _speed;
            public float _verticalVelocity;
            public float _fallTimeoutDelta;
            public float _hasBeenMovingDelta;

            //Midair Movement
            public Vector3 oldAxis;
            public Vector2 oldInput;
            public bool hasBeenGrounded;
            public bool hasBeenStopped;

            //Crouch
            public float currentCrouchLerp;
            public bool hasBeenCrouched;

            //Server Only
            public Vector3 pos;
            public Vector3 velocity;
        }
    }
}