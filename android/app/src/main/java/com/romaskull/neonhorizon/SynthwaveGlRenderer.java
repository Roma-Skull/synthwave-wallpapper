package com.romaskull.neonhorizon;

import android.content.Context;
import android.opengl.GLES30;
import android.util.Log;

import java.io.IOException;
import java.util.HashMap;
import java.util.Map;

public final class SynthwaveGlRenderer {
    private static final String TAG = "SynthwaveGlRenderer";

    private static final class SceneProgram {
        final int program;
        final int uBgCacheTex;
        final int uResolution;
        final int uTime;
        final int uGridTime;
        final int uLayerMode;

        SceneProgram(int program) {
            this.program = program;
            this.uBgCacheTex = GLES30.glGetUniformLocation(program, "bgCacheTex");
            this.uResolution = GLES30.glGetUniformLocation(program, "uResolution");
            this.uTime = GLES30.glGetUniformLocation(program, "uTime");
            this.uGridTime = GLES30.glGetUniformLocation(program, "uGridTime");
            this.uLayerMode = GLES30.glGetUniformLocation(program, "uLayerMode");
        }
    }

    private static final class BloomProgram {
        final int program;
        final int uSceneTex;
        final int uResolution;

        BloomProgram(int program) {
            this.program = program;
            this.uSceneTex = GLES30.glGetUniformLocation(program, "sceneTex");
            this.uResolution = GLES30.glGetUniformLocation(program, "uResolution");
        }
    }

    private static final class CrtProgram {
        final int program;
        final int uSceneTex;
        final int uBloomTex;
        final int uResolution;
        final int uTime;
        final int uCurvature;
        final int uScanlines;
        final int uBloom;
        final int uVhs;
        final int uSweep;
        final int uVignette;
        final int uAnimate;
        final int uScanPeriod;

        CrtProgram(int program) {
            this.program = program;
            this.uSceneTex = GLES30.glGetUniformLocation(program, "sceneTex");
            this.uBloomTex = GLES30.glGetUniformLocation(program, "bloomTex");
            this.uResolution = GLES30.glGetUniformLocation(program, "uResolution");
            this.uTime = GLES30.glGetUniformLocation(program, "uTime");
            this.uCurvature = GLES30.glGetUniformLocation(program, "uCurvature");
            this.uScanlines = GLES30.glGetUniformLocation(program, "uScanlines");
            this.uBloom = GLES30.glGetUniformLocation(program, "uBloom");
            this.uVhs = GLES30.glGetUniformLocation(program, "uVhs");
            this.uSweep = GLES30.glGetUniformLocation(program, "uSweep");
            this.uVignette = GLES30.glGetUniformLocation(program, "uVignette");
            this.uAnimate = GLES30.glGetUniformLocation(program, "uAnimate");
            this.uScanPeriod = GLES30.glGetUniformLocation(program, "uScanPeriod");
        }
    }

    private final Context mContext;
    private final WallpaperConfig mConfig;
    private final Map<String, SceneProgram> mScenePrograms = new HashMap<>();

    private int mVao = 0;
    private int mVertShader = 0;
    private BloomProgram mBloomProgram;
    private CrtProgram mCrtProgram;
    private int mFbo = 0;
    private int mSceneTex = 0;
    private int mBgCacheFbo = 0;
    private int mBgCacheTex = 0;
    private int mBloomFbo = 0;
    private int mBloomTex = 0;
    private int mSceneW = 0;
    private int mSceneH = 0;
    private int mBloomW = 0;
    private int mBloomH = 0;
    private int mTargetW = 0;
    private int mTargetH = 0;
    private float mDensity = 2.0f;
    private float mScanPeriodScale = 1.0f;
    private boolean mInitialized = false;

    private boolean mBgCacheValid = false;
    private String mBgCacheScene = "";
    private float mBgCacheLastTime = 0.0f;
    private float mBgCacheLastGridTime = 0.0f;
    private int mBgCacheFrameCounter = 0;

    public SynthwaveGlRenderer(Context context) {
        mContext = context.getApplicationContext();
        mConfig = WallpaperConfig.get(mContext);
        mDensity = Math.max(0.5f, mContext.getResources().getDisplayMetrics().density);
    }

    public void setScanPeriodScale(float scale) {
        mScanPeriodScale = Math.max(0.25f, scale);
    }

