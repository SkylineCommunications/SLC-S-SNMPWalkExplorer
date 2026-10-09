using System;
using System.Net;
using System.Net.Sockets;

namespace SLCSSNMPWalkCollector
{
    internal sealed class WalkConnectionConfiguration
    {
        private WalkConnectionConfiguration(IPAddress address, int port, string community)
        {
            Address = address;
            Port = port;
            Community = community;
        }

        public IPAddress Address { get; private set; }

        public string Community { get; private set; }

        public int Port { get; private set; }

        public static WalkConnectionConfiguration Create(string targetAddress, int targetPort, string community)
        {
            IPAddress address;
            if (!IPAddress.TryParse(targetAddress, out address) || address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("TargetAddress must be a valid IPv4 address.", "targetAddress");
            }

            if (String.IsNullOrWhiteSpace(community))
            {
                throw new ArgumentException("SNMP community is required.", "community");
            }

            if (targetPort < 1 || targetPort > 65535)
            {
                throw new ArgumentOutOfRangeException("targetPort", "TargetPort must be between 1 and 65535.");
            }

            return new WalkConnectionConfiguration(address, targetPort, community);
        }
    }
}