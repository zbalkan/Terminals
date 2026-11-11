# Dependency inventory

Historical evidence and audit scope for the [single modernization work matrix](Dependency-modernization-roadmap.md). This document is **not** a separate status tracker, SBOM, or redistribution approval. Original blob-level inventory: [pinned Git snapshot `9c5adda047263f87c13507740eeb26a26f331d0a`](https://github.com/zbalkan/Terminals/tree/9c5adda047263f87c13507740eeb26a26f331d0a) (November 9, 2026). Those Git object IDs are SHA-1 identifiers, **not** artifact SHA-256 checksums.

At `master` `dd9871feea5b` (November 11, 2026), the tracked source tree no longer contains `Resources/Libraries/` files, compiled DLL/EXE/OCX/MSI/PDB/ZIP outputs, obsolete `.vspscc` / `.vssscc` bindings or the generated RSS `obj` file. CI now guards against reintroducing tracked build binaries, generated `bin`/`obj` files and those legacy bindings. It does **not** verify the provenance or redistribution rights of restored packages, optional external clients or assembled release payloads.

## Historical evidence

The original inventory covers Amazon S3/Flickr, Citrix ICA ActiveX, Microsoft Virtual Server VMRC, TransPort2006, WMI/RSS output assemblies, VncSharp/zlib, ZedGraph, MSTSC interop, wpcap/SharpPcap/Metro/HexEditor, PuTTY tools, WiX and orphan Mentalis/ThreeSharp/TerminalsLauncher sources. Their current *status and pending acceptance* are recorded only in the [work matrix](Dependency-modernization-roadmap.md); the pinned snapshot retains exact historical file identifiers.

## Outstanding provenance audit

Inventory actual restored and distributed dependencies by file SHA-256, size, version, origin, license and architecture. Trace each dependency through source, project references, Windows CI, installer and ZIP; classify it as runtime, build-only, optional or orphan. Validate COM generation/registration, Npcap installation and licensing, and distribution obligations. Preserve legitimate test fixtures and remove only proven orphans. The completed tracked-artifact guard is a hygiene measure, not a substitute for this audit.
