// 文件作用：注册加密摘要分组的工具元数据。
using 小工具集合.Models;

namespace 小工具集合.Services;

public static partial class ToolCatalog
{
    private static ToolDefinition Hash(string id, string name, string description, string warning = "") => new()
    {
        Id = id,
        Name = name,
        GroupName = "加密摘要",
        Description = description,
        Warning = warning,
        Operations = [new() { Id = "hash", Name = "计算摘要" }]
    };

    private static ToolDefinition Hmac() => new()
    {
        Id = "hmacSha256",
        Name = "HMAC-SHA256",
        GroupName = "加密摘要",
        Description = "使用文本密钥计算 HMAC-SHA256。",
        Operations = [new() { Id = "sign", Name = "计算 HMAC" }],
        Parameters = [new() { Id = "key", Name = "密钥", Kind = ToolParameterKind.Password }]
    };

    private static ToolDefinition Aes() => new()
    {
        Id = "aesGcm",
        Name = "AES-GCM",
        GroupName = "加密摘要",
        Description = "使用密码加密/解密文本，输出带 salt、nonce 和 tag 的 Base64 密文。",
        Operations = [new() { Id = "encrypt", Name = "加密" }, new() { Id = "decrypt", Name = "解密" }],
        Parameters = [new() { Id = "password", Name = "密码", Kind = ToolParameterKind.Password }]
    };

    private static ToolDefinition EncodeCrypto() => new()
    {
        Id = "encodeCrypto",
        Name = "Encode",
        GroupName = "加密摘要",
        Description = "Encode 文本加解密，使用密码生成带认证信息的 Base64 密文。",
        Operations = [new() { Id = "encrypt", Name = "加密" }, new() { Id = "decrypt", Name = "解密" }],
        Parameters = [new() { Id = "password", Name = "密码", Kind = ToolParameterKind.Password }]
    };

    private static ToolDefinition Rsa() => new()
    {
        Id = "rsaOaep",
        Name = "RSA-OAEP",
        GroupName = "加密摘要",
        Description = "使用 PEM 公钥加密、PEM 私钥解密，填充为 OAEP + SHA-256。",
        Operations = [new() { Id = "encrypt", Name = "公钥加密" }, new() { Id = "decrypt", Name = "私钥解密" }],
        Parameters = [new() { Id = "key", Name = "PEM 密钥", Kind = ToolParameterKind.Multiline }]
    };

}
