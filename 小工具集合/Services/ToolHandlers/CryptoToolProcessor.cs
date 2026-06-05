// 文件作用：实现哈希、HMAC、AES、RSA 和 Encode 加密类工具。
using System.Security.Cryptography;
using System.Text;
using 小工具集合.Models;

namespace 小工具集合.Services;

public sealed partial class ToolProcessor
{
    private static string Hash(string input, Func<byte[], byte[]> hashData)
    {
        return Convert.ToHexString(hashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static string HmacSha256(ToolRequest request)
    {
        string key = GetRequiredParameter(request, "key", "请输入 HMAC 密钥。");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(request.Input))).ToLowerInvariant();
    }

    private static string EncryptAes(ToolRequest request, string packagePrefix = AesPackagePrefix)
    {
        string password = GetRequiredParameter(request, "password", "请输入密码。");
        byte[] salt = RandomNumberGenerator.GetBytes(AesSaltSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(AesNonceSize);
        byte[] key = DeriveAesKey(password, salt);
        byte[] plainBytes = Encoding.UTF8.GetBytes(request.Input);
        byte[] cipherBytes = new byte[plainBytes.Length];
        byte[] tag = new byte[AesTagSize];

        try
        {
            // 随机 salt 用于 PBKDF2 派生密钥，nonce 保证每次加密唯一。
            // 二者都不是秘密，因此会和密文一起存储。
            using var aes = new AesGcm(key, AesTagSize);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
            byte[] package = new byte[packagePrefix.Length + salt.Length + nonce.Length + tag.Length + cipherBytes.Length];
            Encoding.ASCII.GetBytes(packagePrefix).CopyTo(package, 0);
            salt.CopyTo(package, packagePrefix.Length);
            nonce.CopyTo(package, packagePrefix.Length + salt.Length);
            tag.CopyTo(package, packagePrefix.Length + salt.Length + nonce.Length);
            cipherBytes.CopyTo(package, packagePrefix.Length + salt.Length + nonce.Length + tag.Length);
            return Convert.ToBase64String(package);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static string DecryptAes(ToolRequest request, string packagePrefix = AesPackagePrefix)
    {
        string password = GetRequiredParameter(request, "password", "请输入密码。");
        byte[] package = Convert.FromBase64String(request.Input.Trim());
        int headerSize = packagePrefix.Length + AesSaltSize + AesNonceSize + AesTagSize;
        if (package.Length <= headerSize || Encoding.ASCII.GetString(package, 0, packagePrefix.Length) != packagePrefix)
        {
            throw new FormatException("密文格式不正确。请使用本工具生成的密文。");
        }

        // 固定头部长度与 EncryptAes/EncryptBytes 保持一致，便于直接切片读取。
        byte[] salt = package[packagePrefix.Length..(packagePrefix.Length + AesSaltSize)];
        byte[] nonce = package[(packagePrefix.Length + AesSaltSize)..(packagePrefix.Length + AesSaltSize + AesNonceSize)];
        byte[] tag = package[(packagePrefix.Length + AesSaltSize + AesNonceSize)..headerSize];
        byte[] cipherBytes = package[headerSize..];
        byte[] plainBytes = new byte[cipherBytes.Length];
        byte[] key = DeriveAesKey(password, salt);

        try
        {
            using var aes = new AesGcm(key, AesTagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("AES 解密失败：密码错误或密文已损坏。", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DeriveAesKey(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, AesKeySize);
    }

    private static byte[] EncryptBytes(byte[] plainBytes, string password, string packagePrefix)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(AesSaltSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(AesNonceSize);
        byte[] key = DeriveAesKey(password, salt);
        byte[] cipherBytes = new byte[plainBytes.Length];
        byte[] tag = new byte[AesTagSize];

        try
        {
            using var aes = new AesGcm(key, AesTagSize);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
            byte[] package = new byte[packagePrefix.Length + salt.Length + nonce.Length + tag.Length + cipherBytes.Length];
            Encoding.ASCII.GetBytes(packagePrefix).CopyTo(package, 0);
            salt.CopyTo(package, packagePrefix.Length);
            nonce.CopyTo(package, packagePrefix.Length + salt.Length);
            tag.CopyTo(package, packagePrefix.Length + salt.Length + nonce.Length);
            cipherBytes.CopyTo(package, packagePrefix.Length + salt.Length + nonce.Length + tag.Length);
            return package;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DecryptBytes(byte[] package, string password, string packagePrefix)
    {
        int headerSize = packagePrefix.Length + AesSaltSize + AesNonceSize + AesTagSize;
        if (package.Length <= headerSize || Encoding.ASCII.GetString(package, 0, packagePrefix.Length) != packagePrefix)
        {
            throw new FormatException("文件密文格式不正确。请使用本工具生成的 Encode 文件密文。");
        }

        byte[] salt = package[packagePrefix.Length..(packagePrefix.Length + AesSaltSize)];
        byte[] nonce = package[(packagePrefix.Length + AesSaltSize)..(packagePrefix.Length + AesSaltSize + AesNonceSize)];
        byte[] tag = package[(packagePrefix.Length + AesSaltSize + AesNonceSize)..headerSize];
        byte[] cipherBytes = package[headerSize..];
        byte[] plainBytes = new byte[cipherBytes.Length];
        byte[] key = DeriveAesKey(password, salt);

        try
        {
            using var aes = new AesGcm(key, AesTagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            return plainBytes;
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("文件解密失败：密码错误或文件已损坏。", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static string EncryptRsa(ToolRequest request)
    {
        string pem = GetRequiredParameter(request, "key", "请输入 PEM 公钥。");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(request.Input), RSAEncryptionPadding.OaepSHA256));
    }

    private static string DecryptRsa(ToolRequest request)
    {
        string pem = GetRequiredParameter(request, "key", "请输入 PEM 私钥。");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return Encoding.UTF8.GetString(rsa.Decrypt(Convert.FromBase64String(request.Input.Trim()), RSAEncryptionPadding.OaepSHA256));
    }
}