    public boolean initialize() {
        release();
        try {
            int[] vaoBuf = new int[1];
            GLES30.glGenVertexArrays(1, vaoBuf, 0);
            mVao = vaoBuf[0];

            String vsSrc = WallpaperConfig.readAssetText(mContext, "shaders/fullscreen.vert");
            mVertShader = compileShader(GLES30.GL_VERTEX_SHADER, vsSrc, "fullscreen.vert");
            if (mVertShader == 0) return false;

            String bloomSrc = WallpaperConfig.readAssetText(mContext, "shaders/bloom_downsample.frag");
            int bloomProg = linkProgram(mVertShader, bloomSrc, "bloom_downsample.frag");
            if (bloomProg == 0) return false;
            mBloomProgram = new BloomProgram(bloomProg);

            String crtSrc = WallpaperConfig.readAssetText(mContext, "shaders/crt_vhs.frag");
            int crtProg = linkProgram(mVertShader, crtSrc, "crt_vhs.frag");
            if (crtProg == 0) return false;
            mCrtProgram = new CrtProgram(crtProg);

            mInitialized = true;
            return true;
        } catch (IOException e) {
            Log.e(TAG, "Failed to load shaders", e);
            release();
            return false;
        }
    }

    public void resize(int width, int height) {
        int nextW = Math.max(64, width);
        int nextH = Math.max(64, height);
        if (nextW != mTargetW || nextH != mTargetH) {
            mTargetW = nextW;
            mTargetH = nextH;
            mBgCacheValid = false;
            mBgCacheFrameCounter = 0;
        }
    }

    public void renderFrame(
            float sceneTime,
            float gridTime,
            float crtTime,
            WallpaperSettings settings) {
        renderFrame(sceneTime, gridTime, crtTime, settings, true);
    }

    public void renderFrame(
            float sceneTime,
            float gridTime,
            float crtTime,
            WallpaperSettings settings,
            boolean useBgCache) {
        if (!mInitialized || mTargetW <= 0 || mTargetH <= 0) return;

        WallpaperConfig.SceneDefinition sceneDef = mConfig.findScene(settings.scene);
        SceneProgram sceneProg = getOrCompileScene(sceneDef.id);
        if (sceneProg == null || mBloomProgram == null || mCrtProgram == null) return;

        float scale = Math.max(0.25f, Math.min(1.0f, settings.sceneScale / 100.0f));
        int sceneW = Math.max(64, Math.round(mTargetW * scale));
        int sceneH = Math.max(64, Math.round(mTargetH * scale));
        if (!ensureRenderTargets(sceneW, sceneH)) return;

        GLES30.glDisable(GLES30.GL_DEPTH_TEST);
        GLES30.glDisable(GLES30.GL_BLEND);
        GLES30.glDisable(GLES30.GL_CULL_FACE);
        GLES30.glBindVertexArray(mVao);

        String sceneId = sceneDef.id;
        if (useBgCache) {
            // Pass 1a: Periodic background cache (stars, nebulae, city skyline, moon, clouds, hills)
            if (shouldRefreshBgCache(sceneId, sceneTime, gridTime)) {
                GLES30.glBindFramebuffer(GLES30.GL_FRAMEBUFFER, mBgCacheFbo);
                GLES30.glViewport(0, 0, sceneW, sceneH);
                GLES30.glUseProgram(sceneProg.program);
                if (sceneProg.uBgCacheTex >= 0) {
                    GLES30.glActiveTexture(GLES30.GL_TEXTURE0);
                    GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, 0);
                    GLES30.glUniform1i(sceneProg.uBgCacheTex, 0);
                }
                GLES30.glUniform2f(sceneProg.uResolution, (float) sceneW, (float) sceneH);
                GLES30.glUniform1f(sceneProg.uTime, sceneTime);
                GLES30.glUniform1f(sceneProg.uGridTime, gridTime);
                if (sceneProg.uLayerMode >= 0) {
                    GLES30.glUniform1f(sceneProg.uLayerMode, 1.0f);
                }
                GLES30.glDrawArrays(GLES30.GL_TRIANGLES, 0, 3);

                mBgCacheValid = true;
                mBgCacheScene = sceneId;
                mBgCacheLastTime = sceneTime;
                mBgCacheLastGridTime = gridTime;
                mBgCacheFrameCounter = 1;
            } else {
                mBgCacheFrameCounter++;
            }
        }

