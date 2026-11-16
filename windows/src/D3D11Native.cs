using System;
using System.Runtime.InteropServices;

namespace NeonHorizonWallpaper
{
    internal static class D3D11Native
    {
        public const uint D3D11_SDK_VERSION = 7;
        public const int D3D_DRIVER_TYPE_HARDWARE = 1;
        public const int D3D_DRIVER_TYPE_WARP = 5;

        public const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
        public const uint D3D11_BIND_SHADER_RESOURCE = 0x8;
        public const uint D3D11_BIND_RENDER_TARGET = 0x20;
        public const uint D3D11_BIND_CONSTANT_BUFFER = 0x4;

        public const uint D3D11_USAGE_DEFAULT = 0;
        public const uint D3D11_USAGE_STAGING = 3;
        public const uint D3D11_CPU_ACCESS_READ = 0x20000;
        public const uint D3D11_MAP_READ = 1;

        public const uint D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST = 4;

        public const uint D3D11_FILTER_MIN_MAG_MIP_LINEAR = 0x15;
        public const uint D3D11_TEXTURE_ADDRESS_CLAMP = 3;
        public const uint D3D11_COMPARISON_NEVER = 1;
        public const uint D3D11_FILL_SOLID = 3;
        public const uint D3D11_CULL_NONE = 1;

        public const uint DXGI_FORMAT_R8G8B8A8_UNORM = 28;
        public const uint DXGI_FORMAT_B8G8R8A8_UNORM = 87;
        public const uint DXGI_USAGE_RENDER_TARGET_OUTPUT = 0x20;
        public const uint DXGI_SWAP_EFFECT_DISCARD = 0;
        public const uint DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL = 3;
        public const uint DXGI_SWAP_EFFECT_FLIP_DISCARD = 4;
        public const uint DXGI_SCALING_STRETCH = 0;
        public const uint DXGI_ALPHA_MODE_UNSPECIFIED = 0;
        public const uint DXGI_ALPHA_MODE_IGNORE = 3;
        public const uint DXGI_MWA_NO_ALT_ENTER = 2;

        public const int DXGI_ERROR_DEVICE_REMOVED = unchecked((int)0x887A0005);
        public const int DXGI_ERROR_DEVICE_RESET = unchecked((int)0x887A0007);

        public static readonly Guid IID_IDXGIDevice = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
        public static readonly Guid IID_IDXGIFactory = new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
        public static readonly Guid IID_IDXGIFactory2 = new Guid("50c83a1c-e072-4c48-87b0-3630fa36a6d0");
        public static readonly Guid IID_ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        public static readonly Guid IID_IDCompositionDevice = new Guid("C37EA93A-E7AA-450D-B16F-9746CB0407F3");

