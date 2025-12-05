using System;

namespace IpChanger.Common;

public class IpConfigRequest
{
    public string AdapterId { get; set; } = string.Empty;
    public bool UseDhcp { get; set; } = false;
    public string IpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = "255.255.255.0";
    public string Gateway { get; set; } = string.Empty;
    public string Dns { get; set; } = string.Empty;
}

public class IpConfigResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
