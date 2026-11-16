package com.romaskull.neonhorizon;

import android.app.WallpaperColors;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.opengl.EGL14;
import android.opengl.EGLConfig;
import android.opengl.EGLContext;
import android.opengl.EGLDisplay;
import android.opengl.EGLSurface;
import android.os.Build;
import android.service.wallpaper.WallpaperService;
import android.util.Log;
import android.view.Surface;
import android.view.SurfaceHolder;

public final class SynthwaveWallpaperService extends WallpaperService {
    private static final String TAG = "SynthwaveWallpaperSvc";

    @Override
    public Engine onCreateEngine() {
        return new SynthwaveEngine();
    }

    private final class SynthwaveEngine extends Engine implements SharedPreferences.OnSharedPreferenceChangeListener {
        private final Object mLock = new Object();
        private RenderThread mThread;
        private SharedPreferences mPrefs;

        private volatile WallpaperSettings mSettings;
        private SurfaceHolder mHolder;
        private int mWidth = 0;
        private int mHeight = 0;
        private boolean mVisible = false;
        private boolean mSurfaceReady = false;
        private boolean mNeedsRedraw = true;
        private boolean mSurfaceRecreated = false;
        private boolean mDestroyed = false;
        private int mLastAppliedFrameRate = -1;

        @Override
        public void onCreate(SurfaceHolder surfaceHolder) {
            super.onCreate(surfaceHolder);
            mHolder = surfaceHolder;
            mSettings = WallpaperSettings.load(SynthwaveWallpaperService.this);
            mPrefs = WallpaperSettings.getPrefs(SynthwaveWallpaperService.this);
            mPrefs.registerOnSharedPreferenceChangeListener(this);

            mThread = new RenderThread();
            mThread.start();
        }

        @Override
        public void onDestroy() {
            if (mPrefs != null) {
                mPrefs.unregisterOnSharedPreferenceChangeListener(this);
            }
            synchronized (mLock) {
                mDestroyed = true;
                mLock.notifyAll();
            }
            if (mThread != null) {
                try {
                    mThread.join(1500);
                } catch (InterruptedException ignored) {
                    Thread.currentThread().interrupt();
                }
                mThread = null;
            }
            super.onDestroy();
        }

        @Override
        public void onSurfaceCreated(SurfaceHolder holder) {
            super.onSurfaceCreated(holder);
            synchronized (mLock) {
                mHolder = holder;
                mSurfaceReady = true;
                mSurfaceRecreated = true;
                mNeedsRedraw = true;
                mLastAppliedFrameRate = -1;
                mLock.notifyAll();
            }
        }

        @Override
        public void onSurfaceChanged(SurfaceHolder holder, int format, int width, int height) {
            super.onSurfaceChanged(holder, format, width, height);
            synchronized (mLock) {
                mHolder = holder;
                mWidth = Math.max(1, width);
                mHeight = Math.max(1, height);
                mSurfaceReady = true;
                mNeedsRedraw = true;
                mLock.notifyAll();
            }
        }

        @Override
        public void onSurfaceDestroyed(SurfaceHolder holder) {
            synchronized (mLock) {
                mSurfaceReady = false;
                mSurfaceRecreated = true;
                mLock.notifyAll();
            }
            super.onSurfaceDestroyed(holder);
        }

        @Override
        public void onVisibilityChanged(boolean visible) {
            super.onVisibilityChanged(visible);
            synchronized (mLock) {
                mVisible = visible;
                if (visible) {
                    mNeedsRedraw = true;
                }
                mLock.notifyAll();
            }
        }

        @Override
        public void onSharedPreferenceChanged(SharedPreferences sharedPreferences, String key) {
            WallpaperSettings next = WallpaperSettings.load(SynthwaveWallpaperService.this);
            synchronized (mLock) {
                mSettings = next;
                mNeedsRedraw = true;
                mLock.notifyAll();
            }
        }

        @Override
        public WallpaperColors onComputeColors() {
            if (Build.VERSION.SDK_INT >= 27) {
                Color primary = Color.valueOf(0xFF0B0718);
                Color secondary = Color.valueOf(0xFF00F0FF);
                Color tertiary = Color.valueOf(0xFFFF2A85);
                if (Build.VERSION.SDK_INT >= 31) {
                    return new WallpaperColors(primary, secondary, tertiary, WallpaperColors.HINT_SUPPORTS_DARK_THEME);
                }
                return new WallpaperColors(primary, secondary, tertiary);
            }
            return super.onComputeColors();
        }

        private void applySurfaceFrameRateIfNeeded(SurfaceHolder holder, int targetFps) {
            if (Build.VERSION.SDK_INT < 30 || holder == null) return;
            if (mLastAppliedFrameRate == targetFps) return;
            try {
                Surface surface = holder.getSurface();
                if (surface != null && surface.isValid()) {
                    if (targetFps > 0) {
                        surface.setFrameRate((float) targetFps, Surface.FRAME_RATE_COMPATIBILITY_FIXED_SOURCE);
                    } else {
                        surface.setFrameRate(0.0f, Surface.FRAME_RATE_COMPATIBILITY_DEFAULT);
                    }
                    mLastAppliedFrameRate = targetFps;
                }
            } catch (Throwable t) {
                Log.w(TAG, "setFrameRate skipped: " + t.getMessage());
            }
        }

