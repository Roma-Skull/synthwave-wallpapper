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
        final int uResolution;
        final int uTime;
        final int uGridTime;

        SceneProgram(int program) {
            this.program = program;
            this.uResolution = GLES30.glGetUniformLocation(program, "uResolution");
            this.uTime = GLES30.glGetUniformLocation(program, "uTime");
            this.uGridTime = GLES30.glGetUniformLocation(program, "uGridTime");
        }
    }

    private static final class CrtProgram {
        final int program;
        final int uSceneTex;
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
    private CrtProgram mCrtProgram;
    private int mFbo = 0;
    private int mSceneTex = 0;
    private int mSceneW = 0;
    private int mSceneH = 0;
    private int mTargetW = 0;
    private int mTargetH = 0;
    private float mDensity = 2.0f;
    private float mScanPeriodScale = 1.0f;
    private boolean mInitialized = false;

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
        mTargetW = Math.max(64, width);
        mTargetH = Math.max(64, height);
    }

    public void renderFrame(
            float sceneTime,
            float gridTime,
            float crtTime,
            WallpaperSettings settings) {
        if (!mInitialized || mTargetW <= 0 || mTargetH <= 0) return;

        SceneProgram sceneProg = getOrCompileScene(settings.scene);
        if (sceneProg == null || mCrtProgram == null) return;

        float scale = Math.max(0.25f, Math.min(1.0f, settings.sceneScale / 100.0f));
        int sceneW = Math.max(64, Math.round(mTargetW * scale));
        int sceneH = Math.max(64, Math.round(mTargetH * scale));
        if (!ensureFbo(sceneW, sceneH)) return;

        GLES30.glDisable(GLES30.GL_DEPTH_TEST);
        GLES30.glDisable(GLES30.GL_BLEND);
        GLES30.glDisable(GLES30.GL_CULL_FACE);
        GLES30.glBindVertexArray(mVao);

        // Pass 1: Procedural Synthwave Scene -> Offscreen FBO
        GLES30.glBindFramebuffer(GLES30.GL_FRAMEBUFFER, mFbo);
        GLES30.glViewport(0, 0, sceneW, sceneH);
        GLES30.glUseProgram(sceneProg.program);
        GLES30.glUniform2f(sceneProg.uResolution, (float) sceneW, (float) sceneH);
        GLES30.glUniform1f(sceneProg.uTime, sceneTime);
        GLES30.glUniform1f(sceneProg.uGridTime, gridTime);
        GLES30.glDrawArrays(GLES30.GL_TRIANGLES, 0, 3);

        // Pass 2: CRT / VHS Post-Processing -> Screen Surface
        GLES30.glBindFramebuffer(GLES30.GL_FRAMEBUFFER, 0);
        GLES30.glViewport(0, 0, mTargetW, mTargetH);
        GLES30.glUseProgram(mCrtProgram.program);

        GLES30.glActiveTexture(GLES30.GL_TEXTURE0);
        GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, mSceneTex);
        GLES30.glUniform1i(mCrtProgram.uSceneTex, 0);

        float effectiveScale = Math.max(mDensity, mTargetH / 1080.0f) * mScanPeriodScale;
        int physPeriod = Math.max(4, Math.round(6.75f * effectiveScale));
        if ((physPeriod & 1) != 0) physPeriod += 1;

        boolean active = settings.enabled;
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

    private boolean ensureFbo(int w, int h) {
        if (mFbo != 0 && mSceneTex != 0 && mSceneW == w && mSceneH == h) {
            return true;
        }
        releaseFbo();

        int[] texBuf = new int[1];
        GLES30.glGenTextures(1, texBuf, 0);
        mSceneTex = texBuf[0];
        GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, mSceneTex);
        GLES30.glTexImage2D(GLES30.GL_TEXTURE_2D, 0, GLES30.GL_RGBA8, w, h, 0, GLES30.GL_RGBA, GLES30.GL_UNSIGNED_BYTE, null);
        GLES30.glTexParameteri(GLES30.GL_TEXTURE_2D, GLES30.GL_TEXTURE_MIN_FILTER, GLES30.GL_LINEAR);
        GLES30.glTexParameteri(GLES30.GL_TEXTURE_2D, GLES30.GL_TEXTURE_MAG_FILTER, GLES30.GL_LINEAR);
        GLES30.glTexParameteri(GLES30.GL_TEXTURE_2D, GLES30.GL_TEXTURE_WRAP_S, GLES30.GL_CLAMP_TO_EDGE);
        GLES30.glTexParameteri(GLES30.GL_TEXTURE_2D, GLES30.GL_TEXTURE_WRAP_T, GLES30.GL_CLAMP_TO_EDGE);
        GLES30.glBindTexture(GLES30.GL_TEXTURE_2D, 0);

        int[] fboBuf = new int[1];
        GLES30.glGenFramebuffers(1, fboBuf, 0);
        mFbo = fboBuf[0];
        GLES30.glBindFramebuffer(GLES30.GL_FRAMEBUFFER, mFbo);
        GLES30.glFramebufferTexture2D(GLES30.GL_FRAMEBUFFER, GLES30.GL_COLOR_ATTACHMENT0, GLES30.GL_TEXTURE_2D, mSceneTex, 0);

        int status = GLES30.glCheckFramebufferStatus(GLES30.GL_FRAMEBUFFER);
        GLES30.glBindFramebuffer(GLES30.GL_FRAMEBUFFER, 0);
        if (status != GLES30.GL_FRAMEBUFFER_COMPLETE) {
            Log.e(TAG, "FBO incomplete: 0x" + Integer.toHexString(status));
            releaseFbo();
            return false;
        }

        mSceneW = w;
        mSceneH = h;
        return true;
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
        mSceneW = 0;
        mSceneH = 0;
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
