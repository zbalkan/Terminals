# External VNC viewers (DEP-06)

Terminals launches an independently installed VNC viewer instead of embedding a VNC decoder. Neither Inno Setup nor the portable ZIP bundles any VNC viewer.

## Configuration

Open **Tools > Options > Plugins**, enable VNC, choose one of **TightVNC**, **UltraVNC**, **TigerVNC**, or **RealVNC**, and use the single **Path** field and **Browse** button to configure its executable when discovery does not find it. The field changes with the selected viewer; paths are stored independently, so switching providers does not overwrite the other configurations. The default for upgraded installations is TightVNC.

Discovery checks known vendor-specific Program Files locations first. For user/machine PATH candidates, a matching filename alone is insufficient: vendor-identifying version metadata is required because multiple clients ship `vncviewer.exe`. Portable clients without identifying metadata can be selected by supplying their full path. An invalid explicit path never falls back to another vendor.

## CLI profiles

| Viewer | Executable names | View only | Automatic scaling |
|---|---|---|---|
| TightVNC | `tvnviewer.exe` | `-viewonly=yes` | `-scale=auto` |
| UltraVNC | `vncviewer.exe` | `-viewonly` | `-autoscaling` |
| TigerVNC | `vncviewer.exe`, `vncviewer64.exe` | `-ViewOnly` | Not forwarded; no equivalent verified |
| RealVNC | `vncviewer.exe` | `-SendKeyEvents=FALSE -SendPointerEvents=FALSE` | `-Scaling=FitAutoAspect` |

All profiles target the favorite's **explicit port** in `host::port` format. Terminals neither passes stored VNC passwords to the executable nor populates credential-bearing environment variables. The selected viewer performs authentication. Existing VNC favorites remain readable and preserve their historical display-number setting, which is not used to derive the launched port.

The viewer owns its session, window, connection errors, and process lifetime. A successful Terminals launch confirms only that the process started; it does not confirm successful VNC authentication.

## Acceptance

Windows interactive tests remain necessary with each installed viewer. Verify path discovery and explicit overrides, non-standard ports, viewer authentication, view-only enforcement, expected scaling, failure diagnostics, IPv4/IPv6, separate window lifecycle and multiple simultaneous sessions. Ensure the package has no VncSharpCore DLL and no viewer EXE. Also check that legacy VNC favorites retain their stored settings under both file and SQL persistence.

CLI references: [TightVNC source](https://github.com/sim0629/tightvnc/blob/master/tvnviewer/ViewerCmdLine.cpp), [UltraVNC documentation](https://uvnc.com/docs/ultravnc-viewer/52-ultravnc-viewer-commandline-parameters.html), [TigerVNC manual](https://tigervnc.org/doc/vncviewer.html), [RealVNC parameter reference](https://help.realvnc.com/hc/en-us/articles/360002254618-RealVNC-Viewer-Parameter-Reference).
