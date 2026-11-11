using System;
using System.Drawing;
using System.Windows.Forms;
using Terminals.Common.Connections;
using Terminals.Data;
using Terminals.Forms.EditFavorite;
using Terminals.Integration.Export;
using Terminals.Plugins.Vnc;
using Terminals.Plugins.Vnc.Properties;
using System.Net.Sockets;

namespace Terminals.Connections.VNC
{
    internal class VncConnectionPlugin : IConnectionPlugin, IOptionsExporterFactory,
        IOptionsConverterFactory, IExtraDetection
    {
        internal const string VNC = "VNC";

        internal const int VncPort = 5900;

        internal static readonly Image TreeIconVnc = Resources.treeIcon_vnc;

        public int Port { get { return VncPort; } }

        public string PortName { get { return VNC; } }

        public Connection CreateConnection()
        {
            return new VNCConnection();
        }

        public Control[] CreateOptionsControls()
        {
            return new Control[] { new VncControl() { Name = "VNC" } };
        }

        public Type GetOptionsType()
        {
            return typeof (VncOptions);
        }

        public ProtocolOptions CreateOptions()
        {
            return new VncOptions();
        }

        public ITerminalsOptionsExport CreateOptionsExporter()
        {
            return new TerminalsVncExport();
        }

        public Image GetIcon()
        {
            return TreeIconVnc;
        }

        public bool IsValid(string ipAddress, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    IAsyncResult pending = client.BeginConnect(ipAddress, port, null, null);
                    using (pending.AsyncWaitHandle)
                    {
                        if (!pending.AsyncWaitHandle.WaitOne(1500))
                            return false;
                    }
                    client.EndConnect(pending);
                    using (NetworkStream stream = client.GetStream())
                    {
                        stream.ReadTimeout = 1500;
                        var banner = new byte[12];
                        int received = 0;
                        while (received < banner.Length)
                        {
                            int count = stream.Read(banner, received, banner.Length - received);
                            if (count <= 0) return false;
                            received += count;
                        }
                        if (banner[0] != 'R' || banner[1] != 'F' ||
                            banner[2] != 'B' || banner[3] != ' ' ||
                            banner[7] != '.' || banner[11] != '\n')
                            return false;
                        for (int i = 4; i <= 10; i++)
                            if (i != 7 && (banner[i] < '0' || banner[i] > '9'))
                                return false;
                        return true;
                    }
                }
            }
            catch (Exception ex) when (ex is SocketException || ex is System.IO.IOException ||
                                       ex is ArgumentException || ex is InvalidOperationException)
            {
                return false;
            }
        }

        public IOptionsConverter CreatOptionsConverter()
        {
            return new VncOptionsConverter();
        }
    }
}
