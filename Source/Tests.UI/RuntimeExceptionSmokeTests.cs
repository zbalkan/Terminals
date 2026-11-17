using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FlaApplication = FlaUI.Core.Application;

namespace Tests.UI
{
    [TestClass]
    [DoNotParallelize]
    public class RuntimeExceptionSmokeTests
    {
        [TestMethod]
        [TestCategory("UI")]
        public void Startup_OpensMainWindowWithoutRuntimeExceptions()
        {
            Run(session => session.WaitForWindow("Terminals"));
        }

        [TestMethod]
        [TestCategory("UI")]
        public void Options_OpensWithoutRuntimeExceptions()
        {
            Run(session =>
            {
                var options = session.OpenOptions();
                session.CloseDialog(options);
            });
        }

        [TestMethod]
        [TestCategory("UI")]
        public void Options_PluginsPageOpensWithoutRuntimeExceptions()
        {
            Run(session =>
            {
                var options = session.OpenOptions();
                var plugins = options.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.TreeItem).And(cf.ByName("Plugins")));
                if (plugins == null)
                    throw new InvalidOperationException("Options/Plugins tree item was not exposed by UIA2.");

                plugins.Click();
                var heading = options.FindFirstDescendant(cf => cf.ByAutomationId("OptionTitelLabel"));
                if (heading == null || heading.Properties.Name.Value != "Plugins")
                    throw new InvalidOperationException("Plugins page was not selected after clicking its tree item.");
                session.CloseDialog(options);
            });
        }

        [TestMethod]
        [TestCategory("UI")]
        public void NewFavorite_OpensWithoutRuntimeExceptions()
        {
            Run(session =>
            {
                var main = session.WaitForWindow("Terminals");
                session.ActivateMenu(main, "fileToolStripMenuItem", "File", "New Favorite");
                var dialog = session.WaitForWindow("New Connection");
                session.CloseDialog(dialog);
            });
        }

        private static void Run(Action<SmokeSession> action)
        {
            using (var session = new SmokeSession())
            {
                try
                {
                    action(session);
                    // Allow queued WinForms callbacks and initial background work to report failures.
                    Thread.Sleep(750);
                    session.AssertHealthy();
                }
                catch (Exception exception)
                {
                    Assert.Fail(session.DescribeFailure(exception));
                }
            }
        }

        private sealed class SmokeSession : IDisposable
        {
            private static readonly Regex ErrorLine = new Regex(
                @"^(ERROR|FATAL)\s|\b[A-Za-z_][\w.]*Exception\s*:",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            private readonly string sandbox;
            private readonly string logFile;
            private readonly UIA2Automation automation;
            private readonly FlaApplication app;

            public SmokeSession()
            {
                var repository = FindRepositoryRoot();
                var releaseExe = Environment.GetEnvironmentVariable("TERMINALS_UI_EXE");
                if (string.IsNullOrWhiteSpace(releaseExe))
                    releaseExe = Path.Combine(repository, "Build", "Output", "Release", "Terminals.exe");
                releaseExe = Path.GetFullPath(releaseExe);

                if (!File.Exists(releaseExe))
                    throw new FileNotFoundException("Build Terminals (Release) before running UI tests.", releaseExe);

                sandbox = Path.Combine(Path.GetTempPath(), "Terminals-UI-" + Guid.NewGuid().ToString("N"));
                var appFolder = Path.Combine(sandbox, "App");
                Directory.CreateDirectory(appFolder);
                CopyDistribution(Path.GetDirectoryName(releaseExe), appFolder);

                var dataFolder = Path.Combine(sandbox, "Data");
                Directory.CreateDirectory(dataFolder);
                var configFile = Path.Combine(dataFolder, "Terminals.config");
                var fixture = Path.Combine(repository, "Resources", "TestData", "EmptyTerminals.config");
                if (!File.Exists(fixture))
                    throw new FileNotFoundException("Missing UI test configuration fixture.", fixture);
                var config = File.ReadAllText(fixture).Replace("minimizeToTray=\"true\"", "minimizeToTray=\"false\"");
                File.WriteAllText(configFile, config);

                logFile = Path.Combine(appFolder, "Data", "logs", "CurrentLog.txt");
                var arguments = "/config:\"" + configFile + "\" /favsfile:\"" +
                    Path.Combine(dataFolder, "Favorites.xml") + "\" /cred:\"" +
                    Path.Combine(dataFolder, "Credentials.xml") + "\"";

                automation = new UIA2Automation();
                app = FlaApplication.Launch(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Path.Combine(appFolder, "Terminals.exe"),
                    WorkingDirectory = appFolder,
                    Arguments = arguments,
                    UseShellExecute = false
                });
            }

            public Window WaitForWindow(string title)
            {
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(25);
                do
                {
                    if (app.HasExited)
                        throw new InvalidOperationException("Terminals exited before '" + title +
                            "' appeared (exit code " + app.ExitCode + ").");

                    var window = app.GetAllTopLevelWindows(automation)
                        .FirstOrDefault(item => MatchesWindow(item, title));
                    if (window != null)
                        return window;
                    Thread.Sleep(200);
                }
                while (DateTime.UtcNow < deadline);

                throw new TimeoutException("Window '" + title + "' did not appear.");
            }

            private static bool MatchesWindow(Window window, string expectedTitle)
            {
                if (expectedTitle == "Terminals")
                {
                    // MainForm assigns a version/store-specific caption at runtime.
                    if (window.Properties.AutomationId.Value == "MainForm")
                        return true;
                    return window.Title != null &&
                        window.Title.StartsWith("Terminals ", StringComparison.OrdinalIgnoreCase) &&
                        window.Title.IndexOf(" store)", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                return string.Equals(window.Title, expectedTitle, StringComparison.OrdinalIgnoreCase);
            }

            public Window OpenOptions()
            {
                var main = WaitForWindow("Terminals");
                ActivateMenu(main, "toolsToolStripMenuItem", "Tools", "Options");
                return WaitForWindow("Options");
            }

            public void ActivateMenu(Window main, string automationId, string menuName, string itemName)
            {
                var menu = main.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) ??
                    main.FindFirstDescendant(cf =>
                        cf.ByControlType(ControlType.MenuItem).And(cf.ByName(menuName)));
                if (menu == null)
                    throw new InvalidOperationException("Menu '" + menuName + "' was not exposed by UIA2.");
                menu.Click();

                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
                do
                {
                    var item = automation.GetDesktop().FindFirstDescendant(cf =>
                        cf.ByControlType(ControlType.MenuItem).And(cf.ByName(itemName)));
                    if (item != null)
                    {
                        item.Click();
                        return;
                    }
                    Thread.Sleep(200);
                }
                while (DateTime.UtcNow < deadline);

                throw new TimeoutException("Menu item '" + itemName + "' did not appear.");
            }

            public void CloseDialog(Window window)
            {
                var cancel = window.FindFirstDescendant(cf => cf.ByAutomationId("btnCancel"));
                if (cancel == null)
                    throw new InvalidOperationException("Cancel button not found in '" + window.Title + "'.");
                cancel.Click();
            }

            public void AssertHealthy()
            {
                if (app.HasExited)
                    throw new InvalidOperationException("Terminals terminated unexpectedly, exit code " + app.ExitCode + ".");

                var fatalDialog = app.GetAllTopLevelWindows(automation)
                    .FirstOrDefault(w => w.Title == "Terminals - problem occurred");
                if (fatalDialog != null)
                    throw new InvalidOperationException("Terminals displayed its unhandled-exception dialog.");

                if (!File.Exists(logFile))
                    throw new InvalidOperationException("No Terminals log was created at " + logFile +
                        "; runtime exception detection cannot be verified.");

                var failures = ReadLog().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => ErrorLine.IsMatch(line)).Take(10).ToArray();
                if (failures.Length > 0)
                    throw new InvalidOperationException("Terminals logged runtime exceptions/errors:" +
                        Environment.NewLine + string.Join(Environment.NewLine, failures));
            }

            public string DescribeFailure(Exception exception)
            {
                string windows;
                try
                {
                    windows = app == null ? "<application not launched>" :
                        string.Join(", ", app.GetAllTopLevelWindows(automation)
                            .Select(w => w.Title).ToArray());
                }
                catch (Exception error)
                {
                    windows = "<unavailable: " + error.Message + ">";
                }

                var log = ReadLog();
                if (log.Length > 6000)
                    log = log.Substring(log.Length - 6000);

                return exception + Environment.NewLine +
                    "Top-level windows: " + windows + Environment.NewLine +
                    "Log: " + logFile + Environment.NewLine +
                    (log.Length == 0 ? "<no log captured>" : log);
            }

            private string ReadLog()
            {
                try
                {
                    using (var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream))
                        return reader.ReadToEnd();
                }
                catch (FileNotFoundException)
                {
                    return string.Empty;
                }
                catch (DirectoryNotFoundException)
                {
                    return string.Empty;
                }
            }

            private static string FindRepositoryRoot()
            {
                var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (directory != null)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "Source", "Terminals.sln")))
                        return directory.FullName;
                    directory = directory.Parent;
                }
                throw new DirectoryNotFoundException("Tests.UI must run from a Terminals repository checkout.");
            }

            private static void CopyDistribution(string source, string destination)
            {
                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    var relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                    if (relative.StartsWith("Data" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var target = Path.Combine(destination, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target);
                }
            }

            public void Dispose()
            {
                if (app != null)
                {
                    app.Kill();
                    app.Dispose();
                }
                if (automation != null)
                    automation.Dispose();
                if (sandbox != null)
                {
                    try { Directory.Delete(sandbox, true); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