        private final class RenderThread extends Thread {
            private EGLDisplay mEglDisplay = EGL14.EGL_NO_DISPLAY;
            private EGLConfig mEglConfig = null;
            private EGLContext mEglContext = EGL14.EGL_NO_CONTEXT;
            private EGLSurface mEglSurface = EGL14.EGL_NO_SURFACE;
            private SynthwaveGlRenderer mRenderer;

            private float mSceneTime = 0.0f;
            private float mGridTime = 0.0f;
            private float mCrtTime = 0.0f;
            private long mLastTickNs = 0L;
            private long mLastPresentNs = 0L;
            private int mCurW = 0;
            private int mCurH = 0;

            RenderThread() {
                super("SynthwaveWallpaperGL");
            }

            @Override
            public void run() {
                mLastTickNs = System.nanoTime();
                mLastPresentNs = mLastTickNs;

                try {
                    while (true) {
                        WallpaperSettings settings;
                        SurfaceHolder holder;
                        int w;
                        int h;
                        boolean visible;
                        boolean surfaceReady;
                        boolean needRedraw;
                        boolean recreateSurface;

                        synchronized (mLock) {
                            while (!mDestroyed && (!mSurfaceReady || (!mVisible && !mNeedsRedraw))) {
                                mLock.wait();
                            }
                            if (mDestroyed) break;

                            settings = mSettings;
                            holder = mHolder;
                            w = mWidth;
                            h = mHeight;
                            visible = mVisible;
                            surfaceReady = mSurfaceReady;
                            needRedraw = mNeedsRedraw;
                            recreateSurface = mSurfaceRecreated;

                            boolean animateNow = visible && settings.shouldAnimateNow();
                            if (!animateNow && !needRedraw) {
                                mLock.wait();
                                continue;
                            }

                            mSurfaceRecreated = false;
                            mNeedsRedraw = false;
                        }

                        if (!surfaceReady || holder == null || w <= 0 || h <= 0) {
                            continue;
                        }

                        if (recreateSurface) {
                            destroyEglSurface();
                        }

                        if (mEglDisplay == EGL14.EGL_NO_DISPLAY) {
                            if (!initEglDisplayAndContext()) {
                                Thread.sleep(250);
                                continue;
                            }
                        }

                        if (mEglSurface == EGL14.EGL_NO_SURFACE) {
                            if (!createEglSurface(holder)) {
                                Thread.sleep(100);
                                continue;
                            }
                            mCurW = 0;
                            mCurH = 0;
                            mLastTickNs = System.nanoTime();
                        }

                        if (mRenderer == null) {
                            mRenderer = new SynthwaveGlRenderer(SynthwaveWallpaperService.this);
                            if (!mRenderer.initialize()) {
                                Log.e(TAG, "Renderer initialization failed");
                                Thread.sleep(500);
                                continue;
                            }
                        }

                        if (w != mCurW || h != mCurH) {
                            mRenderer.resize(w, h);
                            mCurW = w;
                            mCurH = h;
                        }

                        applySurfaceFrameRateIfNeeded(holder, settings.targetFps);

                        boolean animateNow = visible && settings.shouldAnimateNow();
                        long nowNs = System.nanoTime();
                        if (animateNow && settings.targetFps > 0) {
                            long intervalNs = 1_000_000_000L / settings.targetFps;
                            long elapsedNs = nowNs - mLastPresentNs;
                            if (!needRedraw && elapsedNs < intervalNs) {
                                long remainingMs = (intervalNs - elapsedNs) / 1_000_000L;
                                if (remainingMs > 1L) {
                                    synchronized (mLock) {
                                        if (!mDestroyed && !mNeedsRedraw && mVisible) {
                                            mLock.wait(remainingMs);
                                        }
                                    }
                                    continue;
                                }
                            }
                        }

                        nowNs = System.nanoTime();
                        double dt = Math.min(0.1, Math.max(0.0, (nowNs - mLastTickNs) / 1_000_000_000.0));
                        mLastTickNs = nowNs;
                        mLastPresentNs = nowNs;

                        if (animateNow) {
                            if (settings.animateBg) {
                                mSceneTime += (float) dt;
                                mGridTime += (float) (dt * (settings.gridSpeed / 100.0));
                            }
                            if (settings.enabled && settings.animate) {
                                mCrtTime += (float) dt;
                            }
                        }

                        mRenderer.renderFrame(mSceneTime, mGridTime, mCrtTime, settings);
                        if (!EGL14.eglSwapBuffers(mEglDisplay, mEglSurface)) {
                            int err = EGL14.eglGetError();
                            Log.w(TAG, "eglSwapBuffers error: 0x" + Integer.toHexString(err));
                            if (err == EGL14.EGL_CONTEXT_LOST) {
                                releaseAllGl();
                            } else {
                                destroyEglSurface();
                            }
                        }
                    }
                } catch (InterruptedException ignored) {
                    Thread.currentThread().interrupt();
                } finally {
                    releaseAllGl();
                }
            }