        // Pass 1b: Scene foreground / road / grid / cycle composited with BgCache -> Scene FBO
        GLES30.glBindFramebuffer(GLES30.GL_FRAMEBUFFER, mFbo);
        GLES30.glViewport(0, 0, sceneW, sceneH);
        GLES30.glUseProgram(sceneProg.program);
        if (sceneProg.uBgCacheTex >= 0) {
            GLES30.glActiveTexture(GLES30.GL_TEXTURE0);
            GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, useBgCache ? mBgCacheTex : 0);
            GLES30.glUniform1i(sceneProg.uBgCacheTex, 0);
        }
        GLES30.glUniform2f(sceneProg.uResolution, (float) sceneW, (float) sceneH);
        GLES30.glUniform1f(sceneProg.uTime, sceneTime);
        GLES30.glUniform1f(sceneProg.uGridTime, gridTime);
        if (sceneProg.uLayerMode >= 0) {
            GLES30.glUniform1f(sceneProg.uLayerMode, useBgCache ? 2.0f : 0.0f);
        }
        GLES30.glDrawArrays(GLES30.GL_TRIANGLES, 0, 3);
        GLES30.glActiveTexture(GLES30.GL_TEXTURE0);
        GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, 0);

        // Pass 1c: Downsampled 1/9-scene Bloom extraction + soft blur (skipped when Bloom is off)
        boolean active = settings.enabled;
        boolean runBloom = active && settings.bloom > 0;
        if (runBloom) {
            GLES30.glBindFramebuffer(GLES30.GL_FRAMEBUFFER, mBloomFbo);
            GLES30.glViewport(0, 0, mBloomW, mBloomH);
            GLES30.glUseProgram(mBloomProgram.program);
            GLES30.glActiveTexture(GLES30.GL_TEXTURE0);
            GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, mSceneTex);
            GLES30.glUniform1i(mBloomProgram.uSceneTex, 0);
            GLES30.glUniform2f(mBloomProgram.uResolution, (float) sceneW, (float) sceneH);
            GLES30.glDrawArrays(GLES30.GL_TRIANGLES, 0, 3);
            GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, 0);
        }

        // Pass 2: CRT / VHS Post-Processing -> Screen Surface
        GLES30.glBindFramebuffer(GLES30.GL_FRAMEBUFFER, 0);
        GLES30.glViewport(0, 0, mTargetW, mTargetH);
        GLES30.glUseProgram(mCrtProgram.program);

        GLES30.glActiveTexture(GLES30.GL_TEXTURE0);
        GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, mSceneTex);
        GLES30.glUniform1i(mCrtProgram.uSceneTex, 0);

        GLES30.glActiveTexture(GLES30.GL_TEXTURE1);
        GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, runBloom ? mBloomTex : 0);
        if (mCrtProgram.uBloomTex >= 0) {
            GLES30.glUniform1i(mCrtProgram.uBloomTex, 1);
        }

        float effectiveScale = Math.max(mDensity, mTargetH / 1080.0f) * mScanPeriodScale;
        int physPeriod = Math.max(4, Math.round(6.75f * effectiveScale));
        if ((physPeriod & 1) != 0) physPeriod += 1;

        GLES30.glUniform2f(mCrtProgram.uResolution, (float) mTargetW, (float) mTargetH);
        GLES30.glUniform1f(mCrtProgram.uTime, crtTime);
        GLES30.glUniform1f(mCrtProgram.uCurvature, active ? settings.curvature / 100.0f : 0.0f);
        GLES30.glUniform1f(mCrtProgram.uScanlines, active ? settings.scanlines / 100.0f : 0.0f);
        GLES30.glUniform1f(mCrtProgram.uBloom, active ? settings.bloom / 100.0f : 0.0f);
        GLES30.glUniform1f(mCrtProgram.uVhs, active ? settings.vhs / 100.0f : 0.0f);
        GLES30.glUniform1f(mCrtProgram.uSweep, active ? settings.sweep / 100.0f : 0.0f);
        GLES30.glUniform1f(mCrtProgram.uVignette, active ? settings.vignette / 100.0f : 0.0f);
        GLES30.glUniform1f(mCrtProgram.uAnimate, (active && settings.animate) ? 1.0f : 0.0f);
        GLES30.glUniform1f(mCrtProgram.uScanPeriod, (float) physPeriod);

        GLES30.glDrawArrays(GLES30.GL_TRIANGLES, 0, 3);

        // Unbind textures from both units so next frame FBO writes do not alias bound samplers
        GLES30.glActiveTexture(GLES30.GL_TEXTURE1);
        GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, 0);
        GLES30.glActiveTexture(GLES30.GL_TEXTURE0);
        GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, 0);
        GLES30.glBindVertexArray(0);
    }

    public void release() {
        mInitialized = false;
        releaseFbo();
        for (SceneProgram sp : mScenePrograms.values()) {
            if (sp.program != 0) GLES30.glDeleteProgram(sp.program);
        }
        mScenePrograms.clear();
        if (mBloomProgram != null && mBloomProgram.program != 0) {
            GLES30.glDeleteProgram(mBloomProgram.program);
        }
        mBloomProgram = null;
        if (mCrtProgram != null && mCrtProgram.program != 0) {
            GLES30.glDeleteProgram(mCrtProgram.program);
        }
        mCrtProgram = null;
        if (mVertShader != 0) {
            GLES30.glDeleteShader(mVertShader);
            mVertShader = 0;
        }
        if (mVao != 0) {
            GLES30.glDeleteVertexArrays(1, new int[]{mVao}, 0);
            mVao = 0;
        }
    }

    private static int getBgCacheRefreshInterval(String sceneId) {
        if ("light_cycle".equalsIgnoreCase(sceneId) || "outrun_cycle".equalsIgnoreCase(sceneId)) {
            return 3;
        }
        return 4;
    }

    private boolean shouldRefreshBgCache(String sceneId, float sceneTime, float gridTime) {
        if (!mBgCacheValid || !mBgCacheScene.equalsIgnoreCase(sceneId)) {
            return true;
        }
        if (sceneTime == mBgCacheLastTime && gridTime == mBgCacheLastGridTime) {
            return false;
        }
        if (sceneTime < mBgCacheLastTime || (sceneTime - mBgCacheLastTime) > 1.5f) {
            return true;
        }
        return (mBgCacheFrameCounter % getBgCacheRefreshInterval(sceneId)) == 0;
    }

    private SceneProgram getOrCompileScene(String sceneId) {
        WallpaperConfig.SceneDefinition def = mConfig.findScene(sceneId);
        SceneProgram cached = mScenePrograms.get(def.id);
        if (cached != null) return cached;

        try {
            String src = WallpaperConfig.readAssetText(mContext, "shaders/" + def.shaderFile);
            int prog = linkProgram(mVertShader, src, def.shaderFile);
            if (prog == 0) return null;
            SceneProgram sp = new SceneProgram(prog);
            mScenePrograms.put(def.id, sp);
            return sp;
        } catch (IOException e) {
            Log.e(TAG, "Failed to read scene shader " + def.shaderFile, e);
            return null;
        }
    }

    private boolean ensureRenderTargets(int sceneW, int sceneH) {
        int bloomW = Math.max(32, sceneW / 3);
        int bloomH = Math.max(32, sceneH / 3);
        if (mFbo != 0 && mSceneTex != 0
                && mBgCacheFbo != 0 && mBgCacheTex != 0
                && mBloomFbo != 0 && mBloomTex != 0
                && mSceneW == sceneW && mSceneH == sceneH
                && mBloomW == bloomW && mBloomH == bloomH) {
            return true;
        }
        releaseFbo();

        int[] sceneRt = createRenderTarget(sceneW, sceneH, "scene");
        if (sceneRt == null) return false;
        mFbo = sceneRt[0];
        mSceneTex = sceneRt[1];

        int[] bgRt = createRenderTarget(sceneW, sceneH, "bgCache");
        if (bgRt == null) {
            releaseFbo();
            return false;
        }
        mBgCacheFbo = bgRt[0];
        mBgCacheTex = bgRt[1];

        int[] bloomRt = createRenderTarget(bloomW, bloomH, "bloom");
        if (bloomRt == null) {
            releaseFbo();
            return false;
        }
        mBloomFbo = bloomRt[0];
        mBloomTex = bloomRt[1];

        mSceneW = sceneW;
        mSceneH = sceneH;
        mBloomW = bloomW;
        mBloomH = bloomH;
        mBgCacheValid = false;
        mBgCacheFrameCounter = 0;
        return true;
    }

    private static int[] createRenderTarget(int w, int h, String label) {
        int[] texBuf = new int[1];
        GLES30.glGenTextures(1, texBuf, 0);
        int tex = texBuf[0];
        GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, tex);
        GLES30.glTexImage2D(GLES30.GL_TEXTURE_2D, 0, GLES30.GL_RGBA8, w, h, 0, GLES30.GL_RGBA, GLES30.GL_UNSIGNED_BYTE, null);
        GLES30.glTexParameteri(GLES30.GL_TEXTURE_2D, GLES30.GL_TEXTURE_MIN_FILTER, GLES30.GL_LINEAR);
        GLES30.glTexParameteri(GLES30.GL_TEXTURE_2D, GLES30.GL_TEXTURE_MAG_FILTER, GLES30.GL_LINEAR);
        GLES30.glTexParameteri(GLES30.GL_TEXTURE_2D, GLES30.GL_TEXTURE_WRAP_S, GLES30.GL_CLAMP_TO_EDGE);
        GLES30.glTexParameteri(GLES30.GL_TEXTURE_2D, GLES30.GL_TEXTURE_WRAP_T, GLES30.GL_CLAMP_TO_EDGE);
        GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, 0);

        int[] fboBuf = new int[1];
        GLES30.glGenFramebuffers(1, fboBuf, 0);
        int fbo = fboBuf[0];
        GLES30.glBindFramebuffer(GLES30.GL_FRAMEBUFFER, fbo);
        GLES30.glFramebufferTexture2D(GLES30.GL_FRAMEBUFFER, GLES30.GL_COLOR_ATTACHMENT0, GLES30.GL_TEXTURE_2D, tex, 0);

        int status = GLES30.glCheckFramebufferStatus(GLES30.GL_FRAMEBUFFER);
        GLES30.glBindFramebuffer(GLES30.GL_FRAMEBUFFER, 0);
        if (status != GLES30.GL_FRAMEBUFFER_COMPLETE) {
            Log.e(TAG, "FBO incomplete (" + label + "): 0x" + Integer.toHexString(status));
            GLES30.glDeleteFramebuffers(1, new int[]{fbo}, 0);
            GLES30.glDeleteTextures(1, new int[]{tex}, 0);
            return null;
        }
        return new int[]{fbo, tex};
    }

    private void releaseFbo() {
        if (mFbo != 0) {
            GLES30.glDeleteFramebuffers(1, new int[]{mFbo}, 0);
            mFbo = 0;
        }
        if (mSceneTex != 0) {
            GLES30.glDeleteTextures(1, new int[]{mSceneTex}, 0);
            mSceneTex = 0;
        }
        if (mBgCacheFbo != 0) {
            GLES30.glDeleteFramebuffers(1, new int[]{mBgCacheFbo}, 0);
            mBgCacheFbo = 0;
        }
        if (mBgCacheTex != 0) {
            GLES30.glDeleteTextures(1, new int[]{mBgCacheTex}, 0);
            mBgCacheTex = 0;
        }
        if (mBloomFbo != 0) {
            GLES30.glDeleteFramebuffers(1, new int[]{mBloomFbo}, 0);
            mBloomFbo = 0;
        }
        if (mBloomTex != 0) {
            GLES30.glDeleteTextures(1, new int[]{mBloomTex}, 0);
            mBloomTex = 0;
        }
        mSceneW = 0;
        mSceneH = 0;
        mBloomW = 0;
        mBloomH = 0;
        mBgCacheValid = false;
        mBgCacheFrameCounter = 0;
        mBgCacheScene = "";
    }

    private static int compileShader(int type, String source, String label) {
        int shader = GLES30.glCreateShader(type);
        if (shader == 0) return 0;
        GLES30.glShaderSource(shader, source);
        GLES30.glCompileShader(shader);
        int[] compiled = new int[1];
        GLES30.glGetShaderiv(shader, GLES30.GL_COMPILE_STATUS, compiled, 0);
        if (compiled[0] == 0) {
            Log.e(TAG, "Shader compile error (" + label + "): " + GLES30.glGetShaderInfoLog(shader));
            GLES30.glDeleteShader(shader);
            return 0;
        }
        return shader;
    }

    private static int linkProgram(int vertShader, String fragSource, String label) {
        int fragShader = compileShader(GLES30.GL_FRAGMENT_SHADER, fragSource, label);
        if (fragShader == 0) return 0;

        int program = GLES30.glCreateProgram();
        if (program == 0) {
            GLES30.glDeleteShader(fragShader);
            return 0;
        }
        GLES30.glAttachShader(program, vertShader);
        GLES30.glAttachShader(program, fragShader);
        GLES30.glLinkProgram(program);
        GLES30.glDeleteShader(fragShader);

        int[] linked = new int[1];
        GLES30.glGetProgramiv(program, GLES30.GL_LINK_STATUS, linked, 0);
        if (linked[0] == 0) {
            Log.e(TAG, "Program link error (" + label + "): " + GLES30.glGetProgramInfoLog(program));
            GLES30.glDeleteProgram(program);
            return 0;
        }
        return program;
    }
}
