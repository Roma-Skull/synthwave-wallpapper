using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace NeonHorizonWallpaper
{
    public sealed class SceneDefinition
    {
        public string Id;
        public string DisplayName;
        public string ShaderFile;
    }

    public sealed class WallpaperPreset
    {
        public string Id;
        public string DisplayName;
        public int Curvature;
        public int Scanlines;
        public int Bloom;
        public int Vhs;
        public int Sweep;
        public int Vignette;
        public bool Animate;
    }

    public sealed class AppSettings
    {
        public string Scene { get; set; }
        public bool Enabled { get; set; }
        public bool AnimateBg { get; set; }
        public string Preset { get; set; }
        public int Curvature { get; set; }
        public int Scanlines { get; set; }
        public int Bloom { get; set; }
        public int Vhs { get; set; }
        public int Sweep { get; set; }
        public int Vignette { get; set; }
        public bool Animate { get; set; }
        public int GridSpeed { get; set; }
        public int TargetFps { get; set; }
        public bool PauseOnFullscreen { get; set; }

        public AppSettings()
        {
            ApplyDefaults();
        }

        public void ApplyDefaults()
        {
            Scene = "neon_horizon";
            Enabled = true;
            AnimateBg = true;
            Preset = "vhs";
            Curvature = 40;
            Scanlines = 52;
            Bloom = 36;
            Vhs = 75;
            Sweep = 60;
            Vignette = 48;
            Animate = true;
            GridSpeed = 100;
            TargetFps = 30;
            PauseOnFullscreen = true;
        }

        public AppSettings Clone()
        {
            AppSettings copy = new AppSettings();
            copy.Scene = Scene;
            copy.Enabled = Enabled;
            copy.AnimateBg = AnimateBg;
            copy.Preset = Preset;
            copy.Curvature = Curvature;
            copy.Scanlines = Scanlines;
            copy.Bloom = Bloom;
            copy.Vhs = Vhs;
            copy.Sweep = Sweep;
            copy.Vignette = Vignette;
            copy.Animate = Animate;
            copy.GridSpeed = GridSpeed;
            copy.TargetFps = TargetFps;
            copy.PauseOnFullscreen = PauseOnFullscreen;
            return copy;
        }

        public void Normalize()
        {
            if (string.IsNullOrEmpty(Scene) || FindScene(Scene) == null)
            {
                Scene = Scenes[0].Id;
            }
            Curvature = Clamp(Curvature, 0, 100);
            Scanlines = Clamp(Scanlines, 0, 100);
            Bloom = Clamp(Bloom, 0, 100);
            Vhs = Clamp(Vhs, 0, 100);
            Sweep = Clamp(Sweep, 0, 100);
            Vignette = Clamp(Vignette, 0, 100);
            GridSpeed = Clamp(GridSpeed, 25, 250);
            if (TargetFps != 30 && TargetFps != 60 && TargetFps != 120 && TargetFps != 0)
            {
                TargetFps = 30;
            }
            if (string.IsNullOrEmpty(Preset))
            {
                Preset = "custom";
            }
        }

        public bool ApplyPreset(string presetId)
        {
            WallpaperPreset p;
            if (!PresetsById.TryGetValue(presetId, out p))
            {
                return false;
            }
            Enabled = true;
            Preset = p.Id;
            Curvature = p.Curvature;
            Scanlines = p.Scanlines;
            Bloom = p.Bloom;
            Vhs = p.Vhs;
            Sweep = p.Sweep;
            Vignette = p.Vignette;
            Animate = p.Animate;
            return true;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static readonly SceneDefinition[] Scenes = new SceneDefinition[]
        {
            new SceneDefinition
            {
                Id = "neon_horizon",
                DisplayName = "Neon Horizon",
                ShaderFile = "synthwave_scene.ps.hlsl"
            },
            new SceneDefinition
            {
                Id = "miami_drive",
                DisplayName = "Miami Sunset Drive",
                ShaderFile = "miami_drive.ps.hlsl"
            }
        };

        public static SceneDefinition FindScene(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId)) return null;
            for (int i = 0; i < Scenes.Length; i++)
            {
                if (string.Equals(Scenes[i].Id, sceneId, StringComparison.OrdinalIgnoreCase))
                {
                    return Scenes[i];
                }
            }
            return null;
        }

        public static readonly WallpaperPreset[] Presets = new WallpaperPreset[]
        {
            new WallpaperPreset
            {
                Id = "soft",
                DisplayName = "Soft CRT",
                Curvature = 14,
                Scanlines = 28,
                Bloom = 32,
                Vhs = 22,
                Sweep = 35,
                Vignette = 35,
                Animate = true
            },
            new WallpaperPreset
            {
                Id = "outrun",
                DisplayName = "Outrun 84",
                Curvature = 24,
                Scanlines = 45,
                Bloom = 58,
                Vhs = 38,
                Sweep = 55,
                Vignette = 55,
                Animate = true
            },
            new WallpaperPreset
            {
                Id = "vhs",
                DisplayName = "VHS Tape",
                Curvature = 40,
                Scanlines = 52,
                Bloom = 36,
                Vhs = 75,
                Sweep = 60,
                Vignette = 48,
                Animate = true
            },
            new WallpaperPreset
            {
                Id = "minimal",
                DisplayName = "Minimal",
                Curvature = 0,
                Scanlines = 18,
                Bloom = 22,
                Vhs = 8,
                Sweep = 0,
                Vignette = 18,
                Animate = false
            }
        };

        private static readonly Dictionary<string, WallpaperPreset> PresetsById = BuildPresetMap();

        private static Dictionary<string, WallpaperPreset> BuildPresetMap()
        {
            Dictionary<string, WallpaperPreset> map = new Dictionary<string, WallpaperPreset>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Presets.Length; i++)
            {
                map[Presets[i].Id] = Presets[i];
            }
            return map;
        }

        public static string GetSettingsDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "NeonHorizonWallpaper");
        }

        public static string GetSettingsFilePath()
        {
            return Path.Combine(GetSettingsDirectory(), "settings.json");
        }

        public static AppSettings Load()
        {
            AppSettings result = new AppSettings();
            try
            {
                string path = GetSettingsFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    AppSettings loaded = ser.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        loaded.Normalize();
                        return loaded;
                    }
                }
            }
            catch
            {
                // Fall back to defaults on corrupt/unreadable config
            }
            return result;
        }

        public void Save()
        {
            try
            {
                Normalize();
                string dir = GetSettingsDirectory();
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                JavaScriptSerializer ser = new JavaScriptSerializer();
                string json = ser.Serialize(this);
                File.WriteAllText(GetSettingsFilePath(), json);
            }
            catch
            {
                // Non-fatal if settings directory cannot be written
            }
        }
    }

    public static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "NeonHorizonWallpaper";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue(ValueName);
                    return val != null && !string.IsNullOrEmpty(val.ToString());
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SetEnabled(bool enable, string exePath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return false;
                    if (enable)
                    {
                        key.SetValue(ValueName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
