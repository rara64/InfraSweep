using System;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using InfraSweep.App.Models;
using InfraSweep.App.Services.Interfaces;

namespace InfraSweep.App;

public class StorageService : IStorageService
{
    private static string StorageDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InfraSweep");
    private static string FilePath => Path.Combine(StorageDir, "last_scan");
    private static string SettingsFilePath => Path.Combine(StorageDir, "settings.json");
    private static byte[] GetKey() => SHA256.HashData(
        Encoding.UTF8.GetBytes($"{Environment.MachineName}{Environment.UserName}"));

    public void SaveScanResult(ScanResult result)
    {
        Directory.CreateDirectory(StorageDir);

        if (File.Exists(FilePath))
            File.WriteAllText(FilePath, string.Empty);

        byte[] data = JsonSerializer.SerializeToUtf8Bytes(result);

        using FileStream fileStream = new(FilePath, FileMode.Create);

        using Aes aes = Aes.Create();

        aes.Key = GetKey();
        fileStream.Write(aes.IV, 0, aes.IV.Length);

        using CryptoStream cryptoStream = new (fileStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
        cryptoStream.Write(data);
    }
    public ScanResult? LoadScanResult()
    {
        if (!File.Exists(FilePath))
            return null;
        
        try
        {
            using FileStream fileStream = new(FilePath, FileMode.Open);
            using Aes aes = Aes.Create();
            aes.Key = GetKey();

            byte[] iv = new byte[16];
            int bytesRead = fileStream.Read(iv, 0, 16);

            if (bytesRead != 16)
                return null;

            aes.IV = iv;

            using CryptoStream cryptoStream = new (fileStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            return JsonSerializer.Deserialize<ScanResult>(cryptoStream);
        }
        catch
        {
            return null;
        }
    }

    public void SaveAppSettings(AppSettings settings)
    {
        Directory.CreateDirectory(StorageDir);
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings));
    }

    public AppSettings? LoadAppSettings()
    {
        if (!File.Exists(SettingsFilePath))
            return null;

        try
        {
            string raw = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(raw);
        }
        catch
        {
            return null;
        }
    }
}