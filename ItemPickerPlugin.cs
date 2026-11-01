using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class ItemPickerPlugin : MonoBehaviour
    {
        private static object gcInstance;
        private static string[] itemNames;
        private static Type gcType;

        private float lastRefresh;
        private static float lastNullRetry;
        private const float RefreshInterval = 0.5f;

        private void Awake() => SceneManager.sceneLoaded += this.OnSceneLoaded;

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            gcInstance = null;
            itemNames = null;
            gcType = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            gcInstance = null;
            itemNames = null;
            this.lastRefresh = 0f;
            lastNullRetry = 0f;
        }

        private void Update()
        {
            try
            {
                if (Time.unscaledTime - this.lastRefresh < RefreshInterval) return;
                this.lastRefresh = Time.unscaledTime;
                Refresh();
            }
            catch (Exception ex) { ModLog.Error("ItemPickerPlugin.Update", ex); }
        }

        private static void Refresh()
        {
            try
            {
                if (gcType == null)
                    gcType = GameReflection.FindType("GameControllerScript") ?? GameReflection.FindType("GameController");

                if (gcType == null) { gcInstance = null; itemNames = null; return; }

                gcInstance = GameReflection.FindInstance(gcType.Name);
                if (gcInstance == null) { itemNames = null; return; }

                var f = GameReflection.Field(gcType, "itemNames");
                if (f == null) { itemNames = null; return; }

                itemNames = f.GetValue(gcInstance) as string[];
            }
            catch (Exception ex) { ModLog.Error("ItemPickerPlugin.Refresh", ex); }
        }

        public static string[] GetItemNames()
        {
            if (itemNames != null) return itemNames;
            if (Time.unscaledTime - lastNullRetry < RefreshInterval) return null;
            lastNullRetry = Time.unscaledTime;
            Refresh();
            return itemNames;
        }

        public static void TriggerCollectItem(int itemId)
        {
            try
            {
                if (gcInstance == null) Refresh();
                if (gcInstance == null || gcType == null) return;

                var m = GameReflection.Method(gcType, "CollectItem");
                if (m != null) m.Invoke(gcInstance, new object[] { itemId });
            }
            catch (Exception ex) { ModLog.Error("ItemPickerPlugin.TriggerCollectItem", ex); }
        }
    }
}