            private boolean initEglDisplayAndContext() {
                mEglDisplay = EGL14.eglGetDisplay(EGL14.EGL_DEFAULT_DISPLAY);
                if (mEglDisplay == EGL14.EGL_NO_DISPLAY) return false;

                int[] version = new int[2];
                if (!EGL14.eglInitialize(mEglDisplay, version, 0, version, 1)) {
                    mEglDisplay = EGL14.EGL_NO_DISPLAY;
                    return false;
                }

                int[] configAttribs = new int[]{
                        EGL14.EGL_RED_SIZE, 8,
                        EGL14.EGL_GREEN_SIZE, 8,
                        EGL14.EGL_BLUE_SIZE, 8,
                        EGL14.EGL_ALPHA_SIZE, 8,
                        EGL14.EGL_RENDERABLE_TYPE, 0x0040, // EGL_OPENGL_ES3_BIT_KHR
                        EGL14.EGL_NONE
                };
                EGLConfig[] configs = new EGLConfig[1];
                int[] numConfigs = new int[1];
                if (!EGL14.eglChooseConfig(mEglDisplay, configAttribs, 0, configs, 0, 1, numConfigs, 0) || numConfigs[0] == 0) {
                    configAttribs[9] = EGL14.EGL_OPENGL_ES2_BIT;
                    if (!EGL14.eglChooseConfig(mEglDisplay, configAttribs, 0, configs, 0, 1, numConfigs, 0) || numConfigs[0] == 0) {
                        return false;
                    }
                }
                mEglConfig = configs[0];

                int[] ctxAttribs = new int[]{
                        EGL14.EGL_CONTEXT_CLIENT_VERSION, 3,
                        EGL14.EGL_NONE
                };
                mEglContext = EGL14.eglCreateContext(mEglDisplay, mEglConfig, EGL14.EGL_NO_CONTEXT, ctxAttribs, 0);
                if (mEglContext == null || mEglContext == EGL14.EGL_NO_CONTEXT) {
                    mEglContext = EGL14.EGL_NO_CONTEXT;
                    return false;
                }
                return true;
            }

            private boolean createEglSurface(SurfaceHolder holder) {
                Surface surface = holder.getSurface();
                if (surface == null || !surface.isValid()) return false;

                int[] surfAttribs = new int[]{EGL14.EGL_NONE};
                mEglSurface = EGL14.eglCreateWindowSurface(mEglDisplay, mEglConfig, surface, surfAttribs, 0);
                if (mEglSurface == null || mEglSurface == EGL14.EGL_NO_SURFACE) {
                    mEglSurface = EGL14.EGL_NO_SURFACE;
                    return false;
                }
                if (!EGL14.eglMakeCurrent(mEglDisplay, mEglSurface, mEglSurface, mEglContext)) {
                    destroyEglSurface();
                    return false;
                }
                EGL14.eglSwapInterval(mEglDisplay, 1);
                return true;
            }

            private void destroyEglSurface() {
                if (mEglDisplay != EGL14.EGL_NO_DISPLAY) {
                    EGL14.eglMakeCurrent(mEglDisplay, EGL14.EGL_NO_SURFACE, EGL14.EGL_NO_SURFACE, EGL14.EGL_NO_CONTEXT);
                    if (mEglSurface != EGL14.EGL_NO_SURFACE) {
                        EGL14.eglDestroySurface(mEglDisplay, mEglSurface);
                    }
                }
                mEglSurface = EGL14.EGL_NO_SURFACE;
            }

            private void releaseAllGl() {
                if (mRenderer != null) {
                    if (mEglDisplay != EGL14.EGL_NO_DISPLAY && mEglSurface != EGL14.EGL_NO_SURFACE && mEglContext != EGL14.EGL_NO_CONTEXT) {
                        EGL14.eglMakeCurrent(mEglDisplay, mEglSurface, mEglSurface, mEglContext);
                        mRenderer.release();
                    }
                    mRenderer = null;
                }
                destroyEglSurface();
                if (mEglDisplay != EGL14.EGL_NO_DISPLAY) {
                    if (mEglContext != EGL14.EGL_NO_CONTEXT) {
                        EGL14.eglDestroyContext(mEglDisplay, mEglContext);
                    }
                    EGL14.eglTerminate(mEglDisplay);
                }
                mEglContext = EGL14.EGL_NO_CONTEXT;
                mEglDisplay = EGL14.EGL_NO_DISPLAY;
            }
        }
    }
}
