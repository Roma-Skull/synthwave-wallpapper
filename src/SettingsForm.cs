using System;
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
            int pad = 8;
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

            int pad = 8;
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
        private ComboBox _cmbFps;
        private Label _lblStatus;
        private Label _lblVersion;
        private Button _btnPause;
        private Button _btnCheckUpdate;

        private Button[] _sceneBtns;
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

        private bool _updatingUi;
        private bool _allowClose;

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
            Font = new Font("Segoe UI", 9.0f, FontStyle.Regular);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(420, 712);

            BuildLayout();
            RefreshFromSettings();
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
            using (LinearGradientBrush topBar = new LinearGradientBrush(
                new Point(0, 0),
                new Point(ClientSize.Width, 0),
                ColMagenta,
                ColCyan))
            {
                ColorBlend cb = new ColorBlend(3);
                cb.Colors = new Color[] { ColMagenta, ColViolet, ColCyan };
                cb.Positions = new float[] { 0.0f, 0.5f, 1.0f };
                topBar.InterpolationColors = cb;
                e.Graphics.FillRectangle(topBar, 0, 0, ClientSize.Width, 3);
            }
        }

        private void BuildLayout()
        {
            int margin = 18;
            int contentW = ClientSize.Width - margin * 2;
            int y = 16;

            // Header
            Label title = new Label();
            title.Text = "NEON HORIZON";
            title.Font = new Font("Segoe UI", 12.5f, FontStyle.Bold);
            title.ForeColor = ColCyan;
            title.AutoSize = true;
            title.Location = new Point(margin, y);
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = "Live Synthwave & CRT/VHS Wallpaper";
            sub.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            sub.ForeColor = ColTextMuted;
            sub.AutoSize = true;
            sub.Location = new Point(margin, y + 23);
            Controls.Add(sub);

            _toggleShader = new NeonToggle();
            _toggleShader.Location = new Point(ClientSize.Width - margin - _toggleShader.Width, y + 8);
            _toggleShader.CheckedChanged += delegate
            {
                if (_updatingUi) return;
                AppSettings s = _getSettings().Clone();
                s.Enabled = _toggleShader.Checked;
                _applySettings(s, true);
                RefreshFromSettings();
            };
            Controls.Add(_toggleShader);

            y += 50;

            // Scene / Animation selector row
            int sceneCount = AppSettings.Scenes.Length;
            _sceneBtns = new Button[sceneCount];
            int sGap = sceneCount > 2 ? 6 : 8;
            int sBtnW = (contentW - sGap * (sceneCount - 1)) / sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                SceneDefinition sd = AppSettings.Scenes[i];
                Button sBtn = new Button();
                sBtn.Text = sd.DisplayName;
                sBtn.FlatStyle = FlatStyle.Flat;
                sBtn.FlatAppearance.BorderSize = 1;
                sBtn.FlatAppearance.BorderColor = Color.FromArgb(120, ColViolet);
                sBtn.BackColor = ColBgCard;
                sBtn.ForeColor = ColTextMuted;
                sBtn.Font = new Font("Segoe UI", sceneCount > 2 ? 8.25f : 9.0f, FontStyle.Bold);
                sBtn.Cursor = Cursors.Hand;
                sBtn.Bounds = new Rectangle(margin + i * (sBtnW + sGap), y, sBtnW, 30);
                sBtn.Tag = sd.Id;
                sBtn.Click += delegate(object sender, EventArgs ev)
                {
                    Button clicked = sender as Button;
                    if (clicked == null) return;
                    AppSettings s = _getSettings().Clone();
                    s.Scene = (string)clicked.Tag;
                    _applySettings(s, true);
                    RefreshFromSettings();
                };
                _sceneBtns[i] = sBtn;
                Controls.Add(sBtn);
            }

            y += 38;

            // Presets grid
            _presetBtns = new Button[_presetIds.Length];
            int gap = 6;
            int btnW = (contentW - gap * 3) / 4;
            for (int i = 0; i < _presetIds.Length; i++)
            {
                string pid = _presetIds[i];
                Button btn = new Button();
                btn.Text = AppSettings.Presets[i].DisplayName;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = Color.FromArgb(120, ColViolet);
                btn.BackColor = ColBgCard;
                btn.ForeColor = ColTextMuted;
                btn.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                btn.Cursor = Cursors.Hand;
                btn.Bounds = new Rectangle(margin + i * (btnW + gap), y, btnW, 29);
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

            y += 38;

            // Animation checkboxes row
            Panel animCard = CreateCardPanel(margin, y, contentW, 56);
            _chkAnimateBg = CreateStyledCheckbox("Live background (grid flight & twinkling stars)", 10, 7);
            _chkAnimateBg.CheckedChanged += delegate
            {
                if (_updatingUi) return;
                AppSettings s = _getSettings().Clone();
                s.AnimateBg = _chkAnimateBg.Checked;
                _applySettings(s, true);
            };
            animCard.Controls.Add(_chkAnimateBg);

            _chkAnimateScan = CreateStyledCheckbox("Scan animation (interlace, sweep bar & tape noise)", 10, 30);
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
            Controls.Add(animCard);

            y += 66;

            // CRT / VHS Sliders Card
            Panel sliderCard = CreateCardPanel(margin, y, contentW, 296);
            int sy = 10;
            _sldCurvature = AddSliderRow(sliderCard, "Screen curvature (CRT)", 0, 100, ref sy, out _valCurvature, delegate(int v, bool commit)
            {
                PatchSetting(delegate(AppSettings s) { s.Curvature = v; s.Preset = "custom"; }, commit);
            });
            _sldScanlines = AddSliderRow(sliderCard, "Interlaced scanlines", 0, 100, ref sy, out _valScanlines, delegate(int v, bool commit)
            {
                PatchSetting(delegate(AppSettings s) { s.Scanlines = v; s.Preset = "custom"; }, commit);
            });
            _sldBloom = AddSliderRow(sliderCard, "Phosphor bloom glow", 0, 100, ref sy, out _valBloom, delegate(int v, bool commit)
            {
                PatchSetting(delegate(AppSettings s) { s.Bloom = v; s.Preset = "custom"; }, commit);
            });
            _sldVhs = AddSliderRow(sliderCard, "VHS tape & chromaticity", 0, 100, ref sy, out _valVhs, delegate(int v, bool commit)
            {
                PatchSetting(delegate(AppSettings s) { s.Vhs = v; s.Preset = "custom"; }, commit);
            });
            _sldSweep = AddSliderRow(sliderCard, "Scan sweep bar", 0, 100, ref sy, out _valSweep, delegate(int v, bool commit)
            {
                PatchSetting(delegate(AppSettings s) { s.Sweep = v; s.Preset = "custom"; }, commit);
            });
            _sldVignette = AddSliderRow(sliderCard, "Glass & vignette", 0, 100, ref sy, out _valVignette, delegate(int v, bool commit)
            {
                PatchSetting(delegate(AppSettings s) { s.Vignette = v; s.Preset = "custom"; }, commit);
            });
            _sldGridSpeed = AddSliderRow(sliderCard, "Grid flight speed", 25, 250, ref sy, out _valGridSpeed, delegate(int v, bool commit)
            {
                PatchSetting(delegate(AppSettings s) { s.GridSpeed = v; }, commit);
            });
            Controls.Add(sliderCard);

            y += 306;

            // Desktop & Performance Card
            Panel perfCard = CreateCardPanel(margin, y, contentW, 114);

            Label lblFps = new Label();
            lblFps.Text = "FPS limit:";
            lblFps.ForeColor = ColTextMuted;
            lblFps.AutoSize = true;
            lblFps.Location = new Point(10, 12);
            perfCard.Controls.Add(lblFps);

            _cmbFps = new ComboBox();
            _cmbFps.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbFps.BackColor = ColBgElev;
            _cmbFps.ForeColor = ColTextMain;
            _cmbFps.FlatStyle = FlatStyle.Flat;
            _cmbFps.Items.AddRange(new object[] { "30 FPS", "60 FPS", "120 FPS", "Monitor VSync" });
            _cmbFps.Bounds = new Rectangle(82, 8, 124, 24);
            _cmbFps.SelectedIndexChanged += delegate
            {
                if (_updatingUi) return;
                int fps = 30;
                if (_cmbFps.SelectedIndex == 0) fps = 30;
                else if (_cmbFps.SelectedIndex == 1) fps = 60;
                else if (_cmbFps.SelectedIndex == 2) fps = 120;
                else if (_cmbFps.SelectedIndex == 3) fps = 0;
                PatchSetting(delegate(AppSettings s) { s.TargetFps = fps; }, true);
            };
            perfCard.Controls.Add(_cmbFps);

            _btnPause = CreateActionButton("Pause", ColBgElev, ColCyan, 294, 7, 80, 25);
            _btnPause.Click += delegate
            {
                _togglePause();
                RefreshStatus();
            };
            perfCard.Controls.Add(_btnPause);

            _chkPauseFs = CreateStyledCheckbox("Auto-pause when fullscreen apps or games run", 10, 39);
            _chkPauseFs.CheckedChanged += delegate
            {
                if (_updatingUi) return;
                PatchSetting(delegate(AppSettings s) { s.PauseOnFullscreen = _chkPauseFs.Checked; }, true);
            };
            perfCard.Controls.Add(_chkPauseFs);

            _chkStartup = CreateStyledCheckbox("Start automatically with Windows", 10, 62);
            _chkStartup.CheckedChanged += delegate
            {
                if (_updatingUi) return;
                StartupManager.SetEnabled(_chkStartup.Checked, Application.ExecutablePath);
            };
            perfCard.Controls.Add(_chkStartup);

            _chkAutoUpdate = CreateStyledCheckbox("Auto-check for updates", 10, 86);
            _chkAutoUpdate.CheckedChanged += delegate
            {
                if (_updatingUi) return;
                PatchSetting(delegate(AppSettings s) { s.AutoCheckUpdates = _chkAutoUpdate.Checked; }, true);
            };
            perfCard.Controls.Add(_chkAutoUpdate);

            _lblVersion = new Label();
            _lblVersion.Text = "v" + UpdateManager.GetCurrentVersion().Normalized;
            _lblVersion.ForeColor = ColTextMuted;
            _lblVersion.Font = new Font("Consolas", 8.0f, FontStyle.Regular);
            _lblVersion.TextAlign = ContentAlignment.MiddleRight;
            _lblVersion.Bounds = new Rectangle(194, 86, 64, 20);
            perfCard.Controls.Add(_lblVersion);

            _btnCheckUpdate = CreateActionButton("Updates", ColBgElev, ColCyan, 264, 83, 110, 24);
            _btnCheckUpdate.Click += delegate
            {
                if (_triggerUpdateAction != null) _triggerUpdateAction();
            };
            perfCard.Controls.Add(_btnCheckUpdate);

            Controls.Add(perfCard);

            y += 124;

            // Bottom action buttons
            Button btnPreview = CreateActionButton("Preview Window", ColBgElev, ColCyan, margin, y, 126, 30);
            btnPreview.Click += delegate { _openPreview(); };
            Controls.Add(btnPreview);

            Button btnReset = CreateActionButton("Reset", Color.FromArgb(40, 18, 66), ColMagenta, margin + 134, y, 88, 30);
            btnReset.Click += delegate
            {
                _resetSettings();
                RefreshFromSettings();
            };
            Controls.Add(btnReset);

            Button btnHide = CreateActionButton("Hide to Tray", Color.FromArgb(0, 195, 220), ColBgDeep, ClientSize.Width - margin - 110, y, 110, 30);
            btnHide.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            btnHide.Click += delegate { Hide(); };
            Controls.Add(btnHide);

            y += 38;

            _lblStatus = new Label();
            _lblStatus.ForeColor = ColTextMuted;
            _lblStatus.Font = new Font("Consolas", 8.0f, FontStyle.Regular);
            _lblStatus.Bounds = new Rectangle(margin, y, contentW, 20);
            Controls.Add(_lblStatus);
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

        private Button CreateActionButton(string text, Color back, Color fore, int x, int y, int w, int h)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(120, fore);
            b.BackColor = back;
            b.ForeColor = fore;
            b.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.Bounds = new Rectangle(x, y, w, h);
            return b;
        }

        private NeonSlider AddSliderRow(
            Panel parent,
            string title,
            int min,
            int max,
            ref int y,
            out Label readout,
            Action<int, bool> onChange)
        {
            int padX = 12;
            int w = parent.Width - padX * 2;

            Label lbl = new Label();
            lbl.Text = title;
            lbl.ForeColor = ColTextMain;
            lbl.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lbl.AutoSize = true;
            lbl.Location = new Point(padX, y);
            parent.Controls.Add(lbl);

            Label valLbl = new Label();
            valLbl.Text = "0%";
            valLbl.ForeColor = ColCyan;
            valLbl.Font = new Font("Consolas", 8.5f, FontStyle.Bold);
            valLbl.TextAlign = ContentAlignment.TopRight;
            valLbl.Bounds = new Rectangle(parent.Width - padX - 58, y, 58, 16);
            parent.Controls.Add(valLbl);
            readout = valLbl;

            NeonSlider slider = new NeonSlider();
            slider.Minimum = min;
            slider.Maximum = max;
            slider.Bounds = new Rectangle(padX, y + 16, w, 19);
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

            y += 40;
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

                if (s.TargetFps == 30) _cmbFps.SelectedIndex = 0;
                else if (s.TargetFps == 60) _cmbFps.SelectedIndex = 1;
                else if (s.TargetFps == 120) _cmbFps.SelectedIndex = 2;
                else _cmbFps.SelectedIndex = 3;

                RefreshSceneButtons(s.Scene);
                RefreshPresetButtons(s.Preset);
                RefreshStatus();
            }
            finally
            {
                _updatingUi = false;
            }
        }

        private void RefreshSceneButtons(string activeScene)
        {
            if (_sceneBtns == null) return;
            for (int i = 0; i < _sceneBtns.Length; i++)
            {
                bool active = string.Equals((string)_sceneBtns[i].Tag, activeScene, StringComparison.OrdinalIgnoreCase);
                _sceneBtns[i].BackColor = active ? Color.FromArgb(20, 48, 86) : ColBgCard;
                _sceneBtns[i].ForeColor = active ? ColCyan : ColTextMuted;
                _sceneBtns[i].FlatAppearance.BorderColor = active ? ColCyan : Color.FromArgb(120, ColViolet);
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
            if (_btnCheckUpdate == null || _btnCheckUpdate.IsDisposed) return;
            _btnCheckUpdate.Text = string.IsNullOrEmpty(buttonText) ? "Updates" : buttonText;
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
