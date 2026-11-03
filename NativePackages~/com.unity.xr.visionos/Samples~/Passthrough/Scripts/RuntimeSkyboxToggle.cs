using UnityEngine;

public class RuntimeSkyboxToggle : MonoBehaviour
{
    Camera m_MainCamera;
    Color m_ClearBlack = new Color(0, 0, 0, 0);
    bool m_SkyboxEnabled = true;

    void OnEnable()
    {
        m_MainCamera = Camera.main;
        if (m_MainCamera == null)
        {
            m_MainCamera = Camera.current;
        }

        if (m_MainCamera == null)
        {
            Debug.LogError("No active camera found");
            return;
        }

        m_SkyboxEnabled = m_MainCamera.clearFlags == CameraClearFlags.Skybox;

    }

    public void ToggleSkybox()
    {
        m_SkyboxEnabled = !m_SkyboxEnabled;
        if (m_SkyboxEnabled)
        {
            m_MainCamera.clearFlags = CameraClearFlags.Skybox;
        }
        else
        {
            m_MainCamera.clearFlags = CameraClearFlags.SolidColor;
            m_MainCamera.backgroundColor = m_ClearBlack;
        }
    }
}
