using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Terminals.Common.Forms
{
    /// <summary>
    /// Centralizes blocking external process operations off the WinForms thread.
    /// UI notifications are posted asynchronously to a captured synchronization context.
    /// Connections retain ownership of embedded or interactive process lifetimes.
    /// </summary>
    public static class ExternalProcessLauncher
    {
        public static Task RunAsync(Action operation)
        {
            if (operation == null) throw new ArgumentNullException("operation");
            return Task.Run(operation);
        }

        public static Task<T> RunAsync<T>(Func<T> operation)
        {
            if (operation == null) throw new ArgumentNullException("operation");
            return Task.Run(operation);
        }

        public static void Post(SynchronizationContext context, Action callback)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            if (context != null)
                context.Post(_ => callback(), null);
        }

        public static void StartDetached(ProcessStartInfo start, Action<Exception> onError = null,
            SynchronizationContext callbackContext = null)
        {
            if (start == null) throw new ArgumentNullException("start");
            RunAsync(() =>
            {
                try
                {
                    using (var process = new Process { StartInfo = start })
                    {
                        if (!process.Start())
                            throw new InvalidOperationException("The external process did not start.");
                    }
                }
                catch (Exception ex) when (ex is Win32Exception || ex is IOException ||
                                           ex is InvalidOperationException)
                {
                    if (onError != null)
                        Post(callbackContext, () => onError(ex));
                }
            });
        }

        public static Task<int> ExecuteAsync(ProcessStartInfo start, bool waitForExit)
        {
            if (start == null) throw new ArgumentNullException("start");
            return RunAsync(() =>
            {
                using (var process = new Process { StartInfo = start })
                {
                    if (!process.Start())
                        throw new InvalidOperationException("The external process did not start.");
                    if (!waitForExit) return 0;
                    process.WaitForExit(); // Worker thread only
                    return process.ExitCode;
                }
            });
        }

        public static void StopDetached(Process process)
        {
            if (process == null) return;
            RunAsync(() =>
            {
                try { if (!process.HasExited) process.Kill(); }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { }
                finally { process.Dispose(); }
            });
        }
    }
}
