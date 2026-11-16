using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace NeonHorizonWallpaper
{
    public enum SurfaceMode
    {
        DirectComposition = 0,
        FlipHwnd = 1,
        BltDiscard = 2
    }

    public sealed class D3D11Surface : IDisposable
    {
        public IntPtr Hwnd;
        public uint Width;
        public uint Height;
        public string BackendName;
        public IntPtr SwapChain;
        public IntPtr DCompTarget;
        public IntPtr DCompVisual;
        public IntPtr BackBufferRtv;
        public IntPtr SceneTexture;
        public IntPtr SceneRtv;
        public IntPtr SceneSrv;
        public uint SwapBufferCount;
        public uint SwapFormat;
        public uint SceneWidth;
        public uint SceneHeight;

        public void ReleaseTargets()
        {
            D3D11Native.SafeRelease(ref BackBufferRtv);
            D3D11Native.SafeRelease(ref SceneSrv);
            D3D11Native.SafeRelease(ref SceneRtv);
            D3D11Native.SafeRelease(ref SceneTexture);
            SceneWidth = 0;
            SceneHeight = 0;
        }

        public void Dispose()
        {
            ReleaseTargets();
            D3D11Native.SafeRelease(ref DCompVisual);
            D3D11Native.SafeRelease(ref DCompTarget);
            D3D11Native.SafeRelease(ref SwapChain);
            Hwnd = IntPtr.Zero;
        }
    }

    public sealed class D3D11Renderer : IDisposable
    {
        private IntPtr _device;
        private IntPtr _context;
        private IntPtr _dxgiFactory;
        private IntPtr _dxgiFactory2;
        private IntPtr _dcompDevice;
        private IntPtr _vertexShader;
        private readonly Dictionary<string, IntPtr> _scenePixelShaders = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
        private IntPtr _defaultScenePixelShader;
        private IntPtr _crtPixelShader;
        private IntPtr _sceneConstBuffer;
        private IntPtr _crtConstBuffer;
        private IntPtr _linearSampler;
        private IntPtr _rasterState;

        private readonly IntPtr[] _srvOne = new IntPtr[1];
        private readonly IntPtr[] _srvNull = new IntPtr[] { IntPtr.Zero };
        private readonly IntPtr[] _rtvOne = new IntPtr[1];
        private readonly IntPtr[] _cbOne = new IntPtr[1];
        private readonly IntPtr[] _samplerOne = new IntPtr[1];
        private readonly float[] _clearColor = new float[] { 0.0157f, 0.0039f, 0.0471f, 1.0f };

        public bool IsInitialized { get { return _device != IntPtr.Zero; } }

        public bool Initialize(out string errorMessage)
        {
            errorMessage = string.Empty;
            Dispose();

            int[] featureLevels = new int[] { 0xb000, 0xa100, 0xa000 }; // 11_0, 10_1, 10_0
            int chosenLevel;

            int hr = D3D11Native.D3D11CreateDevice(
                IntPtr.Zero,
                D3D11Native.D3D_DRIVER_TYPE_HARDWARE,
                IntPtr.Zero,
                D3D11Native.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                featureLevels,
                (uint)featureLevels.Length,
                D3D11Native.D3D11_SDK_VERSION,
                out _device,
                out chosenLevel,
                out _context);

            if (hr < 0)
            {
                hr = D3D11Native.D3D11CreateDevice(
                    IntPtr.Zero,
                    D3D11Native.D3D_DRIVER_TYPE_WARP,
                    IntPtr.Zero,
                    D3D11Native.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                    featureLevels,
                    (uint)featureLevels.Length,
                    D3D11Native.D3D11_SDK_VERSION,
                    out _device,
                    out chosenLevel,
                    out _context);
            }

            if (hr < 0 || _device == IntPtr.Zero || _context == IntPtr.Zero)
            {
                errorMessage = string.Format("D3D11CreateDevice failed: 0x{0:X8}", hr);
                Dispose();
                return false;
            }

            if (!AcquireDxgiFactories(out errorMessage))
            {
                Dispose();
                return false;
            }

            if (!CompileAndCreateShaders(out errorMessage))
            {
                Dispose();
                return false;
            }

            if (!CreatePipelineObjects(out errorMessage))
            {
                Dispose();
                return false;
            }

            return true;
        }

        private bool AcquireDxgiFactories(out string errorMessage)
        {
            errorMessage = string.Empty;
            IntPtr dxgiDevice = IntPtr.Zero;
            IntPtr adapter = IntPtr.Zero;
            try
            {
                Guid iidDxgiDev = D3D11Native.IID_IDXGIDevice;
                int hr = Marshal.QueryInterface(_device, ref iidDxgiDev, out dxgiDevice);
                if (hr < 0 || dxgiDevice == IntPtr.Zero)
                {
                    errorMessage = string.Format("QueryInterface(IDXGIDevice) failed: 0x{0:X8}", hr);
                    return false;
                }

                hr = D3D11Native.DxgiDeviceGetAdapter(dxgiDevice, out adapter);
                if (hr < 0 || adapter == IntPtr.Zero)
                {
                    errorMessage = string.Format("IDXGIDevice.GetAdapter failed: 0x{0:X8}", hr);
                    return false;
                }

                Guid iidFactory = D3D11Native.IID_IDXGIFactory;
                hr = D3D11Native.DxgiObjectGetParent(adapter, ref iidFactory, out _dxgiFactory);
                if (hr < 0 || _dxgiFactory == IntPtr.Zero)
                {
                    errorMessage = string.Format("IDXGIAdapter.GetParent(IDXGIFactory) failed: 0x{0:X8}", hr);
                    return false;
                }

                Guid iidFactory2 = D3D11Native.IID_IDXGIFactory2;
                int hr2 = D3D11Native.DxgiObjectGetParent(adapter, ref iidFactory2, out _dxgiFactory2);
                if (hr2 < 0)
                {
                    _dxgiFactory2 = IntPtr.Zero;
                }

                try
                {
                    Guid iidDcomp = D3D11Native.IID_IDCompositionDevice;
                    int hrd = D3D11Native.DCompositionCreateDevice(dxgiDevice, ref iidDcomp, out _dcompDevice);
                    if (hrd < 0)
                    {
                        Logger.Info(string.Format("DCompositionCreateDevice returned 0x{0:X8}; using DXGI HWND SwapChain.", hrd));
                        _dcompDevice = IntPtr.Zero;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info("DirectComposition unavailable: " + ex.Message);
                    _dcompDevice = IntPtr.Zero;
                }

                return true;
            }
            finally
            {
                D3D11Native.SafeRelease(ref adapter);
                D3D11Native.SafeRelease(ref dxgiDevice);
            }
        }

        private static byte[] LoadResourceBytes(string fileName)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string[] names = asm.GetManifestResourceNames();
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream s = asm.GetManifestResourceStream(names[i]))
                    {
                        if (s != null)
                        {
                            byte[] buf = new byte[s.Length];
                            int offset = 0;
                            while (offset < buf.Length)
                            {
                                int read = s.Read(buf, offset, buf.Length - offset);
                                if (read <= 0) break;
                                offset += read;
                            }
                            return buf;
                        }
                    }
                }
            }
            return null;
        }

        private static byte[] LoadShaderBytecode(string hlslFileName, string target, out string errorMessage)
        {
            errorMessage = string.Empty;
            string dxbcName = Path.ChangeExtension(hlslFileName, ".dxbc");
            byte[] precompiled = LoadResourceBytes(dxbcName);
            if (precompiled != null && precompiled.Length > 0)
            {
                return precompiled;
            }

            string diskPath = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shaders"), hlslFileName);
            byte[] src = File.Exists(diskPath) ? File.ReadAllBytes(diskPath) : LoadResourceBytes(hlslFileName);
            if (src == null || src.Length == 0)
            {
                errorMessage = "Shader source not found: " + hlslFileName;
                return null;
            }

            IntPtr code = IntPtr.Zero;
            IntPtr err = IntPtr.Zero;
            uint flags = (1u << 15);
            int hr = D3D11Native.D3DCompile(
                src,
                (IntPtr)src.Length,
                hlslFileName,
                IntPtr.Zero,
                IntPtr.Zero,
                "main",
                target,
                flags,
                0,
                out code,
                out err);

            if (err != IntPtr.Zero)
            {
                IntPtr ptr = D3D11Native.BlobGetBufferPointer(err);
                int sz = (int)D3D11Native.BlobGetBufferSize(err);
                if (ptr != IntPtr.Zero && sz > 0)
                {
                    errorMessage = Marshal.PtrToStringAnsi(ptr, sz);
                }
                D3D11Native.SafeRelease(ref err);
            }

            if (hr < 0 || code == IntPtr.Zero)
            {
                D3D11Native.SafeRelease(ref code);
                if (string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = string.Format("D3DCompile({0}) failed: 0x{1:X8}", hlslFileName, hr);
                }
                return null;
            }

            try
            {
                IntPtr codePtr = D3D11Native.BlobGetBufferPointer(code);
                int codeLen = (int)D3D11Native.BlobGetBufferSize(code);
                byte[] bytecode = new byte[codeLen];
                Marshal.Copy(codePtr, bytecode, 0, codeLen);
                return bytecode;
            }
            finally
            {
                D3D11Native.SafeRelease(ref code);
            }
        }

        private bool CreateShaderFromBytecode(byte[] bytecode, bool isVertex, out IntPtr shader, out string errorMessage)
        {
            shader = IntPtr.Zero;
            errorMessage = string.Empty;
            IntPtr mem = Marshal.AllocHGlobal(bytecode.Length);
            try
            {
                Marshal.Copy(bytecode, 0, mem, bytecode.Length);
                int hr = isVertex
                    ? D3D11Native.DeviceCreateVertexShader(_device, mem, (IntPtr)bytecode.Length, out shader)
                    : D3D11Native.DeviceCreatePixelShader(_device, mem, (IntPtr)bytecode.Length, out shader);
                if (hr < 0 || shader == IntPtr.Zero)
                {
                    errorMessage = string.Format("CreateShader failed: 0x{0:X8}", hr);
                    return false;
                }
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        private bool CompileAndCreateShaders(out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                byte[] vsBytes = LoadShaderBytecode("fullscreen.vs.hlsl", "vs_5_0", out errorMessage);
                if (vsBytes == null) return false;
                if (!CreateShaderFromBytecode(vsBytes, true, out _vertexShader, out errorMessage)) return false;

                for (int i = 0; i < AppSettings.Scenes.Length; i++)
                {
                    SceneDefinition sceneDef = AppSettings.Scenes[i];
                    byte[] psBytes = LoadShaderBytecode(sceneDef.ShaderFile, "ps_5_0", out errorMessage);
                    if (psBytes == null) return false;

                    IntPtr ps;
                    if (!CreateShaderFromBytecode(psBytes, false, out ps, out errorMessage)) return false;
                    _scenePixelShaders[sceneDef.Id] = ps;
                    if (_defaultScenePixelShader == IntPtr.Zero)
                    {
                        _defaultScenePixelShader = ps;
                    }
                }

                byte[] crtBytes = LoadShaderBytecode("crt_vhs.ps.hlsl", "ps_5_0", out errorMessage);
                if (crtBytes == null) return false;
                if (!CreateShaderFromBytecode(crtBytes, false, out _crtPixelShader, out errorMessage)) return false;

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private bool CreatePipelineObjects(out string errorMessage)
        {
            errorMessage = string.Empty;

            D3D11Native.D3D11_BUFFER_DESC sceneCbDesc = new D3D11Native.D3D11_BUFFER_DESC();
            sceneCbDesc.ByteWidth = (uint)Marshal.SizeOf(typeof(D3D11Native.SceneConstants));
            sceneCbDesc.Usage = D3D11Native.D3D11_USAGE_DEFAULT;
            sceneCbDesc.BindFlags = D3D11Native.D3D11_BIND_CONSTANT_BUFFER;

            int hr = D3D11Native.DeviceCreateBuffer(_device, ref sceneCbDesc, out _sceneConstBuffer);
            if (hr < 0)
            {
                errorMessage = string.Format("CreateBuffer(SceneCB) failed: 0x{0:X8}", hr);
                return false;
            }

            D3D11Native.D3D11_BUFFER_DESC crtCbDesc = new D3D11Native.D3D11_BUFFER_DESC();
            crtCbDesc.ByteWidth = (uint)Marshal.SizeOf(typeof(D3D11Native.CrtConstants));
            crtCbDesc.Usage = D3D11Native.D3D11_USAGE_DEFAULT;
            crtCbDesc.BindFlags = D3D11Native.D3D11_BIND_CONSTANT_BUFFER;

            hr = D3D11Native.DeviceCreateBuffer(_device, ref crtCbDesc, out _crtConstBuffer);
            if (hr < 0)
            {
                errorMessage = string.Format("CreateBuffer(CrtCB) failed: 0x{0:X8}", hr);
                return false;
            }

            D3D11Native.D3D11_SAMPLER_DESC sampDesc = new D3D11Native.D3D11_SAMPLER_DESC();
            sampDesc.Filter = D3D11Native.D3D11_FILTER_MIN_MAG_MIP_LINEAR;
            sampDesc.AddressU = D3D11Native.D3D11_TEXTURE_ADDRESS_CLAMP;
            sampDesc.AddressV = D3D11Native.D3D11_TEXTURE_ADDRESS_CLAMP;
            sampDesc.AddressW = D3D11Native.D3D11_TEXTURE_ADDRESS_CLAMP;
            sampDesc.MaxAnisotropy = 1;
            sampDesc.ComparisonFunc = D3D11Native.D3D11_COMPARISON_NEVER;
            sampDesc.MinLOD = 0.0f;
            sampDesc.MaxLOD = float.MaxValue;

            hr = D3D11Native.DeviceCreateSamplerState(_device, ref sampDesc, out _linearSampler);
            if (hr < 0)
            {
                errorMessage = string.Format("CreateSamplerState failed: 0x{0:X8}", hr);
                return false;
            }

            D3D11Native.D3D11_RASTERIZER_DESC rsDesc = new D3D11Native.D3D11_RASTERIZER_DESC();
            rsDesc.FillMode = D3D11Native.D3D11_FILL_SOLID;
            rsDesc.CullMode = D3D11Native.D3D11_CULL_NONE;
            rsDesc.DepthClipEnable = true;

            hr = D3D11Native.DeviceCreateRasterizerState(_device, ref rsDesc, out _rasterState);
            if (hr < 0)
            {
                errorMessage = string.Format("CreateRasterizerState failed: 0x{0:X8}", hr);
                return false;
            }

            return true;
        }

        public bool HasDirectComposition { get { return _dcompDevice != IntPtr.Zero && _dxgiFactory2 != IntPtr.Zero; } }

        public D3D11Surface CreateSurface(IntPtr hwnd, int width, int height, SurfaceMode preferredMode, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!IsInitialized || hwnd == IntPtr.Zero)
            {
                errorMessage = "Renderer not initialized or invalid HWND.";
                return null;
            }

            uint w = (uint)Math.Max(64, width);
            uint h = (uint)Math.Max(64, height);

            D3D11Surface surface = new D3D11Surface();
            surface.Hwnd = hwnd;
            surface.Width = w;
            surface.Height = h;
            surface.SwapFormat = D3D11Native.DXGI_FORMAT_B8G8R8A8_UNORM;

            bool bound = false;

            if (preferredMode == SurfaceMode.DirectComposition && HasDirectComposition)
            {
                D3D11Native.DXGI_SWAP_CHAIN_DESC1 compDesc = new D3D11Native.DXGI_SWAP_CHAIN_DESC1();
                compDesc.Width = w;
                compDesc.Height = h;
                compDesc.Format = surface.SwapFormat;
                compDesc.Stereo = false;
                compDesc.SampleDesc.Count = 1;
                compDesc.SampleDesc.Quality = 0;
                compDesc.BufferUsage = D3D11Native.DXGI_USAGE_RENDER_TARGET_OUTPUT;
                compDesc.BufferCount = 2;
                compDesc.Scaling = D3D11Native.DXGI_SCALING_STRETCH;
                compDesc.SwapEffect = D3D11Native.DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
                compDesc.AlphaMode = D3D11Native.DXGI_ALPHA_MODE_IGNORE;
                compDesc.Flags = 0;

                int hrSc = D3D11Native.DxgiFactory2CreateSwapChainForComposition(_dxgiFactory2, _device, ref compDesc, out surface.SwapChain);
                if (hrSc >= 0 && surface.SwapChain != IntPtr.Zero)
                {
                    int hrTgt = D3D11Native.DCompCreateTargetForHwnd(_dcompDevice, hwnd, true, out surface.DCompTarget);
                    int hrVis = hrTgt >= 0 ? D3D11Native.DCompCreateVisual(_dcompDevice, out surface.DCompVisual) : hrTgt;
                    int hrSet = hrVis >= 0 ? D3D11Native.DCompVisualSetContent(surface.DCompVisual, surface.SwapChain) : hrVis;
                    int hrRoot = hrSet >= 0 ? D3D11Native.DCompTargetSetRoot(surface.DCompTarget, surface.DCompVisual) : hrSet;
                    int hrCom = hrRoot >= 0 ? D3D11Native.DCompCommit(_dcompDevice) : hrRoot;

                    if (hrCom >= 0)
                    {
                        surface.SwapBufferCount = 2;
                        surface.BackendName = "DComp";
                        bound = true;
                    }
                    else
                    {
                        Logger.Error(string.Format("DirectComposition bind failed: tgt=0x{0:X8} vis=0x{1:X8} set=0x{2:X8} root=0x{3:X8} commit=0x{4:X8}", hrTgt, hrVis, hrSet, hrRoot, hrCom));
                        D3D11Native.SafeRelease(ref surface.DCompVisual);
                        D3D11Native.SafeRelease(ref surface.DCompTarget);
                        D3D11Native.SafeRelease(ref surface.SwapChain);
                    }
                }
                else
                {
                    Logger.Error(string.Format("CreateSwapChainForComposition failed: 0x{0:X8}", hrSc));
                }
            }

            if (!bound && preferredMode == SurfaceMode.FlipHwnd && _dxgiFactory2 != IntPtr.Zero)
            {
                D3D11Native.DXGI_SWAP_CHAIN_DESC1 desc1 = new D3D11Native.DXGI_SWAP_CHAIN_DESC1();
                desc1.Width = w;
                desc1.Height = h;
                desc1.Format = surface.SwapFormat;
                desc1.Stereo = false;
                desc1.SampleDesc.Count = 1;
                desc1.SampleDesc.Quality = 0;
                desc1.BufferUsage = D3D11Native.DXGI_USAGE_RENDER_TARGET_OUTPUT;
                desc1.BufferCount = 2;
                desc1.Scaling = D3D11Native.DXGI_SCALING_STRETCH;
                desc1.SwapEffect = D3D11Native.DXGI_SWAP_EFFECT_FLIP_DISCARD;
                desc1.AlphaMode = D3D11Native.DXGI_ALPHA_MODE_UNSPECIFIED;
                desc1.Flags = 0;

                int hrFlip = D3D11Native.DxgiFactory2CreateSwapChainForHwnd(_dxgiFactory2, _device, hwnd, ref desc1, out surface.SwapChain);
                if (hrFlip >= 0 && surface.SwapChain != IntPtr.Zero)
                {
                    surface.SwapBufferCount = 2;
                    surface.BackendName = "FlipHwnd";
                    bound = true;
                }
            }

            if (!bound)
            {
                D3D11Native.SafeRelease(ref surface.SwapChain);
                D3D11Native.DXGI_SWAP_CHAIN_DESC scDesc = new D3D11Native.DXGI_SWAP_CHAIN_DESC();
                scDesc.BufferCount = 1;
                scDesc.BufferDesc.Width = w;
                scDesc.BufferDesc.Height = h;
                scDesc.BufferDesc.Format = surface.SwapFormat;
                scDesc.BufferDesc.RefreshRate.Numerator = 60;
                scDesc.BufferDesc.RefreshRate.Denominator = 1;
                scDesc.BufferUsage = D3D11Native.DXGI_USAGE_RENDER_TARGET_OUTPUT;
                scDesc.OutputWindow = hwnd;
                scDesc.Windowed = true;
                scDesc.SampleDesc.Count = 1;
                scDesc.SampleDesc.Quality = 0;
                scDesc.SwapEffect = D3D11Native.DXGI_SWAP_EFFECT_DISCARD;
                scDesc.Flags = 0;

                int hrBlt = D3D11Native.DxgiFactoryCreateSwapChain(_dxgiFactory, _device, ref scDesc, out surface.SwapChain);
                if (hrBlt < 0 || surface.SwapChain == IntPtr.Zero)
                {
                    errorMessage = string.Format("CreateSwapChain failed: 0x{0:X8}", hrBlt);
                    surface.Dispose();
                    return null;
                }
                surface.SwapBufferCount = 1;
                surface.BackendName = "BltDiscard";
            }

            D3D11Native.DxgiFactoryMakeWindowAssociation(_dxgiFactory, hwnd, D3D11Native.DXGI_MWA_NO_ALT_ENTER);

            if (!CreateSurfaceTargets(surface, out errorMessage))
            {
                surface.Dispose();
                return null;
            }

            return surface;
        }

        public bool ResizeSurface(D3D11Surface surface, int width, int height, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (surface == null || surface.SwapChain == IntPtr.Zero) return false;

            uint w = (uint)Math.Max(64, width);
            uint h = (uint)Math.Max(64, height);
            if (surface.Width == w && surface.Height == h && surface.BackBufferRtv != IntPtr.Zero)
            {
                return true;
            }

            surface.ReleaseTargets();
            D3D11Native.ContextOMSetRenderTargets(_context, new IntPtr[] { IntPtr.Zero });

            int hr = D3D11Native.SwapChainResizeBuffers(
                surface.SwapChain,
                surface.SwapBufferCount,
                w,
                h,
                surface.SwapFormat,
                0);
            if (hr < 0)
            {
                errorMessage = string.Format("ResizeBuffers failed: 0x{0:X8}", hr);
                return false;
            }

            surface.Width = w;
            surface.Height = h;
            if (!CreateSurfaceTargets(surface, out errorMessage))
            {
                return false;
            }
            if (surface.DCompTarget != IntPtr.Zero && _dcompDevice != IntPtr.Zero)
            {
                D3D11Native.DCompCommit(_dcompDevice);
            }
            return true;
        }

        private bool CreateSurfaceTargets(D3D11Surface surface, out string errorMessage)
        {
            errorMessage = string.Empty;
            IntPtr backBuffer = IntPtr.Zero;
            try
            {
                Guid iidTex = D3D11Native.IID_ID3D11Texture2D;
                int hr = D3D11Native.SwapChainGetBuffer(surface.SwapChain, 0, ref iidTex, out backBuffer);
                if (hr < 0 || backBuffer == IntPtr.Zero)
                {
                    errorMessage = string.Format("SwapChain.GetBuffer failed: 0x{0:X8}", hr);
                    return false;
                }

                hr = D3D11Native.DeviceCreateRenderTargetView(_device, backBuffer, out surface.BackBufferRtv);
                if (hr < 0 || surface.BackBufferRtv == IntPtr.Zero)
                {
                    errorMessage = string.Format("CreateRenderTargetView(BackBuffer) failed: 0x{0:X8}", hr);
                    return false;
                }

                surface.SceneWidth = surface.Width;
                surface.SceneHeight = surface.Height;
                return CreateOffscreenSceneTarget(surface.Width, surface.Height, out surface.SceneTexture, out surface.SceneRtv, out surface.SceneSrv, out errorMessage);
            }
            finally
            {
                D3D11Native.SafeRelease(ref backBuffer);
            }
        }

        private bool CreateOffscreenSceneTarget(uint width, uint height, out IntPtr tex, out IntPtr rtv, out IntPtr srv, out string errorMessage)
        {
            tex = IntPtr.Zero;
            rtv = IntPtr.Zero;
            srv = IntPtr.Zero;
            errorMessage = string.Empty;

            D3D11Native.D3D11_TEXTURE2D_DESC td = new D3D11Native.D3D11_TEXTURE2D_DESC();
            td.Width = width;
            td.Height = height;
            td.MipLevels = 1;
            td.ArraySize = 1;
            td.Format = D3D11Native.DXGI_FORMAT_R8G8B8A8_UNORM;
            td.SampleDesc.Count = 1;
            td.SampleDesc.Quality = 0;
            td.Usage = D3D11Native.D3D11_USAGE_DEFAULT;
            td.BindFlags = D3D11Native.D3D11_BIND_RENDER_TARGET | D3D11Native.D3D11_BIND_SHADER_RESOURCE;

            int hr = D3D11Native.DeviceCreateTexture2D(_device, ref td, out tex);
            if (hr < 0 || tex == IntPtr.Zero)
            {
                errorMessage = string.Format("CreateTexture2D(Scene) failed: 0x{0:X8}", hr);
                return false;
            }

            hr = D3D11Native.DeviceCreateRenderTargetView(_device, tex, out rtv);
            if (hr < 0 || rtv == IntPtr.Zero)
            {
                errorMessage = string.Format("CreateRenderTargetView(Scene) failed: 0x{0:X8}", hr);
                D3D11Native.SafeRelease(ref tex);
                return false;
            }

            hr = D3D11Native.DeviceCreateShaderResourceView(_device, tex, out srv);
            if (hr < 0 || srv == IntPtr.Zero)
            {
                errorMessage = string.Format("CreateShaderResourceView(Scene) failed: 0x{0:X8}", hr);
                D3D11Native.SafeRelease(ref rtv);
                D3D11Native.SafeRelease(ref tex);
                return false;
            }

            return true;
        }

        private IntPtr ResolveScenePixelShader(string sceneId)
        {
            IntPtr ps;
            if (!string.IsNullOrEmpty(sceneId) && _scenePixelShaders.TryGetValue(sceneId, out ps))
            {
                return ps;
            }
            return _defaultScenePixelShader;
        }

        private static void ComputeSceneDimensions(uint targetW, uint targetH, string quality, out uint sceneW, out uint sceneH)
        {
            float scale = 1.0f;
            if (string.Equals(quality, "eco", StringComparison.OrdinalIgnoreCase))
            {
                scale = 0.60f;
            }
            else if (string.Equals(quality, "balanced", StringComparison.OrdinalIgnoreCase))
            {
                scale = 0.75f;
            }
            else if (!string.Equals(quality, "native", StringComparison.OrdinalIgnoreCase))
            {
                // Auto mode: stay 100% native up to ~1440p, dynamically scale Pass 1 on 4K/5K2K (e.g. 5120x2160) while keeping Pass 2 CRT native
                ulong pixels = (ulong)targetW * targetH;
                if (pixels > 4000000UL)
                {
                    scale = (float)Math.Sqrt(3800000.0 / pixels);
                    if (scale < 0.60f) scale = 0.60f;
                    if (scale > 0.78f) scale = 0.78f;
                }
            }

            sceneW = Math.Max(64u, (uint)Math.Round(targetW * scale));
            sceneH = Math.Max(64u, (uint)Math.Round(targetH * scale));
        }

        private bool EnsureSurfaceSceneTarget(D3D11Surface surface, uint sceneW, uint sceneH)
        {
            if (surface.SceneWidth == sceneW && surface.SceneHeight == sceneH && surface.SceneRtv != IntPtr.Zero && surface.SceneSrv != IntPtr.Zero)
            {
                return true;
            }

            D3D11Native.ContextPSSetShaderResources(_context, 0, _srvNull);
            D3D11Native.ContextOMSetRenderTargets(_context, new IntPtr[] { IntPtr.Zero });
            D3D11Native.SafeRelease(ref surface.SceneSrv);
            D3D11Native.SafeRelease(ref surface.SceneRtv);
            D3D11Native.SafeRelease(ref surface.SceneTexture);
            surface.SceneWidth = 0;
            surface.SceneHeight = 0;

            string err;
            if (!CreateOffscreenSceneTarget(sceneW, sceneH, out surface.SceneTexture, out surface.SceneRtv, out surface.SceneSrv, out err))
            {
                Logger.Error("EnsureSurfaceSceneTarget failed: " + err);
                return false;
            }
            surface.SceneWidth = sceneW;
            surface.SceneHeight = sceneH;
            return true;
        }

        private void RenderPasses(
            IntPtr sceneRtv,
            IntPtr sceneSrv,
            IntPtr targetRtv,
            uint sceneWidth,
            uint sceneHeight,
            uint targetWidth,
            uint targetHeight,
            uint dpi,
            float sceneTime,
            float gridTime,
            float crtTime,
            AppSettings settings)
        {
            D3D11Native.D3D11_VIEWPORT vp = new D3D11Native.D3D11_VIEWPORT();
            vp.Width = sceneWidth;
            vp.Height = sceneHeight;
            vp.MinDepth = 0.0f;
            vp.MaxDepth = 1.0f;

            D3D11Native.ContextIASetPrimitiveTopology(_context, D3D11Native.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            D3D11Native.ContextRSSetState(_context, _rasterState);
            D3D11Native.ContextRSSetViewports(_context, ref vp);
            D3D11Native.ContextVSSetShader(_context, _vertexShader);

            // Pass 1: Active Synthwave scene -> offscreen texture (fullscreen triangle overwrites 100%, no clear needed)
            D3D11Native.SceneConstants sc = new D3D11Native.SceneConstants();
            sc.ResolutionX = sceneWidth;
            sc.ResolutionY = sceneHeight;
            sc.Time = sceneTime;
            sc.GridTime = gridTime;
            UpdateConstantBuffer(_sceneConstBuffer, sc);

            D3D11Native.ContextPSSetShaderResources(_context, 0, _srvNull);
            _rtvOne[0] = sceneRtv;
            D3D11Native.ContextOMSetRenderTargets(_context, _rtvOne);
            D3D11Native.ContextPSSetShader(_context, ResolveScenePixelShader(settings.Scene));
            _cbOne[0] = _sceneConstBuffer;
            D3D11Native.ContextPSSetConstantBuffers(_context, 0, _cbOne);
            D3D11Native.ContextDraw(_context, 3, 0);

            // Pass 2: CRT/VHS post-processing at native target resolution (wider scanline period on 4K/5K displays)
            vp.Width = targetWidth;
            vp.Height = targetHeight;
            D3D11Native.ContextRSSetViewports(_context, ref vp);

            float dpr = Math.Max(0.5f, dpi / 96.0f);
            float effectiveScale = Math.Max(dpr, targetHeight / 1080.0f);
            int physPeriod = Math.Max(4, (int)Math.Round(4.5f * effectiveScale));
            if ((physPeriod & 1) != 0) physPeriod += 1;

            bool active = settings.Enabled;
            D3D11Native.CrtConstants cc = new D3D11Native.CrtConstants();
            cc.ResolutionX = targetWidth;
            cc.ResolutionY = targetHeight;
            cc.Time = crtTime;
            cc.Curvature = active ? settings.Curvature / 100.0f : 0.0f;
            cc.Scanlines = active ? settings.Scanlines / 100.0f : 0.0f;
            cc.Bloom = active ? settings.Bloom / 100.0f : 0.0f;
            cc.Vhs = active ? settings.Vhs / 100.0f : 0.0f;
            cc.Sweep = active ? settings.Sweep / 100.0f : 0.0f;
            cc.Vignette = active ? settings.Vignette / 100.0f : 0.0f;
            cc.Animate = (active && settings.Animate) ? 1.0f : 0.0f;
            cc.ScanPeriod = physPeriod;
            UpdateConstantBuffer(_crtConstBuffer, cc);

            _rtvOne[0] = targetRtv;
            D3D11Native.ContextOMSetRenderTargets(_context, _rtvOne);
            D3D11Native.ContextPSSetShader(_context, _crtPixelShader);
            _cbOne[0] = _crtConstBuffer;
            D3D11Native.ContextPSSetConstantBuffers(_context, 0, _cbOne);
            _samplerOne[0] = _linearSampler;
            D3D11Native.ContextPSSetSamplers(_context, 0, _samplerOne);
            _srvOne[0] = sceneSrv;
            D3D11Native.ContextPSSetShaderResources(_context, 0, _srvOne);
            D3D11Native.ContextDraw(_context, 3, 0);

            // Unbind SRV so next frame Pass 1 can safely write to SceneRtv
            D3D11Native.ContextPSSetShaderResources(_context, 0, _srvNull);
        }

        public int RenderAndPresent(
            D3D11Surface surface,
            uint dpi,
            float sceneTime,
            float gridTime,
            float crtTime,
            AppSettings settings,
            bool vsync)
        {
            if (!IsInitialized || surface == null || surface.SwapChain == IntPtr.Zero) return -1;

            uint sceneW, sceneH;
            ComputeSceneDimensions(surface.Width, surface.Height, settings.RenderQuality, out sceneW, out sceneH);
            if (!EnsureSurfaceSceneTarget(surface, sceneW, sceneH)) return -1;

            RenderPasses(
                surface.SceneRtv,
                surface.SceneSrv,
                surface.BackBufferRtv,
                sceneW,
                sceneH,
                surface.Width,
                surface.Height,
                dpi,
                sceneTime,
                gridTime,
                crtTime,
                settings);

            return D3D11Native.SwapChainPresent(surface.SwapChain, vsync ? 1u : 0u, 0u);
        }

        /// <summary>
        /// Renders both shader passes into an offscreen RGBA8 texture and reads back the pixels.
        /// Useful for automated GPU rendering tests and static wallpaper snapshot export.
        /// </summary>
        public byte[] RenderOffscreenRgba(int width, int height, uint dpi, float sceneTime, float gridTime, float crtTime, AppSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!IsInitialized)
            {
                errorMessage = "Renderer is not initialized.";
                return null;
            }

            uint w = (uint)Math.Max(32, width);
            uint h = (uint)Math.Max(32, height);
            uint sceneW, sceneH;
            ComputeSceneDimensions(w, h, settings.RenderQuality, out sceneW, out sceneH);
            IntPtr sceneTex = IntPtr.Zero;
            IntPtr sceneRtv = IntPtr.Zero;
            IntPtr sceneSrv = IntPtr.Zero;
            IntPtr finalTex = IntPtr.Zero;
            IntPtr finalRtv = IntPtr.Zero;
            IntPtr stagingTex = IntPtr.Zero;

            try
            {
                if (!CreateOffscreenSceneTarget(sceneW, sceneH, out sceneTex, out sceneRtv, out sceneSrv, out errorMessage))
                {
                    return null;
                }

                D3D11Native.D3D11_TEXTURE2D_DESC td = new D3D11Native.D3D11_TEXTURE2D_DESC();
                td.Width = w;
                td.Height = h;
                td.MipLevels = 1;
                td.ArraySize = 1;
                td.Format = D3D11Native.DXGI_FORMAT_R8G8B8A8_UNORM;
                td.SampleDesc.Count = 1;
                td.Usage = D3D11Native.D3D11_USAGE_DEFAULT;
                td.BindFlags = D3D11Native.D3D11_BIND_RENDER_TARGET;

                int hr = D3D11Native.DeviceCreateTexture2D(_device, ref td, out finalTex);
                if (hr < 0) { errorMessage = string.Format("CreateTexture2D(Final) failed: 0x{0:X8}", hr); return null; }

                hr = D3D11Native.DeviceCreateRenderTargetView(_device, finalTex, out finalRtv);
                if (hr < 0) { errorMessage = string.Format("CreateRenderTargetView(Final) failed: 0x{0:X8}", hr); return null; }

                td.Usage = D3D11Native.D3D11_USAGE_STAGING;
                td.BindFlags = 0;
                td.CPUAccessFlags = D3D11Native.D3D11_CPU_ACCESS_READ;
                hr = D3D11Native.DeviceCreateTexture2D(_device, ref td, out stagingTex);
                if (hr < 0) { errorMessage = string.Format("CreateTexture2D(Staging) failed: 0x{0:X8}", hr); return null; }

                RenderPasses(sceneRtv, sceneSrv, finalRtv, sceneW, sceneH, w, h, dpi, sceneTime, gridTime, crtTime, settings);
                D3D11Native.ContextCopyResource(_context, stagingTex, finalTex);

                D3D11Native.D3D11_MAPPED_SUBRESOURCE mapped;
                hr = D3D11Native.ContextMap(_context, stagingTex, 0, D3D11Native.D3D11_MAP_READ, out mapped);
                if (hr < 0) { errorMessage = string.Format("Context.Map failed: 0x{0:X8}", hr); return null; }

                try
                {
                    int rowBytes = (int)(w * 4);
                    int heightInt = (int)h;
                    byte[] pixels = new byte[rowBytes * heightInt];
                    for (int y = 0; y < heightInt; y++)
                    {
                        IntPtr rowPtr = new IntPtr(mapped.pData.ToInt64() + (long)y * mapped.RowPitch);
                        Marshal.Copy(rowPtr, pixels, y * rowBytes, rowBytes);
                    }
                    return pixels;
                }
                finally
                {
                    D3D11Native.ContextUnmap(_context, stagingTex, 0);
                }
            }
            finally
            {
                D3D11Native.SafeRelease(ref stagingTex);
                D3D11Native.SafeRelease(ref finalRtv);
                D3D11Native.SafeRelease(ref finalTex);
                D3D11Native.SafeRelease(ref sceneSrv);
                D3D11Native.SafeRelease(ref sceneRtv);
                D3D11Native.SafeRelease(ref sceneTex);
            }
        }

        private void UpdateConstantBuffer<T>(IntPtr buffer, T value) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr mem = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(value, mem, false);
                D3D11Native.ContextUpdateSubresource(_context, buffer, mem);
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        public void Dispose()
        {
            D3D11Native.SafeRelease(ref _rasterState);
            D3D11Native.SafeRelease(ref _linearSampler);
            D3D11Native.SafeRelease(ref _crtConstBuffer);
            D3D11Native.SafeRelease(ref _sceneConstBuffer);
            D3D11Native.SafeRelease(ref _crtPixelShader);
            foreach (KeyValuePair<string, IntPtr> kv in _scenePixelShaders)
            {
                IntPtr ps = kv.Value;
                D3D11Native.SafeRelease(ref ps);
            }
            _scenePixelShaders.Clear();
            _defaultScenePixelShader = IntPtr.Zero;
            D3D11Native.SafeRelease(ref _vertexShader);
            D3D11Native.SafeRelease(ref _dcompDevice);
            D3D11Native.SafeRelease(ref _dxgiFactory2);
            D3D11Native.SafeRelease(ref _dxgiFactory);
            D3D11Native.SafeRelease(ref _context);
            D3D11Native.SafeRelease(ref _device);
        }
    }
}
