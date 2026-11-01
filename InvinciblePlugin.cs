using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class InvinciblePlugin : MonoBehaviour
    {
        private Harmony harmony;

        // 1 秒扫描一次，NavMeshAgent 数量只有个位数，开销忽略不计
        private const float ScanInterval = 1f;

        private float lastScanTime;

        private Collider playerCollider;
        private readonly HashSet<Collider> ignoredColliders = new HashSet<Collider>();

        private void Awake()
        {
            try
            {
                this.harmony = new Harmony("com.universal.invincible");
                PatchHelper.Patch(this.harmony, "PlayerScript", "OnTriggerEnter", typeof(Patches).GetMethod("Trigger_Prefix"));
                PatchHelper.Patch(this.harmony, "PlayerScript", "OnTriggerStay",  typeof(Patches).GetMethod("Trigger_Prefix"));
            }
            catch { }
            SceneManager.sceneLoaded += this.OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            this.RestoreAllCollisions();
            try { this.harmony?.UnpatchSelf(); } catch { }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this.playerCollider = null;
            this.ignoredColliders.Clear();
            Patches.ClearCache();
        }

        private void Update()
        {
            try
            {
                bool on = HackMenu.ActiveFeatures.Contains(Features.GodMode);

                if (!on)
                {
                    if (this.ignoredColliders.Count > 0) this.RestoreAllCollisions();
                    return;
                }

                if (Time.unscaledTime - this.lastScanTime < ScanInterval) return;
                this.lastScanTime = Time.unscaledTime;

                if (this.playerCollider == null) this.playerCollider = this.FindPlayerCollider();
                if (this.playerCollider == null) return;

                // ★ 核心优化：只扫 NavMeshAgent，不再 FindObjectsOfType<Collider>()
                var agents = UnityEngine.Object.FindObjectsOfType<UnityEngine.AI.NavMeshAgent>();
                for (int i = 0; i < agents.Length; i++)
                {
                    var agent = agents[i];
                    if (agent == null) continue;
                    if (!agent.gameObject.activeInHierarchy) continue;

                    // agent 自身 + 子物体的 Collider
                    var childCols = agent.GetComponentsInChildren<Collider>(true);
                    for (int j = 0; j < childCols.Length; j++)
                        this.TryIgnore(childCols[j]);

                    // 父链上的 Collider（agent 常挂在子物体上，Collider 在父物体上）
                    var p = agent.transform.parent;
                    while (p != null)
                    {
                        var parentCols = p.GetComponents<Collider>();
                        for (int j = 0; j < parentCols.Length; j++)
                            this.TryIgnore(parentCols[j]);
                        p = p.parent;
                    }
                }
            }
            catch (Exception ex) { ModLog.Error("InvinciblePlugin.Update", ex); }
        }

        private void TryIgnore(Collider c)
        {
            if (c == null || c == this.playerCollider) return;
            if (this.ignoredColliders.Contains(c)) return;

            try { Physics.IgnoreCollision(this.playerCollider, c, true); } catch { }
            this.ignoredColliders.Add(c);
        }

        private Collider FindPlayerCollider()
        {
            try
            {
                var t = GameReflection.FindType("PlayerScript");
                if (t == null) return null;
                var inst = GameReflection.FindInstance(t.Name) as MonoBehaviour;
                if (inst == null) return null;

                var ccField = GameReflection.Field(t, "cc");
                if (ccField != null)
                {
                    var cc = ccField.GetValue(inst) as Collider;
                    if (cc != null) return cc;
                }

                var col = inst.GetComponentInChildren<Collider>();
                if (col != null) return col;
                return inst.GetComponent<Collider>();
            }
            catch { return null; }
        }

        private void RestoreAllCollisions()
        {
            if (this.playerCollider != null)
            {
                foreach (var c in this.ignoredColliders)
                {
                    if (c == null) continue;
                    try { Physics.IgnoreCollision(this.playerCollider, c, false); } catch { }
                }
            }
            this.ignoredColliders.Clear();
        }

        // ============================================================
        // AI 判定（只在触发器 patch 里调用，且带缓存）
        // ============================================================

        public static class Patches
        {
            private static readonly Dictionary<int, bool> aiCache = new Dictionary<int, bool>();
            private static readonly Dictionary<Type, bool> typeAiHintCache = new Dictionary<Type, bool>();
            private static readonly Dictionary<Type, bool> agentFieldCache = new Dictionary<Type, bool>();
            private const int CacheLimit = 4096;

            private static readonly HashSet<string> ExcludeTypes = new HashSet<string>
            {
                "PlayerScript", "PickupScript", "NotebookScript", "DoorScript",
                "SwingingDoorScript", "WindowScript", "ButtonCounterScript", "TapePlayerScript",
                "VendingMachineScript", "DevBookScript", "OutdoorEntranceScrpit", "EntranceScript",
                "LiftScript", "ExitTriggerScript",
            };

            private static readonly string[] ClassNameHints =
            {
                "baldi", "principal", "crafters", "playtime", "sweep", "prize", "bully",
                "boss", "enemy", "null", "eraser", "chalk", "alarm", "clock", "student", "npc",
                "chase", "follow", "hunt", "angry", "rage", "slap", "thief"
            };

            private static readonly string[] AISignatureMethods =
            {
                "Hear", "GetAngry", "Kill", "Hurt", "Chase", "Catch", "Attack",
                "SendToOffice", "Slap", "React", "Detect", "Wander", "Patrol"
            };

            public static void ClearCache()
            {
                aiCache.Clear();
                typeAiHintCache.Clear();
                agentFieldCache.Clear();
            }

            public static bool Trigger_Prefix(Collider __0)
            {
                try
                {
                    if (!HackMenu.ActiveFeatures.Contains(Features.GodMode)) return true;
                    if (__0 == null) return true;
                    return !HasAI(__0.gameObject);
                }
                catch { return true; }
            }

            public static bool HasAI(GameObject go)
            {
                if (go == null) return false;

                int id = go.GetInstanceID();
                if (aiCache.TryGetValue(id, out var hit)) return hit;

                bool result = ComputeAI(go);

                if (aiCache.Count >= CacheLimit) aiCache.Clear();
                aiCache[id] = result;
                return result;
            }

            private static bool ComputeAI(GameObject go)
            {
                try
                {
                    // 1. 明确排除
                    var mbs = go.GetComponentsInChildren<MonoBehaviour>(true);
                    for (int i = 0; i < mbs.Length; i++)
                    {
                        var mb = mbs[i];
                        if (mb == null) continue;
                        if (ExcludeTypes.Contains(mb.GetType().Name)) return false;
                    }

                    // 2. NavMeshAgent（自身 / 子物体 / 父链）
                    if (go.GetComponent<UnityEngine.AI.NavMeshAgent>() != null) return true;
                    if (go.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true) != null) return true;

                    var cur = go.transform.parent;
                    while (cur != null)
                    {
                        if (cur.GetComponent<UnityEngine.AI.NavMeshAgent>() != null) return true;
                        cur = cur.parent;
                    }

                    // 3. 父链上的 MonoBehaviour 持有 NavMeshAgent 字段
                    cur = go.transform;
                    while (cur != null)
                    {
                        var parentMbs = cur.GetComponents<MonoBehaviour>();
                        for (int i = 0; i < parentMbs.Length; i++)
                        {
                            if (parentMbs[i] == null) continue;
                            if (HasNavMeshAgentField(parentMbs[i].GetType())) return true;
                        }
                        cur = cur.parent;
                    }

                    // 4 / 5. 特征方法 / 类名关键词
                    cur = go.transform;
                    while (cur != null)
                    {
                        var parentMbs = cur.GetComponents<MonoBehaviour>();
                        for (int i = 0; i < parentMbs.Length; i++)
                        {
                            var mb = parentMbs[i];
                            if (mb == null) continue;
                            var t = mb.GetType();
                            if (ExcludeTypes.Contains(t.Name)) continue;
                            if (TypeHasAIHint(t)) return true;
                        }
                        cur = cur.parent;
                    }
                }
                catch { }
                return false;
            }

            private static bool HasNavMeshAgentField(Type t)
            {
                if (t == null) return false;
                if (agentFieldCache.TryGetValue(t, out var cached)) return cached;

                bool result = false;
                try
                {
                    var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (fields[i].FieldType == typeof(UnityEngine.AI.NavMeshAgent)) { result = true; break; }
                    }
                }
                catch { }

                agentFieldCache[t] = result;
                return result;
            }

            private static bool TypeHasAIHint(Type t)
            {
                if (t == null) return false;
                if (typeAiHintCache.TryGetValue(t, out var cached)) return cached;

                bool result = false;
                try
                {
                    string name = t.Name.ToLowerInvariant();
                    for (int i = 0; i < ClassNameHints.Length; i++)
                    {
                        if (name.Contains(ClassNameHints[i])) { result = true; break; }
                    }

                    if (!result)
                    {
                        for (int i = 0; i < AISignatureMethods.Length; i++)
                        {
                            var m = t.GetMethod(AISignatureMethods[i],
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (m != null && m.DeclaringType == t) { result = true; break; }
                        }
                    }
                }
                catch { }

                typeAiHintCache[t] = result;
                return result;
            }
        }
    }
}