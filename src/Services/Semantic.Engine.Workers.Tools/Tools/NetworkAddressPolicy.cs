using System.Net;
using System.Net.Sockets;

namespace Semantic.Engine.Workers.Tools.Tools;

/// <summary>
/// Decides whether an IP address is a public internet address that the agent may contact.
/// </summary>
public static class NetworkAddressPolicy
{
    /// <summary>
    /// Checks that an address is not loopback, private, link-local or otherwise internal.
    /// </summary>
    /// <param name="address">The resolved target address.</param>
    /// <returns><see langword="true"/> when the address is publicly routable.</returns>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !address.IsIPv6LinkLocal && !address.IsIPv6SiteLocal && !address.IsIPv6UniqueLocal;
        }

        byte[] bytes = address.GetAddressBytes();
        return bytes[0] switch
        {
            10 => false,
            127 => false,
            169 when bytes[1] == 254 => false,
            172 when bytes[1] is >= 16 and <= 31 => false,
            192 when bytes[1] == 168 => false,
            100 when bytes[1] is >= 64 and <= 127 => false,
            0 => false,
            >= 224 => false,
            _ => true
        };
    }
}
