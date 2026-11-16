using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace NeonHorizonWallpaper
{
    public enum DesktopAttachMode
    {
        None = 0,
        RaisedDesktop24H2 = 1,
        ClassicWorkerW = 2
    }

    public sealed class DesktopHostTarget
    {
        public DesktopAttachMode Mode;
        public IntPtr Progman;
        public IntPtr ShellView;
        public IntPtr WorkerW;
        public IntPtr ParentWindow;
    }

    public static class DesktopHost
    {
        public static DesktopHostTarget DiscoverTarget(bool spawnWorkerIfNeeded)
        {
            DesktopHostTarget target = new DesktopHostTarget();
            IntPtr progman = NativeMethods.FindWindow("Progman", null);
            if (progman == IntPtr.Zero)
            {
                return target;
            }
            target.Progman = progman;

            if (spawnWorkerIfNeeded)
            {
                TriggerWorkerSpawn(progman);
            }

            // 1. Check direct child SHELLDLL_DefView under Progman (Windows 11 24H2 Raised Desktop or unsplit Progman)
            IntPtr shellView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                target.Mode = DesktopAttachMode.RaisedDesktop24H2;
                target.ShellView = shellView;
                target.ParentWindow = progman;
                target.WorkerW = FindChildWorkerW(progman, shellView);
                Logger.Info(string.Format("DiscoverTarget: Mode={0} Progman=0x{1:X}(ex=0x{2:X}) ShellView=0x{3:X}(ex=0x{4:X}) ChildWorkerW=0x{5:X}",
                    target.Mode,
                    progman.ToInt64(),
                    NativeMethods.GetWindowLongPtr(progman, NativeMethods.GWL_EXSTYLE),
                    shellView.ToInt64(),
                    NativeMethods.GetWindowLongPtr(shellView, NativeMethods.GWL_EXSTYLE),
                    target.WorkerW.ToInt64()));
                return target;
            }

            // 2. Classic Windows 10 / Pre-24H2 layout: SHELLDLL_DefView inside a top-level WorkerW
            IntPtr workerWithShell = FindTopLevelWorkerWithShellView(out shellView);
            if (workerWithShell != IntPtr.Zero)
            {
                IntPtr bgWorker = NativeMethods.FindWindowEx(IntPtr.Zero, workerWithShell, "WorkerW", null);
                if (bgWorker == IntPtr.Zero && !spawnWorkerIfNeeded)
                {
                    TriggerWorkerSpawn(progman);
                    bgWorker = NativeMethods.FindWindowEx(IntPtr.Zero, workerWithShell, "WorkerW", null);
                }

                if (bgWorker != IntPtr.Zero && bgWorker != workerWithShell)
                {
                    target.Mode = DesktopAttachMode.ClassicWorkerW;
                    target.ShellView = shellView;
                    target.WorkerW = bgWorker;
                    target.ParentWindow = bgWorker;
                    Logger.Info(string.Format("DiscoverTarget: Mode={0} WorkerWithShell=0x{1:X} BgWorkerW=0x{2:X}",
                        target.Mode, workerWithShell.ToInt64(), bgWorker.ToInt64()));
                    return target;
                }
            }

            Logger.Error("DiscoverTarget: Failed to locate SHELLDLL_DefView or WorkerW.");
            return target;
        }

        private static void TriggerWorkerSpawn(IntPtr progman)
        {
            IntPtr res;
            NativeMethods.SendMessageTimeout(
                progman,
                0x052C,
                new IntPtr(0x0D),
                new IntPtr(0x01),
                NativeMethods.SMTO_NORMAL | NativeMethods.SMTO_ABORTIFHUNG,
                500,
                out res);

            // Also send (0, 0) if neither child WorkerW nor top-level WorkerW appeared
            IntPtr directShell = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (directShell != IntPtr.Zero)
            {
                if (FindChildWorkerW(progman, directShell) == IntPtr.Zero)
                {
                    NativeMethods.SendMessageTimeout(
                        progman,
                        0x052C,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        NativeMethods.SMTO_NORMAL | NativeMethods.SMTO_ABORTIFHUNG,
                        500,
                        out res);
                }
            }
        }

        private static IntPtr FindChildWorkerW(IntPtr progman, IntPtr shellView)
        {
            IntPtr child = IntPtr.Zero;
            while ((child = NativeMethods.FindWindowEx(progman, child, "WorkerW", null)) != IntPtr.Zero)
            {
                if (child != shellView && NativeMethods.FindWindowEx(child, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero)
                {
                    return child;
                }
            }
            return IntPtr.Zero;
        }

        private static IntPtr FindTopLevelWorkerWithShellView(out IntPtr shellView)
        {
            IntPtr foundWorker = IntPtr.Zero;
            IntPtr foundShell = IntPtr.Zero;

            NativeMethods.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                IntPtr sv = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (sv != IntPtr.Zero)
                {
                    foundWorker = hWnd;
                    foundShell = sv;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            shellView = foundShell;
            return foundWorker;
        }

        public static bool AttachWindow(IntPtr hwnd, Rectangle screenBounds, DesktopHostTarget target, bool useDirectComposition)
        {
            if (hwnd == IntPtr.Zero || target == null || target.ParentWindow == IntPtr.Zero || !NativeMethods.IsWindow(target.ParentWindow))
            {
                return false;
            }

            long style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE);
            long exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);

            style = (style & ~(long)NativeMethods.WS_POPUP) | (long)NativeMethods.WS_CHILD | (long)NativeMethods.WS_CLIPSIBLINGS | (long)NativeMethods.WS_CLIPCHILDREN;
            exStyle = (exStyle & ~((long)NativeMethods.WS_EX_APPWINDOW | (long)NativeMethods.WS_EX_LAYERED | (long)NativeMethods.WS_EX_NOREDIRECTIONBITMAP))
                | (long)NativeMethods.WS_EX_TOOLWINDOW
                | (long)NativeMethods.WS_EX_NOACTIVATE
                | (long)NativeMethods.WS_EX_TRANSPARENT;

            if (useDirectComposition)
            {
                exStyle |= (long)NativeMethods.WS_EX_NOREDIRECTIONBITMAP;
            }

            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, style);
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

            if (NativeMethods.GetParent(hwnd) != target.ParentWindow)
            {
                NativeMethods.SetParent(hwnd, target.ParentWindow);
            }

            NativeMethods.POINT pt = new NativeMethods.POINT { X = screenBounds.Left, Y = screenBounds.Top };
            NativeMethods.MapWindowPoints(IntPtr.Zero, target.ParentWindow, ref pt, 1);

            if (target.Mode == DesktopAttachMode.RaisedDesktop24H2 && target.ShellView != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(
                    hwnd,
                    target.ShellView,
                    pt.X,
                    pt.Y,
                    screenBounds.Width,
                    screenBounds.Height,
                    NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_FRAMECHANGED);
            }
            else
            {
                NativeMethods.SetWindowPos(
                    hwnd,
                    IntPtr.Zero,
                    pt.X,
                    pt.Y,
                    screenBounds.Width,
                    screenBounds.Height,
                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_FRAMECHANGED);
            }

            Logger.Info(string.Format("AttachWindow: hwnd=0x{0:X} parent=0x{1:X} local=({2},{3}) size=({4}x{5}) dcomp={6}",
                hwnd.ToInt64(), target.ParentWindow.ToInt64(), pt.X, pt.Y, screenBounds.Width, screenBounds.Height, useDirectComposition));
            return true;
        }

        public static bool IsCorrectlyAttached(IntPtr hwnd, Rectangle screenBounds, DesktopHostTarget target)
        {
            if (hwnd == IntPtr.Zero || target == null || target.ParentWindow == IntPtr.Zero) return false;
            if (!NativeMethods.IsWindow(target.ParentWindow)) return false;
            if (NativeMethods.GetParent(hwnd) != target.ParentWindow) return false;
            if (!NativeMethods.IsWindowVisible(hwnd)) return false;

            NativeMethods.RECT r;
            if (NativeMethods.GetWindowRect(hwnd, out r))
            {
                if (r.Left != screenBounds.Left || r.Top != screenBounds.Top ||
                    (r.Right - r.Left) != screenBounds.Width || (r.Bottom - r.Top) != screenBounds.Height)
                {
                    return false;
                }
            }

            if (target.Mode == DesktopAttachMode.RaisedDesktop24H2 && target.ShellView != IntPtr.Zero)
            {
                // Verify hwnd sits below SHELLDLL_DefView and above WorkerW in Z-order
                IntPtr prev = NativeMethods.GetWindow(hwnd, NativeMethods.GW_HWNDPREV);
                bool seenShellView = false;
                int guard = 0;
                while (prev != IntPtr.Zero && guard++ < 64)
                {
                    if (target.WorkerW != IntPtr.Zero && prev == target.WorkerW)
                    {
                        return false;
                    }
                    if (prev == target.ShellView)
                    {
                        seenShellView = true;
                        break;
                    }
                    prev = NativeMethods.GetWindow(prev, NativeMethods.GW_HWNDPREV);
                }
                if (!seenShellView) return false;
            }

            return true;
        }

        public static void RefreshDesktopSurface(DesktopHostTarget target)
        {
            if (target == null) return;
            if (target.Progman != IntPtr.Zero && NativeMethods.IsWindow(target.Progman))
            {
                NativeMethods.RedrawWindow(
                    target.Progman,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_ERASE | NativeMethods.RDW_ALLCHILDREN | NativeMethods.RDW_UPDATENOW);
            }
            if (target.WorkerW != IntPtr.Zero && target.WorkerW != target.Progman && NativeMethods.IsWindow(target.WorkerW))
            {
                NativeMethods.RedrawWindow(
                    target.WorkerW,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_ERASE | NativeMethods.RDW_ALLCHILDREN | NativeMethods.RDW_UPDATENOW);
            }
        }

        public static bool IsSessionLocked()
        {
            IntPtr desktop = NativeMethods.OpenInputDesktop(0, false, NativeMethods.DESKTOP_READOBJECTS);
            if (desktop == IntPtr.Zero)
            {
                return true;
            }
            NativeMethods.CloseDesktop(desktop);
            return false;
        }

        public static bool IsFullscreenAppActive(IList<IntPtr> ignoreHandles)
        {
            if (IsSessionLocked()) return true;

            int quns;
            if (NativeMethods.SHQueryUserNotificationState(out quns) == 0)
            {
                // 3 = QUNS_RUNNING_D3D_FULL_SCREEN, 4 = QUNS_PRESENTATION_MODE
                if (quns == 3 || quns == 4) return true;
            }

            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero || !NativeMethods.IsWindowVisible(fg) || NativeMethods.IsIconic(fg) || NativeMethods.IsZoomed(fg))
            {
                return false;
            }

            if (ignoreHandles != null)
            {
                for (int i = 0; i < ignoreHandles.Count; i++)
                {
                    if (ignoreHandles[i] == fg) return false;
                }
            }

            long exStyle = NativeMethods.GetWindowLongPtr(fg, NativeMethods.GWL_EXSTYLE);
            if ((exStyle & (long)NativeMethods.WS_EX_TOOLWINDOW) != 0)
            {
                return false;
            }

            string cls = NativeMethods.GetWindowClassName(fg);
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" ||
                cls == "Shell_SecondaryTrayWnd" || cls == "NotifyIconOverflowWindow" ||
                cls == "Windows.UI.Core.CoreWindow")
            {
                return false;
            }

            int cloaked = 0;
            if (NativeMethods.DwmGetWindowAttribute(fg, NativeMethods.DWMWA_CLOAKED, out cloaked, Marshal.SizeOf(typeof(int))) == 0 && cloaked != 0)
            {
                return false;
            }

            NativeMethods.RECT wr;
            if (!NativeMethods.GetWindowRect(fg, out wr)) return false;

            IntPtr mon = NativeMethods.MonitorFromWindow(fg, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (mon == IntPtr.Zero) return false;

            NativeMethods.MONITORINFO mi = new NativeMethods.MONITORINFO();
            mi.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
            if (!NativeMethods.GetMonitorInfo(mon, ref mi)) return false;

            return wr.Left <= mi.rcMonitor.Left
                && wr.Top <= mi.rcMonitor.Top
                && wr.Right >= mi.rcMonitor.Right
                && wr.Bottom >= mi.rcMonitor.Bottom;
        }
    }
}
