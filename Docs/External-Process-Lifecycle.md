# External process lifecycle

This describes the implemented process-launch contract (merged as `b178a222b`). Current implementation and acceptance status belong in the [single modernization matrix](Dependency-modernization-roadmap.md).

## Contract

`Terminals.Common.Forms.ExternalProcessLauncher` centralizes slow process work: `RunAsync` for worker tasks, `StartDetached` for independent programs, `ExecuteAsync` for optional before-connect waiting, `StopDetached` for owned-process termination, and `Post` for asynchronous UI notifications.

| Consumer | Ownership / behavior |
|---|---|
| PuTTY SSH/Telnet | Worker starts process, bounded window readiness; UI reparents its window; tab owns and stops child |
| Windows OpenSSH | Separate console, session-owned process; exit is dispatched without blocking the UI |
| Pageant/PuTTY tools | Worker discovery/launch and bounded Pageant polling; UI presents window or error |
| Browser, shortcuts, external paths, MMC/MSInfo32/Notepad | Detached launches; no connection ownership |
| PsExec | Remote session discovery and launch off the UI thread |
| Global and per-favorite before-connect commands | Execute in order; `Wait for exit` delays the connection, **not** the WinForms event loop |

## Rules and acceptance

- Do not block the UI on `Process.Start`, `WaitForInputIdle`, `WaitForExit` or slow discovery. Post completion through a captured synchronization context or `BeginInvoke`, **never synchronous `Invoke`**.
- Session-owned children must terminate when their tabs close; detached tools continue after wrapper disposal. Ignore late callbacks after owner disposal. Preserve favorite `NewWindow` and pending-session behavior.
- Test on Windows: missing/denied client, hung startup, early exit, tab-close during startup, rapid reconnect, concurrent sessions, detached windows, already-running Pageant, before-connect ordering/waiting, PsExec timeouts and normal shutdown.
- **Credential risk remains separate:** Windows OpenSSH avoids password command-line arguments, but existing PuTTY `-pw` handling has not been remediated. Never introduce new argument-based secrets.

Future external VMware VMRC and Hyper-V VMConnect plugins should follow this contract. ConPTY/embedded OpenSSH is a separate proposal. See the [modernization roadmap](Dependency-modernization-roadmap.md).
