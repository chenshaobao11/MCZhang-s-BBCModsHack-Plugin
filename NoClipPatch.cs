using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace UniversalHack
{
    [HarmonyPatch]
    public static class NoClipPatch
    {
        private static FieldInfo moveDirectionField;
        private static FieldInfo ccField;

        private static MethodBase TargetMethod()
        {
            try
            {
                var t = GameReflection.FindType("PlayerScript");
                if (t == null) return null;
                moveDirectionField = GameReflection.Field(t, "moveDirection");
                ccField = GameReflection.Field(t, "cc");
                return GameReflection.Method(t, "PlayerMove");
            }
            catch { return null; }
        }

        [HarmonyPrefix]
        private static void Prefix(object __instance)
        {
            try
            {
                if (!HackMenu.ActiveFeatures.Contains(Features.NoClip)) return;
                if (ccField == null) return;
                var cc = ccField.GetValue(__instance) as CharacterController;
                if (cc != null && cc.enabled) cc.enabled = false;
            }
            catch { }
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance)
        {
            try
            {
                if (!HackMenu.ActiveFeatures.Contains(Features.NoClip)) return;
                if (moveDirectionField == null) return;
                var mb = __instance as MonoBehaviour;
                if (mb == null) return;

                Vector3 move = (Vector3)moveDirectionField.GetValue(__instance);
                // 忽略 y 轴，避免穿墙时受重力影响掉下去或悬空
                move.y = 0f;
                if (move.sqrMagnitude > 0.0000001f)
                {
                    mb.transform.position += move;
                    moveDirectionField.SetValue(__instance, Vector3.zero);
                }
            }
            catch { }
        }
    }
}