using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraControlManager : MonoBehaviour
{
    private String xrRigSceneName = "XRRigControl";
    private String webglRigSceneName = "WebGLControl";

    private void Awake()
    {
#if UNITY_WEBGL
            SceneManager.LoadScene(webglRigSceneName, LoadSceneMode.Additive);
#else
        Debug.Log("in here");
        SceneManager.LoadScene(xrRigSceneName, LoadSceneMode.Additive);
#endif

    }
}
