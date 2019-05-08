# Android Remote Desktop App for PC novel games
GOAL: Android remote desktop app for PC novel games which consume low network  traffic. (So this app can be used on mobile network!))

## Target Platform
- Server: 
  - tested Windows 10 64bit environment only
  - .Net Framework (>= v4.6.1)
  - in some cases, additional VC Runtime may be required
- Client (Android App)
  - Android 5.0 (API level 21 - Lolipop) and later
  - Smartphone and Tablet (ARM based processor)

## License
- GPL-3.0
- but codes and libralies of listed below define sevelal licenses. please check linked pages.

### Server
- Remote Desktop (Zero Configuration / P2P) <- this is base code of server 
  - https://github.com/reignstudios/RemoteDesktop

- inputsimulator (code use)
  - https://archive.codeplex.com/?p=inputsimulator

- Naudio (library use)
  - https://github.com/naudio/NAudio

- OpenH264Lib.NET (code use)
  - https://github.com/secile/OpenH264Lib.NET

- Opus.NET (code use)
  - https://github.com/DevJohnC/Opus.NET


### Client
- Xamarin.Forms
- Xamarin.Android

- Concentus (library use)
  - https://www.nuget.org/packages/Concentus/

- SkiaSharp.Views.Forms (library use)
  - https://www.nuget.org/packages/SkiaSharp.Views.Forms/

- ScnViewGestures.Forms (library use)
  - https://www.nuget.org/packages/ScnViewGestures.Forms/
