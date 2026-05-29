using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text;
using static System.Environment;
using System.Linq;
using System.Security.Cryptography;
using Tmds.DBus.Protocol;

namespace InfraSweep.App;

public class StorageService
{
    private static string StorageDir => Path.Combine(
        Environment.GetFolderPath(SpecialFolder.LocalApplicationData),
        "InfraSweep");

    private static string FilePath => Path.Combine(StorageDir, "last_scan");

    private static byte[] GetKey() => SHA256.HashData(
        Encoding.UTF8.GetBytes($"{Environment.MachineName}{Environment.UserName}"));

    public static void SaveScanResult(ScanResult result)
    {
        Directory.CreateDirectory(StorageDir);

        if (File.Exists(FilePath))
            File.WriteAllText(FilePath, string.Empty);

        byte[] data = JsonSerializer.SerializeToUtf8Bytes(result);

        using FileStream fileStream = new(FilePath, FileMode.OpenOrCreate);

        using Aes aes = Aes.Create();

        aes.Key = GetKey();
        fileStream.Write(aes.IV, 0, aes.IV.Length);

        using CryptoStream cryptoStream = new (fileStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
        cryptoStream.Write(data);
    }
    public static ScanResult? LoadScanResult()
    {
        if (!File.Exists(FilePath))
            return null;
        
        try
        {
            using FileStream fileStream = new(FilePath, FileMode.Open);
            using Aes aes = Aes.Create();
            aes.Key = GetKey();

            byte[] iv = new byte[16];
            fileStream.Read(iv, 0, 16);

            aes.IV = iv;

            using CryptoStream cryptoStream = new (fileStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            return JsonSerializer.Deserialize<ScanResult>(cryptoStream);
        }
        catch
        {
            return null;
        }
    }
}