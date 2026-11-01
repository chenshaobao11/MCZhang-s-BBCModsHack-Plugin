using HarmonyLib;
using UnityEngine;

namespace UniversalHack
{
    public class MouseFixPlugin : MonoBehaviour
    {
        private Harmony harmony;

        private void Awake()
        {
            try
            {
                this.harmony = new Harmony("com.universal.mousefix");
                PatchHelper.Patch(this.harmony, "InGameCursorController", "Update", typeof(Patches).GetMethod("CursorUpdate_Prefix"));
                PatchHelper.Patch(this.harmony, "MouseAppearingScript", "Update", typeof(Patches).GetMethod("MouseAppearing_Prefix"));
            }
            catch { }
        }

        private void OnDestroy()
        {
            try { this.harmony?.UnpatchSelf(); } catch { }
        }

        public static class Patches
        {
            public static bool CursorUpdate_Prefix()
            {
                try
                {
                    if (HackMenu.ShowMenu)
                    {
                        Cursor.visible = true;
                        Cursor.lockState = CursorLockMode.None;
                        return false;
                    }
                    return true;
                }
                catch { return true; }
            }

            public static bool MouseAppearing_Prefix()
            {
                try { return !HackMenu.ShowMenu; } catch { return true; }
            }
        }
    }
}