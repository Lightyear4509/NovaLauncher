using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NovaLauncher.Application.SaveSync;

namespace NovaLauncher.Infrastructure.SaveSync;

public sealed class WindowsCredentialPairingSecretStore : IPairingSecretStore, IPeerCredentialStore
{
    private const string Target = "NovaLauncher/SaveSyncPairingSecret";
    private const string PeerTargetPrefix = "NovaLauncher/SaveSyncPeer/";
    public bool HasSecret => ReadSecret(Target) is { Length: 32 };

    public byte[]? GetSecret() => ReadSecret(Target);

    public bool ContainsSecret(Guid peerDeviceId) => GetSecret(peerDeviceId) is { Length: 32 };

    public byte[]? GetSecret(Guid peerDeviceId) => ReadSecret(GetTarget(peerDeviceId));

    private static byte[]? ReadSecret(string target)
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (!CredRead(target, 1, 0, out var pointer)) return null;
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            if (credential.CredentialBlobSize != 32 || credential.CredentialBlob == IntPtr.Zero) return null;
            var value = new byte[32];
            Marshal.Copy(credential.CredentialBlob, value, 0, value.Length);
            return value;
        }
        finally { CredFree(pointer); }
    }

    public void SetSecret(ReadOnlySpan<byte> secret) => WriteSecret(Target, secret);

    public void SetSecret(Guid peerDeviceId, ReadOnlySpan<byte> secret) => WriteSecret(GetTarget(peerDeviceId), secret);

    private static void WriteSecret(string target, ReadOnlySpan<byte> secret)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows Credential Manager is required.");
        if (secret.Length != 32) throw new ArgumentException("A 256-bit pairing secret is required.", nameof(secret));
        var bytes = secret.ToArray();
        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Credential
            {
                Type = 1,
                TargetName = target,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = 2,
                UserName = "NovaLauncher",
            };
            if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "The pairing secret could not be stored in Windows Credential Manager.");
        }
        finally { Marshal.FreeCoTaskMem(blob); CryptographicOperations.ZeroMemory(bytes); }
    }

    public void Clear() => DeleteSecret(Target);

    public void Clear(Guid peerDeviceId) => DeleteSecret(GetTarget(peerDeviceId));

    public string GetCredentialReference(Guid peerDeviceId) => GetTarget(peerDeviceId);

    private static void DeleteSecret(string target)
    {
        if (OperatingSystem.IsWindows() && !CredDelete(target, 1, 0) && Marshal.GetLastWin32Error() != 1168)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static string GetTarget(Guid peerDeviceId)
    {
        if (peerDeviceId == Guid.Empty) throw new ArgumentException("A peer device ID is required.", nameof(peerDeviceId));
        return PeerTargetPrefix + peerDeviceId.ToString("N", System.Globalization.CultureInfo.InvariantCulture);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr credential);
}
