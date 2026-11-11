# Long-term roadmap

The [modernization work matrix](Dependency-modernization-roadmap.md) is the **only status and priority tracker** for Terminals 5.0.0. **PR #6 is permanent documentation-only planning work and must never be merged.**

The active program is to finish the `Terminals.External` consumer audit, modernize WMI and other network tools without blocking the WinForms UI, complete package provenance and external PuTTY utility discovery, then address residual modernization defects. External VNC runtime functionality has been user-confirmed; release acceptance is narrower than a blanket certification of all viewers.

After modernization, the deferred 5.0.0 gates cover password-safe PuTTY authentication, credential storage/KDF, historical file/SQL and configuration migrations, required CI checks, signing, deployment and end-to-end testing. Potential VMware/Hyper-V consoles and a TabControl replacement remain optional, not committed release blockers. All statuses and detailed exit criteria reside in the work matrix, not here.
