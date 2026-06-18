# CrispShot

🌐 [한국어](README.ko.md)

CrispShot is a lightweight Windows tray utility that fixes the small background border that Windows' built-in `Alt` + `Print Screen` leaves around the active window.
It captures only the active window cleanly, and optionally applies a soft drop shadow so your window screenshots look polished and ready to share.

[![Download from Microsoft Store](https://get.microsoft.com/images/en-US%20dark.svg)](https://apps.microsoft.com/detail/9P718PTBDJ0C)

## Features

- Capture the active window instantly with `Alt` + `Print Screen`
- Add a soft drop shadow with adjustable intensity (Off, Low, Medium, High)
- Dedicated KakaoTalk clipboard format support so transparency is preserved when pasting
- Preserve the OS clipboard when a window cannot be captured by WGC
- Run at system startup
- Optionally run as administrator so windows from elevated programs can be captured
- Tray-only design with no visible window
- Multilingual UI (English, Korean, Japanese, Chinese Simplified, Chinese Traditional)

## Libraries Used

- [Microsoft.WindowsAppSDK](https://github.com/microsoft/WindowsAppSDK) - WinUI 3 app platform and Windows integration
- [SkiaSharp](https://github.com/mono/SkiaSharp) - alpha-aware image rendering and shadow composition
- [DevWinUI.Controls](https://github.com/ghost1372/DevWinUI) - tray icon integration
- [TaskScheduler](https://github.com/dahall/TaskScheduler) - scheduled task management for administrator mode
- [Microsoft.Windows.CsWin32](https://github.com/microsoft/CsWin32) - source-generated Win32 P/Invoke bindings

## License

This project is distributed under the [MIT License](LICENSE.txt).

## Acknowledgement

Windows Graphics Capture implementation was built with reference to [robmikh/Win32CaptureSample](https://github.com/robmikh/Win32CaptureSample).

## Author

**Howon Lee** ([airtaxi](https://github.com/airtaxi))
