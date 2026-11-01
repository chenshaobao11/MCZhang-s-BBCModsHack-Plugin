using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class NoClipPlugin : MonoBehaviour
    {
        private object playerInstance;
        private Type playerType;
        private FieldInfo ccField;
        private CharacterController characterController;
        private static Harmony harmonyInstance;
        private static bool isHarmonyPatched;

        private void Awake()
        {
            try
            {
                SceneManager.sceneLoaded += this.OnSceneLoaded;
                if (!isHarmonyPatched)
                {
                    harmonyInstance = new Harmony("com.universalhack.noclip");
                    harmonyInstance.PatchAll();
                    isHarmonyPatched = true;
                }
            }
            catch { }
        }

        private void OnDestroy()
        {
            try { SceneManager.sceneLoaded -= this.OnSceneLoaded; } catch { }
            if (isHarmonyPatched && harmonyInstance != null)
            {
                try { harmonyInstance.UnpatchSelf(); isHarmonyPatched = false; } catch { }
            }
            try
            {
                if (this.characterController != null)
                    this.characterController.enabled = true;
            }
            catch { }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this.playerInstance = null;
            this.playerType = null;
            this.ccField = null;
            this.characterController = null;
        }

        private void Update()
        {
            try
            {
                if (this.playerInstance == null)
                {
                    var t = GameReflection.FindType("PlayerScript");
                    if (t == null) return;
                    var inst = GameReflection.FindInstance(t.Name);
                    if (inst == null) return;

                    this.playerInstance = inst;
                    this.playerType = t;
                    this.ccField = GameReflection.Field(t, "cc");
                    if (this.ccField != null)
                        this.characterController = this.ccField.GetValue(this.playerInstance) as CharacterController;
                }
                if (this.playerInstance == null || this.characterController == null) return;

                bool noclip = HackMenu.ActiveFeatures.Contains(Features.NoClip);

                if (noclip)
                {
                    try { if (this.characterController.enabled) this.characterController.enabled = false; } catch { }
                }
                else
                {
                    try { if (!this.characterController.enabled) this.characterController.enabled = true; } catch { }
                }
            }
            catch (Exception ex) { ModLog.Error("NoClipPlugin.Update", ex); }
        }
    }
}