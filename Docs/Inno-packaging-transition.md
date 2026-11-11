# Packaging transition: WiX to Inno Setup

This PR changes the maintained distribution outputs to an Inno Setup EXE and a portable ZIP. It does not remove or alter existing remote connection protocols. Both distribution formats share `Build/PackageFiles.ps1`; the application is compiled using the existing `Release` configuration instead of `DistributionRelease`.

**Data handling:** the ZIP preserves `Portable=True`; the installer stages `Portable=False` and uses the existing user-profile log directory. Existing favorites and credentials must remain untouched. Do not uninstall or overwrite a previous MSI installation automatically until the transition behavior has been tested with realistic legacy configurations.

**Acceptance:** the Inno 7 compiler must be version-pinned and hash-checked; create ZIP and EXE twice successfully; silent install and uninstall with diagnostic logs; verify installed application and plugins; validate interactive startup and user-profile persistence separately.

**Completed cleanup:** The Inno script now lives at `Source/Setup/Terminals.iss` and appears in the existing Visual Studio `Setup` solution folder. Obsolete `Source/TerminalsSetup`, `Source/CustomSetupAction`, and orphaned `Source/CustomInstaller` have been removed, along with their solution references and MSI-only tests. `DistributionRelease` and the WiX-specific prerequisite script were retired. **Outstanding:** update or retire the legacy Chocolatey MSI-centric scripts in `Build/createChocolateyPackage.ps1` and `Build/tools/`; they must not be published unchanged.

**Possible later simplification:** audit duplicate root-level MSTSC wrappers, language satellites, VNC/ICA plugin binaries, and legacy resources by tracing real consumers. Remove a protocol only through a separately reviewed functional change with backward-compatibility tests. Dependencies are not an intrinsic blocker to an Inno installer.


## Build and transition safeguards

Run `Build/Build.ps1` from **Visual Studio Developer PowerShell** with MSBuild and Inno Setup 7 available (or set `INNO_ISCC_PATH` to the compiler executable). Both prerequisites are checked before the existing `Build/Output` directory is deleted. The script deletes `Build/Output` before restore to avoid stale release artifacts. `Build/Build-Inno.ps1 -OutputDirectory` passes the target path and staging path to the compiler.

The installer blocks legacy MSI installations and refuses a target directory containing a portable `Data` folder. It does not migrate or delete user favorites and credentials automatically. Back up all user data and uninstall the old MSI first; do not choose a portable application directory.

The `trm://` handler is updated on application startup in `HKCU\Software\Classes\TRM` when its command differs from the running executable. Inno uninstall removes the elevated uninstaller process account's HKCU handler only when it points to the installation being removed. That account may differ from the interactive user who registered `trm://`, so their per-user association may remain. The uninstaller does not modify other users' registry hives.
