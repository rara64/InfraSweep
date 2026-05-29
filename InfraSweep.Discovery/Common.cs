using System.Net;

namespace InfraSweep.Discovery;

public class IpRange
{
    public required IPAddress FirstAddress {get; init;}
    public required IPAddress LastAddress {get; init;}

    public IEnumerable<IPAddress> GetEnumerable()
    {
        int first = IPAddress.NetworkToHostOrder
            (BitConverter.ToInt32(FirstAddress.GetAddressBytes(), 0));

        int last = IPAddress.NetworkToHostOrder
            (BitConverter.ToInt32(LastAddress.GetAddressBytes(), 0));

        if (last < first)
            throw new ArgumentException();

        for (int i = first; i <= last; i++)
        {
            yield return new IPAddress
                (BitConverter.GetBytes(IPAddress.HostToNetworkOrder(i)));
        }
    }
};