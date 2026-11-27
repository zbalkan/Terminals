using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Terminals.Connections.RasNative
{
    internal sealed class RasPhoneBookEntry
    {
        internal string Name { get; set; }
        internal string PhoneNumber { get; set; }
        internal string PhonebookPath { get; set; }
    }

    internal static class RasNativeMethods
    {
        private const int ErrorBufferTooSmall = 603;

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
