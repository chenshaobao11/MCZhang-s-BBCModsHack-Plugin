using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalHack
{
    public class HackMenu : MonoBehaviour
    {
        public static bool ShowMenu { get; private set; } = true;
        public static readonly HashSet<string> ActiveFeatures = new HashSet<string>();
        public static readonly object Lock = new object();

        private readonly string[][] MENUS = new string[][]
        {
            new []{ "事件类", "无巴迪", "无校长", "无袜子", "无欢乐时间", "无扫把", "无第一名", "无校霸",
                    "收集一个书", "触发一次出口", "自动答题" },
            new []{ "移动类", "穿墙", "移速", "任意跳跃" },
            new []{ "玩家类", "无敌", "无限体力", "无限道具" },
            new []{ "视觉类", "绘制", "放大镜", "增大视野", "追踪器", "控件描边",
                    "红温模式", "贴图旋转", "Disco 模式", "手电筒", "FPS 显示", "3D 自由视角" },
            new []{ "音乐类", "音乐UI缩放", "墙壁随音乐抖动", "颜色随音乐", "音量警告" },
            new []{ "玩笑类", "镜像模式", "翻转模式", "欺骗模式", "墙壁抖动" },
            new []{ "物品类" },
        };

        private readonly string[][] MENUS_EN = new string[][]
        {
            new []{ "Events", "No Baldi", "No Principal", "No Crafters", "No Playtime", "No Sweep", "No First Prize", "No Bully",
                    "Collect One Notebook", "Trigger Exit Once", "Auto Answer" },
            new []{ "Movement", "No Clip", "Speed", "Jump Anywhere" },
            new []{ "Player", "God Mode", "Infinite Stamina", "Infinite Items" },
            new []{ "Visual", "ESP", "Magnifier", "FOV", "Tracker", "Outline",
                    "Red Light", "Texture Rotate", "Disco", "Flashlight", "Show FPS", "3D Free Camera" },
            new []{ "Music", "Music UI Scale", "Music Wall Shake", "Music Color", "LOUD WARNING" },
            new []{ "Fun", "Mirror Mode", "Fliparoo", "Deception", "Wall Shake" },
            new []{ "Items" },
        };

        private readonly Dictionary<string, string> enMap = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> featureStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, Rect> menuRects = new Dictionary<string, Rect>();
        private readonly Dictionary<string, bool> menuExpanded = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> isDragging = new Dictionary<string, bool>();
        private readonly Dictionary<string, Vector2> dragOffset = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, Rect> initialMenuRects = new Dictionary<string, Rect>();
        private readonly Dictionary<string, bool> initialMenuExpanded = new Dictionary<string, bool>();

        private float hue;
        private bool showMenu = true;
        private bool hideOverlayOnly;
        private bool englishMode;
        private bool gradientEffect = true;
        private bool hideCoverOnly;
        private float scalePercent = 50f;
        private bool showScaleDialog;
        private string scaleInput = "50";
        private float lastToggleTime = -1f;
        private float lastStyleScale = -1f;
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;

        private bool initialEnglishMode;
        private bool initialGradientEffect = true;
        private bool initialHideCoverOnly;
        private float initialScalePercent = 50f;

        private Texture2D whiteTex, bgTex, titleBgTex, grayTex, checkTex;

        private GUIStyle cachedTitleStyle, cachedButtonStyle, cachedButtonActiveStyle,
                          cachedControlStyle, cachedControlLabelStyle,
                          cachedDialogStyle, cachedDialogButtonStyle;

        private const string ItemMenuKey = "物品类";

        private float GetScale() => 0.75f * (0.5f + this.scalePercent / 100f);

        private void Awake()
        {
            try
            {
                this.whiteTex   = MakeTex(Color.white);
                this.bgTex      = MakeTex(new Color(1f, 1f, 1f, 0.33f));
                this.titleBgTex = MakeTex(Color.white);
                this.grayTex    = MakeTex(new Color(0f, 0f, 0f, 0.6f));
                this.checkTex   = MakeTex(new Color(0f, 0.8f, 0.2f, 1f));

                for (int i = 0; i < this.MENUS.Length; i++)
                {
                    var cn = this.MENUS[i];
                    var en = this.MENUS_EN[i];
                    int n = Mathf.Min(cn.Length, en.Length);
                    for (int j = 0; j < n; j++) this.enMap[cn[j]] = en[j];
                }

                this.RecalculateMenuPositions();
                this.lastScreenWidth  = Screen.width;
                this.lastScreenHeight = Screen.height;

                foreach (var menu in this.MENUS)
                    for (int i = 1; i < menu.Length; i++)
                        this.featureStates[menu[i]] = false;

                this.initialEnglishMode    = this.englishMode;
                this.initialGradientEffect = this.gradientEffect;
                this.initialHideCoverOnly  = this.hideCoverOnly;
                this.initialScalePercent   = this.scalePercent;

                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            catch (Exception ex) { ModLog.Error("HackMenu.Awake", ex); }
        }

        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.Tab)) this.TryToggle();
                if (this.showScaleDialog && Input.GetKeyDown(KeyCode.Escape)) this.showScaleDialog = false;

                if (Screen.width != this.lastScreenWidth || Screen.height != this.lastScreenHeight)
                {
                    this.lastScreenWidth  = Screen.width;
                    this.lastScreenHeight = Screen.height;
                    this.RecalculateMenuPositions();
                    foreach (var kv in this.menuRects)
                        this.menuRects[kv.Key] = this.ClampRectToScreen(kv.Value);
                    this.lastStyleScale = -1f;
                }
            }
            catch (Exception ex) { ModLog.Error("HackMenu.Update", ex); }
        }

        private void OnGUI()
        {
            try
            {
                GUI.depth = -100;

                var cur = Event.current;
                if (cur.type == EventType.KeyDown && cur.keyCode == KeyCode.Tab && this.TryToggle())
                    cur.Use();

                if (cur.type == EventType.Repaint)
                {
                    if (this.gradientEffect) this.hue = Time.realtimeSinceStartup * 90f % 360f;
                    if (this.showMenu)
                    {
                        if (!this.hideOverlayOnly)
                        {
                            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), this.grayTex);
                            this.DrawBottomRightControls();
                        }
                        this.DrawMenus();
                    }
                    ModLog.DrawScreenMessages();
                }
                else if (this.showMenu &&
                        (cur.type == EventType.MouseDown ||
                         cur.type == EventType.MouseUp ||
                         cur.type == EventType.MouseDrag))
                {
                    this.HandleInput(cur);
                    if (!this.hideOverlayOnly && cur.type != EventType.Used)
                        this.HandleBottomRightInput(cur);
                }

                if (this.showScaleDialog) this.DrawScaleDialog();
            }
            catch (Exception ex) { ModLog.Error("HackMenu.OnGUI", ex); }
        }

        private void OnDestroy()
        {
            if (this.whiteTex)   Destroy(this.whiteTex);
            if (this.bgTex)      Destroy(this.bgTex);
            if (this.titleBgTex) Destroy(this.titleBgTex);
            if (this.grayTex)    Destroy(this.grayTex);
            if (this.checkTex)   Destroy(this.checkTex);
        }

        private bool TryToggle()
        {
            float t = Time.unscaledTime;
            if (t - this.lastToggleTime < 0.15f) return false;
            this.lastToggleTime = t;
            this.ToggleMenu();
            return true;
        }

        private void ToggleMenu()
        {
            if (this.showMenu)
            {
                if (this.hideCoverOnly) this.hideOverlayOnly = !this.hideOverlayOnly;
                else { this.showMenu = false; HackMenu.ShowMenu = false; }
            }
            else
            {
                this.showMenu = true; HackMenu.ShowMenu = true; this.hideOverlayOnly = false;
            }
            Cursor.lockState = this.showMenu ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = this.showMenu;
        }

        private void ResetUI()
        {
            try
            {
                this.scalePercent = this.initialScalePercent;
                this.RecalculateMenuPositions();
                foreach (var k in this.initialMenuRects.Keys)
                {
                    this.menuRects[k]    = this.initialMenuRects[k];
                    this.menuExpanded[k] = this.initialMenuExpanded[k];
                }
                this.englishMode     = this.initialEnglishMode;
                this.gradientEffect  = this.initialGradientEffect;
                this.hideCoverOnly   = this.initialHideCoverOnly;
                this.hideOverlayOnly = false;
                this.showMenu        = true;
                HackMenu.ShowMenu    = true;

                lock (Lock)
                {
                    foreach (var menu in this.MENUS)
                        for (int i = 1; i < menu.Length; i++)
                            ActiveFeatures.Remove(menu[i]);
                }
                foreach (var menu in this.MENUS)
                    for (int i = 1; i < menu.Length; i++)
                        this.featureStates[menu[i]] = false;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
                this.lastStyleScale = -1f;
            }
            catch (Exception ex) { ModLog.Error("HackMenu.ResetUI", ex); }
        }

        private void RecalculateMenuPositions()
        {
            try
            {
                float s = this.GetScale();
                float w = 300f * s, h = 72f * s;
                float pad  = 40f * s;
                float gapX = 20f * s;
                float gapY = 20f * s;

                float x = pad;
                float y = pad;

                foreach (var menu in this.MENUS)
                {
                    string key = menu[0];

                    if (x + w > Screen.width - pad && x > pad)
                    {
                        x = pad;
                        y += h + gapY;
                    }

                    var r = this.ClampRectToScreen(new Rect(x, y, w, h));
                    this.menuRects[key]    = r;
                    this.menuExpanded[key] = false;
                    this.isDragging[key]   = false;

                    x += w + gapX;

                    if (!this.initialMenuRects.ContainsKey(key))
                    {
                        this.initialMenuRects[key]    = r;
                        this.initialMenuExpanded[key] = false;
                    }
                }
            }
            catch (Exception ex) { ModLog.Error("HackMenu.RecalculateMenuPositions", ex); }
        }

        private Rect ClampRectToScreen(Rect rect)
        {
            float s = this.GetScale();
            float w = 300f * s, h = 72f * s;
            float maxX = Mathf.Max(0f, Screen.width  - w);
            float maxY = Mathf.Max(0f, Screen.height - h);
            rect.x = Mathf.Clamp(rect.x, 0f, maxX);
            rect.y = Mathf.Clamp(rect.y, 0f, maxY);
            rect.width  = w;
            rect.height = h;
            return rect;
        }

        private void GetBottomRightRects(out Rect y0, out Rect y1, out Rect y2, out Rect y3, out Rect y4)
        {
            float s = this.GetScale();
            float w = 280f * s, h = 32f * s, gap = 4f * s, pad = 20f * s;

            float maxW = Screen.width - 2f * pad;
            if (maxW > 40f && w > maxW) w = maxW;

            float totalH = 5f * h + 4f * gap;
            float maxH   = Screen.height - 2f * pad;
            if (maxH > 40f && totalH > maxH)
            {
                float k = maxH / totalH;
                h   *= k;
                gap *= k;
            }

            float x   = Screen.width - pad - w;
            if (x < pad) x = pad;
            float top = Screen.height - pad - h;
            if (top < pad) top = pad;

            y0 = new Rect(x, top, w, h);
            y1 = new Rect(x, top - (h + gap), w, h);
            y2 = new Rect(x, top - 2f * (h + gap), w, h);
            y3 = new Rect(x, top - 3f * (h + gap), w, h);
            y4 = new Rect(x, top - 4f * (h + gap), w, h);
        }

        private void InitStyles()
        {
            float s = this.GetScale();
            if (Mathf.Approximately(this.lastStyleScale, s) && this.cachedTitleStyle != null) return;
            this.lastStyleScale = s;

            this.cachedTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = (int)(34f * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            this.cachedTitleStyle.normal.textColor = new Color(0f, 0.204f, 1f);

            this.cachedButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = (int)(30f * s),
                alignment = TextAnchor.MiddleCenter,
                border    = new RectOffset(0, 0, 0, 0),
                padding   = new RectOffset(0, 0, 0, 0),
                margin    = new RectOffset(0, 0, 0, 0),
            };
            this.cachedButtonStyle.normal.background = null;
            this.cachedButtonStyle.normal.textColor  = new Color(0f, 0.204f, 1f);
            this.cachedButtonStyle.hover.background  = null;
            this.cachedButtonStyle.hover.textColor   = new Color(0f, 0.204f, 1f);
            this.cachedButtonStyle.active.background = null;
            this.cachedButtonStyle.active.textColor  = new Color(0f, 0.204f, 1f);

            this.cachedButtonActiveStyle = new GUIStyle(this.cachedButtonStyle);
            this.cachedButtonActiveStyle.normal.textColor = Color.white;
            this.cachedButtonActiveStyle.hover.textColor  = Color.white;
            this.cachedButtonActiveStyle.active.textColor = Color.white;

            this.cachedControlStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = (int)(22f * s),
                alignment = TextAnchor.MiddleCenter,
            };
            this.cachedControlStyle.normal.textColor = Color.white;

            this.cachedControlLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = (int)(22f * s),
                alignment = TextAnchor.MiddleLeft,
            };
            this.cachedControlLabelStyle.normal.textColor = Color.white;

            this.cachedDialogStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = (int)(28f * s),
                alignment = TextAnchor.MiddleCenter,
            };
            this.cachedDialogStyle.normal.textColor = Color.white;

            this.cachedDialogButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = (int)(26f * s),
                alignment = TextAnchor.MiddleCenter,
            };
            this.cachedDialogButtonStyle.normal.textColor = Color.white;
            this.cachedDialogButtonStyle.hover.textColor  = Color.white;
            this.cachedDialogButtonStyle.active.textColor = Color.white;
        }

        private string GetDisplayText(string cnText)
        {
            if (!this.englishMode) return cnText;
            string en;
            return this.enMap.TryGetValue(cnText, out en) ? en : cnText;
        }

        private bool IsItemMenu(string key) => key == ItemMenuKey;

        private int GetItemCount()
        {
            var names = ItemPickerPlugin.GetItemNames();
            if (names == null || names.Length <= 1) return 1;
            return names.Length - 1;
        }

        private void HandleInput(Event e)
        {
            try
            {
                this.InitStyles();
                float s = this.GetScale();
                float w = 300f * s, h = 72f * s, itemH = 64f * s;

                for (int mi = this.MENUS.Length - 1; mi >= 0; mi--)
                {
                    if (e.type == EventType.Used) break;

                    var menu      = this.MENUS[mi];
                    string key    = menu[0];
                    var rect      = this.menuRects[key];
                    bool expanded = this.menuExpanded[key];

                    var titleRect = new Rect(rect.x, rect.y, w, h);
                    if (e.type == EventType.MouseDown && e.button == 0 && titleRect.Contains(e.mousePosition))
                    {
                        this.isDragging[key] = true;
                        this.dragOffset[key] = new Vector2(e.mousePosition.x - rect.x, e.mousePosition.y - rect.y);
                        e.Use();
                    }
                    if (this.isDragging[key] && e.type == EventType.MouseDrag)
                    {
                        rect.x = e.mousePosition.x - this.dragOffset[key].x;
                        rect.y = e.mousePosition.y - this.dragOffset[key].y;
                        rect = this.ClampRectToScreen(rect);
                        this.menuRects[key] = rect;
                        e.Use();
                    }
                    if (e.type == EventType.MouseUp && e.button == 0 && this.isDragging[key])
                    {
                        var d = new Vector2(
                            e.mousePosition.x - (rect.x + this.dragOffset[key].x),
                            e.mousePosition.y - (rect.y + this.dragOffset[key].y));
                        if (d.magnitude < 6f) this.menuExpanded[key] = !expanded;
                        this.isDragging[key] = false;
                        e.Use();
                    }

                    if (!expanded) continue;

                    if (this.IsItemMenu(key))
                    {
                        var names = ItemPickerPlugin.GetItemNames();
                        if (names != null && names.Length > 1)
                        {
                            for (int j = 1; j < names.Length; j++)
                            {
                                var r = new Rect(rect.x, rect.y + h + (j - 1) * itemH, w, itemH);
                                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                                {
                                    ItemPickerPlugin.TriggerCollectItem(j);
                                    e.Use();
                                    break;
                                }
                            }
                        }
                        continue;
                    }

                    for (int j = 1; j < menu.Length; j++)
                    {
                        string text = menu[j];
                        var r = new Rect(rect.x, rect.y + h + (j - 1) * itemH, w, itemH);
                        if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                        {
                            bool on = !this.featureStates[text];
                            this.featureStates[text] = on;
                            lock (Lock)
                            {
                                if (on) ActiveFeatures.Add(text);
                                else    ActiveFeatures.Remove(text);
                            }
                            e.Use();
                            break;
                        }
                    }
                }
            }
            catch (Exception ex) { ModLog.Error("HackMenu.HandleInput", ex); }
        }

        private void DrawMenus()
        {
            try
            {
                this.InitStyles();
                float s = this.GetScale();
                float w = 300f * s, h = 72f * s, itemH = 64f * s;

                int activeCount = 0, totalActive = 0;
                foreach (var m in this.MENUS)
                    if (this.menuExpanded[m[0]]) totalActive += m.Length - 1;

                foreach (var menu in this.MENUS)
                {
                    string title   = menu[0];
                    var rect       = this.menuRects[title];
                    bool expanded  = this.menuExpanded[title];

                    float bgH;
                    if (this.IsItemMenu(title))
                        bgH = h + (expanded ? this.GetItemCount() * itemH : 0f);
                    else
                        bgH = h + (expanded ? (menu.Length - 1) * itemH : 0f);

                    GUI.DrawTexture(new Rect(rect.x, rect.y, w, bgH), this.bgTex);
                    var titleRect = new Rect(rect.x, rect.y, w, h);
                    GUI.DrawTexture(titleRect, this.titleBgTex);
                    GUI.Label(titleRect, this.GetDisplayText(title), this.cachedTitleStyle);

                    if (!expanded) continue;

                    if (this.IsItemMenu(title))
                    {
                        this.DrawItemMenu(rect, w, h, itemH);
                        continue;
                    }

                    for (int j = 1; j < menu.Length; j++)
                    {
                        string text = menu[j];
                        bool active;
                        this.featureStates.TryGetValue(text, out active);
                        var r = new Rect(rect.x, rect.y + h + (j - 1) * itemH, w, itemH);

                        if (active)
                        {
                            float t = totalActive > 0 ? (float)activeCount / totalActive : 0f;
                            GUI.color = this.gradientEffect
                                ? this.GetRainbowColor(this.hue - t * 222f)
                                : Color.blue;
                            GUI.DrawTexture(r, this.whiteTex);
                            GUI.color = Color.white;
                            GUI.Label(r, this.GetDisplayText(text), this.cachedButtonActiveStyle);
                        }
                        else
                        {
                            GUI.color = Color.white;
                            GUI.Label(r, this.GetDisplayText(text), this.cachedButtonStyle);
                        }
                        activeCount++;
                    }
                }
                GUI.color = Color.white;
            }
            catch (Exception ex) { ModLog.Error("HackMenu.DrawMenus", ex); }
        }

        private void DrawItemMenu(Rect rect, float w, float h, float itemH)
        {
            var names = ItemPickerPlugin.GetItemNames();

            if (names == null || names.Length <= 1)
            {
                GUI.color = Color.white;
                GUI.Label(
                    new Rect(rect.x, rect.y + h, w, itemH),
                    this.englishMode ? "  (not in game)" : "  （未在游戏中）",
                    this.cachedButtonStyle);
                return;
            }

            for (int j = 1; j < names.Length; j++)
            {
                var r = new Rect(rect.x, rect.y + h + (j - 1) * itemH, w, itemH);
                GUI.color = Color.white;
                GUI.Label(r, "  " + names[j], this.cachedButtonStyle);
            }
        }

        private void DrawBottomRightControls()
        {
            try
            {
                this.InitStyles();
                float s   = this.GetScale();
                float box = 24f * s;
                float lblOff = box + 10f * s;

                Rect y0, y1, y2, y3, y4;
                this.GetBottomRightRects(out y0, out y1, out y2, out y3, out y4);

                float lblW = y0.width - box - 30f * s;

                this.DrawCheckboxAt(y0, box, lblOff, lblW, "English Mode",    this.englishMode);
                this.DrawCheckboxAt(y1, box, lblOff, lblW, "Hide Cover Only", this.hideCoverOnly);
                this.DrawCheckboxAt(y2, box, lblOff, lblW, "Gradient Effect", this.gradientEffect);

                GUI.DrawTexture(y3, this.bgTex);
                GUI.Label(y3, "Scale: " + Mathf.RoundToInt(this.scalePercent) + "%", this.cachedControlStyle);

                GUI.DrawTexture(y4, this.bgTex);
                GUI.Label(y4, "Reset", this.cachedControlStyle);
            }
            catch (Exception ex) { ModLog.Error("HackMenu.DrawBottomRightControls", ex); }
        }

        private void DrawCheckboxAt(Rect rect, float box, float lblOff, float lblW, string label, bool on)
        {
            float s = this.GetScale();
            GUI.DrawTexture(rect, this.bgTex);

            var cb = new Rect(rect.x + 12f * s, rect.y + (rect.height - box) * 0.5f, box, box);
            GUI.DrawTexture(cb, this.whiteTex);
            if (on)
                GUI.DrawTexture(new Rect(cb.x + 4f * s, cb.y + 4f * s, box - 8f * s, box - 8f * s), this.checkTex);

            GUI.Label(new Rect(rect.x + lblOff, rect.y, lblW, rect.height), label, this.cachedControlLabelStyle);
        }

        private void HandleBottomRightInput(Event e)
        {
            try
            {
                if (e.type != EventType.MouseDown || e.button != 0) return;

                Rect y0, y1, y2, y3, y4;
                this.GetBottomRightRects(out y0, out y1, out y2, out y3, out y4);

                if (y4.Contains(e.mousePosition)) { this.ResetUI(); e.Use(); return; }
                if (y3.Contains(e.mousePosition))
                {
                    this.showScaleDialog = true;
                    this.scaleInput = Mathf.RoundToInt(this.scalePercent).ToString();
                    e.Use(); return;
                }
                if (y2.Contains(e.mousePosition)) { this.gradientEffect = !this.gradientEffect; e.Use(); return; }
                if (y1.Contains(e.mousePosition)) { this.hideCoverOnly  = !this.hideCoverOnly;  e.Use(); return; }
                if (y0.Contains(e.mousePosition)) { this.englishMode     = !this.englishMode;     e.Use(); }
            }
            catch (Exception ex) { ModLog.Error("HackMenu.HandleBottomRightInput", ex); }
        }

        private void DrawScaleDialog()
        {
            try
            {
                this.InitStyles();
                float s = this.GetScale();
                float w = 380f * s, h = 170f * s;
                float x = (Screen.width  - w) / 2f;
                float y = (Screen.height - h) / 2f;

                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), this.grayTex);

                var savedColor = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(x, y, w, h), this.whiteTex);

                GUI.color = new Color(0.75f, 0.75f, 0.75f, 1f);
                GUI.DrawTexture(new Rect(x,          y,          w, 1f), this.whiteTex);
                GUI.DrawTexture(new Rect(x,          y + h - 1f, w, 1f), this.whiteTex);
                GUI.DrawTexture(new Rect(x,          y,          1f, h), this.whiteTex);
                GUI.DrawTexture(new Rect(x + w - 1f, y,          1f, h), this.whiteTex);

                GUI.color = new Color(0.12f, 0.12f, 0.12f, 1f);
                GUI.DrawTexture(new Rect(x, y, w, 36f * s), this.whiteTex);
                GUI.color = savedColor;

                var headerStyle = new GUIStyle(this.cachedDialogStyle)
                {
                    fontSize  = (int)(20f * s),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                };
                headerStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(x + 12f * s, y, w - 60f * s, 36f * s), "Scale Setting", headerStyle);

                float rowY = y + 50f * s;
                var lblStyle = new GUIStyle(this.cachedDialogStyle)
                {
                    fontSize  = (int)(16f * s),
                    alignment = TextAnchor.MiddleLeft,
                };
                lblStyle.normal.textColor = new Color(0.05f, 0.05f, 0.05f);
                GUI.Label(new Rect(x + 10f * s, rowY, 100f * s, 32f * s), "Scale:", lblStyle);

                float inW = 280f * s, inX = x + 60f * s, inH = 32f * s;
                GUI.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                GUI.DrawTexture(new Rect(inX, rowY, inW + 2f * s, inH + 2f * s), this.whiteTex);
                GUI.color = new Color(0.95f, 0.95f, 0.95f, 1f);
                GUI.DrawTexture(new Rect(inX + 1f * s, rowY + 1f * s, inW, inH - 2f * s), this.whiteTex);
                GUI.color = savedColor;

                GUI.SetNextControlName("ScaleInput");
                this.scaleInput = GUI.TextField(new Rect(inX + 4f * s, rowY + 2f * s, inW - 8f * s, inH - 4f * s), this.scaleInput, 20);
                GUI.Label(new Rect(inX + inW + 8f * s, rowY, 30f * s, inH), "%", lblStyle);

                float btnW = 120f * s, btnH = 30f * s;
                float btnY = y + h - btnH - 16f * s;

                var okRect = new Rect(x + w - btnW * 2f - 12f * s, btnY, btnW, btnH);
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 1f);
                GUI.DrawTexture(okRect, this.whiteTex);
                GUI.color = savedColor;
                if (GUI.Button(okRect, "OK", this.cachedDialogButtonStyle))
                    this.ApplyScaleInput(true);

                var cancelRect = new Rect(x + w - btnW - 6f * s, btnY, btnW, btnH);
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 1f);
                GUI.DrawTexture(cancelRect, this.whiteTex);
                GUI.color = savedColor;
                if (GUI.Button(cancelRect, "Cancel", this.cachedDialogButtonStyle))
                {
                    this.showScaleDialog = false;
                    GUI.FocusControl(null);
                }

                var ev = Event.current;
                if (ev.type == EventType.MouseDown && !new Rect(x, y, w, h).Contains(ev.mousePosition))
                {
                    this.showScaleDialog = false;
                    GUI.FocusControl(null);
                    ev.Use();
                }
                if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Return)
                {
                    this.ApplyScaleInput(false);
                    ev.Use();
                }
                if (ev.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "ScaleInput")
                    GUI.FocusControl("ScaleInput");
            }
            catch (Exception ex) { ModLog.Error("HackMenu.DrawScaleDialog", ex); }
        }

        private void ApplyScaleInput(bool closeOnInvalid)
        {
            int v;
            if (int.TryParse(this.scaleInput, out v))
            {
                this.scalePercent = Mathf.Clamp(v, 0f, 100f);
                this.RecalculateMenuPositions();
                foreach (var kv in this.menuRects)
                    this.menuRects[kv.Key] = this.ClampRectToScreen(kv.Value);
                this.showScaleDialog = false;
                GUI.FocusControl(null);
                this.lastStyleScale = -1f;
            }
            else if (closeOnInvalid)
            {
                this.scaleInput = Mathf.RoundToInt(this.scalePercent).ToString();
            }
        }

        private Color GetRainbowColor(float h)
        {
            h %= 360f;
            if (h < 0f) h += 360f;
            float seg = h / 60f;
            int i = (int)Mathf.Floor(seg) % 6;
            float f = seg - Mathf.Floor(seg);
            switch (i)
            {
                case 0: return new Color(1f, f, 0f);
                case 1: return new Color(1f - f, 1f, 0f);
                case 2: return new Color(0f, 1f, f);
                case 3: return new Color(0f, 1f - f, 1f);
                case 4: return new Color(f, 0f, 1f);
                default: return new Color(1f, 0f, 1f - f);
            }
        }

        private Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}