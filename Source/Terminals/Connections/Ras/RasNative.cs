using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace Terminals.Connections.RasNative
{
    // Own only references returned directly by RasDial, never enumerated handles.
    // Serialize all operations on the reference, including disposal.
    internal sealed class RasConnectionClient : IDisposable
    {
        private const int RasConnected = 0x2000;
        private const int RasPppIp = 0x8021;
        internal const uint ErrorInvalidHandle = 6;
        internal const uint ErrorTimeout = 1460;
        internal delegate uint DialCall(ref RasNativeMethods.RasDialParams parameters, ref IntPtr connection);
        internal delegate uint StatusCall(IntPtr connection, ref RasNativeMethods.RasConnStatus status);

        private readonly object gate = new object();
        private readonly DialCall dial;
        private readonly Func<IntPtr, uint> hangUp;
        private readonly StatusCall getStatus;
        private readonly Func<Stopwatch, bool> waitForCompletion;
        private IntPtr handle;
        private bool hangUpAccepted;
        private bool cleanupRequired;
        private bool disposed;

        internal RasConnectionClient() : this(
            delegate(ref RasNativeMethods.RasDialParams parameters, ref IntPtr connection)
            {
                return RasNativeMethods.RasDial(IntPtr.Zero, null, ref parameters, 0, IntPtr.Zero, ref connection);
            }, RasNativeMethods.RasHangUp, RasNativeMethods.RasGetConnectStatus, WaitForCompletion)
        { }

        internal RasConnectionClient(DialCall dial, Func<IntPtr, uint> hangUp, StatusCall getStatus,
            Func<Stopwatch, bool> waitForCompletion)
        {
            this.dial = dial ?? throw new ArgumentNullException(nameof(dial));
            this.hangUp = hangUp ?? throw new ArgumentNullException(nameof(hangUp));
            this.getStatus = getStatus ?? throw new ArgumentNullException(nameof(getStatus));
            this.waitForCompletion = waitForCompletion ?? throw new ArgumentNullException(nameof(waitForCompletion));
        }

        private static bool WaitForCompletion(Stopwatch elapsed)
        {
            if (elapsed.ElapsedMilliseconds >= 3000)
                return false;
            Thread.Sleep(20);
            return true;
        }

        internal string EntryName { get; set; }

        internal bool HasOutstandingHandle
        {
            get { lock (gate) return handle != IntPtr.Zero; }
        }

        internal bool Connected
        {
            get
            {
                lock (gate)
                {
                    if (handle == IntPtr.Zero || cleanupRequired)
                        return false;
                    var status = NewStatus();
                    uint result = getStatus(handle, ref status);
                    if (result == ErrorInvalidHandle)
                        ClearHandle();
                    // A status-query error is not evidence of disconnection.
                    if (result != 0 && result != ErrorInvalidHandle)
                        throw new Win32Exception((int)result);
                    return result == 0 && status.State == RasConnected;
                }
            }
        }

        private static RasNativeMethods.RasConnStatus NewStatus()
        {
            return new RasNativeMethods.RasConnStatus { Size = Marshal.SizeOf(typeof(RasNativeMethods.RasConnStatus)) };
        }

        internal uint Dial(string user, string password, string domain)
        {
            lock (gate)
            {
                EnsureCanDial();
                var parameters = new RasNativeMethods.RasDialParams
                {
                    Size = Marshal.SizeOf(typeof(RasNativeMethods.RasDialParams)),
                    EntryName = EntryName ?? string.Empty,
                    PhoneNumber = string.Empty,
                    CallbackNumber = string.Empty,
                    UserName = user ?? string.Empty,
                    Password = password ?? string.Empty,
                    Domain = domain ?? string.Empty
                };
                IntPtr candidate = IntPtr.Zero;
                uint error = dial(ref parameters, ref candidate);
                handle = candidate;
                if (error != 0)
                {
                    // Preserve even a failed dial's returned reference until cleanup completes.
                    if (candidate != IntPtr.Zero)
                    {
                        uint cleanupError = HangUpCore();
                        if (cleanupError != 0)
                            throw new Win32Exception((int)cleanupError,
                                "RAS dial failed (" + error + "); cleanup incomplete (" + cleanupError + ").");
                    }
                    return error;
                }
                if (candidate == IntPtr.Zero)
                    throw new InvalidOperationException("Windows RAS returned success without a connection handle.");
                return 0;
            }
        }

        internal uint DialDialog(IntPtr owner)
        {
            lock (gate)
            {
                EnsureCanDial();
                // RasDialDlg returns a Boolean, not a reference owned by this client.
                // Even a unique post-dialog snapshot match may belong to another caller.
                throw new NotSupportedException("Managed RAS requires explicit credentials; use Windows to manage dialog-based connections.");
            }
        }

        private void EnsureCanDial()
        {
            ThrowIfDisposed();
            if (handle != IntPtr.Zero)
                throw new InvalidOperationException("An existing RAS connection must be disconnected before dialing again.");
        }

        internal uint HangUp()
        {
            lock (gate)
                return HangUpCore();
        }

        private uint HangUpCore()
        {
            if (handle == IntPtr.Zero)
                return 0;

            cleanupRequired = true;
            if (!hangUpAccepted)
            {
                uint result = hangUp(handle);
                if (result == ErrorInvalidHandle)
                {
                    ClearHandle();
                    return 0;
                }
                if (result != 0)
                    return result;
                hangUpAccepted = true;
            }

            // A successful RasHangUp releases our reference. Do not release it twice,
            // even if another caller holds a reference and the handle remains valid.
            var elapsed = Stopwatch.StartNew();
            do
            {
                var status = NewStatus();
                uint result = getStatus(handle, ref status);
                if (result == ErrorInvalidHandle)
                {
                    ClearHandle();
                    return 0;
                }
                if (result != 0)
                    return result;
            }
            while (waitForCompletion(elapsed));

            // Retain the pending token and prohibit redial; another caller may still
            // hold the shared connection. A retry polls status, never hangs up twice.
            return ErrorTimeout;
        }

        private void ClearHandle()
        {
            handle = IntPtr.Zero;
            hangUpAccepted = false;
            cleanupRequired = false;
        }

        internal string IPAddress()
        {
            lock (gate)
            {
                if (!Connected)
                    return string.Empty;
                var projection = new RasNativeMethods.RasPppIp { Size = Marshal.SizeOf(typeof(RasNativeMethods.RasPppIp)) };
                int size = projection.Size;
                return RasNativeMethods.RasGetProjectionInfo(handle, RasPppIp, ref projection, ref size) == 0
                    && projection.Error == 0 ? projection.IpAddress : string.Empty;
            }
        }

        internal static IList<RasPhoneBookEntry> GetEntries()
        {
            var result = new List<RasPhoneBookEntry>();
            foreach (var entry in RasNativeMethods.EnumerateEntries())
            {
                var parameters = new RasNativeMethods.RasDialParams
                {
                    Size = Marshal.SizeOf(typeof(RasNativeMethods.RasDialParams)),
                    EntryName = entry.EntryName
                };
                bool hasPassword;
                uint status = RasNativeMethods.RasGetEntryDialParams(
                    string.IsNullOrEmpty(entry.PhonebookPath) ? null : entry.PhonebookPath,
                    ref parameters, out hasPassword);
                result.Add(new RasPhoneBookEntry
                {
                    Name = entry.EntryName,
                    PhoneNumber = status == 0 ? parameters.PhoneNumber : string.Empty,
                    PhonebookPath = entry.PhonebookPath
                });
            }
            return result;
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed && handle == IntPtr.Zero)
                    return;
                // A failed dispose can be retried, but must never permit another dial.
                disposed = true;
                uint result = HangUpCore();
                if (result != 0)
                    throw new Win32Exception((int)result, "RAS cleanup incomplete (" + result + ").");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException("RasConnectionClient");
        }
    }

    internal sealed class RasPhoneBookEntry
    {
        internal string Name { get; set; }
        internal string PhoneNumber { get; set; }
        internal string PhonebookPath { get; set; }
    }

    internal static class RasNativeMethods
    {
        private const int ErrorBufferTooSmall = 603;

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        internal struct RasDialParams
        {
            internal int Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] internal string EntryName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)] internal string PhoneNumber;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)] internal string CallbackNumber;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] internal string UserName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] internal string Password;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] internal string Domain;
            internal uint SubEntry;
            internal IntPtr CallbackId;
            internal uint IfIndex;
            internal IntPtr EncryptedPassword;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct RasDialDialog
        {
            internal int Size;
            internal IntPtr Owner;
            internal uint Flags;
            internal int X;
            internal int Y;
            internal uint SubEntry;
            internal uint Error;
            internal IntPtr Reserved;
            internal IntPtr Reserved2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        internal struct RasConnStatus
        {
            internal int Size;
            internal int State;
            internal uint Error;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)] internal string DeviceType;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)] internal string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)] internal string PhoneNumber;
            internal RasTunnelEndpoint LocalEndpoint;
            internal RasTunnelEndpoint RemoteEndpoint;
            internal int SubState;
        }

        // RASTUNNELENDPOINT is an enum followed by a 16-byte IPv4/IPv6 union.
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct RasTunnelEndpoint
        {
            internal int Type;
            internal uint Address0;
            internal uint Address1;
            internal uint Address2;
            internal uint Address3;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        internal struct RasPppIp
        {
            internal int Size;
            internal uint Error;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] internal string IpAddress;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] internal string ServerIpAddress;
            internal uint Options;
            internal uint ServerOptions;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        internal struct RasEntryName
        {
            internal int Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] internal string EntryName;
            internal uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)] internal string PhonebookPath;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        internal struct RasConn
        {
            internal int Size;
            internal IntPtr Handle;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] internal string EntryName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)] internal string DeviceType;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)] internal string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string PhonebookPath;
            internal uint SubEntry;
            internal Guid EntryGuid;
            internal uint Flags;
            internal Luid Luid;
            internal Guid CorrelationId;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct Luid
        {
            internal uint LowPart;
            internal int HighPart;
        }

        [DllImport("rasapi32.dll", EntryPoint = "RasDialW", CharSet = CharSet.Unicode)]
        internal static extern uint RasDial(IntPtr extensions, string phonebook,
            ref RasDialParams parameters, uint notifierType, IntPtr notifier, ref IntPtr connection);

        [DllImport("rasdlg.dll", EntryPoint = "RasDialDlgW", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RasDialDlg(string phonebook, string entryName,
            string phoneNumber, ref RasDialDialog dialog);

        [DllImport("rasapi32.dll", EntryPoint = "RasHangUpW")]
        internal static extern uint RasHangUp(IntPtr connection);

        [DllImport("rasapi32.dll", EntryPoint = "RasGetConnectStatusW", CharSet = CharSet.Unicode)]
        internal static extern uint RasGetConnectStatus(IntPtr connection, ref RasConnStatus status);

        [DllImport("rasapi32.dll", EntryPoint = "RasGetProjectionInfoW", CharSet = CharSet.Unicode)]
        internal static extern uint RasGetProjectionInfo(IntPtr connection, int projection,
            ref RasPppIp data, ref int size);

        [DllImport("rasapi32.dll", EntryPoint = "RasGetEntryDialParamsW", CharSet = CharSet.Unicode)]
        internal static extern uint RasGetEntryDialParams(string phonebook,
            ref RasDialParams parameters, [MarshalAs(UnmanagedType.Bool)] out bool hasPassword);

        [DllImport("rasapi32.dll", EntryPoint = "RasEnumEntriesW", CharSet = CharSet.Unicode)]
        private static extern uint RasEnumEntries(string reserved, string phonebook,
            IntPtr entries, ref int bytes, out int count);

        [DllImport("rasapi32.dll", EntryPoint = "RasEnumConnectionsW", CharSet = CharSet.Unicode)]
        private static extern uint RasEnumConnections(IntPtr connections, ref int bytes, out int count);

        internal static IList<RasEntryName> EnumerateEntries()
        {
            int bytes = 0, count;
            uint error = RasEnumEntries(null, null, IntPtr.Zero, ref bytes, out count);
            if (error == 0 && count == 0)
                return new List<RasEntryName>();
            if (error != ErrorBufferTooSmall)
                throw new System.ComponentModel.Win32Exception((int)error);
            return ReadArray<RasEntryName>(bytes, delegate(IntPtr buffer, ref int size, out int found)
            {
                return RasEnumEntries(null, null, buffer, ref size, out found);
            });
        }

        internal static IList<RasConn> EnumerateConnections()
        {
            int bytes = 0, count;
            uint error = RasEnumConnections(IntPtr.Zero, ref bytes, out count);
            if (error == 0 && count == 0)
                return new List<RasConn>();
            if (error != ErrorBufferTooSmall)
                throw new System.ComponentModel.Win32Exception((int)error);
            return ReadArray<RasConn>(bytes, delegate(IntPtr buffer, ref int size, out int found)
            {
                return RasEnumConnections(buffer, ref size, out found);
            });
        }

        private delegate uint ReadArrayCall(IntPtr buffer, ref int size, out int count);

        private static IList<T> ReadArray<T>(int bytes, ReadArrayCall call) where T : struct
        {
            int stride = Marshal.SizeOf(typeof(T));
            // The initial count can change between calls, so resize on demand.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (bytes < stride)
                    bytes = stride;
                IntPtr buffer = Marshal.AllocHGlobal(bytes);
                try
                {
                    for (int offset = 0; offset + sizeof(int) <= bytes; offset += stride)
                        Marshal.WriteInt32(buffer, offset, stride);
                    int count, available = bytes;
                    uint error = call(buffer, ref available, out count);
                    if (error == ErrorBufferTooSmall)
                    {
                        bytes = Math.Max(available, bytes * 2);
                        continue;
                    }
                    if (error != 0)
                        throw new System.ComponentModel.Win32Exception((int)error);
                    if (count < 0 || (long)count * stride > bytes)
                        throw new InvalidOperationException("RAS returned an invalid enumeration count.");
                    var result = new List<T>(count);
                    for (int i = 0; i < count; i++)
                        result.Add((T)Marshal.PtrToStructure(IntPtr.Add(buffer, i * stride), typeof(T)));
                    return result;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            throw new InvalidOperationException("RAS enumeration buffer changed repeatedly.");
        }
    }
}
