using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class NotebookExitPlugin : MonoBehaviour
    {
        private static Type gcType;
        private static MethodInfo collectMethod;
        private static MethodInfo exitMethod;

        // 上一帧的开关状态，用于边沿触发（从关到开才执行一次）
        private bool lastCollectState;
        private bool lastExitState;

        private void Awake()
        {
            try
            {
                gcType = GameReflection.FindType("GameControllerScript") ?? GameReflection.FindType("GameController");
                if (gcType == null)
                {
                    ModLog.Warning("NotebookExitPlugin: 找不到 GameControllerScript，功能不可用");
                    return;
                }

                collectMethod = GameReflection.Method(gcType, "CollectNotebook");
                if (collectMethod == null)
                    ModLog.Warning("NotebookExitPlugin: 找不到 CollectNotebook 方法");

                exitMethod = GameReflection.Method(gcType, "ExitReached");
                if (exitMethod == null)
                    ModLog.Warning("NotebookExitPlugin: 找不到 ExitReached 方法");
            }
            catch (Exception ex) { ModLog.Error("NotebookExitPlugin.Awake", ex); }

            SceneManager.sceneLoaded += this.OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 场景切换：把上一帧状态对齐到当前 ActiveFeatures，
            // 避免「开关还开着」进入新场景时误触发一次。
            this.lastCollectState = HackMenu.ActiveFeatures.Contains(Features.CollectOneNotebook);
            this.lastExitState    = HackMenu.ActiveFeatures.Contains(Features.TriggerExitOnce);
        }

        private void Update()
        {
            try
            {
                this.TickCollectOneNotebook();
                this.TickTriggerExitOnce();
            }
            catch (Exception ex) { ModLog.Error("NotebookExitPlugin.Update", ex); }
        }

        // 「收集一个书」：从关到开时调用一次 CollectNotebook
        private void TickCollectOneNotebook()
        {
            bool on = HackMenu.ActiveFeatures.Contains(Features.CollectOneNotebook);

            if (on && !this.lastCollectState && collectMethod != null)
            {
                var inst = GetGcInstance();
                if (inst != null)
                {
                    try { collectMethod.Invoke(inst, null); }
                    catch (Exception ex) { ModLog.Error("NotebookExitPlugin.CollectNotebook", ex); }
                }
            }

            this.lastCollectState = on;
        }

        // 「触发一次出口」：从关到开时调用一次 ExitReached
        private void TickTriggerExitOnce()
        {
            bool on = HackMenu.ActiveFeatures.Contains(Features.TriggerExitOnce);

            if (on && !this.lastExitState && exitMethod != null)
            {
                var inst = GetGcInstance();
                if (inst != null)
                {
                    try { exitMethod.Invoke(inst, null); }
                    catch (Exception ex) { ModLog.Error("NotebookExitPlugin.ExitReached", ex); }
                }
            }

            this.lastExitState = on;
        }

        private static object GetGcInstance()
        {
            try
            {
                if (gcType == null) return null;
                return GameReflection.FindInstance(gcType.Name);
            }
            catch { return null; }
        }
    }
}