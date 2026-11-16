package com.romaskull.neonhorizon;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotEquals;
import static org.junit.Assert.assertTrue;

import android.content.Context;
import android.opengl.EGL14;
import android.opengl.EGLConfig;
import android.opengl.EGLContext;
import android.opengl.EGLDisplay;
import android.opengl.EGLSurface;
import android.opengl.GLES30;

import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import org.junit.Test;
import org.junit.runner.RunWith;

import java.nio.ByteBuffer;
import java.nio.ByteOrder;

@RunWith(AndroidJUnit4.class)
public class ShaderRenderInstrumentedTest {

    @Test
    public void allScenesRenderWithoutGlErrorsOnPortraitAndLandscape() {
        Context ctx = InstrumentationRegistry.getInstrumentation().getTargetContext();
        WallpaperConfig cfg = WallpaperConfig.get(ctx);
        assertEquals(4, cfg.scenes.size());
        assertEquals(4, cfg.presets.size());

        int[][] sizes = new int[][]{
                {540, 1205},  // 9:20 portrait (Pixel ratio)
                {960, 540}    // 16:9 landscape
        };

        for (int[] sz : sizes) {
            int w = sz[0];
            int h = sz[1];
            EGLDisplay display = EGL14.eglGetDisplay(EGL14.EGL_DEFAULT_DISPLAY);
            assertNotEquals(EGL14.EGL_NO_DISPLAY, display);
            int[] ver = new int[2];
            assertTrue(EGL14.eglInitialize(display, ver, 0, ver, 1));

            int[] cfgAttribs = new int[]{
                    EGL14.EGL_SURFACE_TYPE, EGL14.EGL_PBUFFER_BIT,
                    EGL14.EGL_RED_SIZE, 8,
                    EGL14.EGL_GREEN_SIZE, 8,
                    EGL14.EGL_BLUE_SIZE, 8,
                    EGL14.EGL_ALPHA_SIZE, 8,
                    EGL14.EGL_RENDERABLE_TYPE, 0x0040,
                    EGL14.EGL_NONE
            };
            EGLConfig[] configs = new EGLConfig[1];
            int[] num = new int[1];
            assertTrue(EGL14.eglChooseConfig(display, cfgAttribs, 0, configs, 0, 1, num, 0));
            assertTrue(num[0] > 0);

            EGLContext eglCtx = EGL14.eglCreateContext(
                    display, configs[0], EGL14.EGL_NO_CONTEXT,
                    new int[]{EGL14.EGL_CONTEXT_CLIENT_VERSION, 3, EGL14.EGL_NONE}, 0);
            assertNotEquals(EGL14.EGL_NO_CONTEXT, eglCtx);

            EGLSurface surf = EGL14.eglCreatePbufferSurface(
                    display, configs[0],
                    new int[]{EGL14.EGL_WIDTH, w, EGL14.EGL_HEIGHT, h, EGL14.EGL_NONE}, 0);
            assertNotEquals(EGL14.EGL_NO_SURFACE, surf);
            assertTrue(EGL14.eglMakeCurrent(display, surf, surf, eglCtx));

            SynthwaveGlRenderer renderer = new SynthwaveGlRenderer(ctx);
            assertTrue("Renderer init failed", renderer.initialize());
            renderer.resize(w, h);

            ByteBuffer pixels = ByteBuffer.allocateDirect(w * h * 4).order(ByteOrder.nativeOrder());
            for (WallpaperConfig.SceneDefinition sd : cfg.scenes) {
                WallpaperSettings s = new WallpaperSettings(cfg);
                s.scene = sd.id;
                renderer.renderFrame(1.5f, 1.5f, 1.5f, s);
                GLES30.glFinish();
                assertEquals("GL error on scene " + sd.id, GLES30.GL_NO_ERROR, GLES30.glGetError());

                pixels.clear();
                GLES30.glReadPixels(0, 0, w, h, GLES30.GL_RGBA, GLES30.GL_UNSIGNED_BYTE, pixels);

                // Verify CRT corner bezel is dark and center horizon/scene has non-trivial color
                int cornerIdx = (2 * w + 2) * 4;
                int cornerR = pixels.get(cornerIdx) & 0xFF;
                int cornerG = pixels.get(cornerIdx + 1) & 0xFF;
                int cornerB = pixels.get(cornerIdx + 2) & 0xFF;
                assertTrue("Expected dark CRT bezel at corner for " + sd.id,
                        cornerR < 30 && cornerG < 30 && cornerB < 40);

                long sumLuma = 0;
                for (int y = h / 4; y < (h * 3) / 4; y += 8) {
                    for (int x = w / 4; x < (w * 3) / 4; x += 8) {
                        int idx = (y * w + x) * 4;
                        sumLuma += (pixels.get(idx) & 0xFF)
                                + (pixels.get(idx + 1) & 0xFF)
                                + (pixels.get(idx + 2) & 0xFF);
                    }
                }
                assertTrue("Expected visible non-black render for scene " + sd.id, sumLuma > 5000);
            }

            renderer.release();
            EGL14.eglMakeCurrent(display, EGL14.EGL_NO_SURFACE, EGL14.EGL_NO_SURFACE, EGL14.EGL_NO_CONTEXT);
            EGL14.eglDestroySurface(display, surf);
            EGL14.eglDestroyContext(display, eglCtx);
            EGL14.eglTerminate(display);
        }
    }
}
