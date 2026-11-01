using System;
using BepInEx;
using UnityEngine;

namespace UniversalHack
{
    [BepInPlugin("com.universal.plugin", "Baldis Basics Classic Mods Hack", "2.0.0")]
    public class EntryPoint : BaseUnityPlugin
    {
        private void Awake()
        {
            try
            {
                Application.targetFrameRate = -1;
                QualitySettings.vSyncCount = 0;
            }
            catch { }

            this.SafeAdd<HackMenu>("HackMenu");
            this.SafeAdd<NoClipPlugin>("NoClipPlugin");
            this.SafeAdd<ESPPlugin>("ESPPlugin");
            this.SafeAdd<FOVPlugin>("FOVPlugin");
            this.SafeAdd<VisualPlugin>("VisualPlugin");
            this.SafeAdd<InvinciblePlugin>("InvinciblePlugin");
            this.SafeAdd<StaminaPlugin>("StaminaPlugin");
            this.SafeAdd<ItemPlugin>("ItemPlugin");
            this.SafeAdd<SpeedPlugin>("SpeedPlugin");
            this.SafeAdd<MouseFixPlugin>("MouseFixPlugin");
            this.SafeAdd<EventPlugin>("EventPlugin");
            this.SafeAdd<GameplayPlugin>("GameplayPlugin");
            this.SafeAdd<FunPlugin>("FunPlugin");
            this.SafeAdd<VisualPlusPlugin>("VisualPlusPlugin");
            this.SafeAdd<CameraPlugin>("CameraPlugin");
            this.SafeAdd<NotebookExitPlugin>("NotebookExitPlugin");
            this.SafeAdd<ItemPickerPlugin>("ItemPickerPlugin");
            this.SafeAdd<MusicSyncPlugin>("MusicSyncPlugin");
            this.SafeAdd<AutoAnswerPlugin>("AutoAnswerPlugin");
            this.SafeAdd<VolumeBoostPlugin>("VolumeBoostPlugin");

            try { base.Logger.LogInfo("Hack Plugin Loaded with Crash Protection!"); } catch { }
        }

        private void SafeAdd<T>(string name) where T : MonoBehaviour
        {
            try { base.gameObject.AddComponent<T>(); }
            catch (Exception ex)
            {
                try { base.Logger.LogError($"AddComponent<{name}> 失败: {ex.Message}"); } catch { }
            }
        }
    }
}
