using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace UniversalHack
{
    public static class Features
    {
        public const string NoBaldi          = "无巴迪";
        public const string NoPrincipal      = "无校长";
        public const string NoCrafters       = "无袜子";
        public const string NoPlaytime       = "无欢乐时间";
        public const string NoSweep          = "无扫把";
        public const string NoFirstPrize     = "无第一名";
        public const string NoBully          = "无校霸";
        public const string CollectOneNotebook = "收集一个书";
        public const string TriggerExitOnce    = "触发一次出口";
        public const string AutoAnswer         = "自动答题";

        public const string NoClip           = "穿墙";
        public const string Speed            = "移速";
        public const string JumpAnywhere     = "任意跳跃";

        public const string GodMode          = "无敌";
        public const string InfiniteStamina  = "无限体力";
        public const string InfiniteItems    = "无限道具";

        public const string Esp              = "绘制";
        public const string Magnifier        = "放大镜";
        public const string Fov              = "增大视野";
        public const string Tracker          = "追踪器";
        public const string Outline          = "控件描边";
        public const string RedLight         = "红温模式";
        public const string TextureRotate    = "贴图旋转";
        public const string Disco            = "Disco 模式";
        public const string Flashlight       = "手电筒";
        public const string ShowFps          = "FPS 显示";
        public const string FreeCamera       = "3D 自由视角";

        public const string MusicUIScale     = "音乐UI缩放";
        public const string MusicWallShake   = "墙壁随音乐抖动";
        public const string ColorMusic       = "颜色随音乐";
        public const string VolumeWarning    = "音量警告";

        public const string MirrorMode       = "镜像模式";
        public const string Fliparoo         = "翻转模式";
        public const string Deception        = "欺骗模式";
        public const string WallShake        = "墙壁抖动";
    }

    public static class ModLog
    {
        private class Entry
        {
            public string message;
            public int    count;
            public float  startTime;
        }

        // T0 → T1 纯红 1s  /  T1 → T3 渐变 2s  /  T3 消失  /  T4 下一条
        private const float SolidDuration       = 1f;
        private const float FadeDuration        = 2f;
        private const float ShowDuration        = SolidDuration + FadeDuration;   // 3s
        private const float GapDuration         = 1f;

        private const int   MaxQueue            = 20;
        private const int   MaxCount            = 99;
        private const float SaturatedResetTime  = 10f;
        private const float SaturatedCleanupAge = 60f;

        private const int   FontSize   = 16;
        private const float LineHeight = 24f;
        private const float Padding    = 8f;

        private static readonly Color BgColor   = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color TextColor = new Color(1f, 0.2f, 0.2f, 1f);

        private static readonly List<Entry> queue = new List<Entry>();
        private static readonly Dictionary<string, float> saturatedMessages = new Dictionary<string, float>();

        private static Entry currentEntry;
        private static float currentUntil;
        private static float nextAllowedAt;

        private static GUIStyle  style;
        private static Texture2D bg;

        public static void Error(string context, Exception ex)
        {
            try
            {
                string log = ex != null
                    ? $"[UniversalHack] {context}: {ex.Message}\n{ex.StackTrace}"
                    : $"[UniversalHack] {context}";
                Debug.LogError(log);
            }
            catch { }
            try { Enqueue(context); } catch { }
        }

        public static void Warning(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message)) return;
                Debug.LogWarning("[UniversalHack] " + message);
            }
            catch { }
            try { Enqueue(message); } catch { }
        }

        public static void ReportError(string className, string methodName)
        {
            try
            {
                if (string.IsNullOrEmpty(className))  className  = "Unknown";
                if (string.IsNullOrEmpty(methodName)) methodName = "Unknown";
                Enqueue($"{className}.{methodName}");
            }
            catch { }
        }

        public static void ClearScreen()
        {
            try
            {
                queue.Clear();
                saturatedMessages.Clear();
                currentEntry  = null;
                currentUntil  = 0f;
                nextAllowedAt = 0f;
            }
            catch { }
        }

        public static void DrawScreenMessages()
        {
            try
            {
                float now = Time.unscaledTime;

                if (saturatedMessages.Count > 0)
                {
                    List<string> expired = null;
                    foreach (var kv in saturatedMessages)
                    {
                        if (now - kv.Value > SaturatedCleanupAge)
                        {
                            if (expired == null) expired = new List<string>();
                            expired.Add(kv.Key);
                        }
                    }
                    if (expired != null)
                        for (int i = 0; i < expired.Count; i++) saturatedMessages.Remove(expired[i]);
                }

                if (currentEntry != null && now >= currentUntil)
                {
                    currentEntry  = null;
                    nextAllowedAt = now + GapDuration;
                }

                if (currentEntry == null && queue.Count > 0 && now >= nextAllowedAt)
                {
                    currentEntry = queue[0];
                    queue.RemoveAt(0);
                    currentEntry.startTime = now;
                    currentUntil = now + ShowDuration;
                }

                if (currentEntry == null) return;

                EnsureResources();
                if (bg == null || style == null) return;

                float elapsed = now - currentEntry.startTime;
                float alpha;
                if (elapsed < SolidDuration)          alpha = 1f;
                else if (elapsed < ShowDuration)      alpha = Mathf.Clamp01(1f - (elapsed - SolidDuration) / FadeDuration);
                else                                  alpha = 0f;
                if (alpha <= 0.001f) return;

                string text;
                if (currentEntry.count >= MaxCount)   text = currentEntry.message + " (x99+)";
                else if (currentEntry.count > 1)      text = currentEntry.message + " (x" + currentEntry.count + ")";
                else                                  text = currentEntry.message;

                float x = Padding;
                float y = Screen.height - Padding - LineHeight;
                float w = Mathf.Min(Screen.width * 0.7f, 1000f);
                if (y < 0f) return;

                GUI.color = new Color(BgColor.r, BgColor.g, BgColor.b, BgColor.a * alpha);
                GUI.DrawTexture(new Rect(x, y, w, LineHeight), bg);

                GUI.color = new Color(TextColor.r, TextColor.g, TextColor.b, TextColor.a * alpha);
                GUI.Label(new Rect(x + 6f, y + 2f, w - 12f, LineHeight - 4f), text, style);
                GUI.color = Color.white;
            }
            catch { }
        }

        private static void Enqueue(string context)
        {
            try
            {
                if (string.IsNullOrEmpty(context)) context = "Unknown.Unknown";

                string message = context + " 出现错误，可能无法正常工作";
                float now = Time.unscaledTime;

                bool wasSaturated = false;
                if (saturatedMessages.TryGetValue(message, out var satAt))
                {
                    if (now - satAt < SaturatedResetTime) return;
                    saturatedMessages.Remove(message);
                    wasSaturated = true;
                }

                if (wasSaturated)
                {
                    if (currentEntry != null && currentEntry.message == message)
                    {
                        currentEntry  = null;
                        nextAllowedAt = now + GapDuration;
                    }
                    for (int i = queue.Count - 1; i >= 0; i--)
                        if (queue[i].message == message) queue.RemoveAt(i);
                }

                if (currentEntry != null && currentEntry.message == message)
                {
                    currentEntry.count++;
                    if (currentEntry.count >= MaxCount)
                    {
                        currentEntry.count = MaxCount;
                        saturatedMessages[message] = now;
                    }
                    currentEntry.startTime = now;
                    currentUntil = now + ShowDuration;
                    return;
                }

                for (int i = 0; i < queue.Count; i++)
                {
                    if (queue[i].message != message) continue;
                    queue[i].count++;
                    if (queue[i].count >= MaxCount)
                    {
                        queue[i].count = MaxCount;
                        saturatedMessages[message] = now;
                    }
                    return;
                }

                while (queue.Count >= MaxQueue) queue.RemoveAt(0);
                queue.Add(new Entry { message = message, count = 1, startTime = 0f });
            }
            catch { }
        }

        private static void EnsureResources()
        {
            try
            {
                if (bg == null)
                {
                    bg = new Texture2D(1, 1);
                    bg.SetPixel(0, 0, Color.white);
                    bg.Apply();
                }
                if (style == null)
                {
                    style = new GUIStyle(GUI.skin.label)
                    {
                        fontSize   = FontSize,
                        alignment  = TextAnchor.MiddleLeft,
                        wordWrap   = false,
                        clipping   = TextClipping.Clip,
                    };
                }
            }
            catch { }
        }
    }

    public static class GameReflection
    {
        private static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();
        private static readonly HashSet<string> missingTypes = new HashSet<string>();

        public static Type FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            if (typeCache.TryGetValue(typeName, out var cached)) return cached;
            if (missingTypes.Contains(typeName)) return null;

            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == typeName)
                            {
                                typeCache[typeName] = t;
                                return t;
                            }
                        }
                    }
                    catch { }
                }
                missingTypes.Add(typeName);
            }
            catch (Exception ex) { ModLog.Error("GameReflection.FindType", ex); }
            return null;
        }

        public static UnityEngine.Object FindInstance(string typeName)
        {
            try
            {
                var t = FindType(typeName);
                if (t == null) return null;
                return UnityEngine.Object.FindObjectOfType(t);
            }
            catch (Exception ex) { ModLog.Error("GameReflection.FindInstance", ex); return null; }
        }

        public static FieldInfo Field(Type t, string name)
        {
            try
            {
                if (t == null || string.IsNullOrEmpty(name)) return null;
                return t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            catch (Exception ex) { ModLog.Error("GameReflection.Field", ex); return null; }
        }

        public static MethodInfo Method(Type t, string name)
        {
            try
            {
                if (t == null || string.IsNullOrEmpty(name)) return null;
                return t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            catch (AmbiguousMatchException)
            {
                try
                {
                    foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        if (m.Name == name && m.GetParameters().Length == 0) return m;
                }
                catch { }
                return null;
            }
            catch (Exception ex) { ModLog.Error("GameReflection.Method", ex); return null; }
        }
    }

    public static class InputHelper
    {
        public static bool GetKeyDown(KeyCode k) => Input.GetKeyDown(k);
        public static bool GetKey(KeyCode k) => Input.GetKey(k);
        public static float GetAxis(string a) => Input.GetAxis(a);
    }

    public static class PatchHelper
    {
        public static bool Patch(Harmony harmony, string typeName, string methodName,
                                 MethodInfo prefix = null, MethodInfo postfix = null)
        {
            try
            {
                if (harmony == null) return false;
                if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName)) return false;
                if (prefix == null && postfix == null) return false;

                var t = GameReflection.FindType(typeName);
                if (t == null)
                {
                    ModLog.ReportError("PatchHelper", $"{typeName}.{methodName}");
                    return false;
                }
                var m = GameReflection.Method(t, methodName);
                if (m == null)
                {
                    ModLog.ReportError("PatchHelper", $"{typeName}.{methodName}");
                    return false;
                }

                harmony.Patch(
                    original: m,
                    prefix:   prefix  != null ? new HarmonyMethod(prefix)  : null,
                    postfix:  postfix != null ? new HarmonyMethod(postfix) : null);
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("PatchHelper.Patch", ex);
                return false;
            }
        }
    }
}