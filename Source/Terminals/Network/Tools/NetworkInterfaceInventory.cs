using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Terminals.Network
{
    internal sealed class IPv4InterfaceDetails
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public IPAddress Address { get; set; }
        public IPAddress SubnetMask { get; set; }
        public OperationalStatus Status { get; set; }
        public NetworkInterfaceType Type { get; set; }
    }

    internal static class NetworkInterfaceInventory
    {
        internal static List<IPv4InterfaceDetails> GetIPv4Interfaces()
        {
            var interfaces = new List<IPv4InterfaceDetails>();
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                var properties = adapter.GetIPProperties();
                foreach (UnicastIPAddressInformation address in properties.UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    interfaces.Add(new IPv4InterfaceDetails
                    {
                        Name = adapter.Name,
                        Description = adapter.Description,
                        Address = address.Address,
                        SubnetMask = address.IPv4Mask,
                        Status = adapter.OperationalStatus,
                        Type = adapter.NetworkInterfaceType
                    });
                }
            }
            return interfaces;
        }

        internal static IPAddress GetFirstActiveIPv4()
        {
            foreach (IPv4InterfaceDetails adapter in GetIPv4Interfaces())
            {
                if (adapter.Status == OperationalStatus.Up &&
                    adapter.Type != NetworkInterfaceType.Loopback)
                    return adapter.Address;
            }
            return null;
        }
    }
}
