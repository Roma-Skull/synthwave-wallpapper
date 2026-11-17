package com.romaskull.neonhorizon;

import android.content.Context;
import android.util.Log;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public final class WallpaperConfig {
    private static final String TAG = "WallpaperConfig";
    private static final String CONFIG_ASSET = "wallpaper_config.json";

    public static final class SceneDefinition {
        public final String id;
        public final String displayName;
        public final String shaderFile;

        public SceneDefinition(String id, String displayName, String shaderFile) {
            this.id = id;
            this.displayName = displayName;
            this.shaderFile = shaderFile;
        }
    }

    public static final class WallpaperPreset {
        public final String id;
        public final String displayName;
        public final int curvature;
        public final int scanlines;
        public final int bloom;
        public final int vhs;
        public final int sweep;
        public final int vignette;
        public final boolean animate;

        public WallpaperPreset(
                String id,
                String displayName,
                int curvature,
                int scanlines,
                int bloom,
                int vhs,
                int sweep,
                int vignette,
                boolean animate) {
            this.id = id;
            this.displayName = displayName;
            this.curvature = curvature;
            this.scanlines = scanlines;
            this.bloom = bloom;
            this.vhs = vhs;
            this.sweep = sweep;
            this.vignette = vignette;
            this.animate = animate;
        }
    }

    private static volatile WallpaperConfig sInstance;

    public final List<SceneDefinition> scenes;
    public final List<WallpaperPreset> presets;
    public final String defaultScene;
    public final boolean defaultEnabled;
    public final boolean defaultAnimateBg;
    public final String defaultPreset;
    public final int defaultCurvature;
    public final int defaultScanlines;
    public final int defaultBloom;
    public final int defaultVhs;
    public final int defaultSweep;
    public final int defaultVignette;
    public final boolean defaultAnimate;
    public final int defaultGridSpeed;
    public final int defaultTargetFps;
    public final int defaultSceneScale;
    public final boolean defaultAutoCheckUpdates;

    private WallpaperConfig(
            List<SceneDefinition> scenes,
            List<WallpaperPreset> presets,
            String defaultScene,
            boolean defaultEnabled,
            boolean defaultAnimateBg,
            String defaultPreset,
            int defaultCurvature,
            int defaultScanlines,
            int defaultBloom,
            int defaultVhs,
            int defaultSweep,
            int defaultVignette,
            boolean defaultAnimate,
            int defaultGridSpeed,
            int defaultTargetFps,
            int defaultSceneScale,
            boolean defaultAutoCheckUpdates) {
        this.scenes = Collections.unmodifiableList(scenes);
        this.presets = Collections.unmodifiableList(presets);
        this.defaultScene = defaultScene;
        this.defaultEnabled = defaultEnabled;
        this.defaultAnimateBg = defaultAnimateBg;
        this.defaultPreset = defaultPreset;
        this.defaultCurvature = defaultCurvature;
        this.defaultScanlines = defaultScanlines;
        this.defaultBloom = defaultBloom;
        this.defaultVhs = defaultVhs;
        this.defaultSweep = defaultSweep;
        this.defaultVignette = defaultVignette;
        this.defaultAnimate = defaultAnimate;
        this.defaultGridSpeed = defaultGridSpeed;
        this.defaultTargetFps = defaultTargetFps;
        this.defaultSceneScale = defaultSceneScale;
        this.defaultAutoCheckUpdates = defaultAutoCheckUpdates;
    }

    public static WallpaperConfig get(Context context) {
        WallpaperConfig cached = sInstance;
        if (cached != null) return cached;
        synchronized (WallpaperConfig.class) {
            if (sInstance == null) {
                sInstance = loadFromAssets(context.getApplicationContext());
            }
            return sInstance;
        }
    }

    public SceneDefinition findScene(String id) {
        if (id != null) {
            for (int i = 0; i < scenes.size(); i++) {
                if (scenes.get(i).id.equalsIgnoreCase(id)) {
                    return scenes.get(i);
                }
            }
        }
        return scenes.get(0);
    }

    public WallpaperPreset findPreset(String id) {
        if (id == null) return null;
        for (int i = 0; i < presets.size(); i++) {
            if (presets.get(i).id.equalsIgnoreCase(id)) {
                return presets.get(i);
            }
        }
        return null;
    }

    private static WallpaperConfig loadFromAssets(Context context) {
        try (InputStream is = context.getAssets().open(CONFIG_ASSET)) {
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            byte[] buf = new byte[4096];
            int n;
            while ((n = is.read(buf)) > 0) {
                out.write(buf, 0, n);
            }
            String jsonStr = new String(out.toByteArray(), StandardCharsets.UTF_8);
            JSONObject root = new JSONObject(jsonStr);

            List<SceneDefinition> sceneList = new ArrayList<>();
            JSONArray scArr = root.getJSONArray("scenes");
            for (int i = 0; i < scArr.length(); i++) {
                JSONObject o = scArr.getJSONObject(i);
                sceneList.add(new SceneDefinition(
                        o.getString("id"),
                        o.getString("displayName"),
                        o.getString("glslShader")));
            }

            List<WallpaperPreset> presetList = new ArrayList<>();
            JSONArray prArr = root.getJSONArray("presets");
            for (int i = 0; i < prArr.length(); i++) {
                JSONObject o = prArr.getJSONObject(i);
                presetList.add(new WallpaperPreset(
                        o.getString("id"),
                        o.getString("displayName"),
                        o.getInt("curvature"),
                        o.getInt("scanlines"),
                        o.getInt("bloom"),
                        o.getInt("vhs"),
                        o.getInt("sweep"),
                        o.getInt("vignette"),
                        o.getBoolean("animate")));
            }

            JSONObject def = root.getJSONObject("defaults");
            return new WallpaperConfig(
                    sceneList,
                    presetList,
                    def.optString("scene", "neon_horizon"),
                    def.optBoolean("enabled", true),
                    def.optBoolean("animateBg", true),
                    def.optString("preset", "soft"),
                    def.optInt("curvature", 40),
                    def.optInt("scanlines", 10),
                    def.optInt("bloom", 22),
                    def.optInt("vhs", 30),
                    def.optInt("sweep", 25),
                    def.optInt("vignette", 18),
                    def.optBoolean("animate", true),
                    def.optInt("gridSpeed", 50),
                    def.optInt("targetFps", 24),
                    def.optInt("sceneScale", 60),
                    def.optBoolean("autoCheckUpdates", true));
        } catch (Exception e) {
            Log.w(TAG, "Falling back to built-in config: " + e.getMessage());
            return createFallback();
        }
    }

    private static WallpaperConfig createFallback() {
        List<SceneDefinition> scenes = new ArrayList<>();
        scenes.add(new SceneDefinition("neon_horizon", "Neon Horizon", "synthwave_scene.frag"));
        scenes.add(new SceneDefinition("miami_drive", "Miami Sunset Drive", "miami_drive.frag"));
        scenes.add(new SceneDefinition("light_cycle", "Light Cycle", "light_cycle.frag"));
        scenes.add(new SceneDefinition("outrun_cycle", "Outrun Cycle", "outrun_cycle.frag"));

        List<WallpaperPreset> presets = new ArrayList<>();
        presets.add(new WallpaperPreset("soft", "Soft CRT", 40, 10, 22, 30, 25, 18, true));
        presets.add(new WallpaperPreset("outrun", "Outrun 84", 24, 45, 58, 38, 55, 55, true));
        presets.add(new WallpaperPreset("vhs", "VHS Tape", 40, 52, 36, 75, 60, 48, true));
        presets.add(new WallpaperPreset("minimal", "Minimal", 0, 18, 22, 8, 0, 18, false));

        return new WallpaperConfig(
                scenes, presets,
                "neon_horizon", true, true, "soft",
                40, 10, 22, 30, 25, 18,
                true, 50, 24, 60, true);
    }

    public static String readAssetText(Context context, String assetPath) throws IOException {
        try (InputStream is = context.getAssets().open(assetPath)) {
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            byte[] buf = new byte[4096];
            int n;
            while ((n = is.read(buf)) > 0) {
                out.write(buf, 0, n);
            }
            return new String(out.toByteArray(), StandardCharsets.UTF_8);
        }
    }
}
