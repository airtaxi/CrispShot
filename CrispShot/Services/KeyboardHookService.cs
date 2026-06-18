using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CrispShot.Services;

public sealed partial class KeyboardHookService : IDisposable
{
    private const uint VirtualKeySnapshot = 0x2C;
    private const uint LLKHF_ALTDOWN = 0x20;

    private readonly HOOKPROC _hookProcedure;
    private UnhookWindowsHookExSafeHandle? _hookHandle;
    private bool _disposed;

    public event EventHandler<nint>? AltPrintScreenPressed;

    public KeyboardHookService() => _hookProcedure = OnHook;

    public void Initialize()
    {
        if (_hookHandle is not null && !_hookHandle.IsInvalid) return;

        _hookHandle = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _hookProcedure, default, 0);
        if (_hookHandle.IsInvalid) _hookHandle = null;
    }

    private unsafe LRESULT OnHook(int code, WPARAM messageParameter, LPARAM hookParameter)
    {
        if (code >= 0)
        {
            var keyboardHook = (KBDLLHOOKSTRUCT*)hookParameter.Value;
            if (keyboardHook is not null && keyboardHook->vkCode == VirtualKeySnapshot && ((uint)keyboardHook->flags & LLKHF_ALTDOWN) != 0)
            {
                var windowMessage = (uint)messageParameter.Value;
                if (windowMessage is PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN or PInvoke.WM_KEYUP or PInvoke.WM_SYSKEYUP)
                {
                    var foregroundHandle = PInvoke.GetForegroundWindow();
                    if (!foregroundHandle.IsNull) AltPrintScreenPressed?.Invoke(this, (nint)foregroundHandle);
                }
            }
        }

        return PInvoke.CallNextHookEx(default, code, messageParameter, hookParameter);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _hookHandle?.Dispose();
        _hookHandle = null;
        _disposed = true;
    }
}
