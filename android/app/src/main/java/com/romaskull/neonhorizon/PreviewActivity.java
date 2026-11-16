package com.romaskull.neonhorizon;

import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.Typeface;
import android.os.Bundle;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.WindowManager;
import android.widget.FrameLayout;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.view.WindowInsetsControllerCompat;

public final class PreviewActivity extends AppCompatActivity implements SharedPreferences.OnSharedPreferenceChangeListener {
    private PreviewGLView mPreviewView;
    private TextView mHudBadge;
    private SharedPreferences mPrefs;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);

        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(Color.rgb(11, 7, 24));

        mPreviewView = new PreviewGLView(this);
        root.addView(mPreviewView, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT));

        mHudBadge = new TextView(this);
        mHudBadge.setTextColor(Color.rgb(0, 240, 255));
        mHudBadge.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12);
        mHudBadge.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        mHudBadge.setBackgroundColor(Color.argb(170, 11, 7, 24));
        int padH = dp(14);
        int padV = dp(8);
        mHudBadge.setPadding(padH, padV, padH, padV);
        mHudBadge.setText("Tap screen to toggle HUD  •  Back to close");

        FrameLayout.LayoutParams badgeLp = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.WRAP_CONTENT,
                FrameLayout.LayoutParams.WRAP_CONTENT,
                Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL);
        badgeLp.bottomMargin = dp(28);
        root.addView(mHudBadge, badgeLp);

        ViewCompat.setOnApplyWindowInsetsListener(root, (v, insets) -> {
            int bottomInset = insets.getInsets(WindowInsetsCompat.Type.systemBars()).bottom;
            badgeLp.bottomMargin = dp(20) + bottomInset;
            mHudBadge.setLayoutParams(badgeLp);
            return insets;
        });

        mPreviewView.setOnClickListener(v -> {
            mHudBadge.setVisibility(mHudBadge.getVisibility() == View.VISIBLE ? View.GONE : View.VISIBLE);
        });

        setContentView(root);
        hideSystemBars();

        mPrefs = WallpaperSettings.getPrefs(this);
        mPrefs.registerOnSharedPreferenceChangeListener(this);
        mPreviewView.applySettings(WallpaperSettings.load(this));
    }

    private void hideSystemBars() {
        WindowInsetsControllerCompat controller = WindowCompat.getInsetsController(getWindow(), getWindow().getDecorView());
        if (controller != null) {
            controller.setSystemBarsBehavior(WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
            controller.hide(WindowInsetsCompat.Type.systemBars());
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (mPreviewView != null) {
            mPreviewView.applySettings(WallpaperSettings.load(this));
            mPreviewView.onResume();
        }
    }

    @Override
    protected void onPause() {
        if (mPreviewView != null) {
            mPreviewView.onPause();
        }
        super.onPause();
    }

    @Override
    protected void onDestroy() {
        if (mPrefs != null) {
            mPrefs.unregisterOnSharedPreferenceChangeListener(this);
        }
        super.onDestroy();
    }

    @Override
    public void onSharedPreferenceChanged(SharedPreferences sharedPreferences, String key) {
        if (mPreviewView != null) {
            mPreviewView.applySettings(WallpaperSettings.load(this));
        }
    }

    private int dp(int v) {
        return Math.round(v * getResources().getDisplayMetrics().density);
    }
}
