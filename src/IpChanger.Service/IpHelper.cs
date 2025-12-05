using IpChanger.Common;
using System.Management;

namespace IpChanger.Service;

public static class IpHelper
{
    public static IpConfigResponse ApplyConfig(IpConfigRequest request)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                if (obj["SettingID"]?.ToString() == request.AdapterId)
                {
                    if (request.UseDhcp)
                    {
                        obj.InvokeMethod("EnableDHCP", null);
                        obj.InvokeMethod("SetDNSServerSearchOrder", null); // Clear DNS
                        return new IpConfigResponse { Success = true, Message = "DHCP Enabled" };
                    }
                    else
                    {
                        // Set IP and Subnet
                        var newIP = obj.GetMethodParameters("EnableStatic");
                        newIP["IPAddress"] = new[] { request.IpAddress };
                        newIP["SubnetMask"] = new[] { request.SubnetMask };
                        var resIp = obj.InvokeMethod("EnableStatic", newIP, null);
                        
                        // Set Gateway
                        if (!string.IsNullOrWhiteSpace(request.Gateway))
                        {
                            var newGateway = obj.GetMethodParameters("SetGateways");
                            newGateway["DefaultIPGateway"] = new[] { request.Gateway };
                            newGateway["GatewayCostMetric"] = new[] { 1 };
                            obj.InvokeMethod("SetGateways", newGateway, null);
                        }

                        // Set DNS
                        if (!string.IsNullOrWhiteSpace(request.Dns))
                        {
                            var newDns = obj.GetMethodParameters("SetDNSServerSearchOrder");
                            newDns["DNSServerSearchOrder"] = request.Dns.Split(',', StringSplitOptions.RemoveEmptyEntries);
                            obj.InvokeMethod("SetDNSServerSearchOrder", newDns, null);
                        }

                        return new IpConfigResponse { Success = true, Message = "Static IP configured successfully." };
                    }
                }
            }
            return new IpConfigResponse { Success = false, Message = "Adapter not found." };
        }
        catch (Exception ex)
        {
            return new IpConfigResponse { Success = false, Message = ex.Message };
        }
    }
}
