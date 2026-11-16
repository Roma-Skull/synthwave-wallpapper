#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$OutExe,
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutExe)) {
    $OutExe = Join-Path $Root 'NeonHorizonWallpaper.exe'
}
$VersionFile = Join-Path $Root 'VERSION'
if ([string]::IsNullOrWhiteSpace($Version)) {
    if (Test-Path $VersionFile) {
        $Version = (Get-Content -Path $VersionFile -Raw).Trim()
    } else {
        $Version = '1.1.0'
    }
}
$CleanVersion = $Version.Trim() -replace '^(?:[a-zA-Z0-9_-]+-)?[vV]', ''
$VerParts = ($CleanVersion -split '[-+]')[0] -split '\.'
$Major = if ($VerParts.Count -ge 1 -and $VerParts[0] -match '^\d+$') { [int]$VerParts[0] } else { 1 }
$Minor = if ($VerParts.Count -ge 2 -and $VerParts[1] -match '^\d+$') { [int]$VerParts[1] } else { 0 }
$Patch = if ($VerParts.Count -ge 3 -and $VerParts[2] -match '^\d+$') { [int]$VerParts[2] } else { 0 }
$AsmVersion = "$Major.$Minor.$Patch.0"

$AssetsDir = Join-Path $Root 'assets'
if (-not (Test-Path $AssetsDir)) {
    New-Item -ItemType Directory -Path $AssetsDir -Force | Out-Null
}
$IconPath = Join-Path $AssetsDir 'neon_horizon.ico'

# 1. Generate multi-resolution Synthwave icon (16, 32, 48, 64) using System.Drawing
Add-Type -AssemblyName System.Drawing
$iconCode = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class IconMaker
{
    private static Bitmap RenderIconFrame(int size)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            float pad = Math.Max(1.0f, size * 0.05f);
            float w = size - pad * 2.0f;
            float r = Math.Max(2.0f, size * 0.22f);

            using (GraphicsPath bgPath = new GraphicsPath())
            {
                bgPath.AddArc(pad, pad, r, r, 180, 90);
                bgPath.AddArc(pad + w - r, pad, r, r, 270, 90);
                bgPath.AddArc(pad + w - r, pad + w - r, r, r, 0, 90);
                bgPath.AddArc(pad, pad + w - r, r, r, 90, 90);
                bgPath.CloseFigure();

                using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                    new PointF(0, pad),
                    new PointF(0, pad + w),
                    Color.FromArgb(255, 11, 7, 24),
                    Color.FromArgb(255, 36, 12, 62)))
                {
                    g.FillPath(bgBrush, bgPath);
                }

                g.SetClip(bgPath);

                float horY = pad + w * 0.58f;
                float sunR = w * 0.27f;
                float sunCx = size * 0.5f;
                float sunCy = horY - sunR * 0.12f;

                using (GraphicsPath sunPath = new GraphicsPath())
                {
                    sunPath.AddEllipse(sunCx - sunR, sunCy - sunR, sunR * 2f, sunR * 2f);
                    using (LinearGradientBrush sunBrush = new LinearGradientBrush(
                        new PointF(0, sunCy - sunR),
                        new PointF(0, sunCy + sunR),
                        Color.FromArgb(255, 255, 232, 95),
                        Color.FromArgb(255, 255, 42, 133)))
                    {
                        g.FillPath(sunBrush, sunPath);
                    }
                }

                // Sun scan-slice cutouts
                using (SolidBrush cutBrush = new SolidBrush(Color.FromArgb(255, 24, 10, 44)))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float sy = horY - w * (0.055f + i * 0.065f);
                        float sh = Math.Max(1.0f, w * (0.025f - i * 0.005f));
                        g.FillRectangle(cutBrush, pad, sy, w, sh);
                    }
                }

                // Floor fill + perspective grid
                using (SolidBrush floorBrush = new SolidBrush(Color.FromArgb(255, 13, 6, 30)))
                {
                    g.FillRectangle(floorBrush, pad, horY, w, pad + w - horY);
                }

                float penW = Math.Max(1.0f, size / 32.0f);
                using (Pen gridPen = new Pen(Color.FromArgb(220, 0, 240, 255), penW))
                {
                    g.DrawLine(gridPen, pad, horY, pad + w, horY);
                    for (int gx = -2; gx <= 2; gx++)
                    {
                        float xTop = sunCx + gx * w * 0.10f;
                        float xBot = sunCx + gx * w * 0.34f;
                        g.DrawLine(gridPen, xTop, horY, xBot, pad + w);
                    }
                    g.DrawLine(gridPen, pad, horY + w * 0.14f, pad + w, horY + w * 0.14f);
                    g.DrawLine(gridPen, pad, horY + w * 0.29f, pad + w, horY + w * 0.29f);
                }

                g.ResetClip();
                using (Pen rimPen = new Pen(Color.FromArgb(210, 0, 240, 255), Math.Max(1.0f, size / 24.0f)))
                {
                    g.DrawPath(rimPen, bgPath);
                }
            }
        }
        return bmp;
    }

    public static void SaveIco(string path)
    {
        int[] sizes = new int[] { 16, 32, 48, 64 };
        List<byte[]> pngChunks = new List<byte[]>();
        for (int i = 0; i < sizes.Length; i++)
        {
            using (Bitmap b = RenderIconFrame(sizes[i]))
            using (MemoryStream ms = new MemoryStream())
            {
                b.Save(ms, ImageFormat.Png);
                pngChunks.Add(ms.ToArray());
            }
        }

        using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            bw.Write((ushort)0); // reserved
            bw.Write((ushort)1); // type = icon
            bw.Write((ushort)sizes.Length);

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte sByte = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
                bw.Write(sByte);
                bw.Write(sByte);
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write((uint)pngChunks[i].Length);
                bw.Write((uint)offset);
                offset += pngChunks[i].Length;
            }
            for (int i = 0; i < pngChunks.Count; i++)
            {
                bw.Write(pngChunks[i]);
            }
        }
    }
}
'@
if (-not ('IconMaker' -as [type])) {
    Add-Type -TypeDefinition $iconCode -ReferencedAssemblies System.Drawing
}
[IconMaker]::SaveIco($IconPath)

