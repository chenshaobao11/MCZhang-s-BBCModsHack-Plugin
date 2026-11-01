using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class ItemPlugin : MonoBehaviour
    {
        private Harmony harmony;
        private static bool patched;

        private void Awake()
        {
            try
            {
                this.harmony = new Harmony("com.universal.item");
                SceneManager.sceneLoaded += this.OnSceneLoaded;
                this.TryPatch();
            }
            catch { this.harmony = null; }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            try { this.harmony?.UnpatchSelf(); } catch { }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => this.TryPatch();

        private void TryPatch()
        {
            if (patched || this.harmony == null) return;
            try
            {
                var t = GameReflection.FindType("GameControllerScript") ?? GameReflection.FindType("GameController");
                if (t == null) return;
                var m = GameReflection.Method(t, "ResetItem");
                if (m == null) return;

                var prefix = typeof(Patches).GetMethod("ResetItem_Prefix");
                if (prefix == null) return;

                this.harmony.Patch(m, prefix: new HarmonyMethod(prefix));
                patched = true;
            }
            catch { }
        }

        public static class Patches
        {
            public static bool ResetItem_Prefix()
            {
                try { return !HackMenu.ActiveFeatures.Contains(Features.InfiniteItems); }
                catch { return true; }
            }
        }
    }
}