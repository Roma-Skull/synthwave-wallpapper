package com.romaskull.neonhorizon;

import android.content.Context;
import android.content.SharedPreferences;

public final class WallpaperSettings {
    public static final String PREFS_NAME = "neon_horizon_prefs";

    private static final String KEY_SCENE = "scene";
    private static final String KEY_ENABLED = "enabled";
    private static final String KEY_ANIMATE_BG = "animate_bg";
    private static final String KEY_PRESET = "preset";
    private static final String KEY_CURVATURE = "curvature";
    private static final String KEY_SCANLINES = "scanlines";
    private static final String KEY_BLOOM = "bloom";
    private static final String KEY_VHS = "vhs";
    private static final String KEY_SWEEP = "sweep";
    private static final String KEY_VIGNETTE = "vignette";
    private static final String KEY_ANIMATE = "animate";
    private static final String KEY_GRID_SPEED = "grid_speed";
    private static final String KEY_TARGET_FPS = "target_fps";
    private static final String KEY_SCENE_SCALE = "scene_scale";
    private static final String KEY_AUTO_UPDATES = "auto_check_updates";
    private static final String KEY_PAUSED = "manual_paused";

    public String scene;
    public boolean enabled;
    public boolean animateBg;
    public String preset;
    public int curvature;
    public int scanlines;
    public int bloom;
    public int vhs;
    public int sweep;
    public int vignette;
    public boolean animate;
    public int gridSpeed;
    public int targetFps;
    public int sceneScale;
    public boolean autoCheckUpdates;
    public boolean manualPaused;

    public WallpaperSettings(WallpaperConfig cfg) {
        applyDefaults(cfg);
    }

    public void applyDefaults(WallpaperConfig cfg) {
        scene = cfg.defaultScene;
        enabled = cfg.defaultEnabled;
        animateBg = cfg.defaultAnimateBg;
        preset = cfg.defaultPreset;
        curvature = cfg.defaultCurvature;
        scanlines = cfg.defaultScanlines;
        bloom = cfg.defaultBloom;
        vhs = cfg.defaultVhs;
        sweep = cfg.defaultSweep;
        vignette = cfg.defaultVignette;
        animate = cfg.defaultAnimate;
        gridSpeed = cfg.defaultGridSpeed;
        targetFps = cfg.defaultTargetFps;
        sceneScale = cfg.defaultSceneScale;
        autoCheckUpdates = cfg.defaultAutoCheckUpdates;
        manualPaused = false;
    }

    public WallpaperSettings copy(WallpaperConfig cfg) {
        WallpaperSettings c = new WallpaperSettings(cfg);
        c.scene = scene;
        c.enabled = enabled;
        c.animateBg = animateBg;
        c.preset = preset;
        c.curvature = curvature;
        c.scanlines = scanlines;
        c.bloom = bloom;
        c.vhs = vhs;
        c.sweep = sweep;
        c.vignette = vignette;
        c.animate = animate;
        c.gridSpeed = gridSpeed;
        c.targetFps = targetFps;
        c.sceneScale = sceneScale;
        c.autoCheckUpdates = autoCheckUpdates;
        c.manualPaused = manualPaused;
        return c;
    }

    public void normalize(WallpaperConfig cfg) {
        scene = cfg.findScene(scene).id;
        curvature = clamp(curvature, 0, 100);
        scanlines = clamp(scanlines, 0, 100);
        bloom = clamp(bloom, 0, 100);
        vhs = clamp(vhs, 0, 100);
        sweep = clamp(sweep, 0, 100);
        vignette = clamp(vignette, 0, 100);
        gridSpeed = clamp(gridSpeed, 25, 250);
        if (targetFps != 24 && targetFps != 30 && targetFps != 60 && targetFps != 120 && targetFps != 0) {
            targetFps = cfg.defaultTargetFps;
        }
        if (sceneScale != 50 && sceneScale != 60 && sceneScale != 75 && sceneScale != 100) {
            sceneScale = cfg.defaultSceneScale;
        }
        if (preset == null || preset.isEmpty()) {
            preset = "custom";
        }
    }

