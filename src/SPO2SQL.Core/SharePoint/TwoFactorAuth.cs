#nullable disable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using SPO2SQL.XmlConfig;

namespace SPO2SQL.SharePoint;

public static class TwoFactorAuth
{
    private const string APP_NAME = "SPO2SQL";
    private const int TIME_STEP = 30;
    private const int CODE_DIGITS = 6;
    private const int MOD_VALUE = 1000000;

    private static readonly int[] Base32Lookup = new int[128];

    static TwoFactorAuth()
    {
        for (int i = 0; i < 128; i++)
        {
            Base32Lookup[i] = -1;
        }

        string alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        for (int i = 0; i < alpha.Length; i++)
        {
            Base32Lookup[alpha[i]] = i;
        }
    }

    public static bool IsEnabled => ConfigurationReader.IsTwoFactorEnabled();

    public static bool PerformVerification()
    {
        if (!IsEnabled)
        {
            return true;
        }

        string secret = ConfigurationReader.GetTwoFactorSecret();

        if (string.IsNullOrEmpty(secret))
        {
            return RunSetup();
        }

        return RunVerification(secret);
    }

    private static bool RunSetup()
    {
        string secret = GenerateSecret();

        Console.WriteLine();
        Console.WriteLine("=== Two-Factor Authentication Setup ===");
        Console.WriteLine();
        Console.WriteLine("  App:     " + APP_NAME);
        Console.WriteLine();
        Console.WriteLine("IMPORTANT: Save this secret key securely. It will not be shown again.");
        Console.WriteLine("  Secret:  " + FormatSecret(secret));
        Console.Write("Enter the 6-digit code from your authenticator app: ");
        string input = Console.ReadLine();
        string code = input?.Trim();

        if (!string.IsNullOrEmpty(code) && ValidateCode(secret, code))
        {
            Console.WriteLine("Verification successful!");
            Console.WriteLine();
            Console.WriteLine("Add the following to your UserConfig.xml inside <Security> section:");
            Console.WriteLine("  <TwoFactorSecret>" + secret + "</TwoFactorSecret>");
            Console.WriteLine("2FA is now active.");
            return true;
        }

        Console.WriteLine("Invalid code. Setup aborted.");
        return false;
    }

    private static bool RunVerification(string secret)
    {
        for (int attempts = 0; attempts < 3; attempts++)
        {
            Console.Write("Enter 2FA code: ");
            string input = Console.ReadLine();
            string code = input?.Trim();

            if (!string.IsNullOrEmpty(code) && ValidateCode(secret, code))
            {
                return true;
            }

            if (attempts < 2)
            {
                Console.WriteLine("Invalid code. Try again.");
            }
        }

        Console.WriteLine("Too many invalid attempts.");
        return false;
    }

    public static string GenerateCode(string secretBase32)
    {
        byte[] secret = Base32Decode(secretBase32);
        long counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TIME_STEP;
        return ComputeTotp(secret, counter);
    }

    public static bool ValidateCode(string secretBase32, string code)
    {
        byte[] secret = Base32Decode(secretBase32);
        long counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TIME_STEP;

        for (int i = -1; i <= 1; i++)
        {
            if (ComputeTotp(secret, counter + i) == code)
            {
                return true;
            }
        }

        return false;
    }

    private static string ComputeTotp(byte[] secret, long counter)
    {
        byte[] counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        using var hmac = new HMACSHA256(secret);
        byte[] hash = hmac.ComputeHash(counterBytes);

        int offset = hash[^1] & 0xf;
        int binary = ((hash[offset] & 0x7f) << 24)
                   | ((hash[offset + 1] & 0xff) << 16)
                   | ((hash[offset + 2] & 0xff) << 8)
                   | (hash[offset + 3] & 0xff);

        int otp = binary % MOD_VALUE;
        return otp.ToString().PadLeft(CODE_DIGITS, '0');
    }

    public static string GenerateSecret()
    {
        byte[] random = new byte[20];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(random);
        return Base32Encode(random);
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();

        int buffer = 0;
        int bitsInBuffer = 0;

        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsInBuffer += 8;

            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                int index = (buffer >> bitsInBuffer) & 0x1f;
                result.Append(alphabet[index]);
            }
        }

        if (bitsInBuffer > 0)
        {
            buffer <<= (5 - bitsInBuffer);
            result.Append(alphabet[buffer & 0x1f]);
        }

        return result.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        if (input == null)
        {
            return Array.Empty<byte>();
        }

        input = input.Trim().ToUpperInvariant();
        var bytes = new List<byte>();

        int bitBuffer = 0;
        int bitsInBuffer = 0;

        foreach (char c in input)
        {
            if (c >= 128)
            {
                continue;
            }

            int value = Base32Lookup[c];
            if (value < 0)
            {
                continue;
            }

            bitBuffer = (bitBuffer << 5) | value;
            bitsInBuffer += 5;

            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                bytes.Add((byte)((bitBuffer >> bitsInBuffer) & 0xff));
            }
        }

        return bytes.ToArray();
    }

    private static string FormatSecret(string secret)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < secret.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                sb.Append(' ');
            }

            sb.Append(secret[i]);
        }
        return sb.ToString();
    }
}
