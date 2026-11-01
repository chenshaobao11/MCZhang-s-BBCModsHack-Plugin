using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class FunPlugin : MonoBehaviour
    {
        private Transform playerTf;
        private bool flipApplied, deceptionApplied;
        private readonly Dictionary<SpriteRenderer, Sprite> originalSprites = new Dictionary<SpriteRenderer, Sprite>();

        // ===== 墙壁抖动 =====
        private bool wallShakeActive;
        private Coroutine wallShakeCoroutine;
        private readonly List<MeshFilter> wallShakeTargets = new List<MeshFilter>();
        private readonly Dictionary<MeshFilter, Vector3[]> wallShakeOriginalVerts = new Dictionary<MeshFilter, Vector3[]>();

        // ===== 固定模式参数（手动「墙壁抖动」） =====
        private const float WallShakeBaseAngle    = 10f;    // 固定角度（明显）
        private const float WallShakeDuration     = 0.1f;   // 恢复时长
        private const float WallShakeInterval     = 0.05f;  // 间隔

        // ===== 音乐模式参数（「墙壁随音乐抖动」） =====
        // 静音时完全不抖；有声音时强度越大越猛越快
        private const float MusicSilentThreshold  = 0.02f;  // 低于此强度视为静音，不抖
        private const float MusicWallMinAngle     = 3f;     // 小声音时的角度
        private const float MusicWallMaxAngle     = 25f;    // 满音量时的角度
        private const float MusicWallMinDuration  = 0.15f;  // 小声音时的恢复时长
        private const float MusicWallMaxDuration  = 0.03f;  // 满音量时的恢复时长
        private const float MusicWallMinInterval  = 0.1f;   // 小声音时的间隔
        private const float MusicWallMaxInterval  = 0.02f;  // 满音量时的间隔
        private const float MusicSilentPollTime   = 0.05f;  // 静音时多久检查一次

        private static readonly string[] WallShakeNamePrefixes =
        {
            "Wall", "Floor", "Ceiling",
            "Locker", "Sprite", "Pickup",
            "Desk", "Chair", "Table",
            "WaterFountain", "Window", "_Door"
        };

        private void Awake() => SceneManager.sceneLoaded += this.OnSceneLoaded;

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            try
            {
                this.RestoreDeception();
                this.ApplyFlip(false);
                GL.invertCulling = false;
                this.StopWallShakeCoroutine();
                this.RestoreAllWallShake();
            }
            catch { }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this.playerTf = null;
            this.originalSprites.Clear();
            this.flipApplied = false;
            this.deceptionApplied = false;
            GL.invertCulling = false;

            this.StopWallShakeCoroutine();
            this.wallShakeTargets.Clear();
            this.wallShakeOriginalVerts.Clear();
        }

        private void Update()
        {
            try
            {
                if (this.playerTf == null)
                {
                    var t = GameReflection.FindType("PlayerScript");
                    this.playerTf = (GameReflection.FindInstance(t?.Name) as MonoBehaviour)?.transform;
                }

                var cam = Camera.main;
                if (cam != null)
                {
                    bool m = HackMenu.ActiveFeatures.Contains(Features.MirrorMode);
                    if (m)
                    {
                        cam.ResetProjectionMatrix();
                        cam.projectionMatrix *= Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
                        GL.invertCulling = true;
                    }
                    else if (GL.invertCulling)
                    {
                        GL.invertCulling = false;
                        cam.ResetProjectionMatrix();
                    }
                }

                bool f = HackMenu.ActiveFeatures.Contains(Features.Fliparoo);
                if (f != this.flipApplied) this.ApplyFlip(f);

                bool d = HackMenu.ActiveFeatures.Contains(Features.Deception);
                if (d != this.deceptionApplied)
                {
                    this.deceptionApplied = d;
                    if (d) this.ApplyDeception(); else this.RestoreDeception();
                }
            }
            catch (Exception ex) { ModLog.Error("FunPlugin.Update", ex); }

            this.UpdateWallShake();
        }

        private void UpdateWallShake()
        {
            try
            {
                bool fixedShake = HackMenu.ActiveFeatures.Contains(Features.WallShake);
                bool musicShake = HackMenu.ActiveFeatures.Contains(Features.MusicWallShake);
                bool shouldShake = fixedShake || musicShake;

                if (shouldShake != this.wallShakeActive)
                {
                    this.wallShakeActive = shouldShake;
                    if (shouldShake) this.StartWallShakeCoroutine();
                    else { this.StopWallShakeCoroutine(); this.RestoreAllWallShake(); }
                }
                else if (this.wallShakeActive && this.wallShakeCoroutine == null)
                {
                    this.StartWallShakeCoroutine();
                }
            }
            catch (Exception ex) { ModLog.Error("FunPlugin.UpdateWallShake", ex); }
        }

        private void StartWallShakeCoroutine()
        {
            if (this.wallShakeCoroutine != null) return;
            this.wallShakeCoroutine = base.StartCoroutine(this.WallShakeCoroutine());
        }

        private void StopWallShakeCoroutine()
        {
            if (this.wallShakeCoroutine != null)
            {
                try { base.StopCoroutine(this.wallShakeCoroutine); } catch { }
                this.wallShakeCoroutine = null;
            }
        }

        private void ScanWallShakeTargets()
        {
            this.wallShakeTargets.Clear();
            this.wallShakeOriginalVerts.Clear();

            var renderers = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var mr = renderers[i];
                if (mr == null) continue;

                string n = mr.gameObject.name;
                bool match = false;
                for (int k = 0; k < WallShakeNamePrefixes.Length; k++)
                {
                    if (n.StartsWith(WallShakeNamePrefixes[k])) { match = true; break; }
                }
                if (!match) continue;

                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null) continue;
                var mesh = mf.mesh;
                if (mesh == null) continue;

                if (this.wallShakeOriginalVerts.ContainsKey(mf)) continue;

                this.wallShakeTargets.Add(mf);
                this.wallShakeOriginalVerts[mf] = mesh.vertices;
            }
        }

        private void RestoreAllWallShake()
        {
            try
            {
                foreach (var kv in this.wallShakeOriginalVerts)
                {
                    var mf = kv.Key;
                    if (mf == null) continue;
                    var mesh = mf.mesh;
                    if (mesh == null) continue;

                    mesh.vertices = kv.Value;
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                }
                this.wallShakeOriginalVerts.Clear();
                this.wallShakeTargets.Clear();
            }
            catch (Exception ex) { ModLog.Error("FunPlugin.RestoreAllWallShake", ex); }
        }

        private IEnumerator WallShakeCoroutine()
        {
            this.ScanWallShakeTargets();

            while (this.wallShakeActive)
            {
                for (int i = this.wallShakeTargets.Count - 1; i >= 0; i--)
                {
                    var mf = this.wallShakeTargets[i];
                    if (mf == null || mf.mesh == null) this.wallShakeTargets.RemoveAt(i);
                }

                if (this.wallShakeTargets.Count == 0)
                {
                    this.ScanWallShakeTargets();
                    if (this.wallShakeTargets.Count == 0)
                    {
                        yield return new WaitForSeconds(0.5f);
                        continue;
                    }
                }

                bool musicMode = HackMenu.ActiveFeatures.Contains(Features.MusicWallShake);
                float angle, duration, interval;

                if (musicMode)
                {
                    float intensity = Mathf.Clamp01(MusicSyncPlugin.Intensity);

                    // ★ 静音：不抖，短暂等待后重新检查
                    if (intensity < MusicSilentThreshold)
                    {
                        // 确保之前一轮已经完全恢复
                        float idle = MusicSilentPollTime;
                        while (idle > 0f && this.wallShakeActive)
                        {
                            idle -= Time.deltaTime;
                            yield return null;
                        }
                        continue;
                    }

                    // 有声音：强度越大越猛越快
                    // 把 [SilentThreshold, 1] 映射到 [0, 1]，避免小音量时永远是最小角度
                    float t = Mathf.InverseLerp(MusicSilentThreshold, 1f, intensity);

                    angle    = Mathf.Lerp(MusicWallMinAngle,    MusicWallMaxAngle,    t);
                    duration = Mathf.Lerp(MusicWallMinDuration, MusicWallMaxDuration, t);
                    interval = Mathf.Lerp(MusicWallMinInterval, MusicWallMaxInterval, t);
                }
                else
                {
                    angle    = WallShakeBaseAngle;
                    duration = WallShakeDuration;
                    interval = WallShakeInterval;
                }

                var distorted = new Dictionary<MeshFilter, Vector3[]>(this.wallShakeTargets.Count);
                for (int i = 0; i < this.wallShakeTargets.Count; i++)
                {
                    var mf = this.wallShakeTargets[i];
                    if (mf == null || mf.mesh == null) continue;
                    if (!mf.gameObject.activeInHierarchy) continue;
                    if (!this.wallShakeOriginalVerts.TryGetValue(mf, out var orig)) continue;

                    var rot = Quaternion.Euler(
                        UnityEngine.Random.Range(-angle, angle),
                        UnityEngine.Random.Range(-angle, angle),
                        UnityEngine.Random.Range(-angle, angle));

                    var arr = new Vector3[orig.Length];
                    for (int v = 0; v < orig.Length; v++) arr[v] = rot * orig[v];
                    distorted[mf] = arr;

                    var mesh = mf.mesh;
                    mesh.vertices = arr;
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                }

                float elapsed = 0f;
                while (elapsed < duration && this.wallShakeActive)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    foreach (var kv in distorted)
                    {
                        var mf = kv.Key;
                        if (mf == null) continue;
                        var mesh = mf.mesh;
                        if (mesh == null) continue;
                        if (!this.wallShakeOriginalVerts.TryGetValue(mf, out var orig)) continue;

                        var cur = kv.Value;
                        int len = Mathf.Min(cur.Length, orig.Length);
                        var next = new Vector3[len];
                        for (int v = 0; v < len; v++) next[v] = Vector3.Slerp(cur[v], orig[v], progress);
                        mesh.vertices = next;
                        mesh.RecalculateNormals();
                        mesh.RecalculateBounds();
                    }
                    yield return null;
                }

                foreach (var kv in distorted)
                {
                    var mf = kv.Key;
                    if (mf == null) continue;
                    var mesh = mf.mesh;
                    if (mesh == null) continue;
                    if (!this.wallShakeOriginalVerts.TryGetValue(mf, out var orig)) continue;

                    mesh.vertices = orig;
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                }
                distorted.Clear();

                float wait = interval;
                while (wait > 0f && this.wallShakeActive)
                {
                    wait -= Time.deltaTime;
                    yield return null;
                }
            }

            this.RestoreAllWallShake();
            this.wallShakeCoroutine = null;
        }

        private void ApplyFlip(bool on)
        {
            try
            {
                if (this.playerTf == null) return;
                var s = this.playerTf.localScale;
                s.y = Mathf.Abs(s.y) * (on ? -1f : 1f);
                this.playerTf.localScale = s;
                this.flipApplied = on;
            }
            catch { }
        }

        private void ApplyDeception()
        {
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
                this.originalSprites.Clear();
                var pool = new List<Sprite>();
                foreach (var sr in all)
                    if (sr != null && sr.sprite != null)
                    {
                        this.originalSprites[sr] = sr.sprite;
                        pool.Add(sr.sprite);
                    }
                if (pool.Count == 0) return;
                var rng = new System.Random();
                for (int i = pool.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
                }
                int k = 0;
                foreach (var sr in all)
                    if (sr != null && this.originalSprites.ContainsKey(sr))
                        sr.sprite = pool[k++ % pool.Count];
            }
            catch (Exception ex) { ModLog.Error("FunPlugin.ApplyDeception", ex); }
        }

        private void RestoreDeception()
        {
            try
            {
                foreach (var kv in this.originalSprites)
                    if (kv.Key != null) kv.Key.sprite = kv.Value;
                this.originalSprites.Clear();
            }
            catch { }
        }
    }
}