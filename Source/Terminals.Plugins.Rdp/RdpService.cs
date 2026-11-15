using System;
using System.Threading.Tasks;
using Terminals.TerminalServices;

namespace Terminals.Connections
{
    internal class RdpService
    {
        private readonly object sync = new object();
        private readonly Func<string, TerminalServer> loadServer;
        private Task<TerminalServer> loadTask;

        internal RdpService() : this(TerminalServer.LoadServer)
        {
        }

        internal RdpService(Func<string, TerminalServer> loadServer)
        {
            this.loadServer = loadServer;
        }

        public TerminalServer Server
        {
            get
            {
                Task<TerminalServer> task = this.LoadTask;
                return task != null && task.Status == TaskStatus.RanToCompletion ? task.Result : null;
            }
        }

        public bool IsTerminalServer
        {
            get
            {
                TerminalServer server = this.Server;
                return server != null && server.IsATerminalServer &&
                    server.Sessions != null && server.Errors.Count == 0;
            }
        }

        internal Task<TerminalServer> LoadTask
        {
            get
            {
                lock (this.sync)
                    return this.loadTask;
            }
        }

        // retry also permits an explicit refresh after a successful query.
        // A query already in progress is never duplicated.
        internal Task<TerminalServer> LoadTerminalServerAsync(string serverName, bool retry)
        {
            lock (this.sync)
            {
                if (this.loadTask == null || (retry && this.loadTask.IsCompleted))
                {
                    this.loadTask = Task.Factory.StartNew(() => this.TryLoad(serverName),
                        System.Threading.CancellationToken.None,
                        TaskCreationOptions.None, TaskScheduler.Default);
                }
                return this.loadTask;
            }
        }

        private TerminalServer TryLoad(string serverName)
        {
            try
            {
                TerminalServer server = this.loadServer(serverName);
                if (server == null || !server.IsATerminalServer ||
                    server.Sessions == null || server.Errors.Count != 0)
                    return null;
                return server;
            }
            catch (Exception exception)
            {
                Logging.Error(String.Format("Unable to query Terminal Services on {0}", serverName), exception);
                return null;
            }
        }
    }
}
