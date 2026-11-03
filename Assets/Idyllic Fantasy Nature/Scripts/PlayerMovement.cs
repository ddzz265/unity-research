using UnityEngine;
using UnityEngine.InputSystem;

namespace IdyllicFantasyNature
{
    public class PlayerMovement : MonoBehaviour
    {
        [Range(1f, 20f)]
        [SerializeField] private float _movementSpeed;
        [Tooltip("run multiplier of the movement speed")]
        [Range(1f, 20f)]
        [SerializeField] private float _runMultiplier;
        [SerializeField] private float _gravity = -9.81f;
        [Range(1f, 20f)]
        [SerializeField] private float _jumpHeight;

        private CharacterController characterController;
        Vector3 _controllerVelocity;

        // Start is called before the first frame update
        void Start()
        {
            characterController = GetComponent<CharacterController>();
        }

        // Update is called once per frame
        void Update()
        {
            // stops the y velocity when player is on the ground and the velocity has reached 0
            if (characterController.isGrounded && _controllerVelocity.y < 0)
            {
                _controllerVelocity.y = 0;
            }

            // get the movement input
            float moveX = (Keyboard.current==null?0f:(Keyboard.current.dKey.isPressed||Keyboard.current.rightArrowKey.isPressed?1f:0f)-(Keyboard.current.aKey.isPressed||Keyboard.current.leftArrowKey.isPressed?1f:0f));
            float moveZ = (Keyboard.current==null?0f:(Keyboard.current.wKey.isPressed||Keyboard.current.upArrowKey.isPressed?1f:0f)-(Keyboard.current.sKey.isPressed||Keyboard.current.downArrowKey.isPressed?1f:0f));

            // moves the controller in the desired direction on the x- and z-axis
            Vector3 movement = transform.right * moveX + transform.forward * moveZ;
            characterController.Move(movement * _movementSpeed * Time.deltaTime);

            // gravity affects the controller on the y-axis
            _controllerVelocity.y += _gravity * Time.deltaTime;

            // moves the controller on the y-axis
            characterController.Move(_controllerVelocity * Time.deltaTime);

            // the controller is able to jump when on the ground
            if ((Keyboard.current!=null && Keyboard.current.spaceKey.isPressed) && characterController.isGrounded)
            {
                _controllerVelocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }

            // the controller is able to run
            if ((Keyboard.current!=null && Keyboard.current.leftShiftKey.isPressed))
            {
                characterController.Move(movement * Time.deltaTime * _runMultiplier);
            }
        }
    }
}
