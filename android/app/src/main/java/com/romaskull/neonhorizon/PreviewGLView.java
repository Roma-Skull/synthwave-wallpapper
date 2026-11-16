package com.romaskull.neonhorizon;

import android.content.Context;
import android.opengl.GLSurfaceView;
import android.util.AttributeSet;

import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

public final class PreviewGLView extends GLSurfaceView {
    public interface StatusListener {
        void onRenderStats(int width, int height, float measuredFps);
    }

    private final SynthwaveGlRenderer mRenderer;
    private volatile WallpaperSettings mSettings;
    private volatile StatusListener mStatusListener;

    private float mSceneTime = 0f;
    private float mGridTime = 0f;
    private float mCrtTime = 0f;
    private long mLastTickNs = 0L;
    private long mLastFrameNs = 0L;
    private int mFpsAccumFrames = 0;
    private long mFpsAccumStartNs = 0L;
    private float mMeasuredFps = 0f;
    private int mWidth = 0;
    private int mHeight = 0;

    public PreviewGLView(Context context) {
        this(context, null);
    }

    public PreviewGLView(Context context, AttributeSet attrs) {
        super(context, attrs);
        mSettings = WallpaperSettings.load(context);
        mRenderer = new SynthwaveGlRenderer(context);

        setEGLContextClientVersion(3);
        setPreserveEGLContextOnPause(true);
        setRenderer(new Renderer() {
            @Override
            public void onSurfaceCreated(GL10 gl, EGLConfig config) {
                mRenderer.initialize();
                mLastTickNs = System.nanoTime();
                mLastFrameNs = mLastTickNs;
                mFpsAccumStartNs = mLastTickNs;
                mFpsAccumFrames = 0;
            }

            @Override
            public void onSurfaceChanged(GL10 gl, int width, int height) {
                mWidth = width;
                mHeight = height;
                mRenderer.resize(width, height);
            }

            @Override
            public void onDrawFrame(GL10 gl) {
                WallpaperSettings s = mSettings;
                boolean animateNow = s.shouldAnimateNow();
                if (animateNow && s.targetFps > 0) {
                    long intervalNs = 1_000_000_000L / s.targetFps;
                    long now = System.nanoTime();
                    long elapsed = now - mLastFrameNs;
                    if (elapsed < intervalNs) {
                        long sleepMs = (intervalNs - elapsed) / 1_000_000L;
                        if (sleepMs > 1L) {
                            try {
                                Thread.sleep(sleepMs);
                            } catch (InterruptedException ignored) {
                                Thread.currentThread().interrupt();
                            }
                        }
                    }
                }

                long nowNs = System.nanoTime();
                double dt = Math.min(0.1, Math.max(0.0, (nowNs - mLastTickNs) / 1_000_000_000.0));
                mLastTickNs = nowNs;
                mLastFrameNs = nowNs;

                if (animateNow) {
                    if (s.animateBg) {
                        mSceneTime += (float) dt;
                        mGridTime += (float) (dt * (s.gridSpeed / 100.0));
                    }
                    if (s.enabled && s.animate) {
                        mCrtTime += (float) dt;
                    }
                }

                mRenderer.renderFrame(mSceneTime, mGridTime, mCrtTime, s);

                mFpsAccumFrames++;
                long fpsElapsed = nowNs - mFpsAccumStartNs;
                if (fpsElapsed >= 600_000_000L) {
                    mMeasuredFps = (float) (mFpsAccumFrames * 1_000_000_000.0 / fpsElapsed);
                    mFpsAccumFrames = 0;
                    mFpsAccumStartNs = nowNs;
                    StatusListener sl = mStatusListener;
                    if (sl != null) {
                        sl.onRenderStats(mWidth, mHeight, mMeasuredFps);
                    }
                }
            }
        });
        updateRenderMode();
    }

    public void setScanPeriodScale(float scale) {
        mRenderer.setScanPeriodScale(scale);
    }

    public void setStatusListener(StatusListener listener) {
        mStatusListener = listener;
    }

    public void applySettings(WallpaperSettings settings) {
        mSettings = settings;
        updateRenderMode();
        requestRender();
    }

    private void updateRenderMode() {
        WallpaperSettings s = mSettings;
        int mode = (s != null && s.shouldAnimateNow()) ? RENDERMODE_CONTINUOUSLY : RENDERMODE_WHEN_DIRTY;
        setRenderMode(mode);
    }

    @Override
    public void onResume() {
        super.onResume();
        mLastTickNs = System.nanoTime();
        requestRender();
    }
}
