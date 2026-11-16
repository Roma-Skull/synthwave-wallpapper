using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace NeonHorizonWallpaper
{
    internal static class Program
    {
        private const string MutexName = @"Local\NeonHorizonWallpaper_SingleInstance";

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        [STAThread]
        private static int Main(string[] args)
        {
            bool openSettings = false;
            bool openPreview = false;
            bool previewOnly = false;
            bool exitExisting = false;
            string testRenderPath = null;
            string testSceneId = null;
            int testW = 0;
            int testH = 0;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase)) openSettings = true;
                else if (string.Equals(a, "--preview", StringComparison.OrdinalIgnoreCase)) openPreview = true;
                else if (string.Equals(a, "--preview-only", StringComparison.OrdinalIgnoreCase)) { previewOnly = true; openPreview = true; }
                else if (string.Equals(a, "--exit", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--quit", StringComparison.OrdinalIgnoreCase)) exitExisting = true;
                else if (string.Equals(a, "--test-render", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    testRenderPath = args[++i];
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        testSceneId = args[++i];
                    }
                    int parsed;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out parsed))
                    {
                        testW = parsed;
                        i++;
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out parsed))
                        {
                            testH = parsed;
                            i++;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(testRenderPath))
            {
                AttachConsole(-1);
                return RunOffscreenRenderTest(testRenderPath, testSceneId, testW, testH);
            }

            if (exitExisting)
            {
                IntPtr existing = NativeMethods.FindWindow(null, "NeonHorizonWallpaper_IPC_MsgWindow");
                if (existing != IntPtr.Zero)
                {
                    IntPtr res;
                    NativeMethods.SendMessageTimeout(existing, TrayApplication.WM_EXIT_APP, IntPtr.Zero, IntPtr.Zero, NativeMethods.SMTO_NORMAL, 1000, out res);
                }
                return 0;
            }

            if (!previewOnly)
            {
                bool createdNew;
                using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
                {
                    if (!createdNew)
                    {
                        IntPtr existing = NativeMethods.FindWindow(null, "NeonHorizonWallpaper_IPC_MsgWindow");
                        if (existing != IntPtr.Zero)
                        {
                            IntPtr res;
                            uint msg = openPreview ? TrayApplication.WM_OPEN_PREVIEW : TrayApplication.WM_SHOW_SETTINGS;
                            NativeMethods.SendMessageTimeout(existing, msg, IntPtr.Zero, IntPtr.Zero, NativeMethods.SMTO_NORMAL, 1000, out res);
                        }
                        return 0;
                    }

                    return RunApplication(openSettings, openPreview, false);
                }
            }

            return RunApplication(openSettings, openPreview, true);
        }

        private static int RunApplication(bool openSettings, bool openPreview, bool previewOnly)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            TrayApplication app = new TrayApplication(openSettings, openPreview, previewOnly);
            Application.Run(app);
            return 0;
        }

        private static int RunOffscreenRenderTest(string outputPath, string sceneId, int reqWidth, int reqHeight)
        {
            D3D11Renderer renderer = new D3D11Renderer();
            string err;
            if (!renderer.Initialize(out err))
            {
                Console.Error.WriteLine("INIT_FAIL: " + err);
                return 2;
            }

            try
            {
                int w = reqWidth >= 64 ? reqWidth : 1280;
                int h = reqHeight >= 64 ? reqHeight : 720;
                AppSettings s = new AppSettings();
                if (!string.IsNullOrEmpty(sceneId))
                {
                    s.Scene = sceneId;
                    s.Normalize();
                }
                byte[] rgba = renderer.RenderOffscreenRgba(w, h, 96, 1.5f, 1.5f, 1.5f, s, out err);
                if (rgba == null)
                {
                    Console.Error.WriteLine("RENDER_FAIL: " + err);
                    return 3;
                }

                PrintSample("CORNER_BEZEL", rgba, w, 2, 2);
                PrintSample("SKY_TOP", rgba, w, w / 2, Math.Max(1, (int)(h * 0.055)));
                PrintSample("SUN_DISC", rgba, w, w / 2, Math.Max(1, (int)(h * 0.40)));
                PrintSample("HORIZON_BEAM", rgba, w, w / 2, Math.Max(1, (int)(h * 0.56)));
                PrintSample("GRID_FLOOR", rgba, w, w / 2, Math.Max(1, (int)(h * 0.86)));

                // Save PNG preview image
                using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    byte[] bgra = new byte[rgba.Length];
                    for (int i = 0; i < rgba.Length; i += 4)
                    {
                        bgra[i + 0] = rgba[i + 2];
                        bgra[i + 1] = rgba[i + 1];
                        bgra[i + 2] = rgba[i + 0];
                        bgra[i + 3] = 255;
                    }
                    Marshal.Copy(bgra, 0, bd.Scan0, bgra.Length);
                    bmp.UnlockBits(bd);

                    string dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    bmp.Save(outputPath, ImageFormat.Png);
                }

                Console.WriteLine("OFFSCREEN_OK: " + outputPath);
                return 0;
            }
            finally
            {
                renderer.Dispose();
            }
        }

        private static void PrintSample(string label, byte[] rgba, int width, int x, int y)
        {
            int idx = (y * width + x) * 4;
            Console.WriteLine(string.Format("{0}=({1},{2},{3})", label, rgba[idx], rgba[idx + 1], rgba[idx + 2]));
        }
    }
}