# 2. Locate .NET Framework 4 C# compiler (built into Windows 10/11)
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw "Could not find csc.exe in .NET Framework v4.0.30319."
}

$srcFiles = Get-ChildItem -Path (Join-Path $Root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
$manifest = Join-Path $Root 'app.manifest'
$vsShader = Join-Path $Root 'shaders\fullscreen.vs.hlsl'
$sceneShader = Join-Path $Root 'shaders\synthwave_scene.ps.hlsl'
$miamiShader = Join-Path $Root 'shaders\miami_drive.ps.hlsl'
$cycleShader = Join-Path $Root 'shaders\light_cycle.ps.hlsl'
$outrunCycleShader = Join-Path $Root 'shaders\outrun_cycle.ps.hlsl'
$bloomShader = Join-Path $Root 'shaders\bloom_downsample.ps.hlsl'
$crtShader = Join-Path $Root 'shaders\crt_vhs.ps.hlsl'

# 1b. Pre-compile HLSL shaders to DXBC bytecode so runtime startup is instantaneous (<5ms)
$dxbcCode = @'
using System;
using System.IO;
using System.Runtime.InteropServices;

public static class HlslPrecompiler
{
    [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Ansi)]
    public static extern int D3DCompile(
        byte[] pSrcData, IntPtr SrcDataSize, string pSourceName,
        IntPtr pDefines, IntPtr pInclude, string pEntrypoint, string pTarget,
        uint Flags1, uint Flags2, out IntPtr ppCode, out IntPtr ppErrorMsgs);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr GetBufPtr(IntPtr p);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr GetBufSize(IntPtr p);

    private static IntPtr GetSlot(IntPtr obj, int slot)
    {
        return Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), slot * IntPtr.Size);
    }

    public static void CompileToFile(string srcPath, string target, string outPath)
    {
        byte[] src = File.ReadAllBytes(srcPath);
        IntPtr code, err;
        int hr = D3DCompile(src, (IntPtr)src.Length, Path.GetFileName(srcPath), IntPtr.Zero, IntPtr.Zero, "main", target, (1u << 15), 0, out code, out err);
        string msg = "";
        if (err != IntPtr.Zero)
        {
            var pFn = (GetBufPtr)Marshal.GetDelegateForFunctionPointer(GetSlot(err, 3), typeof(GetBufPtr));
            var sFn = (GetBufSize)Marshal.GetDelegateForFunctionPointer(GetSlot(err, 4), typeof(GetBufSize));
            IntPtr p = pFn(err);
            int sz = (int)sFn(err);
            if (p != IntPtr.Zero && sz > 0) msg = Marshal.PtrToStringAnsi(p, sz);
            Marshal.Release(err);
        }
        if (hr < 0 || code == IntPtr.Zero)
        {
            if (code != IntPtr.Zero) Marshal.Release(code);
            throw new InvalidOperationException("D3DCompile failed for " + srcPath + ": " + msg);
        }
        try
        {
            var pFn = (GetBufPtr)Marshal.GetDelegateForFunctionPointer(GetSlot(code, 3), typeof(GetBufPtr));
            var sFn = (GetBufSize)Marshal.GetDelegateForFunctionPointer(GetSlot(code, 4), typeof(GetBufSize));
            IntPtr p = pFn(code);
            int sz = (int)sFn(code);
            byte[] bytes = new byte[sz];
            Marshal.Copy(p, bytes, 0, sz);
            File.WriteAllBytes(outPath, bytes);
        }
        finally
        {
            Marshal.Release(code);
        }
    }
}
'@
if (-not ('HlslPrecompiler' -as [type])) {
    Add-Type -TypeDefinition $dxbcCode
}
$BinShaderDir = Join-Path $Root 'shaders\bin'
if (-not (Test-Path $BinShaderDir)) {
    New-Item -ItemType Directory -Path $BinShaderDir -Force | Out-Null
}
$vsDxbc = Join-Path $BinShaderDir 'fullscreen.vs.dxbc'
$sceneDxbc = Join-Path $BinShaderDir 'synthwave_scene.ps.dxbc'
$miamiDxbc = Join-Path $BinShaderDir 'miami_drive.ps.dxbc'
$cycleDxbc = Join-Path $BinShaderDir 'light_cycle.ps.dxbc'
$outrunCycleDxbc = Join-Path $BinShaderDir 'outrun_cycle.ps.dxbc'
$bloomDxbc = Join-Path $BinShaderDir 'bloom_downsample.ps.dxbc'
$crtDxbc = Join-Path $BinShaderDir 'crt_vhs.ps.dxbc'

