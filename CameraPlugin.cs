using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class CameraPlugin : MonoBehaviour
    {
        private Harmony harmony;
        private float xRotation = 0f;
        private float yRotation = 0f;
        private bool isActive;
        
        private void Awake()
        {
            try
            {
                this.harmony = new Harmony("com.universal.camera3d");
                SceneManager.sceneLoaded += this.OnSceneLoaded;
                PatchHelper.Patch(this.harmony, "CameraScript", "LateUpdate", typeof(Patches).GetMethod("CameraLateUpdate_Prefix"));
            }
            catch { }
        }
        private void OnDestroy() { SceneManager.sceneLoaded -= this.OnSceneLoaded; try { this.harmony?.UnpatchSelf(); } catch { } }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { this.isActive = false; this.xRotation = 0f; this.yRotation = 0f; }

        private void Update()
        {
            try
            {
                bool wantFreeCam = HackMenu.ActiveFeatures.Contains(Features.FreeCamera);
                var cam = Camera.main;
                if (cam == null) return;

                if (wantFreeCam)
                {
                    if (!this.isActive)
                    {
                        this.isActive = true;
                        this.xRotation = cam.transform.eulerAngles.x;
                        this.yRotation = cam.transform.eulerAngles.y;
                    }
                    float mouseX = Input.GetAxis("Mouse X") * 3f;
                    float mouseY = Input.GetAxis("Mouse Y") * 3f;
                    this.yRotation += mouseX;
                    this.xRotation -= mouseY;
                    this.xRotation = Mathf.Clamp(this.xRotation, -90f, 90f);
                    cam.transform.rotation = Quaternion.Euler(this.xRotation, this.yRotation, 0f);
                }
                else if (this.isActive) this.isActive = false;
            }
            catch (System.Exception ex) { ModLog.Error("CameraPlugin.Update", ex); }
        }
        
        public static class Patches 
        { 
            public static bool CameraLateUpdate_Prefix()
            {
                try { return !HackMenu.ActiveFeatures.Contains(Features.FreeCamera); } catch { return true; }
            }
        }
    }
}