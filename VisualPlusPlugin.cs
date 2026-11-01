using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalHack
{
    public class VisualPlusPlugin : MonoBehaviour
    {
        private Light flashlight;
        private float discoTimer;
        private bool wasDisco;
        private static readonly Color[] DiscoColors = { Color.red, Color.green, Color.blue, Color.cyan, Color.magenta, Color.yellow };
        private readonly Dictionary<SpriteRenderer, Color> discoOriginalColors = new Dictionary<SpriteRenderer, Color>();

        private Color preDiscoAmbient;
        private bool preDiscoAmbientValid;

        private float fpsTime; private int fpsFrames; private float fpsValue;
        private GUIStyle fpsStyle; private Texture2D fpsBgTex;

        private void Awake()
        {
            try
            {
                this.fpsBgTex = new Texture2D(1, 1);
                this.fpsBgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.6f));
                this.fpsBgTex.Apply();
                SceneManager.sceneLoaded += this.OnSceneLoaded;
            }
            catch { }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            if (this.flashlight) Destroy(this.flashlight.gameObject);
            if (this.fpsBgTex) Destroy(this.fpsBgTex);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            this.flashlight = null;
            this.discoOriginalColors.Clear();
            this.discoTimer = 0f;
            this.wasDisco = false;
            this.preDiscoAmbientValid = false;
        }

        private void Update()
        {
            try
            {
                this.fpsTime += Time.unscaledDeltaTime; this.fpsFrames++;
                if (this.fpsTime >= 0.5f) { this.fpsValue = this.fpsFrames / this.fpsTime; this.fpsFrames = 0; this.fpsTime = 0f; }

                bool on = HackMenu.ActiveFeatures.Contains(Features.Flashlight);
                if (on && this.flashlight == null)
                {
                    var cam = Camera.main; if (cam == null) return;
                    var go = new GameObject("HackFlashlight");
                    go.transform.SetParent(cam.transform, false);

                    // 光源放在相机【前方】。玩家在相机附近，光源在前 → 玩家落在光锥背后，不会被自己的手电筒照亮
                    go.transform.localPosition = new Vector3(0.15f, -0.1f, 4f);
                    go.transform.localRotation = Quaternion.identity;

                    this.flashlight = go.AddComponent<Light>();
                    this.flashlight.type = LightType.Spot;
                    this.flashlight.range = 60f;
                    this.flashlight.spotAngle = 55f;
                    this.flashlight.intensity = 1.5f;
                    this.flashlight.shadows = LightShadows.None;
                }
                if (this.flashlight) this.flashlight.enabled = on;

                bool disco = HackMenu.ActiveFeatures.Contains(Features.Disco);
                bool wasDisco = this.wasDisco;

                if (disco && !wasDisco)
                {
                    this.preDiscoAmbient = RenderSettings.ambientLight;
                    this.preDiscoAmbientValid = true;
                }

                if (disco)
                {
                    this.discoTimer += Time.unscaledDeltaTime;
                    if (this.discoTimer > 0.3f)
                    {
                        this.discoTimer = 0f;
                        var c = DiscoColors[UnityEngine.Random.Range(0, DiscoColors.Length)];
                        RenderSettings.ambientLight = c;
                        foreach (var sr in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
                        {
                            if (sr == null) continue;
                            if (!this.discoOriginalColors.ContainsKey(sr)) this.discoOriginalColors[sr] = sr.color;
                            sr.color = c;
                        }
                    }
                }

                if (!disco && wasDisco)
                {
                    if (this.discoOriginalColors.Count > 0)
                    {
                        foreach (var kv in this.discoOriginalColors)
                            if (kv.Key != null) kv.Key.color = kv.Value;
                        this.discoOriginalColors.Clear();
                    }

                    bool otherOverride = HackMenu.ActiveFeatures.Contains(Features.RedLight);
                    if (!otherOverride && this.preDiscoAmbientValid)
                        RenderSettings.ambientLight = this.preDiscoAmbient;

                    this.preDiscoAmbientValid = false;
                    this.discoTimer = 0f;
                }

                this.wasDisco = disco;
            }
            catch (Exception ex) { ModLog.Error("VisualPlusPlugin.Update", ex); }
        }

        private void OnGUI()
        {
            try
            {
                if (Event.current.type != EventType.Repaint) return;
                if (!HackMenu.ActiveFeatures.Contains(Features.ShowFps)) return;
                if (this.fpsStyle == null)
                {
                    this.fpsStyle = new GUIStyle(GUI.skin.label) { fontSize = 22 };
                    this.fpsStyle.normal.textColor = Color.yellow;
                }
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(8, 8, 130, 32), this.fpsBgTex);
                GUI.Label(new Rect(14, 10, 130, 30), "FPS: " + Mathf.RoundToInt(this.fpsValue), this.fpsStyle);
            }
            catch { }
        }
    }
}