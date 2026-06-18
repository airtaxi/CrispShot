using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Security.Cryptography;
using Windows.Win32;
using Windows.Win32.Foundation;
using D3D = Windows.Win32.Graphics.Direct3D11;
using D3DCommon = Windows.Win32.Graphics.Direct3D;
using WinRT;

namespace CrispShot.Services;

internal static partial class WgcCapture
{
    private static readonly Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid GraphicsCaptureItemInteropGuid = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly Guid DxgiDeviceGuid = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");

    private const uint D3D11SdkVersion = 7;

    public static bool IsSupported()
    {
        try { return GraphicsCaptureSession.IsSupported(); }
        catch { return false; }
    }

    public static async Task<(byte[] Pixels, int Width, int Height)> CaptureAsync(HWND hwnd, CancellationToken ct)
    {
        if (!GraphicsCaptureSession.IsSupported()) throw new PlatformNotSupportedException("Windows.Graphics.Capture is not supported on this system.");

        PInvoke.D3D11CreateDevice(pAdapter: null, D3DCommon.D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE, Software: default, D3D.D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT, pFeatureLevels: default, SDKVersion: D3D11SdkVersion, out var device, out _, out var context).ThrowOnFailure();

        try
        {
            var winrtDevice = CreateDirect3DDevice(device);
            var item = CreateItemForWindow(hwnd);
            if (item.Size.Width <= 0 || item.Size.Height <= 0)
            {
                Debug.WriteLine($"CrispShot WGC capture skipped: GraphicsCaptureItem.Size is {item.Size.Width}x{item.Size.Height} — keeping OS clipboard.");
                throw new InvalidOperationException("The target window does not support WGC capture.");
            }

            using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, numberOfBuffers: 2, item.Size);
            using var session = pool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled = false;
            session.IsBorderRequired = false;

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var framesSeen = 0;
            var tcs = new TaskCompletionSource<Direct3D11CaptureFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.FrameArrived += (sender, _) =>
            {
                Direct3D11CaptureFrame? frame = null;
                try
                {
                    frame = sender.TryGetNextFrame();
                    if (frame is null) return;
                    if (!tcs.TrySetResult(frame)) frame.Dispose();
                }
                catch (Exception ex)
                {
                    frame?.Dispose();
                    tcs.TrySetException(ex);
                }
            };

            session.StartCapture();

            while (true)
            {
                linkedCts.Token.ThrowIfCancellationRequested();
                using var frame = await tcs.Task.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                var result = await CopyFrameAsync(frame).ConfigureAwait(false);
                framesSeen++;
                if (!IsBlankCapture(result.Pixels) || framesSeen >= 5) return result;

                await Task.Delay(50, linkedCts.Token).ConfigureAwait(false);
                tcs = new TaskCompletionSource<Direct3D11CaptureFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        finally
        {
            (context as IDisposable)?.Dispose();
            (device as IDisposable)?.Dispose();
        }
    }

    private static unsafe IDirect3DDevice CreateDirect3DDevice(D3D.ID3D11Device device)
    {
        var d3dDevicePtr = Marshal.GetIUnknownForObject(device);
        IntPtr dxgiDevicePtr = IntPtr.Zero;
        IntPtr graphicsDevicePtr = IntPtr.Zero;
        try
        {
            Marshal.QueryInterface(d3dDevicePtr, in DxgiDeviceGuid, out dxgiDevicePtr).ThrowIfFailed("ID3D11Device.QueryInterface(IDXGIDevice)");
            CreateDirect3D11DeviceFromDXGIDevice(dxgiDevicePtr, out graphicsDevicePtr).ThrowIfFailed("CreateDirect3D11DeviceFromDXGIDevice");

            var managed = MarshalInspectable<IDirect3DDevice>.FromAbi(graphicsDevicePtr);
            graphicsDevicePtr = IntPtr.Zero;
            return managed;
        }
        finally
        {
            if (graphicsDevicePtr != IntPtr.Zero) Marshal.Release(graphicsDevicePtr);
            if (dxgiDevicePtr != IntPtr.Zero) Marshal.Release(dxgiDevicePtr);
            Marshal.Release(d3dDevicePtr);
        }
    }

    private static unsafe GraphicsCaptureItem CreateItemForWindow(HWND hwnd)
    {
        using var factory = ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        IntPtr interopPtr = IntPtr.Zero;
        IntPtr itemPtr = IntPtr.Zero;
        try
        {
            Marshal.QueryInterface(factory.ThisPtr, in GraphicsCaptureItemInteropGuid, out interopPtr).ThrowIfFailed("GraphicsCaptureItem.QueryInterface(IGraphicsCaptureItemInterop)");

            var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(interopPtr);

            interop.CreateForWindow((IntPtr)hwnd.Value, in GraphicsCaptureItemGuid, out itemPtr).ThrowIfFailed("GraphicsCaptureItem.CreateForWindow");

            var item = MarshalInspectable<GraphicsCaptureItem>.FromAbi(itemPtr);
            itemPtr = IntPtr.Zero;
            return item;
        }
        finally
        {
            if (itemPtr != IntPtr.Zero) Marshal.Release(itemPtr);
            if (interopPtr != IntPtr.Zero) Marshal.Release(interopPtr);
        }
    }

    private static async Task<(byte[] Pixels, int Width, int Height)> CopyFrameAsync(Direct3D11CaptureFrame frame)
    {
        using var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface).AsTask().ConfigureAwait(false);
        using var convertedBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var width = convertedBitmap.PixelWidth;
        var height = convertedBitmap.PixelHeight;
        if (width <= 0 || height <= 0) throw new InvalidOperationException("WGC returned an empty frame.");

        var buffer = new Windows.Storage.Streams.Buffer((uint)checked(width * height * 4));
        convertedBitmap.CopyToBuffer(buffer);
        CryptographicBuffer.CopyToByteArray(buffer, out var pixels);
        return (pixels, width, height);
    }

    private static bool IsBlankCapture(byte[] pixels)
    {
        var span = MemoryMarshal.Cast<byte, long>(pixels.AsSpan());
        foreach (var chunk in span)
        {
            if (chunk != 0)
            {
                return false;
            }
        }
        for (var i = span.Length * sizeof(long); i < pixels.Length; i++)
        {
            if (pixels[i] != 0)
            {
                return false;
            }
        }
        return true;
    }

    [LibraryImport("d3d11.dll")]
    private static partial int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    internal interface IGraphicsCaptureItemInterop
    {
        [PreserveSig]
        int CreateForWindow(IntPtr window, in Guid iid, out IntPtr result);

        [PreserveSig]
        int CreateForMonitor(IntPtr monitor, in Guid iid, out IntPtr result);
    }

    private static void ThrowIfFailed(this int hr, string operation)
    {
        if (hr < 0) throw new COMException($"{operation} failed with HRESULT 0x{hr:X8}.", hr);
    }
}
