# Long-term roadmap

The [Terminals 5.0.0 modernization work matrix](Dependency-modernization-roadmap.md) is the **only status and priority tracker**. It records completed changes, pending work and interactive/release acceptance criteria. This file is intentionally a short entry point, not a second tracker.

**Current implementation priority:** Packet Capture and Port Scanner hardening. Recent Network Interfaces, Local Connections and Wake-on-LAN responsiveness changes are merged on `master` (`7e8a771b`) after successful [feature CI #44423405339](https://github.com/zbalkan/Terminals/actions/runs/44423405339). Interactive Windows acceptance remains open, as detailed in the matrix.

**PR #6 remains the permanent, documentation-only roadmap and must never be merged.** RDP PR #5 remains deferred. The 5.0.0 release still requires the applicable security, compatibility, packaging and migration gates listed in the matrix.
