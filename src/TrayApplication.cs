using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NeonHorizonWallpaper
{
    public sealed class TrayApplication : ApplicationContext
    {
        private const string SingleInstanceWindowTitle = "NeonHorizonWallpaper_IPC_MsgWindow";
        public const uint WM_SHOW_SETTINGS = NativeMethods.WM_USER + 42;
        public const uint WM_OPEN_PREVIEW = NativeMethods.WM_USER + 43;
        public const uint WM_EXIT_APP = NativeMethods.WM_USER + 44;
        public const uint WM_REBUILD_DESKTOP = NativeMethods.WM_USER + 45;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public NativeMethods.POINT pt;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uPeriod);

        private sealed class IpcMessageWindow : NativeWindow
        {
            private readonly TrayApplication _app;

            public IpcMessageWindow(TrayApplication app)
            {
                _app = app;
                CreateParams cp = new CreateParams();
                cp.Caption = SingleInstanceWindowTitle;
                CreateHandle(cp);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == (int)WM_SHOW_SETTINGS)
                {
                    _app.ShowSettings();
                    return;
                }
                if (m.Msg == (int)WM_OPEN_PREVIEW)
                {
                    _app.OpenPreviewWindow();
                    return;
                }
                if (m.Msg == (int)WM_EXIT_APP)
                {
                    _app.ExitGracefully();
                    return;
                }
                if (m.Msg == (int)WM_REBUILD_DESKTOP)
                {
                    _app.HandleDesktopTopologyRefresh();
                    return;
                }
                base.WndProc(ref m);
            }
        }

        private readonly bool _previewOnly;
        private readonly Icon _appIcon;
        private readonly NotifyIcon _trayIcon;
        private readonly D3D11Renderer _renderer;
        private readonly List<WallpaperWindow> _desktopWindows = new List<WallpaperWindow>();
        private readonly List<WallpaperWindow> _previewWindows = new List<WallpaperWindow>();
        private readonly System.Windows.Forms.Timer _watchdogTimer;
        private readonly Stopwatch _clock = new Stopwatch();

        private IpcMessageWindow _ipcWindow;
        private DesktopHostTarget _desktopTarget;
        private AppSettings _settings;
        private SettingsForm _settingsForm;

        private ToolStripMenuItem _menuPause;
        private ToolStripMenuItem _menuShaderToggle;
        private ToolStripMenuItem[] _menuScenes;
        private ToolStripMenuItem[] _menuPresets;

        private bool _manualPaused;
        private bool _autoPausedFullscreen;
        private bool _timePeriodActive;
        private bool _needsStaticRedraw = true;
        private bool _exiting;

        private long _framesRendered;
        private double _lastTickSec;
        private double _lastPresentSec;
        private float _sceneTime;
        private float _gridTime;
        private float _crtTime;

        public TrayApplication(bool openSettingsOnStart, bool openPreviewOnStart, bool previewOnly)
        {
            Logger.Reset();
            _previewOnly = previewOnly;
            _settings = AppSettings.Load();
            _appIcon = LoadAppIcon();

            _renderer = new D3D11Renderer();
            string err;
            if (!_renderer.Initialize(out err))
            {
                Logger.Error("Renderer Initialize failed: " + err);
                MessageBox.Show(
                    "Unable to initialize Direct3D 11 shader pipeline:\n" + err,
                    "Neon Horizon Wallpaper",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                throw new InvalidOperationException(err);
            }
            Logger.Info("Renderer initialized. HasDirectComposition=" + _renderer.HasDirectComposition);

            _ipcWindow = new IpcMessageWindow(this);
            _trayIcon = BuildTrayIcon();

            if (!_previewOnly)
            {
                RebuildDesktopWindows(true);
            }

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            _watchdogTimer = new System.Windows.Forms.Timer();
            _watchdogTimer.Interval = 1200;
            _watchdogTimer.Tick += OnWatchdogTick;
            _watchdogTimer.Start();

            _clock.Start();
            _lastTickSec = 0.0;
            _lastPresentSec = 0.0;

            Application.Idle += OnApplicationIdle;

            if (openPreviewOnStart || _previewOnly)
            {
                OpenPreviewWindow();
            }
            if (openSettingsOnStart)
            {
                ShowSettings();
            }

            // Always render an initial frame immediately
            RenderAllWindows(true);
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null) return ico;
            }
            catch
            {
            }
            return SystemIcons.Application;
        }

        private NotifyIcon BuildTrayIcon()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(16, 10, 32);
            menu.ForeColor = Color.FromArgb(244, 247, 255);
            menu.ShowImageMargin = false;

            ToolStripMenuItem itemSettings = new ToolStripMenuItem("Configure CRT / VHS && Scene...");
            itemSettings.Font = new Font(menu.Font, FontStyle.Bold);
            itemSettings.Click += delegate { ShowSettings(); };
            menu.Items.Add(itemSettings);

            ToolStripMenuItem itemPreview = new ToolStripMenuItem("Open Preview Window");
            itemPreview.Click += delegate { OpenPreviewWindow(); };
            menu.Items.Add(itemPreview);

            ToolStripMenuItem itemReattach = new ToolStripMenuItem("Re-attach to Desktop");
            itemReattach.Click += delegate { HandleDesktopTopologyRefresh(); };
            menu.Items.Add(itemReattach);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem sceneMenu = new ToolStripMenuItem("Animation / Scene");
            _menuScenes = new ToolStripMenuItem[AppSettings.Scenes.Length];
            for (int i = 0; i < AppSettings.Scenes.Length; i++)
            {
                SceneDefinition sd = AppSettings.Scenes[i];
                ToolStripMenuItem si = new ToolStripMenuItem(sd.DisplayName);
                si.Tag = sd.Id;
                si.Click += delegate(object sender, EventArgs e)
                {
                    ToolStripMenuItem clicked = sender as ToolStripMenuItem;
                    if (clicked == null) return;
                    AppSettings s = _settings.Clone();
                    s.Scene = (string)clicked.Tag;
                    ApplyNewSettings(s, true);
                };
                _menuScenes[i] = si;
                sceneMenu.DropDownItems.Add(si);
            }
            menu.Items.Add(sceneMenu);

            _menuShaderToggle = new ToolStripMenuItem("CRT / VHS Shader Enabled");
            _menuShaderToggle.Checked = _settings.Enabled;
            _menuShaderToggle.Click += delegate
            {
                AppSettings s = _settings.Clone();
                s.Enabled = !s.Enabled;
                ApplyNewSettings(s, true);
            };
            menu.Items.Add(_menuShaderToggle);

            ToolStripMenuItem presetMenu = new ToolStripMenuItem("Presets");
            _menuPresets = new ToolStripMenuItem[AppSettings.Presets.Length];
            for (int i = 0; i < AppSettings.Presets.Length; i++)
            {
                WallpaperPreset p = AppSettings.Presets[i];
                ToolStripMenuItem pi = new ToolStripMenuItem(p.DisplayName);
                pi.Tag = p.Id;
                pi.Click += delegate(object sender, EventArgs e)
                {
                    ToolStripMenuItem clicked = sender as ToolStripMenuItem;
                    if (clicked == null) return;
                    AppSettings s = _settings.Clone();
                    if (s.ApplyPreset((string)clicked.Tag))
                    {
                        ApplyNewSettings(s, true);
                    }
                };
                _menuPresets[i] = pi;
                presetMenu.DropDownItems.Add(pi);
            }
            menu.Items.Add(presetMenu);

            _menuPause = new ToolStripMenuItem("Pause Animation");
            _menuPause.Click += delegate { ToggleManualPause(); };
            menu.Items.Add(_menuPause);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem itemExit = new ToolStripMenuItem("Exit Neon Horizon");
            itemExit.Click += delegate { ExitGracefully(); };
            menu.Items.Add(itemExit);

            NotifyIcon ni = new NotifyIcon();
            ni.Icon = _appIcon;
            ni.Text = "Neon Horizon — Synthwave CRT Wallpaper";
            ni.ContextMenuStrip = menu;
            ni.Visible = true;
            ni.DoubleClick += delegate { ShowSettings(); };

            UpdateMenuChecks();
            return ni;
        }

        private void UpdateMenuChecks()
        {
            if (_menuScenes != null)
            {
                for (int i = 0; i < _menuScenes.Length; i++)
                {
                    string sid = (string)_menuScenes[i].Tag;
                    _menuScenes[i].Checked = string.Equals(sid, _settings.Scene, StringComparison.OrdinalIgnoreCase);
                }
            }
            if (_menuShaderToggle != null)
            {
                _menuShaderToggle.Checked = _settings.Enabled;
            }
            if (_menuPause != null)
            {
                _menuPause.Text = _manualPaused ? "Resume Animation" : "Pause Animation";
            }
            if (_menuPresets != null)
            {
                for (int i = 0; i < _menuPresets.Length; i++)
                {
                    string pid = (string)_menuPresets[i].Tag;
                    _menuPresets[i].Checked = string.Equals(pid, _settings.Preset, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        private void ApplyNewSettings(AppSettings next, bool save)
        {
            next.Normalize();
            _settings = next;
            _needsStaticRedraw = true;
            if (save)
            {
                _settings.Save();
            }
            UpdateMenuChecks();
            if (_settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.Visible)
            {
                _settingsForm.RefreshFromSettings();
            }
            RenderAllWindows(true);
        }

        private void ResetSettings()
        {
            AppSettings def = new AppSettings();
            ApplyNewSettings(def, true);
        }

        private void ToggleManualPause()
        {
            _manualPaused = !_manualPaused;
            UpdateMenuChecks();
            if (!_manualPaused)
            {
                _lastTickSec = _clock.Elapsed.TotalSeconds;
                _needsStaticRedraw = true;
            }
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                _settingsForm.RefreshStatus();
            }
        }

        public void ShowSettings()
        {
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(
                    _appIcon,
                    delegate { return _settings; },
                    ApplyNewSettings,
                    ResetSettings,
                    OpenPreviewWindow,
                    ToggleManualPause,
                    delegate { return _manualPaused; },
                    GetStatusSummary);
            }
            _settingsForm.RefreshFromSettings();
            if (!_settingsForm.Visible)
            {
                _settingsForm.Show();
            }
            if (_settingsForm.WindowState == FormWindowState.Minimized)
            {
                _settingsForm.WindowState = FormWindowState.Normal;
            }
            _settingsForm.Activate();
            NativeMethods.SetForegroundWindow(_settingsForm.Handle);
        }

        public void OpenPreviewWindow()
        {
            if (_previewWindows.Count > 0)
            {
                WallpaperWindow existing = _previewWindows[0];
                if (existing.Handle != IntPtr.Zero && NativeMethods.IsWindow(existing.Handle))
                {
                    NativeMethods.ShowWindow(existing.Handle, NativeMethods.SW_RESTORE);
                    NativeMethods.SetForegroundWindow(existing.Handle);
                    return;
                }
            }

            Rectangle area = Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : new Rectangle(0, 0, 1280, 720);
            int w = Math.Min(1280, Math.Max(720, (int)(area.Width * 0.72)));
            int h = Math.Min(720, Math.Max(420, (int)(w * 9.0 / 16.0)));
            int x = area.X + (area.Width - w) / 2;
            int y = area.Y + (area.Height - h) / 2;

            WallpaperWindow preview = new WallpaperWindow(_renderer, new Rectangle(x, y, w, h), true);
            string err;
            if (preview.Create(out err))
            {
                preview.PreviewClosed += OnPreviewWindowClosed;
                _previewWindows.Add(preview);
                _needsStaticRedraw = true;
                RenderAllWindows(true);
            }
        }

        private void OnPreviewWindowClosed(WallpaperWindow wnd)
        {
            _previewWindows.Remove(wnd);
            if (_previewOnly && _previewWindows.Count == 0 && (_settingsForm == null || !_settingsForm.Visible))
            {
                ExitGracefully();
            }
        }

        private string GetStatusSummary()
        {
            SceneDefinition sd = AppSettings.FindScene(_settings.Scene);
            string sceneName = sd != null ? sd.DisplayName : _settings.Scene;
            string mode = _desktopTarget != null ? _desktopTarget.Mode.ToString() : "Detached";
            if (_previewOnly) mode = "PreviewOnly";
            string backend = _desktopWindows.Count > 0 && _desktopWindows[0].Surface != null
                ? _desktopWindows[0].Surface.BackendName
                : (_previewWindows.Count > 0 && _previewWindows[0].Surface != null ? _previewWindows[0].Surface.BackendName : "None");
            string state = _manualPaused ? "Paused (manual)" : (_autoPausedFullscreen ? "Paused (fullscreen)" : "Active");
            return string.Format("{0} | {1} ({2}) | F:{3} | {4}", sceneName, mode, backend, _framesRendered, state);
        }

        private void RebuildDesktopWindows(bool spawnWorker)
        {
            DisposeDesktopWindows();

            _desktopTarget = DesktopHost.DiscoverTarget(spawnWorker);
            if (_desktopTarget == null || _desktopTarget.Mode == DesktopAttachMode.None)
            {
                Logger.Error("RebuildDesktopWindows: no desktop host target found.");
                return;
            }

            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                WallpaperWindow ww = new WallpaperWindow(_renderer, screens[i].Bounds, false);
                string err;
                if (ww.CreateAndAttach(_desktopTarget, out err))
                {
                    _desktopWindows.Add(ww);
                }
                else
                {
                    Logger.Error("WallpaperWindow.CreateAndAttach failed: " + err);
                    ww.Dispose();
                }
            }
            _needsStaticRedraw = true;
            if (_settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.Visible)
            {
                _settingsForm.RefreshStatus();
            }
        }

        private void DisposeDesktopWindows()
        {
            DesktopHostTarget oldTarget = _desktopTarget;
            for (int i = 0; i < _desktopWindows.Count; i++)
            {
                _desktopWindows[i].Dispose();
            }
            _desktopWindows.Clear();
            DesktopHost.RefreshDesktopSurface(oldTarget);
        }

        internal void HandleDesktopTopologyRefresh()
        {
            if (!_exiting && !_previewOnly)
            {
                RebuildDesktopWindows(true);
                RenderAllWindows(true);
            }
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (_exiting || _ipcWindow == null || _ipcWindow.Handle == IntPtr.Zero) return;
            PostMessage(_ipcWindow.Handle, WM_REBUILD_DESKTOP, IntPtr.Zero, IntPtr.Zero);
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.ConsoleConnect || e.Reason == SessionSwitchReason.RemoteConnect)
            {
                if (_exiting || _ipcWindow == null || _ipcWindow.Handle == IntPtr.Zero) return;
                PostMessage(_ipcWindow.Handle, WM_REBUILD_DESKTOP, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private bool _needsDesktopVerify;

        private void OnWatchdogTick(object sender, EventArgs e)
        {
            if (_exiting) return;

            if (!_previewOnly)
            {
                if (_needsDesktopVerify || _desktopWindows.Count != Screen.AllScreens.Length || _desktopTarget == null || _desktopTarget.Mode == DesktopAttachMode.None)
                {
                    _needsDesktopVerify = false;
                    RebuildDesktopWindows(true);
                    RenderAllWindows(true);
                }
                else
                {
                    Screen[] screens = Screen.AllScreens;
                    bool needReattach = false;
                    for (int i = 0; i < _desktopWindows.Count; i++)
                    {
                        if (!DesktopHost.IsCorrectlyAttached(_desktopWindows[i].Handle, screens[i].Bounds, _desktopTarget))
                        {
                            needReattach = true;
                            break;
                        }
                    }

                    if (needReattach)
                    {
                        _desktopTarget = DesktopHost.DiscoverTarget(true);
                        if (_desktopTarget.Mode != DesktopAttachMode.None)
                        {
                            for (int i = 0; i < _desktopWindows.Count && i < screens.Length; i++)
                            {
                                _desktopWindows[i].UpdateBounds(screens[i].Bounds, _desktopTarget);
                            }
                            _needsStaticRedraw = true;
                        }
                        else
                        {
                            RebuildDesktopWindows(true);
                        }
                    }
                }
            }

            bool settingsVisible = _settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.Visible;
            if (_settings.PauseOnFullscreen && _previewWindows.Count == 0 && !settingsVisible)
            {
                List<IntPtr> ignore = new List<IntPtr>();
                if (_settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.Handle != IntPtr.Zero)
                {
                    ignore.Add(_settingsForm.Handle);
                }
                bool fsNow = DesktopHost.IsFullscreenAppActive(ignore);
                if (fsNow != _autoPausedFullscreen)
                {
                    _autoPausedFullscreen = fsNow;
                    Logger.Info("Fullscreen pause state changed: " + fsNow);
                    if (!fsNow)
                    {
                        _lastTickSec = _clock.Elapsed.TotalSeconds;
                        _needsStaticRedraw = true;
                    }
                    if (_settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.Visible)
                    {
                        _settingsForm.RefreshStatus();
                    }
                }
            }
            else
            {
                _autoPausedFullscreen = false;
            }

            if (settingsVisible)
            {
                _settingsForm.RefreshStatus();
            }

            if (_needsStaticRedraw)
            {
                RenderAllWindows(true);
            }
        }

        private bool ShouldAnimateNow()
        {
            if (_exiting) return false;
            if (_manualPaused) return false;
            if (_autoPausedFullscreen && _previewWindows.Count == 0) return false;

            bool animBg = _settings.AnimateBg;
            bool animCrt = _settings.Enabled && _settings.Animate && (_settings.Vhs > 0 || _settings.Scanlines > 0 || _settings.Sweep > 0);
            return animBg || animCrt;
        }

        private void SetHighResTimer(bool enable)
        {
            if (enable && !_timePeriodActive)
            {
                timeBeginPeriod(1);
                _timePeriodActive = true;
            }
            else if (!enable && _timePeriodActive)
            {
                timeEndPeriod(1);
                _timePeriodActive = false;
            }
        }

        private static bool IsMessageQueueEmpty()
        {
            MSG msg;
            return !PeekMessage(out msg, IntPtr.Zero, 0, 0, 0);
        }

        private void OnApplicationIdle(object sender, EventArgs e)
        {
            while (!_exiting && IsMessageQueueEmpty())
            {
                if (!ShouldAnimateNow())
                {
                    SetHighResTimer(false);
                    if (_needsStaticRedraw)
                    {
                        RenderAllWindows(true);
                    }
                    break;
                }

                SetHighResTimer(true);
                double now = _clock.Elapsed.TotalSeconds;
                int targetFps = _settings.TargetFps;
                bool useVsync = (targetFps == 0);

                if (!useVsync)
                {
                    double interval = 1.0 / targetFps;
                    double elapsedSincePresent = now - _lastPresentSec;
                    if (elapsedSincePresent < interval)
                    {
                        int sleepMs = (int)((interval - elapsedSincePresent) * 1000.0);
                        if (sleepMs > 0)
                        {
                            Thread.Sleep(sleepMs);
                        }
                        continue;
                    }
                }

                double dt = Math.Min(0.1, Math.Max(0.0, now - _lastTickSec));
                _lastTickSec = now;
                _lastPresentSec = now;

                if (_settings.AnimateBg)
                {
                    _sceneTime += (float)dt;
                    _gridTime += (float)(dt * (_settings.GridSpeed / 100.0));
                }
                if (_settings.Enabled && _settings.Animate)
                {
                    _crtTime += (float)dt;
                }

                RenderAllWindows(useVsync);
            }
        }

        private void RenderAllWindows(bool vsyncFirst)
        {
            _needsStaticRedraw = false;
            bool deviceLost = false;
            bool presentedAny = false;

            if (!_autoPausedFullscreen)
            {
                for (int i = 0; i < _desktopWindows.Count; i++)
                {
                    bool vsync = vsyncFirst && !presentedAny;
                    int hr = _desktopWindows[i].Render(_sceneTime, _gridTime, _crtTime, _settings, vsync);
                    presentedAny = true;
                    if (hr < 0)
                    {
                        Logger.Error(string.Format("Desktop Render failed: hr=0x{0:X8}", hr));
                    }
                    if (hr == D3D11Native.DXGI_ERROR_DEVICE_REMOVED || hr == D3D11Native.DXGI_ERROR_DEVICE_RESET)
                    {
                        deviceLost = true;
                        break;
                    }
                }
            }

            for (int i = _previewWindows.Count - 1; i >= 0; i--)
            {
                bool vsync = vsyncFirst && !presentedAny;
                int hr = _previewWindows[i].Render(_sceneTime, _gridTime, _crtTime, _settings, vsync);
                presentedAny = true;
                if (hr == D3D11Native.DXGI_ERROR_DEVICE_REMOVED || hr == D3D11Native.DXGI_ERROR_DEVICE_RESET)
                {
                    deviceLost = true;
                    break;
                }
            }

            if (presentedAny && !deviceLost)
            {
                _framesRendered++;
                if (_framesRendered == 1)
                {
                    Logger.Info("First frame rendered successfully.");
                }
            }

            if (deviceLost)
            {
                HandleDeviceLost();
            }
        }

        private void HandleDeviceLost()
        {
            for (int i = 0; i < _previewWindows.Count; i++) _previewWindows[i].Dispose();
            _previewWindows.Clear();
            DisposeDesktopWindows();

            string err;
            if (_renderer.Initialize(out err))
            {
                if (!_previewOnly) RebuildDesktopWindows(true);
            }
        }

        public void ExitGracefully()
        {
            if (_exiting) return;
            _exiting = true;

            Application.Idle -= OnApplicationIdle;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SetHighResTimer(false);

            if (_watchdogTimer != null)
            {
                _watchdogTimer.Stop();
                _watchdogTimer.Dispose();
            }

            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                _settingsForm.ForceClose();
            }

            for (int i = 0; i < _previewWindows.Count; i++)
            {
                _previewWindows[i].Dispose();
            }
            _previewWindows.Clear();

            DisposeDesktopWindows();
            _renderer.Dispose();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            if (_ipcWindow != null)
            {
                _ipcWindow.DestroyHandle();
                _ipcWindow = null;
            }

            ExitThread();
        }
    }
}
