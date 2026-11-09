using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace Terminals.TerminalServices
{
    public static class TerminalServicesAPI
    {

        [DllImport("WtsApi32.dll", EntryPoint = "WTSQuerySessionInformationW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern bool WTSQuerySessionInformationNative(IntPtr hServer, int sessionId, WTS_INFO_CLASS infoClass, ref IntPtr buffer, ref int bytesReturned);

        public static bool WTSQuerySessionInformation(IntPtr server, int sessionId, WTS_INFO_CLASS infoClass, ref IntPtr buffer, ref int bytesReturned)
        {
            return WTSQuerySessionInformationNative(server, sessionId, infoClass, ref buffer, ref bytesReturned);
        }

        [DllImport("wtsapi32.dll", EntryPoint = "WTSEnumerateProcessesW", SetLastError = true)]
            static extern bool WTSEnumerateProcessesNative(
                IntPtr serverHandle, // Handle to a terminal server. 
                Int32  reserved,     // must be 0
                Int32  version,      // must be 1
                ref IntPtr ppProcessInfo, // pointer to array of WTS_PROCESS_INFO
                ref Int32  pCount);       // pointer to number of processes
            

        [DllImport("WtsApi32.dll", EntryPoint = "WTSQuerySessionInformationW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern bool WTSQuerySessionInformation2(System.IntPtr hServer, int SessionId, WTS_INFO_CLASS WTSInfoClass, ref IntPtr ppBuffer, ref Int32 pCount);
        [DllImport("Kernel32.dll", EntryPoint = "GetCurrentProcessId", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern Int32 GetCurrentProcessId();
        [DllImport("Kernel32.dll", EntryPoint = "ProcessIdToSessionId", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern bool ProcessIdToSessionId(Int32 processID, ref Int32 sessionID);

        [DllImport("wtsapi32.dll", EntryPoint = "WTSSendMessageW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool WTSSendMessage(
                    IntPtr hServer,
                    [MarshalAs(UnmanagedType.I4)] int SessionId,
                    String pTitle,
                    [MarshalAs(UnmanagedType.U4)] int TitleLength,
                    String pMessage,
                    [MarshalAs(UnmanagedType.U4)] int MessageLength,
                    [MarshalAs(UnmanagedType.U4)] int Style,
                    [MarshalAs(UnmanagedType.U4)] int Timeout,
                    [MarshalAs(UnmanagedType.U4)] out int pResponse,
                    bool bWait);


        //Function for TS Client IP Address 

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSLogoffSession(IntPtr hServer, int SessionId, bool bWait);

        [DllImport("advapi32.dll", EntryPoint = "LookupAccountSidW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool LookupAccountSid(string server, IntPtr sid, StringBuilder name,
            ref uint nameLength, StringBuilder domain, ref uint domainLength, out SID_NAME_USE use);

        [DllImport("wtsapi32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, EntryPoint = "WTSEnumerateSessionsW", SetLastError = true, ThrowOnUnmappableChar = true)]
        private static extern Int32 WTSEnumerateSessions(
            [MarshalAs(UnmanagedType.SysInt)] 
            IntPtr hServer, 
            [MarshalAs(UnmanagedType.U4)] 
            int Reserved,
           [MarshalAs(UnmanagedType.U4)] 
            int Vesrion, 
            [MarshalAs(UnmanagedType.SysInt)] 
            ref IntPtr ppSessionInfo,
           [MarshalAs(UnmanagedType.U4)] 
            ref int pCount);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("wtsapi32.dll", EntryPoint = "WTSOpenServerW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr WTSOpenServerNative(string serverName);

        public static IntPtr WTSOpenServer(string serverName)
        {
            return WTSOpenServerNative(serverName);
        }

        [DllImport("wtsapi32.dll", EntryPoint = "WTSCloseServer", ExactSpelling = true, SetLastError = true)]
        private static extern void WTSCloseServerNative(IntPtr server);

        public static void WTSCloseServer(IntPtr server)
        {
            if (server != IntPtr.Zero)
                WTSCloseServerNative(server);
        }
        [DllImport("wtsapi32.dll", EntryPoint = "WTSShutdownSystem", ExactSpelling = true, SetLastError = true)]
        private static extern bool WTSShutdownSystemNative(IntPtr server, int shutdownFlags);

        public static bool WTSShutdownSystem(IntPtr server, int shutdownFlags)
        {
            return WTSShutdownSystemNative(server, shutdownFlags);
        }
        private const int WTS_WSD_REBOOT = 0x00000004;
        private const int WTS_WSD_SHUTDOWN = 0x00000002;

        public static void ShutdownSystem(TerminalServer server, bool reboot)
        {
            IntPtr handle = WTSOpenServer(server.ServerName);
            if (handle == IntPtr.Zero) return;
            try { WTSShutdownSystem(handle, reboot ? WTS_WSD_REBOOT : WTS_WSD_SHUTDOWN); }
            finally { WTSCloseServer(handle); }
        }

        public static bool SendMessage(Session session, string title, string message, int style, int timeout, bool wait)
        {
            IntPtr handle = WTSOpenServer(session.ServerName);
            if (handle == IntPtr.Zero) return false;
            try
            {
                int response;
                return WTSSendMessage(handle, session.SessionID, title, checked(title.Length * 2),
                    message, checked(message.Length * 2), style, timeout, out response, wait);
            }
            finally { WTSCloseServer(handle); }
        }

        public static bool LogOffSession(Session session, bool wait)
        {
            IntPtr handle = WTSOpenServer(session.ServerName);
            if (handle == IntPtr.Zero) return false;
            try { return WTSLogoffSession(handle, session.SessionID, wait); }
            finally { WTSCloseServer(handle); }
        }

        public static TerminalServer GetSessions(string serverName)
        {
            TerminalServer data = new TerminalServer { ServerName = serverName, Sessions = new List<Session>() };
            IntPtr handle = WTSOpenServer(serverName);
            if (handle == IntPtr.Zero)
            {
                data.Errors.Add("WTSOpenServer failed: " + Marshal.GetLastWin32Error());
                return data;
            }

            try
            {
                data.IsATerminalServer = true;
                data.ServerPointer = handle;
                PopulateSessions(handle, data);
                PopulateProcesses(handle, data);
                PopulateClients(handle, data);
            }
            finally
            {
                WTSCloseServer(handle);
                data.ServerPointer = IntPtr.Zero;
            }
            return data;
        }

        private static void PopulateSessions(IntPtr handle, TerminalServer data)
        {
            IntPtr buffer = IntPtr.Zero;
            int count = 0;
            try
            {
                if (WTSEnumerateSessions(handle, 0, 1, ref buffer, ref count) == 0)
                {
                    data.Errors.Add("WTSEnumerateSessions failed: " + Marshal.GetLastWin32Error());
                    return;
                }
                int stride = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                for (int i = 0; i < count; i++)
                {
                    IntPtr item = IntPtr.Add(buffer, checked(i * stride));
                    WTS_SESSION_INFO info = (WTS_SESSION_INFO)Marshal.PtrToStructure(item, typeof(WTS_SESSION_INFO));
                    data.Sessions.Add(new Session
                    {
                        SessionID = info.SessionID,
                        State = (ConnectionStates)(int)info.State,
                        WindowsStationName = info.pWinStationName,
                        ServerName = data.ServerName
                    });
                }
            }
            catch (Exception ex)
            {
                Logging.Error("Enumerate Sessions", ex);
                data.Errors.Add(ex.Message);
            }
            finally { if (buffer != IntPtr.Zero) WTSFreeMemory(buffer); }
        }

        private static void PopulateProcesses(IntPtr handle, TerminalServer data)
        {
            try { WTSEnumerateProcesses(handle, data); }
            catch (Exception ex)
            {
                Logging.Error("Enumerate Processes", ex);
                data.Errors.Add(ex.Message);
            }
        }

        private static void PopulateClients(IntPtr handle, TerminalServer data)
        {
            int activeSession = 0;
            bool active = ProcessIdToSessionId(GetCurrentProcessId(), ref activeSession) && activeSession > 0;
            foreach (Session session in data.Sessions)
            {
                if (session.Client == null) session.Client = new Client();
                try
                {
                    WTS_CLIENT_INFO client = LoadClientInfoForSession(handle, session.SessionID);
                    session.Client.Address = client.Address;
                    session.Client.AddressFamily = client.AddressFamily;
                    session.Client.ClientName = client.WTSClientName;
                    session.Client.DomianName = client.WTSDomainName;
                    session.Client.StationName = client.WTSStationName;
                    session.Client.Status = client.WTSStatus;
                    session.Client.UserName = client.WTSUserName;
                }
                catch (Exception ex)
                {
                    Logging.Error("Load Client Info", ex);
                    data.Errors.Add(ex.Message);
                }
                session.IsTheActiveSession = active && session.SessionID == activeSession;
            }
        }

        public static WTS_PROCESS_INFO[] WTSEnumerateProcesses(IntPtr handle, TerminalServer data)
        {
            IntPtr buffer = IntPtr.Zero;
            int count = 0;
            if (!WTSEnumerateProcessesNative(handle, 0, 1, ref buffer, ref count))
            {
                data.Errors.Add("WTSEnumerateProcesses failed: " + Marshal.GetLastWin32Error());
                if (buffer != IntPtr.Zero) WTSFreeMemory(buffer);
                return new WTS_PROCESS_INFO[0];
            }

            try
            {
                WTS_PROCESS_INFO[] items = new WTS_PROCESS_INFO[count];
                int stride = Marshal.SizeOf(typeof(WTS_PROCESS_INFO));
                for (int i = 0; i < count; i++)
                {
                    IntPtr item = IntPtr.Add(buffer, checked(i * stride));
                    WTS_PROCESS_INFO info = (WTS_PROCESS_INFO)Marshal.PtrToStructure(item, typeof(WTS_PROCESS_INFO));
                    items[i] = info;
                    AddProcess(data, info);
                }
                return items;
            }
            finally { if (buffer != IntPtr.Zero) WTSFreeMemory(buffer); }
        }

        private static void AddProcess(TerminalServer data, WTS_PROCESS_INFO info)
        {
            SessionProcess process = new SessionProcess
            {
                ProcessID = info.ProcessID,
                ProcessName = Marshal.PtrToStringUni(info.ProcessName),
                SessionID = info.SessionID
            };
            if (info.UserSid != IntPtr.Zero)
                ResolveProcessOwner(data.ServerName, info.UserSid, process);
            foreach (Session session in data.Sessions)
            {
                if (session.SessionID != process.SessionID) continue;
                if (session.Processes == null) session.Processes = new List<SessionProcess>();
                session.Processes.Add(process);
                break;
            }
        }

        private static void ResolveProcessOwner(string serverName, IntPtr sid, SessionProcess process)
        {
            StringBuilder name = new StringBuilder(256);
            StringBuilder domain = new StringBuilder(256);
            uint nameSize = (uint)name.Capacity;
            uint domainSize = (uint)domain.Capacity;
            SID_NAME_USE use;
            bool found = LookupAccountSid(serverName, sid, name, ref nameSize, domain, ref domainSize, out use);
            if (!found && Marshal.GetLastWin32Error() == 122)
            {
                name = new StringBuilder(checked((int)nameSize));
                domain = new StringBuilder(checked((int)domainSize));
                found = LookupAccountSid(serverName, sid, name, ref nameSize, domain, ref domainSize, out use);
            }
            if (found)
            {
                process.UserType = use.ToString();
                process.User = name.ToString();
            }
        }

        private static WTS_CLIENT_INFO LoadClientInfoForSession(IntPtr handle, int id)
        {
            WTS_CLIENT_INFO client = new WTS_CLIENT_INFO();
            client.WTSStationName = "";
            client.WTSClientName = "";
            client.Address = new byte[6];
            client.WTSClientName = GetString(handle, id, WTS_INFO_CLASS.WTSClientName);
            client.WTSStationName = GetString(handle, id, WTS_INFO_CLASS.WTSWinStationName);
            client.WTSDomainName = GetString(handle, id, WTS_INFO_CLASS.WTSDomainName);
            IntPtr buffer = IntPtr.Zero;
            int length = 0;
            try
            {
                if (WTSQuerySessionInformation2(handle, id, WTS_INFO_CLASS.WTSClientAddress,
                    ref buffer, ref length) && buffer != IntPtr.Zero &&
                    length >= Marshal.SizeOf(typeof(_WTS_CLIENT_ADDRESS)))
                {
                    _WTS_CLIENT_ADDRESS address = (_WTS_CLIENT_ADDRESS)Marshal.PtrToStructure(buffer, typeof(_WTS_CLIENT_ADDRESS));
                    client.AddressFamily = address.AddressFamily;
                    Array.Copy(address.Address, client.Address, client.Address.Length);
                }
            }
            finally { if (buffer != IntPtr.Zero) WTSFreeMemory(buffer); }
            return client;
        }

        private static string GetString(IntPtr handle, int id, WTS_INFO_CLASS info)
        {
            IntPtr buffer = IntPtr.Zero;
            int length = 0;
            try
            {
                if (WTSQuerySessionInformation(handle, id, info, ref buffer, ref length) && buffer != IntPtr.Zero)
                    return Marshal.PtrToStringUni(buffer);
                return "";
            }
            finally { if (buffer != IntPtr.Zero) WTSFreeMemory(buffer); }
        }



        [StructLayout(LayoutKind.Sequential)]
        public struct WTS_PROCESS_INFO
        {
            public int SessionID;
            public int ProcessID;
            //This is a pointer to string...
            public IntPtr ProcessName;
            public IntPtr UserSid;
        }

        public enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WTS_SESSION_INFO
        {
            //DWORD integer 
            public Int32 SessionID;
            // integer LPTSTR - Pointer to a null-terminated string containing the name of the WinStation for this session 
            public string pWinStationName;
            public WTS_CONNECTSTATE_CLASS State;
        }



        public enum WTS_INFO_CLASS
        {
            WTSInitialProgram,
            WTSApplicationName,
            WTSWorkingDirectory,
            WTSOEMId,
            WTSSessionId,
            WTSUserName,
            WTSWinStationName,
            WTSDomainName,
            WTSConnectState,
            WTSClientBuildNumber,
            WTSClientName,
            WTSClientDirectory,
            WTSClientProductId,
            WTSClientHardwareId,
            WTSClientAddress,
            WTSClientDisplay,
            WTSClientProtocolType,
            WTSIdleTime,
            WTSLogonTime,
            WTSIncomingBytes,
            WTSOutgoingBytes,
            WTSIncomingFrames,
            WTSOutgoingFrames
        }
        //Structure for TS Client IP Address 
        [StructLayout(LayoutKind.Sequential)]
        public struct _WTS_CLIENT_ADDRESS
        {
            public int AddressFamily;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] Address;
        }
        //Structure for TS Client Information 
        public struct WTS_CLIENT_INFO
        {
            [MarshalAs(UnmanagedType.Bool)] 
            public bool WTSStatus;
            [MarshalAs(UnmanagedType.LPWStr)] 
            public string WTSUserName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string WTSStationName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string WTSDomainName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string WTSClientName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public int AddressFamily;
            [MarshalAs(UnmanagedType.ByValArray)]
            public byte[] Address;
        }
       public enum SID_NAME_USE
        {
            User = 1,
            Group,
            Domain,
            Alias,
            WellKnownGroup,
            DeletedAccount,
            Invalid,
            Unknown,
            Computer
        }
    }
}