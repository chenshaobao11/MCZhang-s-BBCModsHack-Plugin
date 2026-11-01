using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class ESPPlugin : MonoBehaviour
    {
        private enum ESPType { None, Character, Notebook, Item, Door, Moving }

        private class CachedSprite
        {
            public SpriteRenderer sr;
            public ESPType type;
            public Vector2 size;
            public Vector2 pivot;
        }

        private const float RefreshInterval = 0.4f;
        private const float MovingWindow = 0.35f;
        private const float MaxOtherSpriteSize = 10f;
        private const float MaxSpriteSizeHard  = 60f;
        private const float CleanupInterval = 5f;
        private const float StaleMoveAge = 10f;

        private Texture2D whiteTex;
        private GUIStyle labelStyle;
        private readonly List<CachedSprite> cached = new List<CachedSprite>();
        private readonly Dictionary<int, Vector3> lastPositions = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, float> lastMoveTimes = new Dictionary<int, float>();
        private readonly Dictionary<int, bool> uiCache = new Dictionary<int, bool>();
        private readonly Dictionary<int, ESPType> typeCache = new Dictionary<int, ESPType>();
        private readonly Dictionary<int, bool> aiCache = new Dictionary<int, bool>();
        private float lastRefreshTime;
        private float lastCleanupTime;
        private Camera cachedCam;

        private void Awake()
        {
            this.whiteTex = MakeWhite(Color.white);
            SceneManager.sceneLoaded += this.OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            if (this.whiteTex) Destroy(this.whiteTex);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this.cached.Clear();
            this.lastPositions.Clear();
            this.lastMoveTimes.Clear();
            this.uiCache.Clear();
            this.typeCache.Clear();
            this.aiCache.Clear();
            this.cachedCam = null;
        }

        private static Texture2D MakeWhite(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private bool IsUIElement(Transform t, out int id)
        {
            id = t != null ? t.gameObject.GetInstanceID() : 0;
            if (id != 0 && this.uiCache.TryGetValue(id, out var hit)) return hit;

            bool result = false;
            var cur = t;
            while (cur != null)
            {
                if (cur.GetComponent("Canvas") != null) { result = true; break; }
                cur = cur.parent;
            }
            if (id != 0) this.uiCache[id] = result;
            return result;
        }

        private bool HasAI(GameObject go, int id)
        {
            if (id != 0 && this.aiCache.TryGetValue(id, out var cachedHit)) return cachedHit;

            bool result = false;
            try
            {
                if (go == null) return false;

                if (go.GetComponent<UnityEngine.AI.NavMeshAgent>() != null) result = true;
                else if (go.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true) != null) result = true;

                if (!result)
                {
                    foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null) continue;
                        string n = mb.GetType().Name;
                        if (n == "BaldiScript" || n == "PrincipalScript" || n == "CraftersScript" ||
                            n == "PlaytimeScript" || n == "FirstPrizeScript" || n == "BullyScript" ||
                            n == "GottaSweepScript" || n == "AlarmClockScript" || n == "NullBossScript" ||
                            n == "ChalkEraserScript")
                        {
                            result = true;
                            break;
                        }
                    }
                }
            }
            catch { }

            if (id != 0) this.aiCache[id] = result;
            return result;
        }

        private ESPType GetESPType(GameObject go, int id)
        {
            if (this.typeCache.TryGetValue(id, out var cachedType)) return cachedType;

            ESPType result = ESPType.None;
            try
            {
                if (go == null) return ESPType.None;

                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    string n = mb.GetType().Name;
                    if (n == "BaldiScript" || n == "PrincipalScript" || n == "CraftersScript" ||
                        n == "PlaytimeScript" || n.Contains("Baldi")) { result = ESPType.Character; break; }
                    if (n.Contains("Notebook")) { result = ESPType.Notebook; break; }
                    if (n.Contains("Door")) { result = ESPType.Door; break; }
                    if (n.Contains("Pickup") || n.Contains("Item")) { result = ESPType.Item; break; }
                }

                if (result == ESPType.None)
                {
                    string name = go.name.ToLower();
                    if (name.Contains("baldi") || name.Contains("principal") || name.Contains("playtime")) result = ESPType.Character;
                    else if (name.Contains("notebook")) result = ESPType.Notebook;
                    else if (name.Contains("door")) result = ESPType.Door;
                    else if (name.Contains("item") || name.Contains("pickup")) result = ESPType.Item;
                }

                if (result == ESPType.None && go.GetComponent<Collider>() != null)
                {
                    float now = Time.unscaledTime;
                    var pos = go.transform.position;
                    if (this.lastPositions.TryGetValue(id, out var old) && (pos - old).sqrMagnitude > 0.00001f)
                    {
                        this.lastMoveTimes[id] = now;
                        this.lastPositions[id] = pos;
                        result = ESPType.Moving;
                    }
                    else
                    {
                        this.lastPositions[id] = pos;
                        if (this.lastMoveTimes.TryGetValue(id, out var t) && now - t < MovingWindow)
                            result = ESPType.Moving;
                    }
                }
            }
            catch { }

            if (result != ESPType.None) this.typeCache[id] = result;
            return result;
        }

        private Color GetESPColor(ESPType type)
        {
            switch (type)
            {
                case ESPType.Character: return Color.red;
                case ESPType.Notebook:  return Color.green;
                case ESPType.Item:      return Color.yellow;
                case ESPType.Door:      return new Color(0.7f, 0.3f, 1f);
                case ESPType.Moving:    return Color.cyan;
                default:                return Color.white;
            }
        }

        private void Update()
        {
            try
            {
                bool showEsp     = HackMenu.ActiveFeatures.Contains(Features.Esp);
                bool showOutline = HackMenu.ActiveFeatures.Contains(Features.Outline);
                bool showTracker = HackMenu.ActiveFeatures.Contains(Features.Tracker);

                if (!showEsp && !showOutline && !showTracker)
                {
                    if (this.cached.Count > 0) this.cached.Clear();
                    return;
                }

                float now = Time.unscaledTime;

                if (now - this.lastCleanupTime > CleanupInterval)
                {
                    this.lastCleanupTime = now;
                    this.CleanupStaleMoves(now);
                }

                if (now - this.lastRefreshTime < RefreshInterval) return;
                this.lastRefreshTime = now;

                if (this.cachedCam == null) this.cachedCam = Camera.main;
                if (this.cachedCam == null) return;

                this.cached.Clear();
                var all = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
                for (int i = 0; i < all.Length; i++)
                {
                    var sr = all[i];
                    if (sr == null || !sr.gameObject.activeInHierarchy) continue;
                    if (sr.sprite == null) continue;
                    if (this.IsUIElement(sr.transform, out _)) continue;

                    var size = sr.sprite.bounds.size;

                    if (size.x > MaxSpriteSizeHard || size.y > MaxSpriteSizeHard) continue;

                    int id = sr.gameObject.GetInstanceID();
                    var type = this.GetESPType(sr.gameObject, id);
                    if (type == ESPType.None) continue;

                    bool needAI = size.x > MaxOtherSpriteSize || size.y > MaxOtherSpriteSize;
                    if (needAI && !this.HasAI(sr.gameObject, id)) continue;

                    this.cached.Add(new CachedSprite
                    {
                        sr = sr,
                        type = type,
                        size = size,
                        pivot = sr.sprite.pivot / sr.sprite.pixelsPerUnit
                    });
                }
            }
            catch (Exception ex) { ModLog.Error("ESPPlugin.Update", ex); }
        }

        private void CleanupStaleMoves(float now)
        {
            try
            {
                List<int> expired = null;
                foreach (var kv in this.lastMoveTimes)
                {
                    if (now - kv.Value > StaleMoveAge)
                    {
                        if (expired == null) expired = new List<int>();
                        expired.Add(kv.Key);
                    }
                }
                if (expired != null)
                {
                    for (int i = 0; i < expired.Count; i++)
                    {
                        int id = expired[i];
                        this.lastMoveTimes.Remove(id);
                        this.lastPositions.Remove(id);
                        this.uiCache.Remove(id);
                        this.typeCache.Remove(id);
                        this.aiCache.Remove(id);
                    }
                }
            }
            catch { }
        }

        private void OnGUI()
        {
            try
            {
                if (Event.current.type != EventType.Repaint) return;

                bool showEsp     = HackMenu.ActiveFeatures.Contains(Features.Esp);
                bool showOutline = HackMenu.ActiveFeatures.Contains(Features.Outline);
                bool showTracker = HackMenu.ActiveFeatures.Contains(Features.Tracker);
                if (!showEsp && !showOutline && !showTracker) return;
                if (this.cached.Count == 0) return;

                if (this.cachedCam == null) this.cachedCam = Camera.main;
                if (this.cachedCam == null) return;

                if (this.labelStyle == null)
                {
                    this.labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                }

                GUI.depth = 0;
                float screenH = Screen.height;
                var topCenter = new Vector2(Screen.width / 2f, 0f);

                for (int i = 0; i < this.cached.Count; i++)
                {
                    var item = this.cached[i];
                    if (item.sr == null || item.sr.sprite == null) continue;
                    this.DrawSprite(item, this.cachedCam, topCenter, screenH, showEsp, showOutline, showTracker);
                }
            }
            catch (Exception ex) { ModLog.Error("ESPPlugin.OnGUI", ex); }
        }

        private void DrawSprite(CachedSprite item, Camera cam, Vector2 topCenter, float screenH,
                                bool showEsp, bool showOutline, bool showTracker)
        {
            try
            {
                var sr = item.sr;
                var size = item.size;
                var pivot = item.pivot;
                var tf = sr.transform;

                Vector3 s0 = cam.WorldToScreenPoint(tf.TransformPoint(new Vector3(-pivot.x, -pivot.y, 0)));
                Vector3 s1 = cam.WorldToScreenPoint(tf.TransformPoint(new Vector3(size.x - pivot.x, -pivot.y, 0)));
                Vector3 s2 = cam.WorldToScreenPoint(tf.TransformPoint(new Vector3(size.x - pivot.x, size.y - pivot.y, 0)));
                Vector3 s3 = cam.WorldToScreenPoint(tf.TransformPoint(new Vector3(-pivot.x, size.y - pivot.y, 0)));

                bool visible = s0.z > 0 || s1.z > 0 || s2.z > 0 || s3.z > 0;
                if (!visible) return;

                float x0 = Mathf.Min(Mathf.Min(s0.x, s1.x), Mathf.Min(s2.x, s3.x));
                float x1 = Mathf.Max(Mathf.Max(s0.x, s1.x), Mathf.Max(s2.x, s3.x));
                float yTop = Mathf.Min(Mathf.Min(s0.y, s1.y), Mathf.Min(s2.y, s3.y));
                float yBot = Mathf.Max(Mathf.Max(s0.y, s1.y), Mathf.Max(s2.y, s3.y));

                float y0 = screenH - yBot;
                float y1 = screenH - yTop;
                float w = x1 - x0;
                float h = y1 - y0;
                if (w < 5f || h < 5f) return;

                Color c = this.GetESPColor(item.type);

                if (showEsp || showOutline)
                {
                    GUI.color = c;
                    GUI.DrawTexture(new Rect(x0, y0, w, 3f), this.whiteTex);
                    GUI.DrawTexture(new Rect(x0, y0 + h - 3f, w, 3f), this.whiteTex);
                    GUI.DrawTexture(new Rect(x0, y0, 3f, h), this.whiteTex);
                    GUI.DrawTexture(new Rect(x0 + w - 3f, y0, 3f, h), this.whiteTex);
                    GUI.color = Color.white;

                    if (showEsp)
                    {
                        float dist = Vector3.Distance(cam.transform.position, sr.transform.position);
                        int distMeters = Mathf.RoundToInt(dist);

                        this.labelStyle.normal.textColor = c;
                        // 第一行：名字
                        GUI.Label(new Rect(x0, y0 - 48f, 300f, 24f), sr.gameObject.name, this.labelStyle);
                        // 第二行：距离
                        GUI.Label(new Rect(x0, y0 - 24f, 300f, 24f), "（" + distMeters + "m）", this.labelStyle);
                    }
                }

                if (showTracker)
                {
                    var st = new Vector2(x0 + w / 2f, y0);
                    var d = topCenter - st;
                    float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    GUI.color = c;
                    GUIUtility.RotateAroundPivot(angle, st);
                    GUI.DrawTexture(new Rect(st.x, st.y - 1.5f, d.magnitude, 3f), this.whiteTex);
                    GUIUtility.RotateAroundPivot(-angle, st);
                    GUI.color = Color.white;
                }
            }
            catch { }
        }
    }
}