        [StructLayout(LayoutKind.Sequential)]
        public struct DXGI_RATIONAL
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DXGI_MODE_DESC
        {
            public uint Width;
            public uint Height;
            public DXGI_RATIONAL RefreshRate;
            public uint Format;
            public uint ScanlineOrdering;
            public uint Scaling;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DXGI_SAMPLE_DESC
        {
            public uint Count;
            public uint Quality;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DXGI_SWAP_CHAIN_DESC
        {
            public DXGI_MODE_DESC BufferDesc;
            public DXGI_SAMPLE_DESC SampleDesc;
            public uint BufferUsage;
            public uint BufferCount;
            public IntPtr OutputWindow;
            [MarshalAs(UnmanagedType.Bool)]
            public bool Windowed;
            public uint SwapEffect;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DXGI_SWAP_CHAIN_DESC1
        {
            public uint Width;
            public uint Height;
            public uint Format;
            [MarshalAs(UnmanagedType.Bool)]
            public bool Stereo;
            public DXGI_SAMPLE_DESC SampleDesc;
            public uint BufferUsage;
            public uint BufferCount;
            public uint Scaling;
            public uint SwapEffect;
            public uint AlphaMode;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_BUFFER_DESC
        {
            public uint ByteWidth;
            public uint Usage;
            public uint BindFlags;
            public uint CPUAccessFlags;
            public uint MiscFlags;
            public uint StructureByteStride;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_TEXTURE2D_DESC
        {
            public uint Width;
            public uint Height;
            public uint MipLevels;
            public uint ArraySize;
            public uint Format;
            public DXGI_SAMPLE_DESC SampleDesc;
            public uint Usage;
            public uint BindFlags;
            public uint CPUAccessFlags;
            public uint MiscFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_VIEWPORT
        {
            public float TopLeftX;
            public float TopLeftY;
            public float Width;
            public float Height;
            public float MinDepth;
            public float MaxDepth;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_SAMPLER_DESC
        {
            public uint Filter;
            public uint AddressU;
            public uint AddressV;
            public uint AddressW;
            public float MipLODBias;
            public uint MaxAnisotropy;
            public uint ComparisonFunc;
            public float BorderR;
            public float BorderG;
            public float BorderB;
            public float BorderA;
            public float MinLOD;
            public float MaxLOD;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_RASTERIZER_DESC
        {
            public uint FillMode;
            public uint CullMode;
            [MarshalAs(UnmanagedType.Bool)]
            public bool FrontCounterClockwise;
            public int DepthBias;
            public float DepthBiasClamp;
            public float SlopeScaledDepthBias;
            [MarshalAs(UnmanagedType.Bool)]
            public bool DepthClipEnable;
            [MarshalAs(UnmanagedType.Bool)]
            public bool ScissorEnable;
            [MarshalAs(UnmanagedType.Bool)]
            public bool MultisampleEnable;
            [MarshalAs(UnmanagedType.Bool)]
            public bool AntialiasedLineEnable;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3D11_MAPPED_SUBRESOURCE
        {
            public IntPtr pData;
            public uint RowPitch;
            public uint DepthPitch;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SceneConstants
        {
            public float ResolutionX;
            public float ResolutionY;
            public float Time;
            public float GridTime;
            public float LayerMode;
            public float Pad0;
            public float Pad1;
            public float Pad2;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CrtConstants
        {
            public float ResolutionX;
            public float ResolutionY;
            public float Time;
            public float Curvature;
            public float Scanlines;
            public float Bloom;
            public float Vhs;
            public float Sweep;
            public float Vignette;
            public float Animate;
            public float ScanPeriod;
            public float Pad0;
        }

        [DllImport("d3d11.dll", CallingConvention = CallingConvention.Winapi, PreserveSig = true)]
        public static extern int D3D11CreateDevice(
            IntPtr pAdapter,
            int DriverType,
            IntPtr Software,
            uint Flags,
            int[] pFeatureLevels,
            uint FeatureLevels,
            uint SDKVersion,
            out IntPtr ppDevice,
            out int pFeatureLevel,
            out IntPtr ppImmediateContext);

        [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Ansi, PreserveSig = true)]
        public static extern int D3DCompile(
            byte[] pSrcData,
            IntPtr SrcDataSize,
            string pSourceName,
            IntPtr pDefines,
            IntPtr pInclude,
            string pEntrypoint,
            string pTarget,
            uint Flags1,
            uint Flags2,
            out IntPtr ppCode,
            out IntPtr ppErrorMsgs);

        [DllImport("dcomp.dll", CallingConvention = CallingConvention.Winapi, PreserveSig = true)]
        public static extern int DCompositionCreateDevice(
            IntPtr dxgiDevice,
            ref Guid iid,
            out IntPtr dcompositionDevice);

        // VTable delegates
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetBufferPointerFn(IntPtr pThis);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetBufferSizeFn(IntPtr pThis);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateBufferFn(IntPtr pThis, ref D3D11_BUFFER_DESC pDesc, IntPtr pInitialData, out IntPtr ppBuffer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateTexture2DFn(IntPtr pThis, ref D3D11_TEXTURE2D_DESC pDesc, IntPtr pInitialData, out IntPtr ppTexture2D);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateShaderResourceViewFn(IntPtr pThis, IntPtr pResource, IntPtr pDesc, out IntPtr ppSRView);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateRenderTargetViewFn(IntPtr pThis, IntPtr pResource, IntPtr pDesc, out IntPtr ppRTView);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateVertexShaderFn(IntPtr pThis, IntPtr pShaderBytecode, IntPtr BytecodeLength, IntPtr pClassLinkage, out IntPtr ppVertexShader);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreatePixelShaderFn(IntPtr pThis, IntPtr pShaderBytecode, IntPtr BytecodeLength, IntPtr pClassLinkage, out IntPtr ppPixelShader);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateRasterizerStateFn(IntPtr pThis, ref D3D11_RASTERIZER_DESC pDesc, out IntPtr ppRasterizerState);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateSamplerStateFn(IntPtr pThis, ref D3D11_SAMPLER_DESC pDesc, out IntPtr ppSamplerState);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void PSSetShaderResourcesFn(IntPtr pThis, uint StartSlot, uint NumViews, IntPtr[] ppShaderResourceViews);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void PSSetShaderFn(IntPtr pThis, IntPtr pPixelShader, IntPtr ppClassInstances, uint NumClassInstances);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void PSSetSamplersFn(IntPtr pThis, uint StartSlot, uint NumSamplers, IntPtr[] ppSamplers);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void VSSetShaderFn(IntPtr pThis, IntPtr pVertexShader, IntPtr ppClassInstances, uint NumClassInstances);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DrawFn(IntPtr pThis, uint VertexCount, uint StartVertexLocation);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int MapFn(IntPtr pThis, IntPtr pResource, uint Subresource, uint MapType, uint MapFlags, out D3D11_MAPPED_SUBRESOURCE pMappedResource);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void UnmapFn(IntPtr pThis, IntPtr pResource, uint Subresource);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void PSSetConstantBuffersFn(IntPtr pThis, uint StartSlot, uint NumBuffers, IntPtr[] ppConstantBuffers);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void IASetPrimitiveTopologyFn(IntPtr pThis, uint Topology);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void OMSetRenderTargetsFn(IntPtr pThis, uint NumViews, IntPtr[] ppRenderTargetViews, IntPtr pDepthStencilView);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void RSSetStateFn(IntPtr pThis, IntPtr pRasterizerState);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void RSSetViewportsFn(IntPtr pThis, uint NumViewports, ref D3D11_VIEWPORT pViewports);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CopyResourceFn(IntPtr pThis, IntPtr pDstResource, IntPtr pSrcResource);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void UpdateSubresourceFn(IntPtr pThis, IntPtr pDstResource, uint DstSubresource, IntPtr pDstBox, IntPtr pSrcData, uint SrcRowPitch, uint SrcDepthPitch);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ClearRenderTargetViewFn(IntPtr pThis, IntPtr pRenderTargetView, float[] ColorRGBA);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetAdapterFn(IntPtr pThis, out IntPtr pAdapter);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetParentFn(IntPtr pThis, ref Guid riid, out IntPtr ppParent);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int MakeWindowAssociationFn(IntPtr pThis, IntPtr WindowHandle, uint Flags);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateSwapChainFn(IntPtr pThis, IntPtr pDevice, ref DXGI_SWAP_CHAIN_DESC pDesc, out IntPtr ppSwapChain);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateSwapChainForHwndFn(IntPtr pThis, IntPtr pDevice, IntPtr hWnd, ref DXGI_SWAP_CHAIN_DESC1 pDesc, IntPtr pFullscreenDesc, IntPtr pRestrictToOutput, out IntPtr ppSwapChain);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateSwapChainForCompositionFn(IntPtr pThis, IntPtr pDevice, ref DXGI_SWAP_CHAIN_DESC1 pDesc, IntPtr pRestrictToOutput, out IntPtr ppSwapChain);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DCompCommitFn(IntPtr pThis);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DCompCreateTargetForHwndFn(IntPtr pThis, IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool topmost, out IntPtr ppTarget);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DCompCreateVisualFn(IntPtr pThis, out IntPtr ppVisual);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DCompTargetSetRootFn(IntPtr pThis, IntPtr visual);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DCompVisualSetContentFn(IntPtr pThis, IntPtr content);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PresentFn(IntPtr pThis, uint SyncInterval, uint Flags);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetBufferFn(IntPtr pThis, uint Buffer, ref Guid riid, out IntPtr ppSurface);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ResizeBuffersFn(IntPtr pThis, uint BufferCount, uint Width, uint Height, uint NewFormat, uint SwapChainFlags);

        private static T GetMethod<T>(IntPtr comObj, int slot) where T : class
        {
            IntPtr vtbl = Marshal.ReadIntPtr(comObj);
            IntPtr fnPtr = Marshal.ReadIntPtr(vtbl, slot * IntPtr.Size);
            return Marshal.GetDelegateForFunctionPointer(fnPtr, typeof(T)) as T;
        }

        public static void SafeRelease(ref IntPtr comPtr)
        {
            if (comPtr != IntPtr.Zero)
            {
                Marshal.Release(comPtr);
                comPtr = IntPtr.Zero;
            }
        }

        public static IntPtr BlobGetBufferPointer(IntPtr blob) { return GetMethod<GetBufferPointerFn>(blob, 3)(blob); }
        public static IntPtr BlobGetBufferSize(IntPtr blob) { return GetMethod<GetBufferSizeFn>(blob, 4)(blob); }

        public static int DeviceCreateBuffer(IntPtr dev, ref D3D11_BUFFER_DESC desc, out IntPtr buf)
        {
            return GetMethod<CreateBufferFn>(dev, 3)(dev, ref desc, IntPtr.Zero, out buf);
        }

        public static int DeviceCreateTexture2D(IntPtr dev, ref D3D11_TEXTURE2D_DESC desc, out IntPtr tex)
        {
            return GetMethod<CreateTexture2DFn>(dev, 5)(dev, ref desc, IntPtr.Zero, out tex);
        }

        public static int DeviceCreateShaderResourceView(IntPtr dev, IntPtr res, out IntPtr srv)
        {
            return GetMethod<CreateShaderResourceViewFn>(dev, 7)(dev, res, IntPtr.Zero, out srv);
        }

        public static int DeviceCreateRenderTargetView(IntPtr dev, IntPtr res, out IntPtr rtv)
        {
            return GetMethod<CreateRenderTargetViewFn>(dev, 9)(dev, res, IntPtr.Zero, out rtv);
        }

        public static int DeviceCreateVertexShader(IntPtr dev, IntPtr bytecode, IntPtr len, out IntPtr vs)
        {
            return GetMethod<CreateVertexShaderFn>(dev, 12)(dev, bytecode, len, IntPtr.Zero, out vs);
        }

        public static int DeviceCreatePixelShader(IntPtr dev, IntPtr bytecode, IntPtr len, out IntPtr ps)
        {
            return GetMethod<CreatePixelShaderFn>(dev, 15)(dev, bytecode, len, IntPtr.Zero, out ps);
        }

        public static int DeviceCreateRasterizerState(IntPtr dev, ref D3D11_RASTERIZER_DESC desc, out IntPtr rs)
        {
            return GetMethod<CreateRasterizerStateFn>(dev, 22)(dev, ref desc, out rs);
        }

        public static int DeviceCreateSamplerState(IntPtr dev, ref D3D11_SAMPLER_DESC desc, out IntPtr ss)
        {
            return GetMethod<CreateSamplerStateFn>(dev, 23)(dev, ref desc, out ss);
        }

        public static void ContextPSSetShaderResources(IntPtr ctx, uint slot, IntPtr[] srvs)
        {
            GetMethod<PSSetShaderResourcesFn>(ctx, 8)(ctx, slot, (uint)srvs.Length, srvs);
        }

        public static void ContextPSSetShader(IntPtr ctx, IntPtr ps)
        {
            GetMethod<PSSetShaderFn>(ctx, 9)(ctx, ps, IntPtr.Zero, 0);
        }

        public static void ContextPSSetSamplers(IntPtr ctx, uint slot, IntPtr[] samplers)
        {
            GetMethod<PSSetSamplersFn>(ctx, 10)(ctx, slot, (uint)samplers.Length, samplers);
        }

        public static void ContextVSSetShader(IntPtr ctx, IntPtr vs)
        {
            GetMethod<VSSetShaderFn>(ctx, 11)(ctx, vs, IntPtr.Zero, 0);
        }

        public static void ContextDraw(IntPtr ctx, uint vertexCount, uint startVertex)
        {
            GetMethod<DrawFn>(ctx, 13)(ctx, vertexCount, startVertex);
        }

        public static int ContextMap(IntPtr ctx, IntPtr res, uint subresource, uint mapType, out D3D11_MAPPED_SUBRESOURCE mapped)
        {
            return GetMethod<MapFn>(ctx, 14)(ctx, res, subresource, mapType, 0, out mapped);
        }

        public static void ContextUnmap(IntPtr ctx, IntPtr res, uint subresource)
        {
            GetMethod<UnmapFn>(ctx, 15)(ctx, res, subresource);
        }

        public static void ContextPSSetConstantBuffers(IntPtr ctx, uint slot, IntPtr[] cbs)
        {
            GetMethod<PSSetConstantBuffersFn>(ctx, 16)(ctx, slot, (uint)cbs.Length, cbs);
        }

        public static void ContextIASetPrimitiveTopology(IntPtr ctx, uint topology)
        {
            GetMethod<IASetPrimitiveTopologyFn>(ctx, 24)(ctx, topology);
        }

        public static void ContextOMSetRenderTargets(IntPtr ctx, IntPtr[] rtvs)
        {
            GetMethod<OMSetRenderTargetsFn>(ctx, 33)(ctx, (uint)rtvs.Length, rtvs, IntPtr.Zero);
        }

        public static void ContextRSSetState(IntPtr ctx, IntPtr rs)
        {
            GetMethod<RSSetStateFn>(ctx, 43)(ctx, rs);
        }

        public static void ContextRSSetViewports(IntPtr ctx, ref D3D11_VIEWPORT vp)
        {
            GetMethod<RSSetViewportsFn>(ctx, 44)(ctx, 1, ref vp);
        }

        public static void ContextCopyResource(IntPtr ctx, IntPtr dst, IntPtr src)
        {
            GetMethod<CopyResourceFn>(ctx, 47)(ctx, dst, src);
        }

        public static void ContextUpdateSubresource(IntPtr ctx, IntPtr dst, IntPtr srcData)
        {
            GetMethod<UpdateSubresourceFn>(ctx, 48)(ctx, dst, 0, IntPtr.Zero, srcData, 0, 0);
        }

        public static void ContextClearRenderTargetView(IntPtr ctx, IntPtr rtv, float[] rgba)
        {
            GetMethod<ClearRenderTargetViewFn>(ctx, 50)(ctx, rtv, rgba);
        }

        public static int DxgiDeviceGetAdapter(IntPtr dxgiDev, out IntPtr adapter)
        {
            return GetMethod<GetAdapterFn>(dxgiDev, 7)(dxgiDev, out adapter);
        }

        public static int DxgiObjectGetParent(IntPtr obj, ref Guid riid, out IntPtr parent)
        {
            return GetMethod<GetParentFn>(obj, 6)(obj, ref riid, out parent);
        }

        public static int DxgiFactoryMakeWindowAssociation(IntPtr factory, IntPtr hwnd, uint flags)
        {
            return GetMethod<MakeWindowAssociationFn>(factory, 8)(factory, hwnd, flags);
        }

        public static int DxgiFactoryCreateSwapChain(IntPtr factory, IntPtr dev, ref DXGI_SWAP_CHAIN_DESC desc, out IntPtr swapChain)
        {
            return GetMethod<CreateSwapChainFn>(factory, 10)(factory, dev, ref desc, out swapChain);
        }

        public static int DxgiFactory2CreateSwapChainForHwnd(IntPtr factory2, IntPtr dev, IntPtr hwnd, ref DXGI_SWAP_CHAIN_DESC1 desc, out IntPtr swapChain)
        {
            return GetMethod<CreateSwapChainForHwndFn>(factory2, 15)(factory2, dev, hwnd, ref desc, IntPtr.Zero, IntPtr.Zero, out swapChain);
        }

        public static int DxgiFactory2CreateSwapChainForComposition(IntPtr factory2, IntPtr dev, ref DXGI_SWAP_CHAIN_DESC1 desc, out IntPtr swapChain)
        {
            return GetMethod<CreateSwapChainForCompositionFn>(factory2, 24)(factory2, dev, ref desc, IntPtr.Zero, out swapChain);
        }

        public static int DCompCommit(IntPtr dcompDevice)
        {
            return GetMethod<DCompCommitFn>(dcompDevice, 3)(dcompDevice);
        }

        public static int DCompCreateTargetForHwnd(IntPtr dcompDevice, IntPtr hwnd, bool topmost, out IntPtr target)
        {
            return GetMethod<DCompCreateTargetForHwndFn>(dcompDevice, 6)(dcompDevice, hwnd, topmost, out target);
        }

        public static int DCompCreateVisual(IntPtr dcompDevice, out IntPtr visual)
        {
            return GetMethod<DCompCreateVisualFn>(dcompDevice, 7)(dcompDevice, out visual);
        }

        public static int DCompTargetSetRoot(IntPtr target, IntPtr visual)
        {
            return GetMethod<DCompTargetSetRootFn>(target, 3)(target, visual);
        }

        public static int DCompVisualSetContent(IntPtr visual, IntPtr content)
        {
            return GetMethod<DCompVisualSetContentFn>(visual, 15)(visual, content);
        }

        public static int SwapChainPresent(IntPtr swapChain, uint syncInterval, uint flags)
        {
            return GetMethod<PresentFn>(swapChain, 8)(swapChain, syncInterval, flags);
        }

        public static int SwapChainGetBuffer(IntPtr swapChain, uint buffer, ref Guid riid, out IntPtr surface)
        {
            return GetMethod<GetBufferFn>(swapChain, 9)(swapChain, buffer, ref riid, out surface);
        }

        public static int SwapChainResizeBuffers(IntPtr swapChain, uint bufferCount, uint width, uint height, uint format, uint flags)
        {
            return GetMethod<ResizeBuffersFn>(swapChain, 13)(swapChain, bufferCount, width, height, format, flags);
        }
    }
}
