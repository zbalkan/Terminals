# Citrix Workspace integration

Terminals launches published Citrix applications and desktops through the locally installed **Citrix Workspace app for Windows**. It does not implement ICA/HDX, manage authentication, or host the resulting session window. The new plugin is deployed as `Terminals.Plugins.Ica.dll`; the active favorite protocol identifier is `CTXWS` (the SQL `Protocol` column is limited to five characters for stored procedures). The favorite editor displays **Citrix Workspace**.

## Configure the executable

In **Tools > Options > Plugins**, enable **Citrix Workspace** and configure either or both supported client executables:

- **SelfService.exe** for Workspace/Cloud with SSO. The launcher uses `SelfService.exe storebrowse -q "Published resource" "https://store.example/Citrix/Store/discovery"`.
- **storebrowse.exe** for on-premises StoreFront. The launcher uses `storebrowse.exe -S "Published resource" "https://store.example/Citrix/Store/discovery"` without `-U`, `-P`, or `-D` arguments, allowing Citrix's `AuthManager` to handle the prompt.

When no path is configured, the resolver checks the user and system `PATH`. Select **Browse** if Citrix's installation directory is not on `PATH`; an explicit, missing path is an error, not a silent fallback. Disabling the plugin does not erase saved executable paths.

## Create a favorite

Choose **Citrix Workspace** as the protocol. Enter the HTTPS StoreFront or Workspace discovery URL in **Store URL** on the General tab. In **Citrix Workspace Options**, choose **Workspace / Cloud (SSO)** or **StoreFront / on-premises**, and enter the exact published-resource name as known to Citrix. Terminals does not discover or register stores or infer the resource name from the favorite name.

The hostname, port and relative URL are persisted using the existing favorite structure; nondefault HTTPS ports and case-sensitive paths are preserved. The launch plugin does not read Terminals credentials and does not pass passwords on the command line. It validates the HTTPS URL and resource before invoking the Citrix executable.

A launch opens **no Terminals tab**. After configured pre-connection commands, Terminals starts the external process without blocking the WinForms thread. The connection history records successful process creation, **not** a verified Citrix session or authentication result. Citrix Workspace owns its windows, authentication, brokering, and process lifetime. Terminals must not terminate it during shutdown.

## Historical ICA Citrix favorites

The original `ICA Citrix` file protocol, the legacy SQL-truncated `ICA C` protocol, and the `ICAOptions` XML schema are retained as a compatibility-only plugin. Existing file-backed and SQL favorites can be deserialized and inspected without relying on `AxICAClient`. These favorites cannot be launched and the retired protocol does not appear when creating new favorites. Existing legacy options are shown read-only. Users must create a distinct modern Citrix Workspace favorite; Terminals does not assume that a direct ICA host equals a brokered published resource.

The plugin build and installer no longer require or package `AxWFICALib.dll`, `WFICALib.dll`, `Wfica.ocx`, or `ica32pkg.msi`.

## Validation and restrictions

Automated tests cover launch-command selection and quoting, HTTPS URL validation, executable discovery, protocol registration, legacy ICA isolation and serialization. Both file and SQL persistence test suites must pass.

**Manual acceptance remains necessary** on supported Citrix Workspace current releases and LTSR: confirm Cloud SSO and StoreFront prompt behavior, valid/invalid resource names, MFA handling, session opening, immediate process exit behavior, and fresh/upgrade installer cleanup. No inference should be made about remote session success from `Process.Start` alone.

Citrix documentation:
- [Storebrowse for Workspace (Cloud)](https://docs.citrix.com/en-us/citrix-workspace-app-for-windows/storebrowse-for-workspace.html).
- [Storebrowse for Workspace app 2507.1 LTSR (StoreFront)](https://docs.citrix.com/us/en/citrix-workspace-app-for-windows/2507-1-ltsr/sdk-and-api/store-browse.html).

**Out of scope:** Unified Workspace API, OAuth, resource enumeration, dynamic ICA launch-file management, store registration/deletion and ActiveX embedding.