    public boolean applyPreset(WallpaperConfig cfg, String presetId) {
        WallpaperConfig.WallpaperPreset p = cfg.findPreset(presetId);
        if (p == null) return false;
        enabled = true;
        preset = p.id;
        curvature = p.curvature;
        scanlines = p.scanlines;
        bloom = p.bloom;
        vhs = p.vhs;
        sweep = p.sweep;
        vignette = p.vignette;
        animate = p.animate;
        return true;
    }

    public boolean shouldAnimateNow() {
        if (manualPaused) return false;
        boolean animCrt = enabled && animate && (vhs > 0 || scanlines > 0 || sweep > 0);
        return animateBg || animCrt;
    }

    public static SharedPreferences getPrefs(Context context) {
        return context.getApplicationContext().getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
    }

    public static WallpaperSettings load(Context context) {
        WallpaperConfig cfg = WallpaperConfig.get(context);
        SharedPreferences prefs = getPrefs(context);
        WallpaperSettings s = new WallpaperSettings(cfg);
        s.scene = prefs.getString(KEY_SCENE, s.scene);
        s.enabled = prefs.getBoolean(KEY_ENABLED, s.enabled);
        s.animateBg = prefs.getBoolean(KEY_ANIMATE_BG, s.animateBg);
        s.preset = prefs.getString(KEY_PRESET, s.preset);
        s.curvature = prefs.getInt(KEY_CURVATURE, s.curvature);
        s.scanlines = prefs.getInt(KEY_SCANLINES, s.scanlines);
        s.bloom = prefs.getInt(KEY_BLOOM, s.bloom);
        s.vhs = prefs.getInt(KEY_VHS, s.vhs);
        s.sweep = prefs.getInt(KEY_SWEEP, s.sweep);
        s.vignette = prefs.getInt(KEY_VIGNETTE, s.vignette);
        s.animate = prefs.getBoolean(KEY_ANIMATE, s.animate);
        s.gridSpeed = prefs.getInt(KEY_GRID_SPEED, s.gridSpeed);
        s.targetFps = prefs.getInt(KEY_TARGET_FPS, s.targetFps);
        s.sceneScale = prefs.getInt(KEY_SCENE_SCALE, s.sceneScale);
        s.autoCheckUpdates = prefs.getBoolean(KEY_AUTO_UPDATES, s.autoCheckUpdates);
        s.manualPaused = prefs.getBoolean(KEY_PAUSED, s.manualPaused);
        s.normalize(cfg);
        return s;
    }

    public void save(Context context) {
        WallpaperConfig cfg = WallpaperConfig.get(context);
        normalize(cfg);
        getPrefs(context).edit()
                .putString(KEY_SCENE, scene)
                .putBoolean(KEY_ENABLED, enabled)
                .putBoolean(KEY_ANIMATE_BG, animateBg)
                .putString(KEY_PRESET, preset)
                .putInt(KEY_CURVATURE, curvature)
                .putInt(KEY_SCANLINES, scanlines)
                .putInt(KEY_BLOOM, bloom)
                .putInt(KEY_VHS, vhs)
                .putInt(KEY_SWEEP, sweep)
                .putInt(KEY_VIGNETTE, vignette)
                .putBoolean(KEY_ANIMATE, animate)
                .putInt(KEY_GRID_SPEED, gridSpeed)
                .putInt(KEY_TARGET_FPS, targetFps)
                .putInt(KEY_SCENE_SCALE, sceneScale)
                .putBoolean(KEY_AUTO_UPDATES, autoCheckUpdates)
                .putBoolean(KEY_PAUSED, manualPaused)
                .apply();
    }

    private static int clamp(int value, int min, int max) {
        return Math.max(min, Math.min(max, value));
    }
}
