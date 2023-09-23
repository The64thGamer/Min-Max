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

        const float menuScale = 1.44f;
        const float menuY = -0.43f;
        const float menuZ = 0.55f;

        //Wire
        bool directionDecided;
        Vector3 wireCollisionVector;
        Wire.WirePoint heldWire;

        //Menu
        bool holdingMenuButton;

        //Mouselook
        bool usingMouse;
        float height;
        const float sensitivity = 10f;
        const float maxYAngle = 80f;
        const ulong botID = 64646464646464;
        Vector2 currentRotation;

        bool nowSendServerValues;

        private void Awake()
        {
            gm = GameObject.Find("Global Manager").GetComponent<GlobalManager>();
            tracker = this.GetComponent<PlayerTracker>();
            _controller = GetComponent<CharacterController>();
            player = GetComponent<Player>();
            menu = player.GetMenu();
        }

        public override void OnNetworkSpawn()
        {
            if (PlayerPrefs.GetInt("IsVREnabled") == 0 && IsOwner && player.GetPlayerID() < botID)
            {
                usingMouse = true;
                height = PlayerPrefs.GetFloat("Settings: PlayerHeight") - 0.127f;
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
                Destroy(_mainCamera.GetComponent<TrackedPoseDriver>());
                Destroy(tracker.GetRightHand().GetComponent<ActionBasedController>());
                Destroy(tracker.GetLeftHand().GetComponent<ActionBasedController>());
                menu.SetMouseOnly();
            }
        }

        public void Update()
        {
            if (_controller == null) { return; }

            //Mouselook
            if (usingMouse && !menu.GetOpenState())
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

            //currentPositionData
            PlayerPositionData currentPositionData = gm.GetCurrentPlayerPositonData(player.GetPlayerID());
            PlayerInputData currentInputData = gm.GetCurrentPlayerInputData(player.GetPlayerID());

            if(player.GetPlayerID() < botID)
            {
                currentInputData = player.GetTracker().GetPlayerInputData();

            }
            else
            {
                currentInputData = gm.GetCurrentPlayerInputData(player.GetPlayerID());
            }
            currentPositionData.position = transform.position;
            currentPositionData.velocity = _controller.velocity;
            currentPositionData.mainCamforward = _mainCamera.transform.forward;
            currentPositionData.mainCamRight = _mainCamera.transform.right;
            currentPositionData.baseSpeed = gm.FindPlayerStat(player.GetPlayerID(), ChangablePlayerStats.groundSpeed);

            //Remove inputs in situations
            if (menu.GetOpenState() || gm.FindPlayerStat(player.GetPlayerID(),ChangablePlayerStats.currentHealth) <= 0)
            {
                currentInputData.rightJoystick = Vector2.zero;
                currentInputData.crouch = false;
                currentInputData.shoot = false;
                currentInputData.jump = false;
                tracker.ResetInputs();
            }

            //Wire
            heldWire = player.GetWirePoint();
            if (IsHost)
            {
                HostWireChecking(currentInputData, currentPositionData);
            }

            Vector3 oldPos = transform.position;

            //old deltatime function == NetworkManager.LocalTime.TimeAsFloat - currentPositionData.lastTimeSynced;
            currentPositionData = MovePlayer(currentPositionData, currentInputData, Time.deltaTime);

            _controller.Move(currentPositionData.velocity);

            if (IsHost)
            {
                if (IsOwner)
                {
                    gm.SetPlayerPositionDataClientRpc(false,0,player.GetPlayerID(), currentPositionData);
                }
                else if (nowSendServerValues || player.GetPlayerID() >= botID)
                {
                    nowSendServerValues = false;
                    gm.SetPlayerPositionDataClientRpc(false, 0, player.GetPlayerID(), currentPositionData);
                }
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
                    gm.GetAchievements().AddToValue("Achievement: Total Air Travel", Vector3.Distance(oldPos, transform.position)); ;
                    gm.GetAchievements().AddToValue("Achievement: Total Air-Time", Time.deltaTime);
                }
            }

            if (currentInputData.shoot)
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

                            player.SetWirePoint(gm.GetWire(gm.FindPlayerTeam(player.GetPlayerID())).RequestForWire(transform.position), false);
                            heldWire = player.GetWirePoint();
                            if (heldWire != null)
                            {
                                gm.UpdateMatchFocalPoint(gm.FindPlayerTeam(player.GetPlayerID()));
                                gm.SegmentClientWireClientRpc(player.GetPlayerID(), heldWire.point, heldWire.wireID, heldWire.parent.wireID, gm.FindPlayerTeam(player.GetPlayerID()));
                            }
                        }
                    }
                }
            }
        }

        private void LateUpdate()
        {
            PlayerPositionData currentPositionData = gm.GetCurrentPlayerPositonData(player.GetPlayerID());
            PlayerInputData currentInputData = gm.GetCurrentPlayerInputData(player.GetPlayerID());

            //Menu Stuff
            if (menu != null)
            {
                if (currentInputData.menu && !holdingMenuButton)
                {
                    holdingMenuButton = true;
                    menu.CloseOpen(!menu.GetOpenState());
                }
                if (!currentInputData.menu && holdingMenuButton)
                {
                    holdingMenuButton = false;
                }
                if (menu.GetOpenState())
                {
                    if (usingMouse)
                    {
                        menu.transform.position = _mainCamera.transform.position + (_mainCamera.transform.forward * 0.1f);
                        menu.transform.LookAt(_mainCamera.transform.position);
                        menu.transform.localScale = 3.5f * Vector3.Distance(_mainCamera.transform.position, menu.transform.position) * Mathf.Tan((PlayerPrefs.GetFloat("Settings: FOV") * Mathf.Deg2Rad) / 2) * Vector3.one;
                    }
                    else
                    {
                        menu.transform.localScale = menuScale * Vector3.Distance(_mainCamera.transform.position, menu.transform.position) * Mathf.Tan((PlayerPrefs.GetFloat("Settings: FOV") * Mathf.Deg2Rad) / 2) * Vector3.one;
                        menu.transform.LookAt(_mainCamera.transform.position);
                        Vector3 forward = currentPositionData.mainCamforward;
                        Vector3 menuVector = menu.transform.position - _mainCamera.transform.position;
                        menuVector.y = 0;
                        forward.y = 0f;
                        forward.Normalize();
                        menuVector.Normalize();
                        menu.transform.position = Vector3.Lerp(_mainCamera.transform.position + new Vector3(0, menuY, 0) + (forward * menuZ), menu.transform.position, Mathf.Max(0, Vector3.Dot(forward, menuVector)));
                    }
                }
            }
        }

        PlayerPositionData MovePlayer(PlayerPositionData currentPositionData, PlayerInputData currentInputData, float deltaTime)
        {

            Vector3 forward = currentPositionData.mainCamforward;
            Vector3 right = currentPositionData.mainCamRight;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            Vector3 newAxis = forward * currentInputData.rightJoystick.y + right * currentInputData.rightJoystick.x;
            Vector3 targetSpeed = new Vector3(newAxis.x, 0.0f, newAxis.z).normalized * currentPositionData.baseSpeed / 25.0f;



            //Crouch
            if (currentInputData.crouch && _controller.isGrounded)
            {
                if (!currentPositionData.hasBeenCrouched)
                {
                    currentPositionData.hasBeenCrouched = true;
                }
                currentPositionData.currentCrouchLerp = Mathf.Clamp01(currentPositionData.currentCrouchLerp + (deltaTime * crouchSpeed));
            }
            else
            {
                if (currentPositionData.hasBeenCrouched)
                {
                    currentPositionData.hasBeenCrouched = false;
                }
                currentPositionData.currentCrouchLerp = Mathf.Clamp01(currentPositionData.currentCrouchLerp - (deltaTime * crouchSpeed));
            }
            targetSpeed *= ((1 - currentPositionData.currentCrouchLerp) / 2.0f) + 0.5f;
            tracker.ModifyPlayerHeight(currentPositionData.currentCrouchLerp);

            //Movement rotation halted in midair
            if (!_controller.isGrounded)
            {
                if (!currentPositionData.hasBeenGrounded)
                {
                    currentPositionData.oldAxis = newAxis;
                    currentPositionData.oldInput = currentInputData.rightJoystick;
                    currentPositionData.hasBeenGrounded = true;
                }
                else
                {
                    if (!currentPositionData.hasBeenStopped)
                    {
                        //No Starting Input In Air
                        if (currentPositionData.oldAxis == Vector3.zero)
                        {
                            currentPositionData.oldAxis = newAxis;
                            currentPositionData.oldInput = currentInputData.rightJoystick;
                        }
                        else if (Vector2.Dot(currentPositionData.oldInput, currentInputData.rightJoystick) < -0.5f)
                        {
                            //Stop Mid-air if holding opposite direction
                            newAxis = Vector3.zero;
                            currentPositionData.hasBeenStopped = true;
                        }
                        else
                        {
                            newAxis = currentPositionData.oldAxis;
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
                currentPositionData.hasBeenGrounded = false;
                currentPositionData.hasBeenStopped = false;
                // accelerate or decelerate to target speed
                if (newAxis == Vector3.zero) targetSpeed = Vector2.zero;


                if (targetSpeed != Vector3.zero)
                {
                    if (currentPositionData._hasBeenMovingDelta > 0.01f)
                    {
                        currentPositionData._speed.x = Mathf.Lerp(currentPositionData.velocity.x, targetSpeed.x, deltaTime * acceleration);
                        currentPositionData._speed.z = Mathf.Lerp(currentPositionData.velocity.z, targetSpeed.z, deltaTime * acceleration);
                    }
                    else
                    {
                        currentPositionData._speed = targetSpeed;
                    }
                }
                else
                {
                    currentPositionData._speed.x = Mathf.Lerp(currentPositionData.velocity.x, targetSpeed.x, deltaTime * deceleration);
                    currentPositionData._speed.z = Mathf.Lerp(currentPositionData.velocity.z, targetSpeed.z, deltaTime * deceleration);
                }
            }

            //Jump
            if (_controller.isGrounded)
            {
                // reset the fall timeout timer
                currentPositionData._fallTimeoutDelta = FallTimeout;

                // stop our velocity dropping infinitely when grounded
                if (currentPositionData._verticalVelocity < 0.0f)
                {
                    currentPositionData._verticalVelocity = -2f;
                }

                // Jump
                if (currentInputData.jump)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    currentPositionData._verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }
            }
            else
            {
                // fall timeout
                if (currentPositionData._fallTimeoutDelta >= 0.0f)
                {
                    currentPositionData._fallTimeoutDelta -= deltaTime;
                }
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (currentPositionData._verticalVelocity < _terminalVelocity)
            {
                currentPositionData._verticalVelocity += Gravity * deltaTime;
            }

            if (currentInputData.rightJoystick.magnitude == 0)
            {
                currentPositionData._hasBeenMovingDelta = Mathf.Lerp(currentPositionData._hasBeenMovingDelta, 0, deltaTime * hasMovedDeltaTimeout);
            }
            else
            {
                currentPositionData._hasBeenMovingDelta = Mathf.Lerp(currentPositionData._hasBeenMovingDelta, 1, deltaTime * hasMovedDeltaTimeout);
            }

            // move the player
            Vector3 finalVelocity = (currentPositionData._speed * deltaTime) + new Vector3(0.0f, currentPositionData._verticalVelocity, 0.0f) * deltaTime;

            currentPositionData.velocity = finalVelocity;
            return currentPositionData;
        }


        public void RecalculateClientPosition()
        {
            /*
            //Initial Pass
            currentPositionData._speed = data._speed;
            currentPositionData._verticalVelocity = data._verticalVelocity;
            currentPositionData._fallTimeoutDelta = data._fallTimeoutDelta;
            currentPositionData._hasBeenMovingDelta = data._hasBeenMovingDelta;
            currentPositionData.oldAxis = data.oldAxis;
            currentPositionData.oldInput = data.oldInput;
            currentPositionData.hasBeenGrounded = data.hasBeenGrounded;
            currentPositionData.hasBeenStopped = data.hasBeenStopped;
            currentPositionData.currentCrouchLerp = data.currentCrouchLerp;
            currentPositionData.hasBeenCrouched = data.hasBeenCrouched;
            currentPositionData.velocity = data.velocity;
            currentPositionData.mainCamforward = data.mainCamforward;
            currentPositionData.mainCamRight = data.mainCamRight;
            currentPositionData.baseSpeed = data.baseSpeed;

            _controller.enabled = false;
            transform.position = data.pos;
            _controller.enabled = true;
            _controller.SimpleMove(data.velocity);
            _controller.enabled = false;
            transform.position = data.pos;
            _controller.enabled = true;

            //Rollback Netcode
            int deleteBehindThis = 0;
            float deltaTime = data.serverTime;

            for (int i = 0; i < oldTicksClient.Count; i++)
            {
                if (oldTicksClient[i].localTime > data.serverTime)
                {
                    if (i - 1 < 0)
                    {
                        currentInputData = oldTicksClient[i];
                    }
                    else
                    {
                        currentInputData = oldTicksClient[i - 1];
                    }
                    currentInputData.deltaTime = oldTicksClient[i].localTime - deltaTime;
                    deltaTime = oldTicksClient[i].localTime;
                    //For non-owned clients
                    if (!IsOwner && !IsHost)
                    {
                        currentInputData.rightJoystick = data.rightJoystick;
                        currentInputData.jump = data.jump;
                        currentInputData.shoot = data.shoot;
                        currentInputData.crouch = data.crouch;
                        currentInputData.menu = data.menu;
                    }
                    _controller.Move(MovePlayer());
                }
                else
                {
                    deleteBehindThis++;
                }
            }
            oldTicksClient.RemoveRange(0, deleteBehindThis);
            */
        }

        public void RecalculateServerPosition()
        {
            /*
            nowSendServerValues = true;
            if (oldTicksServer.Count > 0)
            {
                _controller.enabled = false;
                transform.position = oldTicksServer[0].pos;
                _controller.enabled = true;
                _controller.SimpleMove(oldTicksServer[0].velocity);
                _controller.enabled = false;
                transform.position = oldTicksServer[0].pos;
                _controller.enabled = true;
                currentPositionData = oldTicksServer[0];
                currentInputData.rightJoystick = data.rightJoystick;
                currentInputData.jump = data.jump;
                currentInputData.shoot = data.shoot;
                currentInputData.crouch = data.crouch;
                currentInputData.menu = data.menu;
            }
            //Player Input Compensation
            for (int i = 0; i < oldTicksServer.Count; i++)
            {
                currentInputData.deltaTime = oldTicksServer[i].inputs.deltaTime;
                _controller.Move(MovePlayer());
            }
            oldTicksServer = new List<TickValues
            */
        }

        void HostWireChecking(PlayerInputData currentInputData, PlayerPositionData currentPositionData)
        {
            if (currentInputData.crouch && _controller.isGrounded)
            {
                if (!currentPositionData.hasBeenCrouched && gm.FindPlayerStat(player.GetPlayerID(),ChangablePlayerStats.currentHealth) > 0)
                {
                    player.SetWirePoint(gm.GetWire(gm.FindPlayerTeam(player.GetPlayerID())).RequestForWire(transform.position), true);
                    heldWire = player.GetWirePoint();
                    if (heldWire != null)
                    {
                        gm.UpdateMatchFocalPoint(gm.FindPlayerTeam(player.GetPlayerID()));
                        gm.GiveClientWireClientRpc(player.GetPlayerID(), heldWire.wireID, heldWire.parent.wireID, gm.FindPlayerTeam(player.GetPlayerID()));
                    }
                }
            }
            else if (currentPositionData.hasBeenCrouched && heldWire != null)
            {
                gm.UpdateMatchFocalPoint(gm.FindPlayerTeam(player.GetPlayerID()));
                gm.RemoveClientWireClientRpc(player.GetPlayerID(), heldWire.point, true);
            }

            if (currentInputData.jump && heldWire != null)
            {
                gm.UpdateMatchFocalPoint(gm.FindPlayerTeam(player.GetPlayerID()));
                gm.RemoveClientWireClientRpc(player.GetPlayerID(), heldWire.point, true);
            }
        }

        public bool isUsingMouse()
        {
            return usingMouse;
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