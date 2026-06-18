using SkiaSharp;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace CrispShot.Services;

public sealed partial class ClipboardService
{
    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint CF_DIB = 8;
    private const uint CF_DIBV5 = 17;
    private const uint BI_BITFIELDS = 3;
    private const uint LCS_sRGB = 0x73524742;

    public Task SetPngAsync(byte[] pngBytes)
    {
        if (!OpenClipboard(IntPtr.Zero)) throw new InvalidOperationException("Clipboard could not be opened.");

        try
        {
            if (!EmptyClipboard()) throw new InvalidOperationException("Clipboard could not be emptied.");

            var pngFormat = RegisterClipboardFormat("PNG");
            if (pngFormat == 0) throw new InvalidOperationException("PNG clipboard format could not be registered.");

            SetClipboardBytes(pngFormat, pngBytes);

            var kakaoTalkImageFormat = RegisterClipboardFormat("KAKAOTALK_CLIPBOARD_FORMAT_IMAGE");
            if (kakaoTalkImageFormat != 0) SetClipboardBytes(kakaoTalkImageFormat, pngBytes);

            var imagePixels = DecodePngPixels(pngBytes);
            SetClipboardBytes(CF_DIB, CreateDib(imagePixels));
            SetClipboardBytes(CF_DIBV5, CreateDibV5(imagePixels));

            return Task.CompletedTask;
        }
        finally { CloseClipboard(); }
    }

    private static void SetClipboardBytes(uint format, byte[] bytes)
    {
        var memoryHandle = CopyBytesToGlobalMemory(bytes);
        if (SetClipboardData(format, memoryHandle) != IntPtr.Zero) return;

        GlobalFree(memoryHandle);
        throw new InvalidOperationException($"Clipboard format {format} could not be set.");
    }

    private static ClipboardImagePixels DecodePngPixels(byte[] pngBytes)
    {
        using var sourceBitmap = SKBitmap.Decode(pngBytes) ?? throw new InvalidOperationException("PNG clipboard image could not be decoded.");
        using var convertedBitmap = new SKBitmap(sourceBitmap.Width, sourceBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(convertedBitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(sourceBitmap, 0, 0);

        var pixels = new byte[convertedBitmap.Width * convertedBitmap.Height * 4];
        unsafe
        {
            var sourcePointer = (byte*)convertedBitmap.GetPixels().ToPointer();
            var destinationIndex = 0;
            var rowBytes = convertedBitmap.Width * 4;
            for (var row = 0; row < convertedBitmap.Height; row++)
            {
                Marshal.Copy((nint)(sourcePointer + row * convertedBitmap.RowBytes), pixels, destinationIndex, rowBytes);
                destinationIndex += rowBytes;
            }
        }

        return new ClipboardImagePixels(pixels, convertedBitmap.Width, convertedBitmap.Height);
    }

    private static byte[] CreateDib(ClipboardImagePixels imagePixels)
    {
        var imageSize = imagePixels.Pixels.Length;
        var dibBytes = new byte[52 + imageSize];
        WriteBitmapInfoHeader(dibBytes, headerSize: 40, imagePixels.Width, imagePixels.Height, BI_BITFIELDS, imageSize);
        WriteBgraMasks(dibBytes.AsSpan(40, 12));
        CopyBottomUpPixels(imagePixels, dibBytes.AsSpan(52));
        return dibBytes;
    }

    private static byte[] CreateDibV5(ClipboardImagePixels imagePixels)
    {
        var imageSize = imagePixels.Pixels.Length;
        var dibBytes = new byte[124 + imageSize];
        WriteBitmapInfoHeader(dibBytes, headerSize: 124, imagePixels.Width, imagePixels.Height, BI_BITFIELDS, imageSize);
        BinaryPrimitives.WriteUInt32LittleEndian(dibBytes.AsSpan(40, 4), 0x00FF0000);
        BinaryPrimitives.WriteUInt32LittleEndian(dibBytes.AsSpan(44, 4), 0x0000FF00);
        BinaryPrimitives.WriteUInt32LittleEndian(dibBytes.AsSpan(48, 4), 0x000000FF);
        BinaryPrimitives.WriteUInt32LittleEndian(dibBytes.AsSpan(52, 4), 0xFF000000);
        BinaryPrimitives.WriteUInt32LittleEndian(dibBytes.AsSpan(56, 4), LCS_sRGB);
        CopyBottomUpPixels(imagePixels, dibBytes.AsSpan(124));
        return dibBytes;
    }

    private static void WriteBitmapInfoHeader(byte[] bytes, int headerSize, int width, int height, uint compression, int imageSize)
    {
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), headerSize);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), height);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14, 2), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), compression);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20, 4), imageSize);
    }

    private static void WriteBgraMasks(Span<byte> bytes)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[..4], 0x00FF0000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(4, 4), 0x0000FF00);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(8, 4), 0x000000FF);
    }

    private static void CopyBottomUpPixels(ClipboardImagePixels imagePixels, Span<byte> destination)
    {
        var rowBytes = imagePixels.Width * 4;
        for (var row = 0; row < imagePixels.Height; row++)
        {
            var sourceOffset = (imagePixels.Height - 1 - row) * rowBytes;
            imagePixels.Pixels.AsSpan(sourceOffset, rowBytes).CopyTo(destination.Slice(row * rowBytes, rowBytes));
        }
    }

    private static IntPtr CopyBytesToGlobalMemory(byte[] bytes)
    {
        var memoryHandle = GlobalAlloc(GMEM_MOVEABLE, (nuint)bytes.Length);
        if (memoryHandle == IntPtr.Zero) throw new OutOfMemoryException("Clipboard memory allocation failed.");

        var memoryPointer = GlobalLock(memoryHandle);
        if (memoryPointer == IntPtr.Zero)
        {
            GlobalFree(memoryHandle);
            throw new InvalidOperationException("Clipboard memory could not be locked.");
        }

        try { Marshal.Copy(bytes, 0, memoryPointer, bytes.Length); }
        finally { GlobalUnlock(memoryHandle); }

        return memoryHandle;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(IntPtr windowHandle);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial uint RegisterClipboardFormat(string format);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr SetClipboardData(uint format, IntPtr memoryHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalLock(IntPtr memoryHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(IntPtr memoryHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalFree(IntPtr memoryHandle);

    private sealed record ClipboardImagePixels(byte[] Pixels, int Width, int Height);
}
