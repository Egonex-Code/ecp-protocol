using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;

namespace ECP.BlackBoxTests;

public class EnvelopeTests
{
    private static readonly byte[] HmacKey = Convert.FromHexString("00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F");
    private const ulong MessageId = 0x0102030405060708;
    private const uint TimestampSeconds = 1700000000;

    [Fact]
    public void EnvelopeHeaderSize_Is22Bytes()
    {
        Assert.Equal(22, EmergencyEnvelope.HeaderSize);
    }

    [Fact]
    public void EnvelopeMagicAndVersion_AreExpected()
    {
        var envelope = CreateSignedEnvelope(new byte[] { 0xAA, 0xBB }, hmacLength: 12);
        var bytes = envelope.ToBytes();

        Assert.Equal(0xEC, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x01, bytes[2]);
    }

    [Fact]
    public void EnvelopeSigned_EncodeDecode_RoundTrip_IsValid()
    {
        var payload = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        var envelope = CreateSignedEnvelope(payload, hmacLength: 12);
        var bytes = envelope.ToBytes();

        var decoded = EmergencyEnvelope.Decode(bytes, HmacKey, hmacLength: 12);

        Assert.True(decoded.IsValid);
        Assert.Equal(EcpPriority.Critical, decoded.Priority);
        Assert.Equal(EcpPayloadType.Alert, decoded.PayloadType);
        Assert.Equal(payload, decoded.Payload.ToArray());
    }

    [Fact]
    public void EnvelopeUnsigned_EncodeDecode_RoundTrip_IsValid()
    {
        var payload = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        var envelope = CreateUnsignedEnvelope(payload);
        var bytes = envelope.ToBytes();

        var decoded = EmergencyEnvelope.Decode(bytes, hmacLength: 0);

        Assert.True(decoded.IsValid);
        Assert.Equal(0, decoded.Hmac.Length);
        Assert.Equal(payload, decoded.Payload.ToArray());
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(16)]
    public void EnvelopeSigned_HmacLength_8_10_12_16_AreSupported(int hmacLength)
    {
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var envelope = CreateSignedEnvelope(payload, hmacLength);
        var bytes = envelope.ToBytes();

        var decoded = EmergencyEnvelope.Decode(bytes, HmacKey, hmacLength);

        Assert.True(decoded.IsValid);
        Assert.Equal(hmacLength, decoded.Hmac.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(64)]
    [InlineData(256)]
    public void EnvelopePayloadLengths_0_8_64_256_RoundTrip(int payloadLength)
    {
        var payload = BuildDeterministicPayload(payloadLength);
        var envelope = CreateSignedEnvelope(payload, hmacLength: 12);
        var bytes = envelope.ToBytes();

        var decoded = EmergencyEnvelope.Decode(bytes, HmacKey, hmacLength: 12);

        Assert.True(decoded.IsValid);
        Assert.Equal(payloadLength, decoded.PayloadLength);
        Assert.Equal(payload, decoded.Payload.ToArray());
    }

    [Fact]
    public void EnvelopeTotalLength_MatchesFormula_22PlusPayloadPlusHmac()
    {
        var payload = BuildDeterministicPayload(64);
        var envelope = CreateSignedEnvelope(payload, hmacLength: 12);

        Assert.Equal(EmergencyEnvelope.HeaderSize + payload.Length + 12, envelope.TotalLength);
    }

    [Fact]
    public void EnvelopeDecodeView_WithValidKey_IsValid()
    {
        var payload = BuildDeterministicPayload(64);
        var envelope = CreateSignedEnvelope(payload, hmacLength: 12);
        var bytes = envelope.ToBytes();

        var view = EmergencyEnvelope.DecodeView(bytes, HmacKey, hmacLength: 12);

        Assert.True(view.IsValid);
        Assert.Equal(payload.Length, view.Payload.Length);
        Assert.Equal(12, view.Hmac.Length);
    }

    [Fact]
    public void EnvelopeKnownVector_SignedHmac12_MatchesExpectedHex()
    {
        var payload = Convert.FromHexString("0C000FA4C0E40000");
        var envelope = Ecp.Envelope()
            .WithType(EmergencyType.Fire)
            .WithFlags(EcpFlags.NeedsConfirmation | EcpFlags.Broadcast)
            .WithPriority(EcpPriority.Critical)
            .WithTtl(120)
            .WithKeyVersion(1)
            .WithMessageId(MessageId)
            .WithTimestamp(TimestampSeconds)
            .WithPayload(payload)
            .WithHmacLength(12)
            .WithHmacKey(HmacKey)
            .Build();

        var expectedHex = "EC50010303780101020304050607086553F1000000080C000FA4C0E400003A034F4B73B4D65E5B4AF650";
        var actualHex = Convert.ToHexString(envelope.ToBytes());

        Assert.Equal(expectedHex, actualHex);
    }

    private static EmergencyEnvelope CreateSignedEnvelope(byte[] payload, int hmacLength)
    {
        return Ecp.Envelope()
            .WithType(EmergencyType.Fire)
            .WithFlags(EcpFlags.NeedsConfirmation | EcpFlags.Broadcast)
            .WithPriority(EcpPriority.Critical)
            .WithTtl(120)
            .WithKeyVersion(1)
            .WithMessageId(MessageId)
            .WithTimestamp(TimestampSeconds)
            .WithPayload(payload)
            .WithHmacLength(hmacLength)
            .WithHmacKey(HmacKey)
            .Build();
    }

    private static EmergencyEnvelope CreateUnsignedEnvelope(byte[] payload)
    {
        return Ecp.Envelope()
            .WithType(EmergencyType.Fire)
            .WithFlags(EcpFlags.None)
            .WithPriority(EcpPriority.Critical)
            .WithTtl(120)
            .WithKeyVersion(0)
            .WithMessageId(MessageId)
            .WithTimestamp(TimestampSeconds)
            .WithPayload(payload)
            .WithHmacLength(0)
            .Build();
    }

    private static byte[] BuildDeterministicPayload(int length)
    {
        return Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();
    }
}
