using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class VolumeBoostPlugin : MonoBehaviour
    {
        // ★ 1000%：原 5f → 10f
        public const float Gain = 10f;

        public static bool FilterActive = false;

        private const float ScanInterval = 0.5f;

        private float lastScanTime;
        private readonly List<GameObject> attached = new List<GameObject>();

        private void Awake() => SceneManager.sceneLoaded += this.OnSceneLoaded;

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            this.DetachAll();
            FilterActive = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this.DetachAll();
            this.lastScanTime = 0f;
            FilterActive = false;
        }

        private void Update()
        {
            try
            {
                bool want = HackMenu.ActiveFeatures.Contains(Features.VolumeWarning);
                FilterActive = want;

                if (!want)
                {
                    if (this.attached.Count > 0) this.DetachAll();
                    return;
                }

                if (Time.unscaledTime - this.lastScanTime < ScanInterval) return;
                this.lastScanTime = Time.unscaledTime;

                this.AttachToAllAudioSources();
                this.CleanupDestroyed();
            }
            catch (Exception ex) { ModLog.Error("VolumeBoostPlugin.Update", ex); }
        }

        private void AttachToAllAudioSources()
        {
            var all = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            for (int i = 0; i < all.Length; i++)
            {
                var src = all[i];
                if (src == null) continue;

                var go = src.gameObject;
                if (go == null) continue;

                if (go.GetComponent<VolumeBoostFilter>() != null) continue;

                try
                {
                    go.AddComponent<VolumeBoostFilter>();
                    this.attached.Add(go);
                }
                catch { }
            }
        }

        private void CleanupDestroyed()
        {
            for (int i = this.attached.Count - 1; i >= 0; i--)
            {
                if (this.attached[i] == null) this.attached.RemoveAt(i);
            }
        }

        private void DetachAll()
        {
            for (int i = 0; i < this.attached.Count; i++)
            {
                var go = this.attached[i];
                if (go == null) continue;
                var f = go.GetComponent<VolumeBoostFilter>();
                if (f != null)
                {
                    try { UnityEngine.Object.Destroy(f); } catch { }
                }
            }
            this.attached.Clear();
        }
    }

    public class VolumeBoostFilter : MonoBehaviour
    {
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!VolumeBoostPlugin.FilterActive) return;

            float g = VolumeBoostPlugin.Gain;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= g;
            }
        }
    }
}