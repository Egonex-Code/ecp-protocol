using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;

namespace ECP.BlackBoxTests;

public class DecodeRouterTests
{
    private static readonly byte[] HmacKey = Convert.FromHexString("00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F");

    [Fact]
    public void TryDecode_UetBytes_ReturnsUetKind()
    {
        var bytes = Ecp.Alert(
            EmergencyType.Fire,
            zoneHash: 1001,
            priority: EcpPriority.Critical,
            timestampMinutes: 12345,
            confirmHash: 0);

        var ok = Ecp.TryDecode(bytes, out var message);

        Assert.True(ok);
        Assert.True(message.IsUet);
        Assert.Equal(EcpMessageKind.Uet, message.Kind);
    }

    [Fact]
    public void TryDecode_SignedEnvelope_ReturnsEnvelopeKind()
    {
        var bytes = CreateSignedEnvelopeBytes(new byte[] { 0xAA, 0xBB }, hmacLength: 12);

        var ok = Ecp.TryDecode(bytes, out var message);

        Assert.True(ok);
        Assert.True(message.IsEnvelope);
        Assert.Equal(EcpMessageKind.Envelope, message.Kind);
    }

    [Fact]
    public void TryDecode_UnsignedEnvelope_ReturnsEnvelopeKind()
    {
        var bytes = CreateUnsignedEnvelopeBytes(new byte[] { 0xAA, 0xBB });

        var ok = Ecp.TryDecode(bytes, out var message);

        Assert.True(ok);
        Assert.True(message.IsEnvelope);
        Assert.Equal(EcpMessageKind.Envelope, message.Kind);
    }

    [Fact]
    public void TryDecode_InvalidBytes_ReturnsFalse()
    {
        var bytes = new byte[] { 0xEC, 0x50, 0x01, 0x00, 0xFF };

        var ok = Ecp.TryDecode(bytes, out _);

        Assert.False(ok);
    }

    [Fact]
    public void Decode_InvalidBytes_ThrowsEcpDecodeException()
    {
        var bytes = new byte[] { 0xEC, 0x50, 0x01, 0x00, 0xFF };

        Assert.Throws<EcpDecodeException>(() => Ecp.Decode(bytes));
    }

    [Fact]
    public void TryDecode_EnvelopeWithInvalidMagic_ReturnsFalse()
    {
        var bytes = CreateUnsignedEnvelopeBytes(new byte[] { 0xAA, 0xBB });
        bytes[0] = 0x00;

        var ok = Ecp.TryDecode(bytes, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryDecode_EnvelopeWithInvalidVersion_ReturnsFalse()
    {
        var bytes = CreateUnsignedEnvelopeBytes(new byte[] { 0xAA, 0xBB });
        bytes[2] = 0xFF;

        var ok = Ecp.TryDecode(bytes, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryDecode_EnvelopeLengthMismatchShort_ReturnsFalse()
    {
        var bytes = CreateUnsignedEnvelopeBytes(new byte[] { 0xAA, 0xBB });
        var truncated = bytes[..^1];

        var ok = Ecp.TryDecode(truncated, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryDecode_EnvelopeLengthMismatchLong_ReturnsFalse()
    {
        var bytes = CreateUnsignedEnvelopeBytes(new byte[] { 0xAA, 0xBB });
        var extended = bytes.Concat(new byte[] { 0x00 }).ToArray();

        var ok = Ecp.TryDecode(extended, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryDecode_EnvelopeWithDerivedInvalidHmacLength5_ReturnsFalse()
    {
        var bytes = CreateUnsignedEnvelopeBytes(Array.Empty<byte>());
        var withInvalidTrailing = bytes.Concat(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }).ToArray();

        var ok = Ecp.TryDecode(withInvalidTrailing, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryDecode_RandomFuzz_DoesNotThrow()
    {
        var random = new Random(123456);

        for (var i = 0; i < 100; i++)
        {
            var length = random.Next(1, 257);
            var bytes = new byte[length];
            random.NextBytes(bytes);

            var ex = Record.Exception(() => Ecp.TryDecode(bytes, out _));
            Assert.Null(ex);
        }
    }

    private static byte[] CreateSignedEnvelopeBytes(byte[] payload, int hmacLength)
    {
        return Ecp.Envelope()
            .WithType(EmergencyType.Fire)
            .WithFlags(EcpFlags.NeedsConfirmation | EcpFlags.Broadcast)
            .WithPriority(EcpPriority.Critical)
            .WithTtl(120)
            .WithKeyVersion(1)
            .WithMessageId(0x0102030405060708)
            .WithTimestamp(1700000000)
            .WithPayload(payload)
            .WithHmacLength(hmacLength)
            .WithHmacKey(HmacKey)
            .Build()
            .ToBytes();
    }

    private static byte[] CreateUnsignedEnvelopeBytes(byte[] payload)
    {
        return Ecp.Envelope()
            .WithType(EmergencyType.Fire)
            .WithFlags(EcpFlags.None)
            .WithPriority(EcpPriority.Critical)
            .WithTtl(120)
            .WithKeyVersion(0)
            .WithMessageId(0x0102030405060708)
            .WithTimestamp(1700000000)
            .WithPayload(payload)
            .WithHmacLength(0)
            .Build()
            .ToBytes();
    }
}
