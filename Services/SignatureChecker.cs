using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace SetupCrate.Services;

/// <summary>Проверка Authenticode-подписи файла (как в свойствах файла → Цифровая подпись).</summary>
public static class SignatureChecker
{
    private static readonly Guid WinTrustAction = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private const uint WTD_UI_NONE = 2;
    private const uint WTD_REVOKE_NONE = 0;
    private const uint WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_VERIFY = 1;
    private const uint WTD_STATEACTION_CLOSE = 2;
    private const uint WTD_CACHE_ONLY_URL_RETRIEVAL = 0x1000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)] public string FilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, ref WINTRUST_DATA pWVTData);

    /// <summary>Возвращает текст статуса: ✓/⚠ + подпись, либо причину.</summary>
    public static string Check(string path)
    {
        if (!File.Exists(path)) return "✗ файл не найден";

        try
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
                FilePath = path
            };

            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>()),
                dwStateAction = WTD_STATEACTION_VERIFY,
                dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL
            };

            Marshal.StructureToPtr(fileInfo, data.pFile, false);

            try
            {
                var action = WinTrustAction;
                var result = WinVerifyTrust(IntPtr.Zero, ref action, ref data);

                data.dwStateAction = WTD_STATEACTION_CLOSE;
                var action2 = WinTrustAction;
                WinVerifyTrust(IntPtr.Zero, ref action2, ref data);

                if (result == 0)
                    return "✓ подписано: " + TryGetSigner(path);

                return "⚠ " + Describe(result);
            }
            finally
            {
                Marshal.FreeHGlobal(data.pFile);
            }
        }
        catch (Exception ex)
        {
            return "⚠ проверка подписи не удалась: " + ex.Message;
        }
    }

    private static string TryGetSigner(string path)
    {
        try
        {
#pragma warning disable SYSLIB0057
            using var cert = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            var match = Regex.Match(cert.Subject, @"CN=([^,]+)");
            return match.Success ? match.Groups[1].Value.Trim() : cert.Subject;
        }
        catch
        {
            return "(подпись действительна)";
        }
    }

    private static string Describe(uint code) => code switch
    {
        0x800B0100 => "нет встроенной подписи (для установщиков это повод насторожиться; у системных файлов Windows подпись каталожная)",
        0x800B0003 => "подпись не распознана",
        0x800B0001 => "провайдер подписи не найден",
        0x800B0109 => "подпись есть, но корневой сертификат не доверенный",
        0x800B0101 => "сертификат подписи истёк",
        _ => $"подпись недействительна (0x{code:X8})"
    };
}
