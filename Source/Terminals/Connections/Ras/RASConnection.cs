using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Terminals.Connections.RasNative;

namespace Terminals.Connections
{
    // Serializes background native work without holding a lock during a native call.
    // A queued close always runs after an in-flight dial, including a failed dial.
    internal sealed class RasOperationQueue
    {
        private readonly object gate = new object();
        private System.Threading.Tasks.Task tail = System.Threading.Tasks.Task.FromResult(0);
        private bool closing;

        internal System.Threading.Tasks.Task<T> Enqueue<T>(Func<T> operation)
        {
            lock (gate)
            {
                if (closing)
                    throw new InvalidOperationException("The RAS session is closing.");
                return Append(operation);
            }
        }

        internal System.Threading.Tasks.Task<T> Close<T>(Func<T> cleanup)
        {
            lock (gate)
            {
                closing = true;
                return Append(cleanup);
            }
        }

        private System.Threading.Tasks.Task<T> Append<T>(Func<T> operation)
        {
            var next = tail.ContinueWith(previous => operation(),
                System.Threading.CancellationToken.None,
                System.Threading.Tasks.TaskContinuationOptions.None,
                System.Threading.Tasks.TaskScheduler.Default);
            // Observe any exception even if the UI has disappeared.
            tail = next.ContinueWith(completed => { var error = completed.Exception; },
                System.Threading.CancellationToken.None,
                System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                System.Threading.Tasks.TaskScheduler.Default);
            return next;
        }
    }

    // UI-independent session registry: a failed close must not abandon a native reference.
    internal sealed class RasBackgroundSession
    {
        private static readonly object RegistryGate = new object();
        private static readonly HashSet<RasBackgroundSession> Active = new HashSet<RasBackgroundSession>();
        private readonly RasOperationQueue queue = new RasOperationQueue();
        private readonly RasConnectionClient client;
        private readonly object gate = new object();
        private Task<bool> closing;

        internal RasBackgroundSession(RasConnectionClient client)
        {
            this.client = client;
            lock (RegistryGate) Active.Add(this);
        }

        internal static bool HasActiveSessions
        {
            get { lock (RegistryGate) return Active.Count != 0; }
        }

        internal Task<uint> Dial(string user, string password, string domain)
        {
            return queue.Enqueue(() => client.Dial(user, password, domain));
        }

        internal Task<RasStatusSnapshot> ReadStatus()
        {
            return queue.Enqueue(() =>
            {
                bool connected = client.Connected;
                return new RasStatusSnapshot(connected, client.HasOutstandingHandle,
                    connected ? client.IPAddress() : string.Empty);
            });
        }

        internal Task<bool> Close()
        {
            lock (gate)
            {
                if (closing != null && !closing.IsCompleted)
                    return closing;
                closing = queue.Close(() =>
                {
                    client.Dispose();
                    lock (RegistryGate) Active.Remove(this);
                    return true;
                });
                return closing;
            }
        }

        internal static async Task CloseAll()
        {
            RasBackgroundSession[] sessions;
            lock (RegistryGate)
            {
                sessions = new RasBackgroundSession[Active.Count];
                Active.CopyTo(sessions);
            }
            await Task.WhenAll(Array.ConvertAll(sessions, session => session.Close())).ConfigureAwait(false);
        }
    }

    internal sealed class RasStatusSnapshot
    {
        internal readonly bool Connected;
        internal readonly bool Outstanding;
        internal readonly string Address;
        internal RasStatusSnapshot(bool connected, bool outstanding, string address)
        {
            Connected = connected;
            Outstanding = outstanding;
            Address = address;
        }
    }

    internal class RASConnection : Connection
    {
        private RasBackgroundSession session;
        private bool pending;
        private bool connected;
        private bool closing;
        private bool refreshing;
        private bool closed;
        private bool disposed;
        private Task<bool> closeTask;
        internal string StatusText { get; private set; } = "Not Connected";
        internal string Address { get; private set; }
        internal string EntryName { get; private set; }
        public override bool Connected { get { return connected; } }
        public override bool ConnectionPending { get { return pending || closing || (session != null && !closed && !connected); } }

        public override bool Connect()
        {
            if (session != null || disposed)
                return false;
            try
            {
                var credentials = ResolveFavoriteCredentials();
                string user = credentials.UserName, password = credentials.Password, domain = credentials.Domain;
                if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
                    throw new NotSupportedException("Managed RAS requires explicit credentials; use Windows to manage dialog-based connections.");
                EntryName = Favorite.ServerName;
                var properties = new RASProperties { RASConnection = this, Dock = DockStyle.Fill };
                Dock = DockStyle.Fill;
                Controls.Add(properties);
                properties.BringToFront();
                session = new RasBackgroundSession(new RasConnectionClient { EntryName = EntryName });
                pending = true;
                StatusText = "Connecting";
                StartDial(user, password, domain);
                return true;
            }
            catch (Exception exc)
            {
                LastError = exc.Message;
                Logging.Error("Connecting to RAS", exc);
                return false;
            }
        }

        private async void StartDial(string user, string password, string domain)
        {
            try
            {
                uint error = await session.Dial(user, password, domain);
                if (disposed || closing || closed)
                    return;
                if (error != 0)
                    throw new InvalidOperationException("Windows RAS dial failed with error " + error);
                connected = true;
                StatusText = "Connected";
                Log("RAS connection established");
            }
            catch (Exception exc)
            {
                if (disposed || closing || closed)
                    return;
                LastError = exc.Message;
                StatusText = LastError;
                Logging.Error("Connecting to RAS", exc);
                await CloseAsync();
                if (!disposed && closed)
                    FireDisconnected();
            }
            finally { pending = false; }
        }

        internal async Task RefreshStatusAsync()
        {
            if (disposed || pending || closing || closed || refreshing || session == null)
                return;
            refreshing = true;
            try
            {
                var status = await session.ReadStatus();
                if (disposed || closing || closed)
                    return;
                connected = status.Connected;
                Address = status.Address;
                StatusText = connected ? "Connected" : status.Outstanding ? "Connection state unresolved" : "Not Connected";
                if (!status.Outstanding)
                {
                    await CloseAsync();
                    if (!disposed && closed)
                        FireDisconnected();
                }
            }
            catch (Exception exc)
            {
                if (!disposed && !closing && !closed)
                    StatusText = "Status unavailable: " + exc.Message;
            }
            finally { refreshing = false; }
        }

        internal Task<bool> CloseAsync()
        {
            if (closed || session == null)
                return Task.FromResult(true);
            if (closeTask != null && !closeTask.IsCompleted)
                return closeTask;
            closing = true;
            StatusText = "Disconnecting";
            closeTask = FinishCloseAsync();
            return closeTask;
        }

        private async Task<bool> FinishCloseAsync()
        {
            try
            {
                await session.Close();
                closed = true;
                connected = false;
                Address = string.Empty;
                StatusText = "Not Connected";
                return true;
            }
            catch (Exception exc)
            {
                LastError = exc.Message;
                connected = false;
                StatusText = "Cleanup incomplete: " + LastError;
                Logging.Error("Disconnecting RAS", exc);
                return false;
            }
            finally { closing = false; }
        }

        public async void Disconnect()
        {
            if (await CloseAsync() && !disposed)
                FireDisconnected();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                // The registry owns the session until cleanup succeeds, even after UI disposal.
                if (session != null && !closed)
                {
                    var cleanup = session.Close();
                    cleanup.ContinueWith(task => Logging.Error("RAS disposal cleanup failed", task.Exception),
                        TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            base.Dispose(disposing);
        }
    }
}
