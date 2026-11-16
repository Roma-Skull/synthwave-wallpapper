package com.romaskull.neonhorizon;

import android.app.AlertDialog;
import android.app.WallpaperInfo;
import android.app.WallpaperManager;
import android.content.ComponentName;
import android.content.Intent;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.TextUtils;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;

import java.io.File;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class SettingsActivity extends AppCompatActivity {
    private static final int COL_BG_DEEP = Color.rgb(11, 7, 24);
    private static final int COL_BG_CARD = Color.rgb(20, 10, 40);
    private static final int COL_BG_ELEV = Color.rgb(30, 16, 58);
    private static final int COL_CYAN = Color.rgb(0, 240, 255);
    private static final int COL_MAGENTA = Color.rgb(255, 42, 133);
    private static final int COL_VIOLET = Color.rgb(181, 55, 242);
    private static final int COL_TEXT_MAIN = Color.rgb(244, 247, 255);
    private static final int COL_TEXT_MUTED = Color.rgb(196, 178, 232);

    private static final int[] FPS_OPTIONS = new int[]{24, 30, 60, 120, 0};
    private static final String[] FPS_LABELS = new String[]{
            "24 FPS", "30 FPS", "60 FPS", "120 FPS", "Display VSync"
    };
    private static final int[] SCALE_OPTIONS = new int[]{60, 100, 75, 50};
    private static final String[] SCALE_LABELS = new String[]{
            "60% (Balanced)", "100% (Native)", "75% (Sharp)", "50% (Eco)"
    };

    private WallpaperConfig mConfig;
    private WallpaperSettings mSettings;

    private final Handler mMainHandler = new Handler(Looper.getMainLooper());
    private final ExecutorService mExecutor = Executors.newSingleThreadExecutor();

    private PreviewGLView mPreviewView;
    private NeonToggleView mToggleMaster;
    private Button mBtnSetWallpaper;
    private Spinner mSpnScene;
    private final List<Button> mPresetBtns = new ArrayList<>();
    private CheckBox mChkAnimateBg;
    private CheckBox mChkAnimateScan;

    private NeonSliderView mSldCurvature;
    private NeonSliderView mSldScanlines;
    private NeonSliderView mSldBloom;
    private NeonSliderView mSldVhs;
    private NeonSliderView mSldSweep;
    private NeonSliderView mSldVignette;
    private NeonSliderView mSldGridSpeed;

    private TextView mValCurvature;
    private TextView mValScanlines;
    private TextView mValBloom;
    private TextView mValVhs;
    private TextView mValSweep;
    private TextView mValVignette;
    private TextView mValGridSpeed;

    private Spinner mSpnFps;
    private Spinner mSpnScale;
    private Button mBtnPause;
    private CheckBox mChkAutoUpdate;
    private Button mBtnCheckUpdate;
    private TextView mLblStatus;

    private boolean mUpdatingUi = false;
    private boolean mUpdateBusy = false;
    private UpdateManager.ReleaseInfo mAvailableUpdate;
    private int mPreviewW = 0;
    private int mPreviewH = 0;
    private float mMeasuredFps = 0f;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        mConfig = WallpaperConfig.get(this);
        mSettings = WallpaperSettings.load(this);

        buildUi();
        refreshUiFromSettings();

        if (mSettings.autoCheckUpdates && UpdateManager.shouldAutoCheck(this)) {
            mMainHandler.postDelayed(() -> {
                if (!isFinishing() && mSettings.autoCheckUpdates) {
                    startUpdateCheck(false);
                }
            }, 2500L);
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        mSettings = WallpaperSettings.load(this);
        refreshUiFromSettings();
        updateWallpaperActiveBadge();
        if (mPreviewView != null) {
            mPreviewView.applySettings(mSettings);
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
        mExecutor.shutdownNow();
        super.onDestroy();
    }

    private void buildUi() {
        LinearLayout outerRoot = new LinearLayout(this);
        outerRoot.setOrientation(LinearLayout.VERTICAL);
        outerRoot.setBackgroundColor(COL_BG_DEEP);

        // Top Neon Gradient Accent Bar
        View topBar = new View(this);
        GradientDrawable barGrad = new GradientDrawable(
                GradientDrawable.Orientation.LEFT_RIGHT,
                new int[]{COL_MAGENTA, COL_VIOLET, COL_CYAN});
        topBar.setBackground(barGrad);
        outerRoot.addView(topBar, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, dp(3)));

        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        outerRoot.addView(scroll, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));

        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        int pad = dp(14);
        content.setPadding(pad, dp(12), pad, dp(18));
        scroll.addView(content, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        ViewCompat.setOnApplyWindowInsetsListener(outerRoot, (v, insets) -> {
            int top = insets.getInsets(WindowInsetsCompat.Type.systemBars()).top;
            int bottom = insets.getInsets(WindowInsetsCompat.Type.systemBars()).bottom;
            outerRoot.setPadding(0, top, 0, bottom);
            return insets;
        });

        // 1. Header Row
        LinearLayout headerRow = new LinearLayout(this);
        headerRow.setOrientation(LinearLayout.HORIZONTAL);
        headerRow.setGravity(Gravity.CENTER_VERTICAL);

        LinearLayout titleCol = new LinearLayout(this);
        titleCol.setOrientation(LinearLayout.VERTICAL);
        TextView title = new TextView(this);
        title.setText("NEON HORIZON");
        title.setTextColor(COL_CYAN);
        title.setTextSize(TypedValue.COMPLEX_UNIT_SP, 20);
        title.setTypeface(Typeface.DEFAULT_BOLD);
        titleCol.addView(title);

        TextView subtitle = new TextView(this);
        subtitle.setText("Live Synthwave & CRT/VHS Wallpaper");
        subtitle.setTextColor(COL_TEXT_MUTED);
        subtitle.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12);
        titleCol.addView(subtitle);

        headerRow.addView(titleCol, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

        mToggleMaster = new NeonToggleView(this);
        mToggleMaster.setOnCheckedChangeListener((view, checked) -> {
            if (mUpdatingUi) return;
            patchSettings(s -> s.enabled = checked, true);
        });
        headerRow.addView(mToggleMaster);
        content.addView(headerRow);
        addSpacer(content, 10);

        // 2. Live Preview Card + Set Wallpaper CTA
        LinearLayout previewCard = createCard();
        previewCard.setPadding(dp(8), dp(8), dp(8), dp(10));

        FrameLayout previewFrame = new FrameLayout(this);
        int previewHeight = Math.max(dp(180), Math.round(getResources().getDisplayMetrics().widthPixels * 0.56f));
        mPreviewView = new PreviewGLView(this);
        mPreviewView.setScanPeriodScale(0.50f);
        mPreviewView.setStatusListener((w, h, fps) -> mMainHandler.post(() -> {
            mPreviewW = w;
            mPreviewH = h;
            mMeasuredFps = fps;
            refreshStatusLine();
        }));
        mPreviewView.setOnClickListener(v -> startActivity(new Intent(this, PreviewActivity.class)));
        previewFrame.addView(mPreviewView, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        TextView previewHint = new TextView(this);
        previewHint.setText("Tap for Fullscreen");
        previewHint.setTextColor(COL_CYAN);
        previewHint.setTextSize(TypedValue.COMPLEX_UNIT_SP, 11);
        previewHint.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        previewHint.setBackgroundColor(Color.argb(150, 11, 7, 24));
        previewHint.setPadding(dp(8), dp(3), dp(8), dp(3));
        FrameLayout.LayoutParams hintLp = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.BOTTOM | Gravity.END);
        hintLp.setMargins(0, 0, dp(6), dp(6));
        previewFrame.addView(previewHint, hintLp);

        previewCard.addView(previewFrame, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, previewHeight));

        LinearLayout ctaRow = new LinearLayout(this);
        ctaRow.setOrientation(LinearLayout.HORIZONTAL);
        LinearLayout.LayoutParams ctaLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        ctaLp.topMargin = dp(8);

        mBtnSetWallpaper = createButton("Set Live Wallpaper", Color.rgb(0, 195, 220), COL_BG_DEEP, COL_CYAN, true);
        mBtnSetWallpaper.setOnClickListener(v -> openLiveWallpaperChooser());
        ctaRow.addView(mBtnSetWallpaper, new LinearLayout.LayoutParams(0, dp(40), 1f));

        Button btnFullscreen = createButton("Preview", COL_BG_ELEV, COL_CYAN, COL_CYAN, false);
        btnFullscreen.setOnClickListener(v -> startActivity(new Intent(this, PreviewActivity.class)));
        LinearLayout.LayoutParams fullLp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, dp(40));
        fullLp.leftMargin = dp(8);
        ctaRow.addView(btnFullscreen, fullLp);

        previewCard.addView(ctaRow, ctaLp);
        content.addView(previewCard);
        addSpacer(content, 8);

        // 3. Scene Selector Card
        LinearLayout sceneCard = createCard();
        LinearLayout sceneRow = new LinearLayout(this);
        sceneRow.setOrientation(LinearLayout.HORIZONTAL);
        sceneRow.setGravity(Gravity.CENTER_VERTICAL);

        TextView lblScene = new TextView(this);
        lblScene.setText("Scene:");
        lblScene.setTextColor(COL_TEXT_MUTED);
        lblScene.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
        sceneRow.addView(lblScene);

        mSpnScene = createStyledSpinner();
        List<String> sceneNames = new ArrayList<>();
        for (WallpaperConfig.SceneDefinition sd : mConfig.scenes) {
            sceneNames.add(sd.displayName);
        }
        mSpnScene.setAdapter(createSpinnerAdapter(sceneNames, COL_CYAN, true));
        mSpnScene.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            @Override
            public void onItemSelected(AdapterView<?> parent, View view, int pos, long id) {
                if (mUpdatingUi || pos < 0 || pos >= mConfig.scenes.size()) return;
                String chosenId = mConfig.scenes.get(pos).id;
                patchSettings(s -> s.scene = chosenId, true);
            }

            @Override
            public void onNothingSelected(AdapterView<?> parent) {}
        });
        LinearLayout.LayoutParams spnSceneLp = new LinearLayout.LayoutParams(0, dp(40), 1f);
        spnSceneLp.leftMargin = dp(10);
        sceneRow.addView(mSpnScene, spnSceneLp);
        sceneCard.addView(sceneRow);
        content.addView(sceneCard);
        addSpacer(content, 8);

        // 4. Presets Grid (4 buttons)
        LinearLayout presetRow = new LinearLayout(this);
        presetRow.setOrientation(LinearLayout.HORIZONTAL);
        for (int i = 0; i < mConfig.presets.size(); i++) {
            WallpaperConfig.WallpaperPreset p = mConfig.presets.get(i);
            Button btn = createButton(p.displayName, COL_BG_CARD, COL_TEXT_MUTED, COL_VIOLET, false);
            btn.setTag(p.id);
            btn.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12);
            btn.setSingleLine(true);
            btn.setEllipsize(TextUtils.TruncateAt.END);
            btn.setOnClickListener(v -> {
                String pid = (String) v.getTag();
                WallpaperSettings next = mSettings.copy(mConfig);
                if (next.applyPreset(mConfig, pid)) {
                    applyNewSettings(next, true);
                    refreshUiFromSettings();
                }
            });
            LinearLayout.LayoutParams pLp = new LinearLayout.LayoutParams(0, dp(36), 1f);
            if (i > 0) pLp.leftMargin = dp(6);
            mPresetBtns.add(btn);
            presetRow.addView(btn, pLp);
        }
        content.addView(presetRow);
        addSpacer(content, 8);

        // 5. Animation Checkboxes Card
        LinearLayout animCard = createCard();
        mChkAnimateBg = createStyledCheckbox("Live background (grid flight & twinkling stars)");
        mChkAnimateBg.setOnCheckedChangeListener((buttonView, isChecked) -> {
            if (mUpdatingUi) return;
            patchSettings(s -> s.animateBg = isChecked, true);
        });
        animCard.addView(mChkAnimateBg);

        mChkAnimateScan = createStyledCheckbox("Scan animation (interlace, sweep bar & tape noise)");
        mChkAnimateScan.setOnCheckedChangeListener((buttonView, isChecked) -> {
            if (mUpdatingUi) return;
            patchSettings(s -> {
                s.animate = isChecked;
                s.preset = "custom";
            }, true);
        });
        animCard.addView(mChkAnimateScan);
        content.addView(animCard);
        addSpacer(content, 8);

        // 6. Sliders Card
        LinearLayout sliderCard = createCard();
        TextView[] valRef = new TextView[1];

        mSldCurvature = addSliderRow(sliderCard, "Screen curvature (CRT)", 0, 100, valRef, (v, committed) -> {
            mValCurvature.setText(v + "%");
            patchSettings(s -> { s.curvature = v; s.preset = "custom"; }, committed);
        });
        mValCurvature = valRef[0];

        mSldScanlines = addSliderRow(sliderCard, "Interlaced scanlines", 0, 100, valRef, (v, committed) -> {
            mValScanlines.setText(v + "%");
            patchSettings(s -> { s.scanlines = v; s.preset = "custom"; }, committed);
        });
        mValScanlines = valRef[0];

        mSldBloom = addSliderRow(sliderCard, "Phosphor bloom glow", 0, 100, valRef, (v, committed) -> {
            mValBloom.setText(v + "%");
            patchSettings(s -> { s.bloom = v; s.preset = "custom"; }, committed);
        });
        mValBloom = valRef[0];

        mSldVhs = addSliderRow(sliderCard, "VHS tape & chromaticity", 0, 100, valRef, (v, committed) -> {
            mValVhs.setText(v + "%");
            patchSettings(s -> { s.vhs = v; s.preset = "custom"; }, committed);
        });
        mValVhs = valRef[0];

        mSldSweep = addSliderRow(sliderCard, "Scan sweep bar", 0, 100, valRef, (v, committed) -> {
            mValSweep.setText(v + "%");
            patchSettings(s -> { s.sweep = v; s.preset = "custom"; }, committed);
        });
        mValSweep = valRef[0];

        mSldVignette = addSliderRow(sliderCard, "Glass & vignette", 0, 100, valRef, (v, committed) -> {
            mValVignette.setText(v + "%");
            patchSettings(s -> { s.vignette = v; s.preset = "custom"; }, committed);
        });
        mValVignette = valRef[0];

        mSldGridSpeed = addSliderRow(sliderCard, "Grid flight speed", 25, 250, valRef, (v, committed) -> {
            mValGridSpeed.setText(v + "%");
            patchSettings(s -> s.gridSpeed = v, committed);
        });
        mValGridSpeed = valRef[0];

        content.addView(sliderCard);
        addSpacer(content, 8);

        // 7. Performance & Updates Card
        LinearLayout perfCard = createCard();

        // FPS row
        LinearLayout fpsRow = new LinearLayout(this);
        fpsRow.setOrientation(LinearLayout.HORIZONTAL);
        fpsRow.setGravity(Gravity.CENTER_VERTICAL);

        TextView lblFps = new TextView(this);
        lblFps.setText("FPS limit:");
        lblFps.setTextColor(COL_TEXT_MUTED);
        lblFps.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        fpsRow.addView(lblFps);

        mSpnFps = createStyledSpinner();
        List<String> fpsList = new ArrayList<>();
        for (String lbl : FPS_LABELS) fpsList.add(lbl);
        mSpnFps.setAdapter(createSpinnerAdapter(fpsList, COL_TEXT_MAIN, false));
        mSpnFps.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            @Override
            public void onItemSelected(AdapterView<?> parent, View view, int pos, long id) {
                if (mUpdatingUi || pos < 0 || pos >= FPS_OPTIONS.length) return;
                patchSettings(s -> s.targetFps = FPS_OPTIONS[pos], true);
            }

            @Override
            public void onNothingSelected(AdapterView<?> parent) {}
        });
        LinearLayout.LayoutParams fpsSpnLp = new LinearLayout.LayoutParams(0, dp(38), 1f);
        fpsSpnLp.leftMargin = dp(8);
        fpsSpnLp.rightMargin = dp(8);
        fpsRow.addView(mSpnFps, fpsSpnLp);

        mBtnPause = createButton("Pause", COL_BG_ELEV, COL_CYAN, COL_CYAN, false);
        mBtnPause.setOnClickListener(v -> patchSettings(s -> s.manualPaused = !s.manualPaused, true));
        fpsRow.addView(mBtnPause, new LinearLayout.LayoutParams(dp(88), dp(36)));
        perfCard.addView(fpsRow);

        // Render Scale row
        LinearLayout scaleRow = new LinearLayout(this);
        scaleRow.setOrientation(LinearLayout.HORIZONTAL);
        scaleRow.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout.LayoutParams scaleRowLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        scaleRowLp.topMargin = dp(8);

        TextView lblScale = new TextView(this);
        lblScale.setText("Render scale:");
        lblScale.setTextColor(COL_TEXT_MUTED);
        lblScale.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        scaleRow.addView(lblScale);

        mSpnScale = createStyledSpinner();
        List<String> scaleList = new ArrayList<>();
        for (String lbl : SCALE_LABELS) scaleList.add(lbl);
        mSpnScale.setAdapter(createSpinnerAdapter(scaleList, COL_TEXT_MAIN, false));
        mSpnScale.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            @Override
            public void onItemSelected(AdapterView<?> parent, View view, int pos, long id) {
                if (mUpdatingUi || pos < 0 || pos >= SCALE_OPTIONS.length) return;
                patchSettings(s -> s.sceneScale = SCALE_OPTIONS[pos], true);
            }

            @Override
            public void onNothingSelected(AdapterView<?> parent) {}
        });
        LinearLayout.LayoutParams scaleSpnLp = new LinearLayout.LayoutParams(0, dp(38), 1f);
        scaleSpnLp.leftMargin = dp(8);
        scaleRow.addView(mSpnScale, scaleSpnLp);
        perfCard.addView(scaleRow, scaleRowLp);

        // Updates row
        LinearLayout updRow = new LinearLayout(this);
        updRow.setOrientation(LinearLayout.HORIZONTAL);
        updRow.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout.LayoutParams updRowLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        updRowLp.topMargin = dp(8);

        mChkAutoUpdate = createStyledCheckbox("Auto-check updates");
        mChkAutoUpdate.setOnCheckedChangeListener((buttonView, isChecked) -> {
            if (mUpdatingUi) return;
            patchSettings(s -> s.autoCheckUpdates = isChecked, true);
        });
        updRow.addView(mChkAutoUpdate, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

        TextView lblVer = new TextView(this);
        lblVer.setText("v" + UpdateManager.getCurrentVersion().normalized);
        lblVer.setTextColor(COL_TEXT_MUTED);
        lblVer.setTypeface(Typeface.MONOSPACE);
        lblVer.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12);
        lblVer.setPadding(dp(6), 0, dp(8), 0);
        updRow.addView(lblVer);

        mBtnCheckUpdate = createButton("Updates", COL_BG_ELEV, COL_CYAN, COL_CYAN, false);
        mBtnCheckUpdate.setOnClickListener(v -> handleUpdateClicked());
        updRow.addView(mBtnCheckUpdate, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, dp(34)));
        perfCard.addView(updRow, updRowLp);

        content.addView(perfCard);
        addSpacer(content, 10);

        // 8. Bottom Row (Reset) & Status Footer
        LinearLayout bottomRow = new LinearLayout(this);
        bottomRow.setOrientation(LinearLayout.HORIZONTAL);
        Button btnReset = createButton("Reset Defaults", Color.rgb(40, 18, 66), COL_MAGENTA, COL_MAGENTA, false);
        btnReset.setOnClickListener(v -> {
            WallpaperSettings def = new WallpaperSettings(mConfig);
            applyNewSettings(def, true);
            refreshUiFromSettings();
            Toast.makeText(this, "Settings reset to defaults", Toast.LENGTH_SHORT).show();
        });
        bottomRow.addView(btnReset, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(38)));
        content.addView(bottomRow);
        addSpacer(content, 8);

        mLblStatus = new TextView(this);
        mLblStatus.setTextColor(COL_TEXT_MUTED);
        mLblStatus.setTypeface(Typeface.MONOSPACE);
        mLblStatus.setTextSize(TypedValue.COMPLEX_UNIT_SP, 11);
        mLblStatus.setEllipsize(TextUtils.TruncateAt.END);
        mLblStatus.setSingleLine(true);
        content.addView(mLblStatus);

        setContentView(outerRoot);
    }

    private void patchSettings(SettingsMutator mutator, boolean save) {
        WallpaperSettings next = mSettings.copy(mConfig);
        mutator.mutate(next);
        applyNewSettings(next, save);
        refreshPresetButtons(next.preset);
        refreshStatusLine();
        mBtnPause.setText(next.manualPaused ? "Resume" : "Pause");
    }

    private void applyNewSettings(WallpaperSettings next, boolean save) {
        next.normalize(mConfig);
        mSettings = next;
        if (mPreviewView != null) {
            mPreviewView.applySettings(next);
        }
        if (save) {
            next.save(this);
        }
    }

    private void refreshUiFromSettings() {
        mUpdatingUi = true;
        try {
            mToggleMaster.setChecked(mSettings.enabled, false);
            mChkAnimateBg.setChecked(mSettings.animateBg);
            mChkAnimateScan.setChecked(mSettings.animate);
            mChkAutoUpdate.setChecked(mSettings.autoCheckUpdates);

            mSldCurvature.setValue(mSettings.curvature, false);
            mValCurvature.setText(mSettings.curvature + "%");
            mSldScanlines.setValue(mSettings.scanlines, false);
            mValScanlines.setText(mSettings.scanlines + "%");
            mSldBloom.setValue(mSettings.bloom, false);
            mValBloom.setText(mSettings.bloom + "%");
            mSldVhs.setValue(mSettings.vhs, false);
            mValVhs.setText(mSettings.vhs + "%");
            mSldSweep.setValue(mSettings.sweep, false);
            mValSweep.setText(mSettings.sweep + "%");
            mSldVignette.setValue(mSettings.vignette, false);
            mValVignette.setText(mSettings.vignette + "%");
            mSldGridSpeed.setValue(mSettings.gridSpeed, false);
            mValGridSpeed.setText(mSettings.gridSpeed + "%");

            for (int i = 0; i < mConfig.scenes.size(); i++) {
                if (mConfig.scenes.get(i).id.equalsIgnoreCase(mSettings.scene)) {
                    mSpnScene.setSelection(i, false);
                    break;
                }
            }

            for (int i = 0; i < FPS_OPTIONS.length; i++) {
                if (FPS_OPTIONS[i] == mSettings.targetFps) {
                    mSpnFps.setSelection(i, false);
                    break;
                }
            }

            for (int i = 0; i < SCALE_OPTIONS.length; i++) {
                if (SCALE_OPTIONS[i] == mSettings.sceneScale) {
                    mSpnScale.setSelection(i, false);
                    break;
                }
            }

            mBtnPause.setText(mSettings.manualPaused ? "Resume" : "Pause");
            refreshPresetButtons(mSettings.preset);
            refreshStatusLine();
        } finally {
            mUpdatingUi = false;
        }
    }

    private void refreshPresetButtons(String activePreset) {
        for (Button btn : mPresetBtns) {
            boolean active = activePreset != null && activePreset.equalsIgnoreCase((String) btn.getTag());
            styleButton(
                    btn,
                    active ? Color.rgb(44, 24, 86) : COL_BG_CARD,
                    active ? Color.WHITE : COL_TEXT_MUTED,
                    active ? COL_CYAN : Color.argb(130, 181, 55, 242));
        }
    }

    private void refreshStatusLine() {
        if (mLblStatus == null) return;
        String sceneName = mConfig.findScene(mSettings.scene).displayName;
        String state = mSettings.manualPaused ? "Paused" : "Active";
        String res = (mPreviewW > 0 && mPreviewH > 0) ? (mPreviewW + "x" + mPreviewH) : "...";
        String fpsText = mMeasuredFps > 0.5f ? String.format(Locale.US, "%.1f FPS", mMeasuredFps) : "-- FPS";
        mLblStatus.setText(String.format(Locale.US, "%s | GLES 3.0 | %s | %s | %s",
                sceneName, res, fpsText, state));
    }

    private void updateWallpaperActiveBadge() {
        if (mBtnSetWallpaper == null) return;
        boolean active = false;
        try {
            WallpaperInfo info = WallpaperManager.getInstance(this).getWallpaperInfo();
            if (info != null && getPackageName().equals(info.getPackageName())) {
                active = true;
            }
        } catch (Exception ignored) {
        }
        mBtnSetWallpaper.setText(active ? "Live Wallpaper Active  •  Change" : "Set Live Wallpaper");
    }

    private void openLiveWallpaperChooser() {
        try {
            Intent intent = new Intent(WallpaperManager.ACTION_CHANGE_LIVE_WALLPAPER);
            intent.putExtra(
                    WallpaperManager.EXTRA_LIVE_WALLPAPER_COMPONENT,
                    new ComponentName(this, SynthwaveWallpaperService.class));
            startActivity(intent);
        } catch (Exception e) {
            try {
                startActivity(new Intent(WallpaperManager.ACTION_LIVE_WALLPAPER_CHOOSER));
            } catch (Exception ex) {
                Toast.makeText(this, "Live wallpaper picker is unavailable on this device.", Toast.LENGTH_LONG).show();
            }
        }
    }

    private void handleUpdateClicked() {
        if (mUpdateBusy) return;
        if (mAvailableUpdate != null) {
            promptInstallUpdate(mAvailableUpdate);
            return;
        }
        startUpdateCheck(true);
    }

    private void startUpdateCheck(boolean userInitiated) {
        if (mUpdateBusy) return;
        mUpdateBusy = true;
        UpdateManager.markAutoChecked(this);
        if (userInitiated) {
            mBtnCheckUpdate.setText("Checking...");
            mBtnCheckUpdate.setEnabled(false);
        }

        mExecutor.execute(() -> {
            UpdateManager.CheckResult res = UpdateManager.checkForUpdates();
            mMainHandler.post(() -> {
                mUpdateBusy = false;
                mBtnCheckUpdate.setEnabled(true);
                if (!res.success) {
                    mBtnCheckUpdate.setText("Updates");
                    if (userInitiated) {
                        Toast.makeText(this, "Update check failed: " + res.errorMessage, Toast.LENGTH_LONG).show();
                    }
                    return;
                }
                if (res.updateAvailable && res.latestRelease != null) {
                    mAvailableUpdate = res.latestRelease;
                    mBtnCheckUpdate.setText("Update v" + mAvailableUpdate.version.normalized);
                    styleButton(mBtnCheckUpdate, Color.rgb(46, 16, 74), COL_MAGENTA, COL_CYAN);
                    if (userInitiated) {
                        promptInstallUpdate(mAvailableUpdate);
                    }
                } else {
                    mAvailableUpdate = null;
                    mBtnCheckUpdate.setText("Updates");
                    styleButton(mBtnCheckUpdate, COL_BG_ELEV, COL_CYAN, COL_CYAN);
                    if (userInitiated) {
                        Toast.makeText(this, "Up to date (v" + res.currentVersion.normalized + ")", Toast.LENGTH_SHORT).show();
                    }
                }
            });
        });
    }

    private void promptInstallUpdate(UpdateManager.ReleaseInfo rel) {
        new AlertDialog.Builder(this)
                .setTitle("Update Available")
                .setMessage("Android release v" + rel.version.normalized + " (" + rel.tagName + ") is available.\n\nDownload, verify SHA-256, and install?")
                .setPositiveButton("Download & Install", (d, w) -> downloadAndInstall(rel))
                .setNegativeButton("Later", null)
                .show();
    }

    private void downloadAndInstall(UpdateManager.ReleaseInfo rel) {
        if (mUpdateBusy) return;
        mUpdateBusy = true;
        mBtnCheckUpdate.setEnabled(false);
        mBtnCheckUpdate.setText("0%");

        mExecutor.execute(() -> {
            try {
                File apk = UpdateManager.downloadAndVerifyApk(this, rel, (pct, msg) ->
                        mMainHandler.post(() -> mBtnCheckUpdate.setText(pct + "%")));
                mMainHandler.post(() -> {
                    mUpdateBusy = false;
                    mBtnCheckUpdate.setEnabled(true);
                    mBtnCheckUpdate.setText("Install v" + rel.version.normalized);
                    UpdateManager.launchApkInstaller(this, apk);
                });
            } catch (Exception e) {
                mMainHandler.post(() -> {
                    mUpdateBusy = false;
                    mBtnCheckUpdate.setEnabled(true);
                    mBtnCheckUpdate.setText("Update v" + rel.version.normalized);
                    Toast.makeText(this, "Update failed: " + e.getMessage(), Toast.LENGTH_LONG).show();
                });
            }
        });
    }

    private LinearLayout createCard() {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        int padX = dp(12);
        int padY = dp(10);
        card.setPadding(padX, padY, padX, padY);
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(COL_BG_CARD);
        bg.setCornerRadius(dp(8));
        bg.setStroke(dp(1), Color.argb(90, 181, 55, 242));
        card.setBackground(bg);
        return card;
    }

    private CheckBox createStyledCheckbox(String text) {
        CheckBox chk = new CheckBox(this);
        chk.setText(text);
        chk.setTextColor(COL_TEXT_MAIN);
        chk.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        chk.setButtonTintList(ColorStateList.valueOf(COL_CYAN));
        return chk;
    }

    private Button createButton(String text, int bgColor, int textColor, int borderColor, boolean bold) {
        Button btn = new Button(this);
        btn.setText(text);
        btn.setAllCaps(false);
        btn.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        if (bold) btn.setTypeface(Typeface.DEFAULT_BOLD);
        btn.setPadding(dp(10), 0, dp(10), 0);
        styleButton(btn, bgColor, textColor, borderColor);
        return btn;
    }

    private void styleButton(Button btn, int bgColor, int textColor, int borderColor) {
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(bgColor);
        bg.setCornerRadius(dp(6));
        bg.setStroke(dp(1), borderColor);
        btn.setBackground(bg);
        btn.setTextColor(textColor);
    }

    private Spinner createStyledSpinner() {
        Spinner spn = new Spinner(this, Spinner.MODE_DROPDOWN);
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(COL_BG_ELEV);
        bg.setCornerRadius(dp(6));
        bg.setStroke(dp(1), Color.argb(120, 181, 55, 242));
        spn.setBackground(bg);
        spn.setPopupBackgroundDrawable(new GradientDrawable() {{
            setColor(COL_BG_ELEV);
            setStroke(dp(1), COL_VIOLET);
        }});
        return spn;
    }

    private ArrayAdapter<String> createSpinnerAdapter(List<String> items, int textColor, boolean bold) {
        return new ArrayAdapter<String>(this, android.R.layout.simple_spinner_item, items) {
            @Override
            public View getView(int position, View convertView, ViewGroup parent) {
                TextView tv = (TextView) super.getView(position, convertView, parent);
                tv.setTextColor(textColor);
                tv.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
                if (bold) tv.setTypeface(Typeface.DEFAULT_BOLD);
                tv.setPadding(dp(10), 0, dp(10), 0);
                return tv;
            }

            @Override
            public View getDropDownView(int position, View convertView, ViewGroup parent) {
                TextView tv = (TextView) super.getDropDownView(position, convertView, parent);
                tv.setBackgroundColor(COL_BG_ELEV);
                tv.setTextColor(COL_TEXT_MAIN);
                tv.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
                tv.setPadding(dp(14), dp(12), dp(14), dp(12));
                return tv;
            }
        };
    }

    private NeonSliderView addSliderRow(
            LinearLayout parent,
            String label,
            int min,
            int max,
            TextView[] outReadout,
            SliderChangeCallback listener) {
        LinearLayout rowHeader = new LinearLayout(this);
        rowHeader.setOrientation(LinearLayout.HORIZONTAL);
        rowHeader.setGravity(Gravity.CENTER_VERTICAL);

        TextView lbl = new TextView(this);
        lbl.setText(label);
        lbl.setTextColor(COL_TEXT_MAIN);
        lbl.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        rowHeader.addView(lbl, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

        TextView readout = new TextView(this);
        readout.setText("0%");
        readout.setTextColor(COL_CYAN);
        readout.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        readout.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        rowHeader.addView(readout);
        outReadout[0] = readout;

        LinearLayout.LayoutParams hdrLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        if (parent.getChildCount() > 0) hdrLp.topMargin = dp(6);
        parent.addView(rowHeader, hdrLp);

        NeonSliderView slider = new NeonSliderView(this);
        slider.setRange(min, max);
        slider.setOnValueChangeListener((s, v, committed) -> {
            if (!mUpdatingUi) listener.onChanged(v, committed);
        });
        parent.addView(slider, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, dp(28)));
        return slider;
    }

    private void addSpacer(LinearLayout parent, int dpHeight) {
        View sp = new View(this);
        parent.addView(sp, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(dpHeight)));
    }

    private int dp(int v) {
        return Math.round(v * getResources().getDisplayMetrics().density);
    }

    private interface SettingsMutator {
        void mutate(WallpaperSettings settings);
    }

    private interface SliderChangeCallback {
        void onChanged(int value, boolean committed);
    }
}
