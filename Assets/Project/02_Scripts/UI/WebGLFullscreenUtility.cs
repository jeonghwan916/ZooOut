using System.Runtime.InteropServices;
using UnityEngine;

public static class WebGLFullscreenUtility
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void RequestWebGLFullscreen();
#endif

    public static void RequestFullscreen()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        RequestWebGLFullscreen();
#else
        Debug.Log("WebGL fullscreen request is only available in WebGL builds.");
#endif
    }
}
