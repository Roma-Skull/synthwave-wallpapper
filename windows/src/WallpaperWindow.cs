using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NeonHorizonWallpaper
{
    public sealed class WallpaperWindow : IDisposable
    {
        private static readonly string WindowClassName = "NeonHorizonWallpaperSurfaceWnd";
        private static readonly Dictionary<IntPtr, WallpaperWindow> Instances = new Dictionary<IntPtr, WallpaperWindow>();
        private static bool _classRegistered;
        private static NativeMethods.WndProcDelegate _wndProcDelegate;

        private readonly D3D11Renderer _renderer;
        private readonly bool _previewMode;
        private IntPtr _hwnd;
        private D3D11Surface _surface;
        private Rectangle _bounds;
        private uint _dpi = 96;
        private bool _disposed;

        public event Action<WallpaperWindow> PreviewClosed;

        public IntPtr Handle { get { return _hwnd; } }
        public Rectangle Bounds { get { return _bounds; } }
        public uint Dpi { get { return _dpi; } }
        public D3D11Surface Surface { get { return _surface; } }

        public WallpaperWindow(D3D11Renderer renderer, Rectangle bounds, bool previewMode)
        {
            _renderer = renderer;
            _bounds = bounds;
            _previewMode = previewMode;
            _dpi = NativeMethods.GetMonitorDpi(bounds.X, bounds.Y);
        }

        private static void EnsureWindowClass()
        {
            if (_classRegistered) return;
            _wndProcDelegate = new NativeMethods.WndProcDelegate(StaticWndProc);

            NativeMethods.WNDCLASSEX wc = new NativeMethods.WNDCLASSEX();
            wc.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX));
            wc.style = 0x0003; // CS_HREDRAW | CS_VREDRAW
            wc.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            wc.hInstance = NativeMethods.GetModuleHandle(null);
            wc.hCursor = NativeMethods.LoadCursor(IntPtr.Zero, 32512); // IDC_ARROW
            wc.hbrBackground = IntPtr.Zero;
            wc.lpszClassName = WindowClassName;

            ushort atom = NativeMethods.RegisterClassEx(ref wc);
            if (atom == 0)
            {
                int err = Marshal.GetLastWin32Error();
                if (err != 1410) // ERROR_CLASS_ALREADY_EXISTS
                {
                    throw new InvalidOperationException("RegisterClassEx failed: " + err);
                }
            }
            _classRegistered = true;
        }

        private static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == NativeMethods.WM_ERASEBKGND)
            {
                return new IntPtr(1);
            }
            if (msg == NativeMethods.WM_NCHITTEST)
            {
                long style = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_STYLE);
                if ((style & (long)NativeMethods.WS_CHILD) != 0)
                {
                    return new IntPtr(NativeMethods.HTTRANSPARENT);
                }
            }
            if (msg == NativeMethods.WM_DESTROY)
            {
                WallpaperWindow inst;
                if (Instances.TryGetValue(hWnd, out inst))
                {
                    Instances.Remove(hWnd);
                    inst._hwnd = IntPtr.Zero;
                    if (inst._surface != null)
                    {
                        inst._surface.Dispose();
                        inst._surface = null;
                    }
                    Action<WallpaperWindow> handler = inst.PreviewClosed;
                    if (handler != null) handler(inst);
                }
            }
            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public bool Create(out string errorMessage)
        {
            return CreateAndAttach(null, out errorMessage);
        }

        public bool CreateAndAttach(DesktopHostTarget target, out string errorMessage)
        {
            errorMessage = string.Empty;
            EnsureWindowClass();

            uint style;
            uint exStyle;
            string title;

            bool useDcomp = !_previewMode && _renderer.HasDirectComposition;

            if (_previewMode)
            {
                style = NativeMethods.WS_OVERLAPPEDWINDOW | NativeMethods.WS_VISIBLE;
                exStyle = NativeMethods.WS_EX_APPWINDOW;
                title = "Neon Horizon — Wallpaper Preview";
            }
            else
            {
                style = NativeMethods.WS_POPUP | NativeMethods.WS_CLIPSIBLINGS | NativeMethods.WS_CLIPCHILDREN;
                exStyle = NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT;
                if (useDcomp)
                {
                    exStyle |= NativeMethods.WS_EX_NOREDIRECTIONBITMAP;
                }
                title = "Neon Horizon Wallpaper Surface";
            }

            _hwnd = NativeMethods.CreateWindowEx(
                exStyle,
                WindowClassName,
                title,
                style,
                _bounds.X,
                _bounds.Y,
                Math.Max(64, _bounds.Width),
                Math.Max(64, _bounds.Height),
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.GetModuleHandle(null),
                IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                errorMessage = "CreateWindowEx failed: " + Marshal.GetLastWin32Error();
                return false;
            }
            Instances[_hwnd] = this;

            if (!_previewMode && target != null)
            {
                if (!DesktopHost.AttachWindow(_hwnd, _bounds, target, useDcomp))
                {
                    errorMessage = "DesktopHost.AttachWindow failed.";
                    DestroyHandle();
                    return false;
                }
            }

            int cw = _bounds.Width;
            int ch = _bounds.Height;
            if (_previewMode)
            {
                NativeMethods.RECT cr;
                if (NativeMethods.GetClientRect(_hwnd, out cr))
                {
                    cw = Math.Max(64, cr.Right - cr.Left);
                    ch = Math.Max(64, cr.Bottom - cr.Top);
                }
            }

            SurfaceMode mode = _previewMode
                ? SurfaceMode.FlipHwnd
                : (useDcomp ? SurfaceMode.DirectComposition : SurfaceMode.BltDiscard);

            // Create SwapChain / DirectComposition target AFTER SetParent settles
            _surface = _renderer.CreateSurface(_hwnd, cw, ch, mode, out errorMessage);
            if (_surface == null)
            {
                DestroyHandle();
                return false;
            }

            Logger.Info(string.Format("WallpaperWindow created: hwnd=0x{0:X} preview={1} backend={2} size={3}x{4} dpi={5}",
                _hwnd.ToInt64(), _previewMode, _surface.BackendName, cw, ch, _dpi));
            return true;
        }

        public bool AttachToDesktop(DesktopHostTarget target)
        {
            if (_previewMode || _hwnd == IntPtr.Zero) return false;
            return DesktopHost.AttachWindow(_hwnd, _bounds, target, _renderer.HasDirectComposition);
        }

        public void UpdateBounds(Rectangle newBounds, DesktopHostTarget target)
        {
            _bounds = newBounds;
            _dpi = NativeMethods.GetMonitorDpi(newBounds.X, newBounds.Y);
            if (_hwnd == IntPtr.Zero) return;

            bool useDcomp = !_previewMode && _renderer.HasDirectComposition;
            bool parentChanged = false;
            if (!_previewMode && target != null)
            {
                parentChanged = NativeMethods.GetParent(_hwnd) != target.ParentWindow;
                DesktopHost.AttachWindow(_hwnd, newBounds, target, useDcomp);
            }

            string err;
            if (parentChanged)
            {
                if (_surface != null)
                {
                    _surface.Dispose();
                    _surface = null;
                }
                SurfaceMode mode = _previewMode
                    ? SurfaceMode.FlipHwnd
                    : (useDcomp ? SurfaceMode.DirectComposition : SurfaceMode.BltDiscard);
                _surface = _renderer.CreateSurface(_hwnd, newBounds.Width, newBounds.Height, mode, out err);
            }
            else
            {
                _renderer.ResizeSurface(_surface, newBounds.Width, newBounds.Height, out err);
            }
        }

        public void SyncPreviewClientSize()
        {
            if (!_previewMode || _hwnd == IntPtr.Zero || _surface == null) return;
            if (!NativeMethods.IsWindow(_hwnd))
            {
                Action<WallpaperWindow> handler = PreviewClosed;
                _hwnd = IntPtr.Zero;
                if (handler != null) handler(this);
                return;
            }

            NativeMethods.RECT cr;
            if (NativeMethods.GetClientRect(_hwnd, out cr))
            {
                int cw = Math.Max(64, cr.Right - cr.Left);
                int ch = Math.Max(64, cr.Bottom - cr.Top);
                if (cw != (int)_surface.Width || ch != (int)_surface.Height)
                {
                    string err;
                    _renderer.ResizeSurface(_surface, cw, ch, out err);
                }
            }
            _dpi = NativeMethods.GetMonitorDpi(_bounds.X, _bounds.Y);
        }

        public int Render(float sceneTime, float gridTime, float crtTime, AppSettings settings, bool vsync)
        {
            if (_disposed || _hwnd == IntPtr.Zero || _surface == null) return 0;
            if (_previewMode)
            {
                SyncPreviewClientSize();
                if (_hwnd == IntPtr.Zero) return 0;
            }
            return _renderer.RenderAndPresent(_surface, _dpi, sceneTime, gridTime, crtTime, settings, vsync);
        }

        private void DestroyHandle()
        {
            if (_hwnd != IntPtr.Zero)
            {
                IntPtr h = _hwnd;
                Instances.Remove(h);
                _hwnd = IntPtr.Zero;
                if (!_previewMode)
                {
                    NativeMethods.ShowWindow(h, NativeMethods.SW_HIDE);
                    NativeMethods.SetParent(h, IntPtr.Zero);
                }
                NativeMethods.DestroyWindow(h);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_surface != null)
            {
                _surface.Dispose();
                _surface = null;
            }
            DestroyHandle();
        }
    }
}
