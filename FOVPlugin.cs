using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class FOVPlugin : MonoBehaviour
    {
        private Camera targetCamera;
        private float defaultFOV = 60f;
        private bool fovRecorded;
        private bool zoomInApplied, fovApplied;

        private void Awake() => SceneManager.sceneLoaded += this.OnSceneLoaded;
        private void OnDestroy() => SceneManager.sceneLoaded -= this.OnSceneLoaded;
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this.targetCamera = null;
            this.fovRecorded = false;
            this.zoomInApplied = false;
            this.fovApplied = false;
        }

        private void Update()
        {
            try
            {
                // fake null 检查：相机被销毁后 Camera.main 会返回 null
                if (this.targetCamera == null)
                {
                    this.targetCamera = Camera.main;
                    if (this.targetCamera == null) return;
                    this.fovRecorded = false;
                    this.zoomInApplied = false;
                    this.fovApplied = false;
                }

                if (!this.fovRecorded)
                {
                    try
                    {
                        this.defaultFOV = this.targetCamera.fieldOfView;
                        this.fovRecorded = true;
                    }
                    catch
                    {
                        this.targetCamera = null;
                        this.fovRecorded = false;
                        return;
                    }
                }

                bool zoomIn = HackMenu.ActiveFeatures.Contains(Features.Magnifier);
                bool fovWide = HackMenu.ActiveFeatures.Contains(Features.Fov);
                if (zoomIn == this.zoomInApplied && fovWide == this.fovApplied) return;
                this.zoomInApplied = zoomIn;
                this.fovApplied = fovWide;

                try
                {
                    if (zoomIn) this.targetCamera.fieldOfView = this.defaultFOV - 30f;
                    else if (fovWide) this.targetCamera.fieldOfView = this.defaultFOV + 50f;
                    else this.targetCamera.fieldOfView = this.defaultFOV;
                }
                catch
                {
                    this.targetCamera = null;
                    this.fovRecorded = false;
                }
            }
            catch (Exception ex) { ModLog.Error("FOVPlugin.Update", ex); }
        }
    }
}