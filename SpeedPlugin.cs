using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class SpeedPlugin : MonoBehaviour
    {
        private float originalWalkSpeed;
        private float originalRunSpeed;
        private bool speedApplied;

        private object playerInstance;
        private FieldInfo walkField;
        private FieldInfo runField;

        private void Awake() => SceneManager.sceneLoaded += this.OnSceneLoaded;
        private void OnDestroy() => SceneManager.sceneLoaded -= this.OnSceneLoaded;
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this.playerInstance = null;
            this.walkField = null;
            this.runField = null;
            this.speedApplied = false;
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
                    this.walkField = GameReflection.Field(t, "walkSpeed");
                    this.runField  = GameReflection.Field(t, "runSpeed");

                    try
                    {
                        if (this.walkField != null) this.originalWalkSpeed = Convert.ToSingle(this.walkField.GetValue(inst));
                        if (this.runField  != null) this.originalRunSpeed  = Convert.ToSingle(this.runField.GetValue(inst));
                    }
                    catch
                    {
                        this.playerInstance = null;
                        this.walkField = null;
                        this.runField = null;
                        return;
                    }
                }
                if (this.playerInstance == null || this.walkField == null || this.runField == null) return;

                bool wantSpeed = HackMenu.ActiveFeatures.Contains(Features.Speed);

                if (wantSpeed)
                {
                    if (!this.speedApplied)
                    {
                        try
                        {
                            this.walkField.SetValue(this.playerInstance, this.originalWalkSpeed * 3f);
                            this.runField.SetValue(this.playerInstance, this.originalRunSpeed * 3f);
                            this.speedApplied = true;
                        }
                        catch { }
                    }
                }
                else if (this.speedApplied)
                {
                    try
                    {
                        this.walkField.SetValue(this.playerInstance, this.originalWalkSpeed);
                        this.runField.SetValue(this.playerInstance, this.originalRunSpeed);
                    }
                    catch { }
                    this.speedApplied = false;
                }
            }
            catch (Exception ex) { ModLog.Error("SpeedPlugin.Update", ex); }
        }
    }
}