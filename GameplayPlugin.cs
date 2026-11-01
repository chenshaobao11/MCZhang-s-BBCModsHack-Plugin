using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace UniversalHack
{
    public class GameplayPlugin : MonoBehaviour
    {
        private Harmony harmony;

        private void Awake()
        {
            try
            {
                this.harmony = new Harmony("com.universal.gameplay");
                PatchHelper.Patch(this.harmony, "PlayerScript", "Update", null, typeof(Patches).GetMethod("PlayerUpdate_Postfix"));
            }
            catch { }
        }

        private void OnDestroy() { try { this.harmony?.UnpatchSelf(); } catch { } }

        public static class Patches
        {
            private static FieldInfo ccField;

            [HarmonyPostfix]
            public static void PlayerUpdate_Postfix(object __instance)
            {
                try
                {
                    if (__instance == null) return;
                    if (!HackMenu.ActiveFeatures.Contains(Features.JumpAnywhere)) return;
                    if (!Input.GetKey(KeyCode.Space)) return;

                    if (ccField == null)
                    {
                        var t = __instance.GetType();
                        if (t == null) return;
                        ccField = GameReflection.Field(t, "cc");
                        if (ccField == null) return;
                    }

                    var cc = ccField.GetValue(__instance) as CharacterController;
                    // CharacterController.Move 在 disabled 状态会抛异常
                    if (cc != null && cc.enabled)
                        cc.Move(Vector3.up * Time.deltaTime * 6f);
                }
                catch { }
            }
        }
    }
}