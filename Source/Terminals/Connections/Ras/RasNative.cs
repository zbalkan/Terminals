using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Terminals.Connections.RasNative
{
    // Windows RAS calls are isolated here; no native handles escape to the UI.
    internal sealed class RasConnectionClient : IDisposable
    {
        private const int RasConnected = 0x2000;
        private const int RasPppIp = 0x8021;
        private IntPtr handle;
        private bool disposed;

        internal string EntryName { get; set; }

        internal bool Connected
        {
            get
            {
                if (handle == IntPtr.Zero)
                    return false;
                var status = new RasNativeMethods.RasConnStatus { Size = Marshal.SizeOf(typeof(RasNativeMethods.RasConnStatus)) };
                return RasNativeMethods.RasGetConnectStatus(handle, ref status) == 0
                    && status.State == RasConnected;
            }
        }

        internal uint Dial(string user, string password, string domain)
        {
            ThrowIfDisposed();
            if (handle != IntPtr.Zero)
                throw new InvalidOperationException("An existing RAS connection must be disconnected before dialing again.");

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
            uint error = RasNativeMethods.RasDial(IntPtr.Zero, null, ref parameters, 0, IntPtr.Zero, ref candidate);
            // Even a failed RasDial can return a connection handle that must be hung up.
            if (error != 0)
            {
                if (candidate != IntPtr.Zero)
                    RasNativeMethods.RasHangUp(candidate);
                return error;
            }
            if (candidate == IntPtr.Zero)
                throw new InvalidOperationException("Windows RAS returned success without a connection handle.");
            handle = candidate;
            return 0;
        }

        internal uint DialDialog(IntPtr owner)
        {
            ThrowIfDisposed();
            if (handle != IntPtr.Zero)
                throw new InvalidOperationException("An existing RAS connection must be disconnected before dialing again.");

            // The dialog provides no HRASCONN. Capture existing handles to avoid taking
            // ownership of a connection started by another process.
            var before = new HashSet<IntPtr>();
            foreach (var conn in RasNativeMethods.EnumerateConnections())
                before.Add(conn.Handle);

            var dialog = new RasNativeMethods.RasDialDialog
            {
                Size = Marshal.SizeOf(typeof(RasNativeMethods.RasDialDialog)),
                Owner = owner
            };
            bool success = RasNativeMethods.RasDialDlg(null, EntryName, null, ref dialog);
            if (!success)
                return dialog.Error != 0 ? dialog.Error : 1223; // user cancelled

            foreach (var conn in RasNativeMethods.EnumerateConnections())
            {
                if (!before.Contains(conn.Handle) && string.Equals(conn.EntryName, EntryName, StringComparison.OrdinalIgnoreCase))
                {
                    handle = conn.Handle;
                    return 0;
                }
            }
            // Never claim ownership of an unrelated or pre-existing connection.
            throw new InvalidOperationException("The RAS dialog succeeded, but the new connection handle could not be identified.");
        }

        internal uint HangUp()
        {
            IntPtr old = handle;
            handle = IntPtr.Zero;
            return old == IntPtr.Zero ? 0 : RasNativeMethods.RasHangUp(old);
        }

        internal string IPAddress()
        {
            if (!Connected)
                return string.Empty;
            var projection = new RasNativeMethods.RasPppIp { Size = Marshal.SizeOf(typeof(RasNativeMethods.RasPppIp)) };
            int size = projection.Size;
            return RasNativeMethods.RasGetProjectionInfo(handle, RasPppIp, ref projection, ref size) == 0
                && projection.Error == 0 ? projection.IpAddress : string.Empty;
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
            if (disposed)
                return;
            disposed = true;
            HangUp();
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

        [StructLayout(LayoutKind.Sequential)]
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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
        [StructLayout(LayoutKind.Sequential)]
        internal struct RasTunnelEndpoint
        {
            internal int Type;
            internal uint Address0;
            internal uint Address1;
            internal uint Address2;
            internal uint Address3;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct RasPppIp
        {
            internal int Size;
            internal uint Error;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] internal string IpAddress;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] internal string ServerIpAddress;
            internal uint Options;
            internal uint ServerOptions;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct RasEntryName
        {
            internal int Size;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] internal string EntryName;
            internal uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)] internal string PhonebookPath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

        [StructLayout(LayoutKind.Sequential)]
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