[HlslPrecompiler]::CompileToFile($vsShader, 'vs_5_0', $vsDxbc)
[HlslPrecompiler]::CompileToFile($sceneShader, 'ps_5_0', $sceneDxbc)
[HlslPrecompiler]::CompileToFile($miamiShader, 'ps_5_0', $miamiDxbc)
[HlslPrecompiler]::CompileToFile($cycleShader, 'ps_5_0', $cycleDxbc)
[HlslPrecompiler]::CompileToFile($outrunCycleShader, 'ps_5_0', $outrunCycleDxbc)
[HlslPrecompiler]::CompileToFile($bloomShader, 'ps_5_0', $bloomDxbc)
[HlslPrecompiler]::CompileToFile($crtShader, 'ps_5_0', $crtDxbc)

$asmInfoPath = Join-Path $env:TEMP 'neon_horizon_assembly_info.cs'
$asmInfoCode = @"
using System.Reflection;
[assembly: AssemblyTitle("Neon Horizon Wallpaper")]
[assembly: AssemblyProduct("Neon Horizon Wallpaper")]
[assembly: AssemblyDescription("Native Synthwave & CRT/VHS Live Desktop Wallpaper")]
[assembly: AssemblyVersion("$AsmVersion")]
[assembly: AssemblyFileVersion("$AsmVersion")]
[assembly: AssemblyInformationalVersion("$CleanVersion")]
"@
[System.IO.File]::WriteAllText($asmInfoPath, $asmInfoCode, [System.Text.Encoding]::UTF8)

$rspPath = Join-Path $env:TEMP 'neon_horizon_wallpaper_csc.rsp'
$rspLines = @(
    '/nologo'
    '/target:winexe'
    '/optimize+'
    '/platform:anycpu'
    "/out:`"$OutExe`""
    "/win32manifest:`"$manifest`""
    "/win32icon:`"$IconPath`""
    "/resource:`"$vsDxbc`",fullscreen.vs.dxbc"
    "/resource:`"$sceneDxbc`",synthwave_scene.ps.dxbc"
    "/resource:`"$miamiDxbc`",miami_drive.ps.dxbc"
    "/resource:`"$cycleDxbc`",light_cycle.ps.dxbc"
    "/resource:`"$outrunCycleDxbc`",outrun_cycle.ps.dxbc"
    "/resource:`"$bloomDxbc`",bloom_downsample.ps.dxbc"
    "/resource:`"$crtDxbc`",crt_vhs.ps.dxbc"
    "/resource:`"$vsShader`",fullscreen.vs.hlsl"
    "/resource:`"$sceneShader`",synthwave_scene.ps.hlsl"
    "/resource:`"$miamiShader`",miami_drive.ps.hlsl"
    "/resource:`"$cycleShader`",light_cycle.ps.hlsl"
    "/resource:`"$outrunCycleShader`",outrun_cycle.ps.hlsl"
    "/resource:`"$bloomShader`",bloom_downsample.ps.hlsl"
    "/resource:`"$crtShader`",crt_vhs.ps.hlsl"
    '/reference:System.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Windows.Forms.dll'
    '/reference:System.Web.Extensions.dll'
    "`"$asmInfoPath`""
) + ($srcFiles | ForEach-Object { "`"$_`"" })

[System.IO.File]::WriteAllLines($rspPath, $rspLines)

Write-Host "Building $OutExe (v$CleanVersion)..."
& $csc "@$rspPath"
if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed with exit code $LASTEXITCODE"
}
Remove-Item -Path $rspPath -ErrorAction SilentlyContinue
Remove-Item -Path $asmInfoPath -ErrorAction SilentlyContinue

$info = Get-Item $OutExe
Write-Host ("Built successfully: {0} ({1:N0} bytes)" -f $info.FullName, $info.Length)
