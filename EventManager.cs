using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class EventPlugin : MonoBehaviour
    {
        private readonly Dictionary<string, bool> lastStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, FieldInfo> fieldCache = new Dictionary<string, FieldInfo>();
        private object gcInstance;
        private Type gcType;
        private bool initialized;

        private static readonly string[] ToggleFeatures = { Features.NoBaldi, Features.NoPrincipal, Features.NoCrafters, Features.NoPlaytime, Features.NoSweep, Features.NoFirstPrize, Features.NoBully };
        private static readonly string[] ToggleFields   = { "baldi", "principal", "crafters", "playtime", "gottaSweep", "firstPrize", "bully" };

        private void Awake() => SceneManager.sceneLoaded += this.OnSceneLoaded;
        private void OnDestroy() => SceneManager.sceneLoaded -= this.OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this.gcInstance = null;
            this.gcType = null;
            this.initialized = false;
            this.lastStates.Clear();
            this.fieldCache.Clear();
        }

        private void Update()
        {
            try
            {
                if (this.gcInstance == null)
                {
                    this.gcType = GameReflection.FindType("GameControllerScript") ?? GameReflection.FindType("GameController");
                    if (this.gcType != null) this.gcInstance = GameReflection.FindInstance(this.gcType.Name);
                    if (this.gcInstance == null) return;
                }
                if (!this.initialized) { this.initialized = true; return; }

                for (int i = 0; i < ToggleFeatures.Length; i++)
                {
                    bool on = HackMenu.ActiveFeatures.Contains(ToggleFeatures[i]);
                    this.lastStates.TryGetValue(ToggleFeatures[i], out var prev);

                    if (on)
                    {
                        var go = this.GetNpcObject(i);
                        if (go != null && go.activeSelf) go.SetActive(false);
                        this.lastStates[ToggleFeatures[i]] = true;
                    }
                    else if (prev)
                    {
                        var go = this.GetNpcObject(i);
                        if (go != null && !go.activeSelf) go.SetActive(true);
                        this.lastStates[ToggleFeatures[i]] = false;
                    }
                    else if (!this.lastStates.ContainsKey(ToggleFeatures[i]))
                    {
                        this.lastStates[ToggleFeatures[i]] = false;
                    }
                }
            }
            catch (Exception ex) { ModLog.Error("EventPlugin.Update", ex); }
        }

        private GameObject GetNpcObject(int index)
        {
            try
            {
                if (this.gcInstance == null || this.gcType == null) return null;
                if (!this.fieldCache.TryGetValue(ToggleFields[index], out var f))
                {
                    f = GameReflection.Field(this.gcType, ToggleFields[index]);
                    this.fieldCache[ToggleFields[index]] = f;
                }
                if (f == null) return null;
                return f.GetValue(this.gcInstance) as GameObject;
            }
            catch { return null; }
        }
    }
}
