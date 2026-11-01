using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class MusicSyncPlugin : MonoBehaviour
    {
        public static float Intensity { get; private set; } = 0f;
        public static float BeatIntensity { get; private set; } = 0f;

        private const float LerpSpeed   = 15f;
        private const float Sensitivity = 1.5f;
        private const int   SampleCount = 256;
        private const float ScanInterval = 2f;

        // AudioSource 列表刷新间隔（音效可能瞬间播放完，太慢会漏）
        private const float SourceScanInterval = 0.3f;

        private class TargetInfo
        {
            public RectTransform rt;
            public Vector3 originalScale;
        }

        private float[] samples = new float[SampleCount];
        private float smoothed = 0f;
        private float beatSmoothed = 0f;

        private readonly List<TargetInfo> targets = new List<TargetInfo>();
        private float lastScanTime;

        // 当前正在播放的 AudioSource 缓存
        private readonly List<AudioSource> activeSources = new List<AudioSource>();
        private float lastSourceScanTime;

        private bool colorApplied;
        private Color preColorAmbient;
        private bool preColorAmbientValid;
        private float hueAccum;

        private void Awake() => SceneManager.sceneLoaded += this.OnSceneLoaded;

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            this.RestoreAllUI();
            this.RestoreColor();
            Intensity = 0f;
            BeatIntensity = 0f;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this.RestoreAllUI();
            this.RestoreColor();
            this.smoothed = 0f;
            this.beatSmoothed = 0f;
            Intensity = 0f;
            BeatIntensity = 0f;
            this.activeSources.Clear();
            this.lastSourceScanTime = 0f;
        }

        private void Update()
        {
            try
            {
                this.SampleAudio();

                this.smoothed = Mathf.Lerp(this.smoothed, this.rawRms, Time.deltaTime * LerpSpeed);
                this.beatSmoothed = Mathf.Lerp(this.beatSmoothed, this.rawBeat, Time.deltaTime * LerpSpeed);

                Intensity = Mathf.Clamp01(this.smoothed);
                BeatIntensity = Mathf.Clamp01(this.beatSmoothed);

                this.TickUIScale();
                this.TickColorMusic();
            }
            catch (Exception ex) { ModLog.Error("MusicSyncPlugin.Update", ex); }
        }

        private float rawRms;
        private float rawBeat;

        // 采样来源：
        //   1. AudioListener.GetOutputData（最终混音输出）
        //   2. 兜底：遍历所有正在播放的 AudioSource 各自 GetOutputData
        //   两个路径取最大值，保证任何场景任何音源都能被捕获
        private void SampleAudio()
        {
            float bestRms = 0f;
            float bestBeat = 0f;

            if (!AudioListener.pause)
            {
                // ---- 1. AudioListener ----
                try
                {
                    AudioListener.GetOutputData(this.samples, 1);
                    float r = CalcRms(this.samples);
                    float b = CalcBeat(this.samples);
                    if (r > bestRms) bestRms = r;
                    if (b > bestBeat) bestBeat = b;
                }
                catch { }
            }

            // ---- 2. 兜底：各 AudioSource ----
            // 定期刷新正在播放的音源列表
            if (Time.unscaledTime - this.lastSourceScanTime > SourceScanInterval)
            {
                this.lastSourceScanTime = Time.unscaledTime;
                this.activeSources.Clear();

                var all = UnityEngine.Object.FindObjectsOfType<AudioSource>();
                for (int i = 0; i < all.Length; i++)
                {
                    var s = all[i];
                    if (s == null) continue;
                    if (!s.isPlaying) continue;
                    if (s.volume <= 0.005f) continue;
                    if (s.mute) continue;
                    this.activeSources.Add(s);
                }
            }

            // 遍历当前活跃音源采样
            for (int i = 0; i < this.activeSources.Count; i++)
            {
                var src = this.activeSources[i];
                if (src == null || !src.isPlaying) continue;
                try
                {
                    src.GetOutputData(this.samples, 0);
                    float r = CalcRms(this.samples);
                    float b = CalcBeat(this.samples);
                    if (r > bestRms) bestRms = r;
                    if (b > bestBeat) bestBeat = b;
                }
                catch { }
            }

            this.rawRms = bestRms;
            this.rawBeat = bestBeat;
        }

        private static float CalcRms(float[] data)
        {
            float sum = 0f;
            for (int i = 0; i < data.Length; i++) sum += data[i] * data[i];
            return Mathf.Sqrt(sum / data.Length);
        }

        private static float CalcBeat(float[] data)
        {
            // 低频段（前 1/4 采样）→ 鼓点/镲片
            int lowEnd = data.Length / 4;
            if (lowEnd <= 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < lowEnd; i++) sum += data[i] * data[i];
            float beat = Mathf.Sqrt(sum / lowEnd);
            return Mathf.Clamp01(beat * 2.5f);
        }

        private void TickUIScale()
        {
            bool wantScale = HackMenu.ActiveFeatures.Contains(Features.MusicUIScale);
            if (!wantScale)
            {
                if (this.targets.Count > 0) this.RestoreAllUI();
                return;
            }

            if (Time.unscaledTime - this.lastScanTime > ScanInterval)
            {
                this.lastScanTime = Time.unscaledTime;
                this.ScanTargets();
            }

            float screenFactor = Mathf.Clamp(Screen.height / 1080f, 0.67f, 2f);
            float maxScale = 1f + screenFactor * 0.25f;

            float factor = 1f + this.smoothed * Sensitivity;
            if (factor > maxScale) factor = maxScale;
            if (factor < 1f) factor = 1f;

            for (int i = 0; i < this.targets.Count; i++)
            {
                var t = this.targets[i];
                if (t.rt == null) continue;
                t.rt.localScale = Vector3.Lerp(t.rt.localScale, t.originalScale * factor, Time.deltaTime * LerpSpeed);
            }
        }

        private void ScanTargets()
        {
            var allCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            for (int i = 0; i < allCanvases.Length; i++)
            {
                var canvas = allCanvases[i];
                if (canvas == null) continue;
                if (IsExcluded(canvas.gameObject)) continue;
                if (HasCanvasInParents(canvas.transform)) continue;

                var tf = canvas.transform;
                int childCount = tf.childCount;
                for (int c = 0; c < childCount; c++)
                {
                    var child = tf.GetChild(c);
                    if (child == null) continue;
                    var rt = child as RectTransform;
                    if (rt == null || !rt.gameObject.activeInHierarchy) continue;

                    bool exists = false;
                    for (int k = 0; k < this.targets.Count; k++)
                        if (this.targets[k].rt == rt) { exists = true; break; }
                    if (exists) continue;

                    this.targets.Add(new TargetInfo { rt = rt, originalScale = rt.localScale });
                }
            }
        }

        private static bool HasCanvasInParents(Transform t)
        {
            var p = t.parent;
            while (p != null)
            {
                if (p.GetComponent("Canvas") != null) return true;
                p = p.parent;
            }
            return false;
        }

        private static bool IsExcluded(GameObject go)
        {
            var cur = go;
            while (cur != null)
            {
                string n = cur.name.ToLowerInvariant();
                if (n.Contains("unityexplorer") || n.Contains("universellib") ||
                    n.Contains("universalhack") || n.Contains("hackmenu") ||
                    n.Contains("modmenu") || n.Contains("explorer"))
                    return true;
                cur = cur.transform.parent != null ? cur.transform.parent.gameObject : null;
            }
            return false;
        }

        private void RestoreAllUI()
        {
            for (int i = 0; i < this.targets.Count; i++)
            {
                var t = this.targets[i];
                if (t.rt != null) { try { t.rt.localScale = t.originalScale; } catch { } }
            }
            this.targets.Clear();
        }

        private void TickColorMusic()
        {
            bool wantColor = HackMenu.ActiveFeatures.Contains(Features.ColorMusic);

            if (wantColor)
            {
                if (!this.preColorAmbientValid)
                {
                    this.preColorAmbient = RenderSettings.ambientLight;
                    this.preColorAmbientValid = true;
                }

                // 只有玩家听到声音时才推进色相，静音时颜色保持不变
                if (this.smoothed > 0.01f)
                {
                    this.hueAccum += (0.15f + this.beatSmoothed * 2.5f) * Time.deltaTime;
                    if (this.hueAccum > 1f) this.hueAccum -= 1f;
                    if (this.hueAccum < 0f) this.hueAccum += 1f;
                }

                float sat = 0.4f + this.beatSmoothed * 0.6f;
                float val = 0.45f + this.beatSmoothed * 0.55f;

                RenderSettings.ambientLight = Color.HSVToRGB(this.hueAccum, sat, val);
                this.colorApplied = true;
            }
            else if (this.colorApplied)
            {
                this.RestoreColor();
            }
        }

        private void RestoreColor()
        {
            if (this.colorApplied)
            {
                this.colorApplied = false;

                bool otherOverride =
                    HackMenu.ActiveFeatures.Contains(Features.RedLight) ||
                    HackMenu.ActiveFeatures.Contains(Features.Disco);

                if (!otherOverride && this.preColorAmbientValid)
                    RenderSettings.ambientLight = this.preColorAmbient;
            }
            this.preColorAmbientValid = false;
        }
    }
}