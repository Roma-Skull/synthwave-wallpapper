using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NeonHorizonWallpaper
{
    public sealed class NeonToggle : Control
    {
        private bool _checked;
        private bool _hovered;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                EventHandler h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public NeonToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Size = new Size(46, 24);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Color.FromArgb(11, 7, 24));

            Rectangle rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath path = CreatePill(rect))
            {
                Color trackFill = _checked ? Color.FromArgb(56, 0, 240, 255) : Color.FromArgb(36, 20, 66);
                Color border = _checked ? Color.FromArgb(0, 240, 255) : (_hovered ? Color.FromArgb(200, 181, 55, 242) : Color.FromArgb(115, 181, 55, 242));
                using (SolidBrush br = new SolidBrush(trackFill)) g.FillPath(br, path);
                using (Pen pen = new Pen(border, 1.4f)) g.DrawPath(pen, path);
            }

            int pad = 3;
            int thumbSize = Height - pad * 2 - 1;
            int thumbX = _checked ? (Width - thumbSize - pad - 1) : pad;
            Rectangle thumbRect = new Rectangle(thumbX, pad, thumbSize, thumbSize);
            Color thumbCol = _checked ? Color.FromArgb(0, 240, 255) : Color.FromArgb(196, 178, 232);
            using (SolidBrush tb = new SolidBrush(thumbCol))
            {
                g.FillEllipse(tb, thumbRect);
            }
        }

        private static GraphicsPath CreatePill(Rectangle r)
        {
            GraphicsPath p = new GraphicsPath();
            int d = Math.Max(2, r.Height);
            p.AddArc(r.X, r.Y, d, d, 90, 180);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            p.CloseFigure();
            return p;
        }
    }

    public sealed class NeonSlider : Control
    {
        private int _min = 0;
        private int _max = 100;
        private int _value = 50;
        private bool _dragging;
        private bool _hovered;

        public event EventHandler ValueChanged;
        public event EventHandler ValueCommitted;

        public int Minimum
        {
            get { return _min; }
            set { _min = value; if (_value < _min) Value = _min; Invalidate(); }
        }

        public int Maximum
        {
            get { return _max; }
            set { _max = Math.Max(_min + 1, value); if (_value > _max) Value = _max; Invalidate(); }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int clamped = Math.Max(_min, Math.Min(_max, value));
                if (_value == clamped) return;
                _value = clamped;
                Invalidate();
                EventHandler h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public NeonSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Height = 20;
        }

        private void UpdateFromMouseX(int x)
        {
            int pad = Math.Max(8, Height / 2);
            int trackW = Math.Max(1, Width - pad * 2);
            double norm = Math.Max(0.0, Math.Min(1.0, (double)(x - pad) / trackW));
            Value = (int)Math.Round(_min + norm * (_max - _min));
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                Capture = true;
                UpdateFromMouseX(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                UpdateFromMouseX(e.X);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
                EventHandler h = ValueCommitted;
                if (h != null) h(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Color.FromArgb(18, 9, 36));

            int pad = Math.Max(8, Height / 2);
            int trackH = Math.Max(5, (int)Math.Round(Height * 0.28));
            int trackY = (Height - trackH) / 2;
            int trackW = Math.Max(10, Width - pad * 2);
            Rectangle trackRect = new Rectangle(pad, trackY, trackW, trackH);

            double norm = (double)(_value - _min) / Math.Max(1, _max - _min);
            int fillW = Math.Max(0, (int)Math.Round(trackW * norm));

            using (SolidBrush bg = new SolidBrush(Color.FromArgb(38, 22, 70)))
            {
                g.FillRectangle(bg, trackRect);
            }

            if (fillW > 0)
            {
                Rectangle fillRect = new Rectangle(pad, trackY, fillW, trackH);
                using (LinearGradientBrush lgb = new LinearGradientBrush(
                    new Point(pad, 0),
                    new Point(pad + trackW, 0),
                    Color.FromArgb(255, 42, 133),
                    Color.FromArgb(0, 240, 255)))
                {
                    g.FillRectangle(lgb, fillRect);
                }
            }

            int thumbD = Math.Max(12, Height - 6);
            int thumbX = pad + fillW - thumbD / 2;
            int thumbY = (Height - thumbD) / 2;

            if (_hovered || _dragging)
            {
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(72, 0, 240, 255)))
                {
                    g.FillEllipse(glow, thumbX - 3, thumbY - 3, thumbD + 6, thumbD + 6);
                }
            }

            using (SolidBrush white = new SolidBrush(Color.White))
            {
                g.FillEllipse(white, thumbX, thumbY, thumbD, thumbD);
            }
            using (Pen p = new Pen(Color.FromArgb(0, 240, 255), 2.0f))
            {
                g.DrawEllipse(p, thumbX, thumbY, thumbD, thumbD);
            }
        }
    }

    public sealed class SettingsForm : Form
    {
        private static readonly Color ColBgDeep = Color.FromArgb(11, 7, 24);
        private static readonly Color ColBgCard = Color.FromArgb(20, 10, 40);
        private static readonly Color ColBgElev = Color.FromArgb(30, 16, 58);
        private static readonly Color ColCyan = Color.FromArgb(0, 240, 255);
        private static readonly Color ColMagenta = Color.FromArgb(255, 42, 133);
        private static readonly Color ColViolet = Color.FromArgb(181, 55, 242);
        private static readonly Color ColTextMain = Color.FromArgb(244, 247, 255);
        private static readonly Color ColTextMuted = Color.FromArgb(196, 178, 232);

        private readonly Func<AppSettings> _getSettings;
        private readonly Action<AppSettings, bool> _applySettings;
        private readonly Action _resetSettings;
        private readonly Action _openPreview;
        private readonly Action _togglePause;
        private readonly Func<bool> _isPaused;
        private readonly Func<string> _getStatusSummary;
        private readonly Action _triggerUpdateAction;

        private NeonToggle _toggleShader;
        private CheckBox _chkAnimateBg;
        private CheckBox _chkAnimateScan;
        private CheckBox _chkPauseFs;
        private CheckBox _chkStartup;
        private CheckBox _chkAutoUpdate;
        private ComboBox _cmbScene;
        private ComboBox _cmbFps;
        private Label _lblStatus;
        private Label _lblVersion;
        private Button _btnPause;
        private Button _btnCheckUpdate;

        private Button[] _presetBtns;
        private readonly string[] _presetIds = new string[] { "soft", "outrun", "vhs", "minimal" };

        private NeonSlider _sldCurvature;
        private NeonSlider _sldScanlines;
        private NeonSlider _sldBloom;
        private NeonSlider _sldVhs;
        private NeonSlider _sldSweep;
        private NeonSlider _sldVignette;
        private NeonSlider _sldGridSpeed;

        private Label _valCurvature;
        private Label _valScanlines;
        private Label _valBloom;
        private Label _valVhs;
        private Label _valSweep;
        private Label _valVignette;
        private Label _valGridSpeed;

        private readonly List<Font> _ownedFonts = new List<Font>();

        private bool _updatingUi;
        private bool _allowClose;
        private int _builtDpi = 96;
        private string _lastUpdateBtnText = "Updates";
        private bool _lastUpdateHighlight;
        private bool _lastUpdateBusy;

        public SettingsForm(
            Icon appIcon,
            Func<AppSettings> getSettings,
            Action<AppSettings, bool> applySettings,
            Action resetSettings,
            Action openPreview,
            Action togglePause,
            Func<bool> isPaused,
            Func<string> getStatusSummary,
            Action triggerUpdateAction)
        {
            _getSettings = getSettings;
            _applySettings = applySettings;
            _resetSettings = resetSettings;
            _openPreview = openPreview;
            _togglePause = togglePause;
            _isPaused = isPaused;
            _getStatusSummary = getStatusSummary;
            _triggerUpdateAction = triggerUpdateAction;

            Icon = appIcon;
            Text = "Neon Horizon — CRT & VHS Desktop Wallpaper";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            BackColor = ColBgDeep;
            ForeColor = ColTextMain;
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);

            BuildLayout(0);
            RefreshFromSettings();
        }

        private int DetectCurrentDpi()
        {
            try
            {
                if (!IsHandleCreated) CreateHandle();
                using (Graphics g = CreateGraphics())
                {
                    if (g.DpiY >= 72f) return (int)Math.Round(g.DpiY);
                }
            }
            catch
            {
            }
            return (int)NativeMethods.GetMonitorDpi(0, 0);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            int liveDpi = DetectCurrentDpi();
            if (Math.Abs(liveDpi - _builtDpi) >= 4)
            {
                RebuildLayout(liveDpi);
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == (int)NativeMethods.WM_DPICHANGED && IsHandleCreated && !IsDisposed)
            {
                int newDpi = m.WParam.ToInt32() & 0xFFFF;
                if (newDpi >= 72 && Math.Abs(newDpi - _builtDpi) >= 4)
                {
                    RebuildLayout(newDpi);
                }
            }
        }

        private void RebuildLayout(int dpiHint)
        {
            bool prevUpdating = _updatingUi;
            _updatingUi = true;
            SuspendLayout();
            try
            {
                Control[] old = new Control[Controls.Count];
                Controls.CopyTo(old, 0);
                Controls.Clear();
                for (int i = 0; i < old.Length; i++)
                {
                    old[i].Dispose();
                }
                DisposeOwnedFonts();

                BuildLayout(dpiHint);
                RefreshFromSettings();
                SetUpdateUiState(_lastUpdateBtnText, _lastUpdateHighlight, _lastUpdateBusy);
            }
            finally
            {
                ResumeLayout(true);
                _updatingUi = prevUpdating;
            }
            Invalidate();
        }

        private void DisposeOwnedFonts()
        {
            for (int i = 0; i < _ownedFonts.Count; i++)
            {
                try { _ownedFonts[i].Dispose(); }
                catch { }
            }
            _ownedFonts.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeOwnedFonts();
            }
            base.Dispose(disposing);
        }

        private Font TrackFont(Font f)
        {
            _ownedFonts.Add(f);
            return f;
        }

        private static Size MeasureSingleLine(Graphics g, string text, Font font)
        {
            if (string.IsNullOrEmpty(text)) return new Size(0, TextRenderer.MeasureText(g, "Ay", font).Height);
            return TextRenderer.MeasureText(
                g,
                text,
                font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoClipping);
        }

        public void ForceClose()
        {
            _allowClose = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int topH = Math.Max(3, (int)Math.Round(3.0 * _builtDpi / 96.0));
            using (LinearGradientBrush topBar = new LinearGradientBrush(
                new Point(0, 0),
                new Point(Math.Max(1, ClientSize.Width), 0),
                ColMagenta,
                ColCyan))
            {
                ColorBlend cb = new ColorBlend(3);
                cb.Colors = new Color[] { ColMagenta, ColViolet, ColCyan };
                cb.Positions = new float[] { 0.0f, 0.5f, 1.0f };
                topBar.InterpolationColors = cb;
                e.Graphics.FillRectangle(topBar, 0, 0, ClientSize.Width, topH);
            }
        }

        private void BuildLayout(int dpiHint)
        {
            if (!IsHandleCreated) CreateHandle();
            int dpi = dpiHint >= 72 ? dpiHint : DetectCurrentDpi();
            _builtDpi = dpi;

            // Reset base Font in Points so Windows renders cleanly at the active monitor DPI and OS Text Scale
            Font bodyFont = TrackFont(new Font("Segoe UI", 9.0f, FontStyle.Regular));
            Font = bodyFont;

            using (Graphics g = CreateGraphics())
            {
                Size sampleBody = MeasureSingleLine(g, "Ay", bodyFont);
                float dpiScale = Math.Max(1.0f, dpi / 96.0f);
                float textScale = Math.Max(1.0f, sampleBody.Height / 15.0f);
                float uiScale = Math.Max(dpiScale, textScale);

                Func<float, int> S = delegate(float v96) { return Math.Max(1, (int)Math.Round(v96 * uiScale)); };

                Font titleFont = TrackFont(new Font("Segoe UI", 12.5f, FontStyle.Bold));
                Font subFont = TrackFont(new Font("Segoe UI", 8.5f, FontStyle.Regular));
                Font smallBoldFont = TrackFont(new Font("Segoe UI", 8.5f, FontStyle.Bold));
                Font boldFont = TrackFont(new Font("Segoe UI", 9.0f, FontStyle.Bold));
                Font monoFont = TrackFont(new Font("Consolas", 8.0f, FontStyle.Regular));
                Font monoBoldFont = TrackFont(new Font("Consolas", 8.5f, FontStyle.Bold));
                int sceneCount = AppSettings.Scenes.Length;

                int margin = S(16);
                int cardPadX = S(12);
                int cardPadY = S(8);
                int rowGap = S(5);
                int cardGap = S(8);
                int btnPadX = S(22);
                int btnPadY = S(10);

                // Measure all widgets upfront so container width and height fit EVERY DPI and Text Scale automatically
                Size titleSz = MeasureSingleLine(g, "NEON HORIZON", titleFont);
                Size subSz = MeasureSingleLine(g, "Live Synthwave & CRT/VHS Wallpaper", subFont);
                Size toggleSz = new Size(S(46), S(24));

                int lblSceneW = MeasureSingleLine(g, "Scene:", bodyFont).Width;
                int maxSceneW = S(160);
                for (int i = 0; i < sceneCount; i++)
                {
                    maxSceneW = Math.Max(maxSceneW, MeasureSingleLine(g, AppSettings.Scenes[i].DisplayName, boldFont).Width + S(36));
                }
                int sceneRowMinW = lblSceneW + S(12) + maxSceneW + cardPadX * 2;

                int maxPresetW = 0;
                for (int i = 0; i < _presetIds.Length; i++)
                {
                    maxPresetW = Math.Max(maxPresetW, MeasureSingleLine(g, AppSettings.Presets[i].DisplayName, smallBoldFont).Width + btnPadX);
                }
                int pGap = S(6);
                int presetRowMinW = maxPresetW * _presetIds.Length + pGap * (_presetIds.Length - 1);

                CheckBox chkBgTmp = CreateStyledCheckbox("Live background (grid flight & twinkling stars)", 0, 0);
                chkBgTmp.Font = bodyFont;
                CheckBox chkScanTmp = CreateStyledCheckbox("Scan animation (interlace, sweep bar & tape noise)", 0, 0);
                chkScanTmp.Font = bodyFont;
                CheckBox chkPauseTmp = CreateStyledCheckbox("Auto-pause when fullscreen apps or games run", 0, 0);
                chkPauseTmp.Font = bodyFont;
                CheckBox chkStartTmp = CreateStyledCheckbox("Start automatically with Windows", 0, 0);
                chkStartTmp.Font = bodyFont;
                CheckBox chkUpdTmp = CreateStyledCheckbox("Auto-check for updates", 0, 0);
                chkUpdTmp.Font = bodyFont;

                int chkAnimMaxW = Math.Max(chkBgTmp.GetPreferredSize(Size.Empty).Width, chkScanTmp.GetPreferredSize(Size.Empty).Width);
                int chkPerfMaxW = Math.Max(chkPauseTmp.GetPreferredSize(Size.Empty).Width, chkStartTmp.GetPreferredSize(Size.Empty).Width);
                int chkUpdW = chkUpdTmp.GetPreferredSize(Size.Empty).Width;

                string verStr = "v" + UpdateManager.GetCurrentVersion().Normalized;
                int lblFpsW = MeasureSingleLine(g, "FPS limit:", bodyFont).Width;
                int cmbMinW = Math.Max(S(136), MeasureSingleLine(g, "Monitor VSync", bodyFont).Width + S(30));
                int btnPauseW = Math.Max(S(80), MeasureSingleLine(g, "Resume", smallBoldFont).Width + btnPadX);
                int btnUpdateW = Math.Max(
                    S(104),
                    Math.Max(
                        MeasureSingleLine(g, "Updates", smallBoldFont).Width,
                        Math.Max(MeasureSingleLine(g, "Update v10.0.0", smallBoldFont).Width, MeasureSingleLine(g, "Checking...", smallBoldFont).Width)) + btnPadX);
                int verW = MeasureSingleLine(g, verStr, monoFont).Width + S(6);

                int perfRow1W = lblFpsW + S(10) + cmbMinW + S(16) + btnPauseW + cardPadX * 2;
                int perfRow4W = chkUpdW + S(14) + verW + S(10) + btnUpdateW + cardPadX * 2;

                int btnPreviewW = Math.Max(S(126), MeasureSingleLine(g, "Preview Window", smallBoldFont).Width + btnPadX);
                int btnResetW = Math.Max(S(88), MeasureSingleLine(g, "Reset", smallBoldFont).Width + btnPadX);
                int btnHideW = Math.Max(S(110), MeasureSingleLine(g, "Hide to Tray", boldFont).Width + btnPadX);
                int bottomBtnGap = S(8);
                int bottomRowMinW = btnPreviewW + btnResetW + btnHideW + bottomBtnGap * 2;

                int statusMinW = MeasureSingleLine(g, "Miami Sunset Drive | RaisedDesktop24H2 (DComp) | 1920x1200", monoFont).Width;

                int contentW = Math.Max(S(384), Math.Max(titleSz.Width, subSz.Width) + S(20) + toggleSz.Width);
                contentW = Math.Max(contentW, Math.Max(sceneRowMinW, presetRowMinW));
                contentW = Math.Max(contentW, Math.Max(chkAnimMaxW, chkPerfMaxW) + cardPadX * 2);
                contentW = Math.Max(contentW, Math.Max(perfRow1W, perfRow4W));
                contentW = Math.Max(contentW, bottomRowMinW);
                contentW = Math.Max(contentW, statusMinW);

                int y = S(16);

                // Header
                Label title = new Label();
                title.Text = "NEON HORIZON";
                title.Font = titleFont;
                title.ForeColor = ColCyan;
                title.AutoSize = true;
                title.Location = new Point(margin, y);
                Controls.Add(title);

                Label sub = new Label();
                sub.Text = "Live Synthwave & CRT/VHS Wallpaper";
                sub.Font = subFont;
                sub.ForeColor = ColTextMuted;
                sub.AutoSize = true;
                sub.Location = new Point(margin, y + titleSz.Height + S(2));
                Controls.Add(sub);

                int headerH = Math.Max(titleSz.Height + S(2) + subSz.Height, toggleSz.Height);
                _toggleShader = new NeonToggle();
                _toggleShader.Size = toggleSz;
                _toggleShader.Location = new Point(margin + contentW - toggleSz.Width, y + (headerH - toggleSz.Height) / 2);
                _toggleShader.CheckedChanged += delegate
                {
                    if (_updatingUi) return;
                    AppSettings s = _getSettings().Clone();
                    s.Enabled = _toggleShader.Checked;
                    _applySettings(s, true);
                    RefreshFromSettings();
                };
                Controls.Add(_toggleShader);

                y += headerH + S(10);

                // Scene / Animation selector dropdown card
                _cmbScene = new ComboBox();
                _cmbScene.DropDownStyle = ComboBoxStyle.DropDownList;
                _cmbScene.BackColor = ColBgElev;
                _cmbScene.ForeColor = ColCyan;
                _cmbScene.FlatStyle = FlatStyle.Flat;
                _cmbScene.Font = boldFont;
                for (int i = 0; i < sceneCount; i++)
                {
                    _cmbScene.Items.Add(AppSettings.Scenes[i].DisplayName);
                }
                int cmbSceneH = _cmbScene.PreferredHeight;
                int lblSceneH = MeasureSingleLine(g, "Scene:", bodyFont).Height;
                int sceneRowH = Math.Max(cmbSceneH, lblSceneH);

                Panel sceneCard = CreateCardPanel(margin, y, contentW, sceneRowH + cardPadY * 2);
                Label lblScene = new Label();
                lblScene.Text = "Scene:";
                lblScene.ForeColor = ColTextMuted;
                lblScene.Font = bodyFont;
                lblScene.AutoSize = true;
                lblScene.Location = new Point(cardPadX, cardPadY + (sceneRowH - lblSceneH) / 2);
                sceneCard.Controls.Add(lblScene);

                int cmbSceneX = cardPadX + lblSceneW + S(12);
                _cmbScene.Bounds = new Rectangle(cmbSceneX, cardPadY + (sceneRowH - cmbSceneH) / 2, contentW - cmbSceneX - cardPadX, cmbSceneH);
                _cmbScene.SelectedIndexChanged += delegate
                {
                    if (_updatingUi) return;
                    int idx = _cmbScene.SelectedIndex;
                    if (idx >= 0 && idx < AppSettings.Scenes.Length)
                    {
                        PatchSetting(delegate(AppSettings s) { s.Scene = AppSettings.Scenes[idx].Id; }, true);
                        RefreshStatus();
                    }
                };
                sceneCard.Controls.Add(_cmbScene);
                Controls.Add(sceneCard);

                y += sceneCard.Height + cardGap;

                // Presets grid
                int presetBtnH = Math.Max(S(29), MeasureSingleLine(g, "Ay", smallBoldFont).Height + btnPadY);
                _presetBtns = new Button[_presetIds.Length];
                int pBtnW = (contentW - pGap * (_presetIds.Length - 1)) / _presetIds.Length;
                for (int i = 0; i < _presetIds.Length; i++)
                {
                    string pid = _presetIds[i];
                    int curW = (i == _presetIds.Length - 1) ? (contentW - i * (pBtnW + pGap)) : pBtnW;
                    Button btn = CreateActionButton(AppSettings.Presets[i].DisplayName, ColBgCard, ColTextMuted, margin + i * (pBtnW + pGap), y, curW, presetBtnH, smallBoldFont);
                    btn.FlatAppearance.BorderColor = Color.FromArgb(120, ColViolet);
                    btn.Tag = pid;
                    btn.Click += delegate(object sender, EventArgs ev)
                    {
                        Button clicked = sender as Button;
                        if (clicked == null) return;
                        AppSettings s = _getSettings().Clone();
                        if (s.ApplyPreset((string)clicked.Tag))
                        {
                            _applySettings(s, true);
                            RefreshFromSettings();
                        }
                    };
                    _presetBtns[i] = btn;
                    Controls.Add(btn);
                }

                y += presetBtnH + S(10);

                // Animation checkboxes card
                Panel animCard = CreateCardPanel(margin, y, contentW, 10);
                int ay = S(8);
                _chkAnimateBg = chkBgTmp;
                _chkAnimateBg.Location = new Point(cardPadX, ay);
                _chkAnimateBg.CheckedChanged += delegate
                {
                    if (_updatingUi) return;
                    AppSettings s = _getSettings().Clone();
                    s.AnimateBg = _chkAnimateBg.Checked;
                    _applySettings(s, true);
                };
                animCard.Controls.Add(_chkAnimateBg);
                ay += _chkAnimateBg.GetPreferredSize(Size.Empty).Height + S(4);

                _chkAnimateScan = chkScanTmp;
                _chkAnimateScan.Location = new Point(cardPadX, ay);
                _chkAnimateScan.CheckedChanged += delegate
                {
                    if (_updatingUi) return;
                    AppSettings s = _getSettings().Clone();
                    s.Animate = _chkAnimateScan.Checked;
                    s.Preset = "custom";
                    _applySettings(s, true);
                    RefreshPresetButtons(s.Preset);
                };
                animCard.Controls.Add(_chkAnimateScan);
                ay += _chkAnimateScan.GetPreferredSize(Size.Empty).Height + S(8);
                animCard.Height = ay;
                Controls.Add(animCard);

                y += animCard.Height + cardGap;

                // CRT / VHS Sliders Card
                Panel sliderCard = CreateCardPanel(margin, y, contentW, 10);
                int sy = cardPadY;
                _sldCurvature = AddSliderRow(sliderCard, g, "Screen curvature (CRT)", 0, 100, subFont, monoBoldFont, cardPadX, S, ref sy, out _valCurvature, delegate(int v, bool commit)
                {
                    PatchSetting(delegate(AppSettings s) { s.Curvature = v; s.Preset = "custom"; }, commit);
                });
                _sldScanlines = AddSliderRow(sliderCard, g, "Interlaced scanlines", 0, 100, subFont, monoBoldFont, cardPadX, S, ref sy, out _valScanlines, delegate(int v, bool commit)
                {
                    PatchSetting(delegate(AppSettings s) { s.Scanlines = v; s.Preset = "custom"; }, commit);
                });
                _sldBloom = AddSliderRow(sliderCard, g, "Phosphor bloom glow", 0, 100, subFont, monoBoldFont, cardPadX, S, ref sy, out _valBloom, delegate(int v, bool commit)
                {
                    PatchSetting(delegate(AppSettings s) { s.Bloom = v; s.Preset = "custom"; }, commit);
                });
                _sldVhs = AddSliderRow(sliderCard, g, "VHS tape & chromaticity", 0, 100, subFont, monoBoldFont, cardPadX, S, ref sy, out _valVhs, delegate(int v, bool commit)
                {
                    PatchSetting(delegate(AppSettings s) { s.Vhs = v; s.Preset = "custom"; }, commit);
                });
                _sldSweep = AddSliderRow(sliderCard, g, "Scan sweep bar", 0, 100, subFont, monoBoldFont, cardPadX, S, ref sy, out _valSweep, delegate(int v, bool commit)
                {
                    PatchSetting(delegate(AppSettings s) { s.Sweep = v; s.Preset = "custom"; }, commit);
                });
                _sldVignette = AddSliderRow(sliderCard, g, "Glass & vignette", 0, 100, subFont, monoBoldFont, cardPadX, S, ref sy, out _valVignette, delegate(int v, bool commit)
                {
                    PatchSetting(delegate(AppSettings s) { s.Vignette = v; s.Preset = "custom"; }, commit);
                });
                _sldGridSpeed = AddSliderRow(sliderCard, g, "Grid flight speed", 25, 250, subFont, monoBoldFont, cardPadX, S, ref sy, out _valGridSpeed, delegate(int v, bool commit)
                {
                    PatchSetting(delegate(AppSettings s) { s.GridSpeed = v; }, commit);
                });
                sliderCard.Height = sy + S(2);
                Controls.Add(sliderCard);

                y += sliderCard.Height + cardGap;

                // Desktop, Performance & Updates Card (height dynamically matches all rows + button height)
                Panel perfCard = CreateCardPanel(margin, y, contentW, 10);
                int smallBtnH = Math.Max(S(27), MeasureSingleLine(g, "Updates", smallBoldFont).Height + btnPadY);
                int py = cardPadY;

                _cmbFps = new ComboBox();
                _cmbFps.DropDownStyle = ComboBoxStyle.DropDownList;
                _cmbFps.BackColor = ColBgElev;
                _cmbFps.ForeColor = ColTextMain;
                _cmbFps.FlatStyle = FlatStyle.Flat;
                _cmbFps.Font = bodyFont;
                _cmbFps.Items.AddRange(new object[] { "24 FPS", "30 FPS", "60 FPS", "120 FPS", "Monitor VSync" });
                _cmbFps.Width = cmbMinW;
                int cmbH = _cmbFps.PreferredHeight;

                int fpsRowH = Math.Max(smallBtnH, Math.Max(cmbH, MeasureSingleLine(g, "FPS limit:", bodyFont).Height));
                Label lblFps = new Label();
                lblFps.Text = "FPS limit:";
                lblFps.ForeColor = ColTextMuted;
                lblFps.Font = bodyFont;
                lblFps.AutoSize = true;
                int lblFpsH = MeasureSingleLine(g, "FPS limit:", bodyFont).Height;
                lblFps.Location = new Point(cardPadX, py + (fpsRowH - lblFpsH) / 2);
                perfCard.Controls.Add(lblFps);

                _cmbFps.Location = new Point(cardPadX + lblFpsW + S(10), py + (fpsRowH - cmbH) / 2);
                _cmbFps.SelectedIndexChanged += delegate
                {
                    if (_updatingUi) return;
                    int fps = 24;
                    if (_cmbFps.SelectedIndex == 0) fps = 24;
                    else if (_cmbFps.SelectedIndex == 1) fps = 30;
                    else if (_cmbFps.SelectedIndex == 2) fps = 60;
                    else if (_cmbFps.SelectedIndex == 3) fps = 120;
                    else if (_cmbFps.SelectedIndex == 4) fps = 0;
                    PatchSetting(delegate(AppSettings s) { s.TargetFps = fps; }, true);
                };
                perfCard.Controls.Add(_cmbFps);

                _btnPause = CreateActionButton("Pause", ColBgElev, ColCyan, contentW - cardPadX - btnPauseW, py + (fpsRowH - smallBtnH) / 2, btnPauseW, smallBtnH, smallBoldFont);
                _btnPause.Click += delegate
                {
                    _togglePause();
                    RefreshStatus();
                };
                perfCard.Controls.Add(_btnPause);

                py += fpsRowH + S(8);

                _chkPauseFs = chkPauseTmp;
                _chkPauseFs.Location = new Point(cardPadX, py);
                _chkPauseFs.CheckedChanged += delegate
                {
                    if (_updatingUi) return;
                    PatchSetting(delegate(AppSettings s) { s.PauseOnFullscreen = _chkPauseFs.Checked; }, true);
                };
                perfCard.Controls.Add(_chkPauseFs);
                py += _chkPauseFs.GetPreferredSize(Size.Empty).Height + S(4);

                _chkStartup = chkStartTmp;
                _chkStartup.Location = new Point(cardPadX, py);
                _chkStartup.CheckedChanged += delegate
                {
                    if (_updatingUi) return;
                    StartupManager.SetEnabled(_chkStartup.Checked, Application.ExecutablePath);
                };
                perfCard.Controls.Add(_chkStartup);
                py += _chkStartup.GetPreferredSize(Size.Empty).Height + S(6);

                int chkUpdH = chkUpdTmp.GetPreferredSize(Size.Empty).Height;
                int updRowH = Math.Max(smallBtnH, chkUpdH);

                _chkAutoUpdate = chkUpdTmp;
                _chkAutoUpdate.Location = new Point(cardPadX, py + (updRowH - chkUpdH) / 2);
                _chkAutoUpdate.CheckedChanged += delegate
                {
                    if (_updatingUi) return;
                    PatchSetting(delegate(AppSettings s) { s.AutoCheckUpdates = _chkAutoUpdate.Checked; }, true);
                };
                perfCard.Controls.Add(_chkAutoUpdate);

                _btnCheckUpdate = CreateActionButton("Updates", ColBgElev, ColCyan, contentW - cardPadX - btnUpdateW, py + (updRowH - smallBtnH) / 2, btnUpdateW, smallBtnH, smallBoldFont);
                _btnCheckUpdate.Click += delegate
                {
                    if (_triggerUpdateAction != null) _triggerUpdateAction();
                };
                perfCard.Controls.Add(_btnCheckUpdate);

                _lblVersion = new Label();
                _lblVersion.Text = verStr;
                _lblVersion.ForeColor = ColTextMuted;
                _lblVersion.Font = monoFont;
                _lblVersion.TextAlign = ContentAlignment.MiddleRight;
                int lblVerH = Math.Max(S(20), MeasureSingleLine(g, verStr, monoFont).Height + S(2));
                _lblVersion.Bounds = new Rectangle(_btnCheckUpdate.Left - S(8) - verW, py + (updRowH - lblVerH) / 2, verW, lblVerH);
                perfCard.Controls.Add(_lblVersion);

                py += updRowH + cardPadY;
                perfCard.Height = py;
                Controls.Add(perfCard);

                y += perfCard.Height + cardGap;

                // Bottom action buttons (sized from measured text so Preview Window & Hide to Tray never wrap onto 2 lines)
                int actionBtnH = Math.Max(S(30), MeasureSingleLine(g, "Preview Window", boldFont).Height + btnPadY);
                Button btnPreview = CreateActionButton("Preview Window", ColBgElev, ColCyan, margin, y, btnPreviewW, actionBtnH, smallBoldFont);
                btnPreview.Click += delegate { _openPreview(); };
                Controls.Add(btnPreview);

                Button btnReset = CreateActionButton("Reset", Color.FromArgb(40, 18, 66), ColMagenta, btnPreview.Right + bottomBtnGap, y, btnResetW, actionBtnH, smallBoldFont);
                btnReset.Click += delegate
                {
                    _resetSettings();
                    RefreshFromSettings();
                };
                Controls.Add(btnReset);

                Button btnHide = CreateActionButton("Hide to Tray", Color.FromArgb(0, 195, 220), ColBgDeep, margin + contentW - btnHideW, y, btnHideW, actionBtnH, boldFont);
                btnHide.Click += delegate { Hide(); };
                Controls.Add(btnHide);

                y += actionBtnH + S(8);

                _lblStatus = new Label();
                _lblStatus.ForeColor = ColTextMuted;
                _lblStatus.Font = monoFont;
                _lblStatus.AutoEllipsis = true;
                int statusH = Math.Max(S(20), MeasureSingleLine(g, "Ay", monoFont).Height + S(4));
                _lblStatus.Bounds = new Rectangle(margin, y, contentW, statusH);
                Controls.Add(_lblStatus);

                int reqW = contentW + margin * 2;
                int reqH = _lblStatus.Bottom + margin;

                Rectangle work = Screen.FromControl(this) != null
                    ? Screen.FromControl(this).WorkingArea
                    : (Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : new Rectangle(0, 0, 1280, 800));
                int maxW = Math.Max(320, work.Width - 24);
                int maxH = Math.Max(240, work.Height - 40);

                if (reqH > maxH || reqW > maxW)
                {
                    AutoScroll = true;
                    AutoScrollMinSize = new Size(reqW, reqH);
                    int cw = Math.Min(maxW, reqW + (reqH > maxH ? SystemInformation.VerticalScrollBarWidth : 0));
                    int ch = Math.Min(maxH, reqH);
                    ClientSize = new Size(cw, ch);
                }
                else
                {
                    AutoScroll = false;
                    ClientSize = new Size(reqW, reqH);
                }
            }
        }

        private Panel CreateCardPanel(int x, int y, int w, int h)
        {
            Panel p = new Panel();
            p.BackColor = ColBgCard;
            p.Bounds = new Rectangle(x, y, w, h);
            p.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Color.FromArgb(72, ColViolet), 1f))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
                }
            };
            return p;
        }

        private CheckBox CreateStyledCheckbox(string text, int x, int y)
        {
            CheckBox chk = new CheckBox();
            chk.Text = text;
            chk.ForeColor = ColTextMain;
            chk.AutoSize = true;
            chk.Location = new Point(x, y);
            chk.Cursor = Cursors.Hand;
            return chk;
        }

        private Button CreateActionButton(string text, Color back, Color fore, int x, int y, int w, int h, Font font)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(120, fore);
            b.BackColor = back;
            b.ForeColor = fore;
            b.Font = font;
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.Padding = Padding.Empty;
            b.UseCompatibleTextRendering = false;
            b.Cursor = Cursors.Hand;
            b.Bounds = new Rectangle(x, y, w, h);
            return b;
        }

        private NeonSlider AddSliderRow(
            Panel parent,
            Graphics g,
            string title,
            int min,
            int max,
            Font titleFont,
            Font valueFont,
            int padX,
            Func<float, int> S,
            ref int y,
            out Label readout,
            Action<int, bool> onChange)
        {
            int w = parent.Width - padX * 2;
            int lblH = Math.Max(MeasureSingleLine(g, title, titleFont).Height, MeasureSingleLine(g, "250%", valueFont).Height);
            int valW = Math.Max(S(58), MeasureSingleLine(g, "250%", valueFont).Width + S(8));

            Label lbl = new Label();
            lbl.Text = title;
            lbl.ForeColor = ColTextMain;
            lbl.Font = titleFont;
            lbl.AutoSize = true;
            lbl.Location = new Point(padX, y);
            parent.Controls.Add(lbl);

            Label valLbl = new Label();
            valLbl.Text = "0%";
            valLbl.ForeColor = ColCyan;
            valLbl.Font = valueFont;
            valLbl.TextAlign = ContentAlignment.TopRight;
            valLbl.Bounds = new Rectangle(parent.Width - padX - valW, y, valW, lblH);
            parent.Controls.Add(valLbl);
            readout = valLbl;

            int sliderH = S(18);
            NeonSlider slider = new NeonSlider();
            slider.Minimum = min;
            slider.Maximum = max;
            slider.Bounds = new Rectangle(padX, y + lblH + S(1), w, sliderH);
            slider.ValueChanged += delegate
            {
                valLbl.Text = slider.Value + "%";
                if (!_updatingUi) onChange(slider.Value, false);
            };
            slider.ValueCommitted += delegate
            {
                if (!_updatingUi) onChange(slider.Value, true);
            };
            parent.Controls.Add(slider);

            y += lblH + S(1) + sliderH + S(4);
            return slider;
        }

        private void PatchSetting(Action<AppSettings> mutator, bool save)
        {
            AppSettings s = _getSettings().Clone();
            mutator(s);
            _applySettings(s, save);
            RefreshPresetButtons(s.Preset);
        }

        public void RefreshFromSettings()
        {
            _updatingUi = true;
            try
            {
                AppSettings s = _getSettings();
                _toggleShader.Checked = s.Enabled;
                _chkAnimateBg.Checked = s.AnimateBg;
                _chkAnimateScan.Checked = s.Animate;
                _chkPauseFs.Checked = s.PauseOnFullscreen;
                _chkStartup.Checked = StartupManager.IsEnabled();
                _chkAutoUpdate.Checked = s.AutoCheckUpdates;

                _sldCurvature.Value = s.Curvature;
                _valCurvature.Text = s.Curvature + "%";
                _sldScanlines.Value = s.Scanlines;
                _valScanlines.Text = s.Scanlines + "%";
                _sldBloom.Value = s.Bloom;
                _valBloom.Text = s.Bloom + "%";
                _sldVhs.Value = s.Vhs;
                _valVhs.Text = s.Vhs + "%";
                _sldSweep.Value = s.Sweep;
                _valSweep.Text = s.Sweep + "%";
                _sldVignette.Value = s.Vignette;
                _valVignette.Text = s.Vignette + "%";
                _sldGridSpeed.Value = s.GridSpeed;
                _valGridSpeed.Text = s.GridSpeed + "%";

                if (s.TargetFps == 24) _cmbFps.SelectedIndex = 0;
                else if (s.TargetFps == 30) _cmbFps.SelectedIndex = 1;
                else if (s.TargetFps == 60) _cmbFps.SelectedIndex = 2;
                else if (s.TargetFps == 120) _cmbFps.SelectedIndex = 3;
                else _cmbFps.SelectedIndex = 4;

                RefreshSceneSelection(s.Scene);
                RefreshPresetButtons(s.Preset);
                RefreshStatus();
            }
            finally
            {
                _updatingUi = false;
            }
        }

        private void RefreshSceneSelection(string activeScene)
        {
            if (_cmbScene == null || _cmbScene.Items.Count == 0) return;
            int foundIdx = 0;
            for (int i = 0; i < AppSettings.Scenes.Length; i++)
            {
                if (string.Equals(AppSettings.Scenes[i].Id, activeScene, StringComparison.OrdinalIgnoreCase))
                {
                    foundIdx = i;
                    break;
                }
            }
            if (foundIdx < _cmbScene.Items.Count)
            {
                _cmbScene.SelectedIndex = foundIdx;
            }
        }

        private void RefreshPresetButtons(string activePreset)
        {
            for (int i = 0; i < _presetBtns.Length; i++)
            {
                bool active = string.Equals((string)_presetBtns[i].Tag, activePreset, StringComparison.OrdinalIgnoreCase);
                _presetBtns[i].BackColor = active ? Color.FromArgb(44, 24, 86) : ColBgCard;
                _presetBtns[i].ForeColor = active ? Color.White : ColTextMuted;
                _presetBtns[i].FlatAppearance.BorderColor = active ? ColCyan : Color.FromArgb(120, ColViolet);
            }
        }

        public void RefreshStatus()
        {
            bool paused = _isPaused();
            _btnPause.Text = paused ? "Resume" : "Pause";
            _lblStatus.Text = _getStatusSummary();
        }

        public void SetUpdateUiState(string buttonText, bool highlightAvailable, bool busy)
        {
            _lastUpdateBtnText = string.IsNullOrEmpty(buttonText) ? "Updates" : buttonText;
            _lastUpdateHighlight = highlightAvailable;
            _lastUpdateBusy = busy;
            if (_btnCheckUpdate == null || _btnCheckUpdate.IsDisposed) return;
            _btnCheckUpdate.Text = _lastUpdateBtnText;
            _btnCheckUpdate.Enabled = !busy;
            if (highlightAvailable)
            {
                _btnCheckUpdate.BackColor = Color.FromArgb(46, 16, 74);
                _btnCheckUpdate.ForeColor = ColMagenta;
                _btnCheckUpdate.FlatAppearance.BorderColor = ColCyan;
            }
            else
            {
                _btnCheckUpdate.BackColor = ColBgElev;
                _btnCheckUpdate.ForeColor = ColCyan;
                _btnCheckUpdate.FlatAppearance.BorderColor = Color.FromArgb(120, ColCyan);
            }
        }
    }
}
