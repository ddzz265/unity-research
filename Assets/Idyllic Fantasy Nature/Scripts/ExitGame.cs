using UnityEngine;
using UnityEngine.InputSystem;

namespace IdyllicFantasyNature
{
    public class ExitGame : MonoBehaviour
    {
        void Update()
        {
            // If you press the ESC key in the game, the application will be closed
            if ((Keyboard.current!=null && Keyboard.current.escapeKey.isPressed))
            {
                Application.Quit();
            }
        }
    }
}
