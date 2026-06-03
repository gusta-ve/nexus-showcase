using Nexus.Infrastructure.Services;
using Xunit;

namespace Nexus.Tests;

/// <summary>
/// Cofre de senhas: AES-256-GCM autenticado. Estes testes provam round-trip,
/// não-determinismo (nonce aleatório por operação) e detecção de adulteração /
/// chave errada — as garantias que importam num cofre.
/// </summary>
public class EncryptionServiceTests
{
    private const string Key = "k9Wm2Qx7Lp4Zt8Rb1Yn6Vd3Hf5Jc0Ng";
    private readonly EncryptionService _sut = new(Key);

    [Theory]
    [InlineData("hunter2")]
    [InlineData("")]
    [InlineData("senha com espaços, acentuação çãõ e símbolos !@#$%")]
    [InlineData("🔐 unicode + emoji 中文")]
    public void Encrypt_then_Decrypt_round_trips(string plaintext)
    {
        var cipher = _sut.Encrypt(plaintext);
        Assert.Equal(plaintext, _sut.Decrypt(cipher));
    }

    [Fact]
    public void Same_plaintext_yields_different_ciphertexts()
    {
        // Nonce aleatório por operação → ciphertexts diferentes, ambos válidos.
        var a = _sut.Encrypt("segredo");
        var b = _sut.Encrypt("segredo");

        Assert.NotEqual(a, b);
        Assert.Equal("segredo", _sut.Decrypt(a));
        Assert.Equal("segredo", _sut.Decrypt(b));
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected()
    {
        // GCM é autenticado: alterar 1 byte invalida a tag → lança, nunca devolve lixo.
        var bytes = Convert.FromBase64String(_sut.Encrypt("dados sensíveis"));
        bytes[^1] ^= 0xFF;

        Assert.ThrowsAny<Exception>(() => _sut.Decrypt(Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void Wrong_key_cannot_decrypt()
    {
        var cipher = _sut.Encrypt("dados");
        var other = new EncryptionService("uma-chave-totalmente-diferente-x");

        Assert.ThrowsAny<Exception>(() => other.Decrypt(cipher));
    }
}
