using HarmonyLib;
using UnityEngine;

namespace UniversalHack
{
    public class StaminaPlugin : MonoBehaviour
    {
        private Harmony harmony;
        private void Awake() { try { this.harmony = new Harmony("com.universal.stamina"); PatchHelper.Patch(this.harmony, "PlayerScript", "StaminaCheck", typeof(Patches).GetMethod("Prefix")); } catch { } }
        private void OnDestroy() { try { this.harmony?.UnpatchSelf(); } catch { } }
        public static class Patches 
        { 
            public static bool Prefix() 
            { 
                try { return !HackMenu.ActiveFeatures.Contains(Features.InfiniteStamina); } catch { return true; }
            } 
        }
    }
}