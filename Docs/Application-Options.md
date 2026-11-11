# Application options

Open **Tools → Options** (`Ctrl+Shift+P`). This describes the 5.0.0 development baseline, not a completed release. For work status and outstanding acceptance, use the [single modernization matrix](Dependency-modernization-roadmap.md).

## Startup and interface

| Setting | Behavior |
|---|---|
| Single instance / updates | Reuse an existing application instance for incoming arguments; optionally check for updates |
| Close confirmation / save connections | Confirm closing Terminals and optionally restore previously open connections |
| Tab title and tooltips | Include user name, endpoint/protocol and, with full details enabled, groups and notes |
| System tray and main-window restore | Minimize to tray and optionally restore the window after the last connection closes |
| Favorites/Groups | Toggle Favorites panel and Groups menu; configure auto-expand and sorting |
| Theme | Select toolbar and menu appearance |

## Security and connections

| Setting | Behavior |
|---|---|
| Master password | Protect stored credentials; changes require restart. **Vault/KDF modernization is deferred**, so do not claim that the present format meets current password-hardening expectations |
| Default credentials | Used when a connection has no favorite-specific credentials |
| Server-name validation | Check endpoints when saving favorites; current Save & Close validation correction merged and CI is green, but manual file/SQL testing remains open |
| Close/reconnect prompts | Warn on disconnection; RDP can offer reconnection after reboot/shutdown |
| Default Desktop Share | See [connection sharing](Configure-connection-share.md) |
| Port scanner timeout | Maximum TCP connection attempt time (default 5 seconds) |
| Execute before connect | Run configured global and favorite commands in order; `Wait for exit` delays connection without blocking the UI |
| Proxy | Applies to update/download operations, not HTTPS favorites |

Legacy **Amazon S3 backup** and **Flickr upload** settings are removed; their old configuration attributes require compatibility testing before 5.0.0 release.

## Screen capture and data store

Screen capture supports **clipboard** and **folder** destinations, an optional automatic switch to Capture Manager, and a configurable root folder (default under Pictures). Legacy Flickr synchronization is unavailable.

The data store is **local-profile files** by default or **Microsoft SQL Server**; changing the storage mode requires restart. See [file backup](Backup-and-restore-settings-files.md) and [SQL storage](Store-data-in-SQL-database.md).

## Protocol clients and plugins

- **Citrix Workspace:** independently installed; SelfService/Storebrowse paths and HTTPS store URL are configured separately. Sessions launch outside Terminals; retired ICA favorites remain readable but non-launchable. See [Citrix guide](Citrix-Workspace.md).
- **SSH client:** global `preferredSshClient` selects **PuTTY** (existing default) or available **Windows OpenSSH**. PuTTY remains tab-embedded; OpenSSH opens a separate console, uses its own authentication and host-key prompts, and is located under Windows `System32\OpenSSH` (or `Sysnative`). No ConPTY embedding or per-favorite SSH client override. Telnet requires PuTTY.
- **PuTTY/Pageant:** independently installed. Plugins page exposes enable state, executable path textboxes and Browse buttons. Explicit configured paths take precedence; automatic discovery checks **user PATH then machine PATH**, not arbitrary Program Files directories. Disabling the plugin retains paths but disables entry. Other protocol toggles are independent.
- **Known gap (BIN-05B):** main-menu **Open SSH Agent** and **Open SSH Keygen** still refer to obsolete bundled locations rather than external discovery; their behavior is not guaranteed.
- **VNC:** launches independently installed TightVNC, UltraVNC, TigerVNC or RealVNC, configured in Plugins. The user confirmed VNC works at runtime on November 11, 2026. Individual viewers, favorite migration and Inno/ZIP non-bundling remain release checks. See [External VNC viewers](External-VNC-Viewers.md).
- **Retired protocols:** legacy HTTP, Microsoft Virtual Server VMRC and ICA ActiveX are not offered as new connection types. VMware VMRC and Hyper-V VMConnect are future, **unimplemented** proposals.

External processes use the [shared nonblocking lifecycle](External-Process-Lifecycle.md). Connection owners manage their child processes; independent tools remain detached. The existing PuTTY `-pw` secret-in-arguments behavior is a **deferred pre-publication security gate**, not fixed by the asynchronous launcher.

## New Favorite save behavior

**Save & Close** commits validated favorites before optional MRU updates. **Save & Connect** stays open if saving fails; empty server names are rejected. CI passed [run 43044264860](https://github.com/zbalkan/Terminals/actions/runs/43044264860). Interactive save, edit and persistence tests for file/SQL stores remain pending.
