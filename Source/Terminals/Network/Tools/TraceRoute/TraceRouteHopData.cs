using System;
using System.Net;
using System.Net.NetworkInformation;

namespace Terminals.Network
{
    internal class TraceRouteHopData
    {
        public byte Count { get; set; }
        public IPAddress Address { get; set; }
        public long RoundTripTime { get; set; }
        public IPStatus Status { get; set; }
        public string HostName { get; set; }

        public TraceRouteHopData(byte hopCount, IPAddress address, long roundTripTime, IPStatus status)
        {
            Count = hopCount;
            Address = address;
            RoundTripTime = roundTripTime;
            Status = status;
            HostName = string.Empty;
        }
    }
}
