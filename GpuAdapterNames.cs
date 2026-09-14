using System.Runtime.InteropServices;

namespace TrayMeters;

/// <summary>
/// Minimal DXGI adapter enumeration via raw COM interop — used only to map the LUID reported by
/// the "GPU Engine" performance counter category to a human-readable adapter name for the GPU
/// selection menu. No SwapChain/Output/rendering members are implemented; their vtable slots are
/// declared with placeholder signatures purely to keep interface method ordering correct, since a
/// hand-written COM interface must declare every base-interface method in exact vtable order.
/// </summary>
internal static class GpuAdapterNames
{
    private const uint DXGI_ADAPTER_FLAG_SOFTWARE = 0x2;

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public LUID AdapterLuid;
        public uint Flags;
    }

    [ComImport, Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter
    {
        void SetPrivateData(ref Guid name, uint dataSize, IntPtr data);
        void SetPrivateDataInterface(ref Guid name, [MarshalAs(UnmanagedType.IUnknown)] object unknown);
        void GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);
        void GetParent(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object parent);
        void EnumOutputs(uint index, out IntPtr output);
        void GetDesc(out DXGI_ADAPTER_DESC1 desc);
        void CheckInterfaceSupport(ref Guid interfaceName, out long umdVersion);
    }

    [ComImport, Guid("29038f61-3839-4626-91fd-086879011a05"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter1
    {
        void SetPrivateData(ref Guid name, uint dataSize, IntPtr data);
        void SetPrivateDataInterface(ref Guid name, [MarshalAs(UnmanagedType.IUnknown)] object unknown);
        void GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);
        void GetParent(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object parent);
        void EnumOutputs(uint index, out IntPtr output);
        void GetDesc(out DXGI_ADAPTER_DESC1 desc);
        void CheckInterfaceSupport(ref Guid interfaceName, out long umdVersion);
        void GetDesc1(out DXGI_ADAPTER_DESC1 desc);
    }

    [ComImport, Guid("770aae78-f26f-4dba-a829-253c83d1b387"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIFactory1
    {
        void SetPrivateData(ref Guid name, uint dataSize, IntPtr data);
        void SetPrivateDataInterface(ref Guid name, [MarshalAs(UnmanagedType.IUnknown)] object unknown);
        void GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);
        void GetParent(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object parent);
        void EnumAdapters(uint index, out IDXGIAdapter adapter);
        void MakeWindowAssociation(IntPtr windowHandle, uint flags);
        void GetWindowAssociation(out IntPtr windowHandle);
        void CreateSwapChain([MarshalAs(UnmanagedType.IUnknown)] object device, IntPtr desc, out IntPtr swapChain);
        void CreateSoftwareAdapter(IntPtr module, out IDXGIAdapter adapter);
        void EnumAdapters1(uint index, out IDXGIAdapter1 adapter);
        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsCurrent();
    }

    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1(ref Guid riid, out IDXGIFactory1 factory);

    /// <summary>
    /// Maps each real (non-software) display adapter's LUID — formatted to match the
    /// "0xHIGH_0xLOW" segment inside GPU Engine counter instance names — to its friendly
    /// description, e.g. "NVIDIA GeForce RTX 4070". Enumeration order matches DXGI's adapter
    /// order (primary/highest-performance adapter first).
    /// </summary>
    public static List<(string Luid, string Name)> GetAdapters()
    {
        var result = new List<(string, string)>();

        Guid iid = typeof(IDXGIFactory1).GUID;
        int hr = CreateDXGIFactory1(ref iid, out IDXGIFactory1 factory);
        if (hr != 0 || factory is null)
        {
            return result;
        }

        try
        {
            for (uint i = 0; ; i++)
            {
                IDXGIAdapter1 adapter;
                try
                {
                    factory.EnumAdapters1(i, out adapter);
                }
                catch (COMException)
                {
                    break; // DXGI_ERROR_NOT_FOUND: no more adapters.
                }

                try
                {
                    adapter.GetDesc1(out DXGI_ADAPTER_DESC1 desc);
                    if ((desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) != 0)
                    {
                        continue; // Skip the "Microsoft Basic Render Driver" software rasterizer.
                    }

                    uint high = unchecked((uint)desc.AdapterLuid.HighPart);
                    uint low = desc.AdapterLuid.LowPart;
                    string luidKey = $"0x{high:X8}_0x{low:X8}";
                    result.Add((luidKey, desc.Description));
                }
                finally
                {
                    Marshal.ReleaseComObject(adapter);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }

        return result;
    }
}
