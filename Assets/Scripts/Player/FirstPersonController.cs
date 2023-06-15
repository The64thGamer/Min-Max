using UnityEngine;

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

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

		//Consts
        const float _threshold = 0.01f;

        // player
        float _speed;
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


		 void Start()
		{
			_controller = GetComponent<CharacterController>();

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
		}

		public void MovePlayer(Vector2 _input, bool jump, bool crouch)
        {
            Vector3 forward = _mainCamera.transform.forward;
            Vector3 right = _mainCamera.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            Vector3 newAxis = forward * _input.y + right * _input.x;

			//Crouch
			if(crouch)
			{

			}
			else
			{

			}

			//Movement rotation halted in midair
			if (_controller != null)
			{
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
			}


			//Jump
            JumpAndGravity(jump);

            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = MoveSpeed;

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (newAxis == Vector3.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(newAxis.x, 0.0f, newAxis.z).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (newAxis != Vector3.zero)
			{
				// move
				inputDirection = transform.right * newAxis.x + transform.forward * newAxis.z;
			}

			// move the player
			Vector3 finalVelocity = inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime;

            _controller.Move(finalVelocity);
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