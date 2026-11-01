using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class VisualPlugin : MonoBehaviour
    {
        private Harmony harmony;
        private bool redModeApplied;
        private static readonly System.Random random = new System.Random();

        private Color preRedAmbient;
        private bool preRedAmbientValid;

        private void Awake()
        {
            try
            {
                this.harmony = new Harmony("com.universal.visual");
                SceneManager.sceneLoaded += this.OnSceneLoaded;

                var billboardPrefix = typeof(Patches).GetMethod("BillboardLateUpdate_Prefix");
                PatchHelper.Patch(this.harmony, "Billboard", "LateUpdate", billboardPrefix, null);
            }
            catch { }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            try { this.harmony?.UnpatchSelf(); } catch { }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this.redModeApplied = false;
            this.preRedAmbientValid = false;
        }

        private void Update()
        {
            try
            {
                bool red = HackMenu.ActiveFeatures.Contains(Features.RedLight);

                if (red && !this.redModeApplied)
                {
                    if (!this.preRedAmbientValid)
                    {
                        this.preRedAmbient = RenderSettings.ambientLight;
                        this.preRedAmbientValid = true;
                    }
                    RenderSettings.ambientLight = Color.red;
                    this.redModeApplied = true;
                }
                else if (!red && this.redModeApplied)
                {
                    this.redModeApplied = false;

                    bool otherOverride = HackMenu.ActiveFeatures.Contains(Features.Disco);
                    if (!otherOverride && this.preRedAmbientValid)
                        RenderSettings.ambientLight = this.preRedAmbient;

                    this.preRedAmbientValid = false;
                }
            }
            catch (Exception ex) { ModLog.Error("VisualPlugin.Update", ex); }
        }

        public static class Patches
        {
            public static bool BillboardLateUpdate_Prefix(MonoBehaviour __instance)
            {
                try
                {
                    if (__instance == null) return true;
                    if (!HackMenu.ActiveFeatures.Contains(Features.TextureRotate)) return true;
                    __instance.transform.rotation = Quaternion.Euler(
                        (float)(random.NextDouble() * 360.0),
                        (float)(random.NextDouble() * 360.0),
                        (float)(random.NextDouble() * 360.0));
                    return false;
                }
                catch { return true; }
            }
        }
    }
}