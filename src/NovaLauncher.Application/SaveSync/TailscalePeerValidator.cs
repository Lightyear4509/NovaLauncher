using System.Net;

namespace NovaLauncher.Application.SaveSync;

public static class TailscalePeerValidator
{
    public static bool TryNormalize(string? value, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;
        if (!IPAddress.TryParse(value?.Trim(), out var address))
        {
            error = "Enter a valid Tailscale IPv4 or IPv6 address.";
            return false;
        }

        var bytes = address.GetAddressBytes();
        var isTailscaleV4 = bytes.Length == 4 && bytes[0] == 100 && (bytes[1] & 0xC0) == 0x40;
        var isTailscaleV6 = bytes.Length == 16 && bytes[0] == 0xFD && bytes[1] == 0x7A &&
                            bytes[2] == 0x11 && bytes[3] == 0x5C && bytes[4] == 0xA1 && bytes[5] == 0xE0;
        if (!isTailscaleV4 && !isTailscaleV6)
        {
            error = "The address is outside Tailscale's 100.64.0.0/10 and fd7a:115c:a1e0::/48 ranges.";
            return false;
        }

        normalized = address.ToString();
        return true;
    }
}
