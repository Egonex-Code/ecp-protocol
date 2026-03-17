using ECP.Core.Security;

namespace ECP.BlackBoxTests;

public class HmacTests
{
    private static readonly byte[] Key = Convert.FromHexString("00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F");
    private static readonly byte[] WrongKey = Convert.FromHexString("F0E1D2C3B4A5968778695A4B3C2D1E0F00112233445566778899AABBCCDDEEFF");
    private static readonly byte[] Data = Convert.FromHexString("DEADBEEFCAFEBABE");
    private static readonly byte[] TamperedData = Convert.FromHexString("DEADBEEFCAFEBABF");

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(16)]
    public void HmacCompute_Lengths_0_8_10_12_16_AreSupported(int hmacLength)
    {
        var hmac = EcpSecurity.ComputeHmac(Key, Data, hmacLength);

        Assert.Equal(hmacLength, hmac.Length);
    }

    [Fact]
    public void HmacCompute_InvalidLength5_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EcpSecurity.ComputeHmac(Key, Data, 5));
    }

    [Fact]
    public void HmacVerify_CorrectKey_ReturnsTrue()
    {
        var hmac = EcpSecurity.ComputeHmac(Key, Data, 12);
        var ok = EcpSecurity.VerifyHmac(Key, Data, hmac);

        Assert.True(ok);
    }

    [Fact]
    public void HmacVerify_WrongKey_ReturnsFalse()
    {
        var hmac = EcpSecurity.ComputeHmac(Key, Data, 12);
        var ok = EcpSecurity.VerifyHmac(WrongKey, Data, hmac);

        Assert.False(ok);
    }

    [Fact]
    public void HmacVerify_TamperedPayload_ReturnsFalse()
    {
        var hmac = EcpSecurity.ComputeHmac(Key, Data, 12);
        var ok = EcpSecurity.VerifyHmac(Key, TamperedData, hmac);

        Assert.False(ok);
    }

    [Fact]
    public void HmacVerify_EmptyTagInUnsignedMode_ReturnsTrue()
    {
        var ok = EcpSecurity.VerifyHmac(Key, Data, Array.Empty<byte>());
        Assert.True(ok);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void HmacVerify_KeyLengths16_32_64_IsSupported(int keyLength)
    {
        var key = BuildDeterministicKey(keyLength);
        var hmac = EcpSecurity.ComputeHmac(key, Data, 12);
        var ok = EcpSecurity.VerifyHmac(key, Data, hmac);

        Assert.True(ok);
    }

    [Fact]
    public void HmacKnownVector_Data8Bytes_Length12_MatchesExpectedHex()
    {
        var expectedHex = "9815E43D048CC88DE7405E98";
        var actualHex = Convert.ToHexString(EcpSecurity.ComputeHmac(Key, Data, 12));

        Assert.Equal(expectedHex, actualHex);
    }

    private static byte[] BuildDeterministicKey(int keyLength)
    {
        var key = new byte[keyLength];
        for (var i = 0; i < keyLength; i++)
        {
            key[i] = (byte)((i + 1) % 256);
        }

        return key;
    }
}
