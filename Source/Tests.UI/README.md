# UI runtime-exception smoke tests

This is an intentionally small black-box test suite. It launches a private copy of
`Terminals.exe` and checks startup, Options, the Plugins page, and New Favorite.

The tests fail if the application terminates unexpectedly, displays its fatal-error
dialog, or writes ERROR/FATAL or managed exception details to its log. They also
fail if the expected UI cannot be reached. This is exception detection, not UI
layout, protocol, or connection correctness testing.

Each test uses a temporary application copy and the existing
`Resources/TestData/EmptyTerminals.config` fixture with separate config,
favorites, and credentials paths. Temporary files are removed after each test.
No real credentials or remote endpoints are required.

Run on Windows with an interactive desktop session:

1. Build `Source/Terminals.sln` in Release.
2. Execute `Source/Tests.UI/bin/Release/net481/Tests.UI.dll` with VSTest
   (or run its tests from Visual Studio).
3. Optionally set `TERMINALS_UI_EXE` to another built Terminals executable.

The GUI tests are **not** invoked by the existing GitHub Actions VSTest suites,
which explicitly run only `Source/Tests/bin/Release/Tests.dll`. The project is
included in the solution so CI can restore and compile it.

The initial scope deliberately excludes protocol connectivity and screenshots.
If the UIA2 selectors prove unreliable, inspect the controls with FlaUInspect
before extending the suite. These tests depend on the application's existing
log4net file appender and should run in a dedicated Windows user profile.
