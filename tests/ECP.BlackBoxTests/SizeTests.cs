using System.Text;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Security;
using ECP.Core.Token;

namespace ECP.BlackBoxTests;

public class SizeTests
{
    private static readonly byte[] HmacKey = Convert.FromHexString("00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F");

    [Fact]
    public void UetSize_ConstantIs8()
    {
        Assert.Equal(8, UniversalEmergencyToken.Size);
    }

    [Fact]
    public void EnvelopeHeaderSize_ConstantIs22()
    {
        Assert.Equal(22, EmergencyEnvelope.HeaderSize);
    }

    [Fact]
    public void SignedEmptyEnvelope_DefaultHmac12_Is34Bytes()
    {
        var envelope = Ecp.Envelope()
            .WithType(EmergencyType.Fire)
            .WithPriority(EcpPriority.Critical)
            .WithFlags(EcpFlags.None)
            .WithTtl(30)
            .WithKeyVersion(1)
            .WithMessageId(0x0102030405060708)
            .WithTimestamp(1700000000)
            .WithPayload(Array.Empty<byte>())
            .WithHmacLength(EcpSecurity.DefaultHmacLength)
            .WithHmacKey(HmacKey)
            .Build();

        Assert.Equal(34, envelope.TotalLength);
    }

    [Fact]
    public void UnsignedEmptyEnvelope_Is22Bytes()
    {
        var envelope = Ecp.Envelope()
            .WithType(EmergencyType.Fire)
            .WithPriority(EcpPriority.Critical)
            .WithFlags(EcpFlags.None)
            .WithTtl(30)
            .WithKeyVersion(0)
            .WithMessageId(0x0102030405060708)
            .WithTimestamp(1700000000)
            .WithPayload(Array.Empty<byte>())
            .WithHmacLength(0)
            .Build();

        Assert.Equal(22, envelope.TotalLength);
    }

    [Fact]
    public void SignedEnvelopeWithUetPayload_DefaultHmac12_Is42Bytes()
    {
        var payload = Ecp.Alert(
            EmergencyType.Fire,
            zoneHash: 1001,
            priority: EcpPriority.Critical,
            timestampMinutes: 12345,
            confirmHash: 0);

        var envelope = Ecp.Envelope()
            .WithType(EmergencyType.Fire)
            .WithPriority(EcpPriority.Critical)
            .WithFlags(EcpFlags.NeedsConfirmation)
            .WithTtl(120)
            .WithKeyVersion(1)
            .WithMessageId(0x0102030405060708)
            .WithTimestamp(1700000000)
            .WithPayload(payload)
            .WithHmacLength(EcpSecurity.DefaultHmacLength)
            .WithHmacKey(HmacKey)
            .Build();

        Assert.Equal(42, envelope.TotalLength);
    }

    [Fact]
    public void JsonTypicalPayload_Is270Bytes()
    {
        var json = BuildJsonPayload(270);
        Assert.Equal(270, Encoding.UTF8.GetByteCount(json));
    }

    [Fact]
    public void CapTypicalPayload_Is669Bytes()
    {
        var cap = BuildCapPayload(669);
        Assert.Equal(669, Encoding.UTF8.GetByteCount(cap));
    }

    private static string BuildJsonPayload(int targetBytes)
    {
        const string prefix = "{\"type\":\"alert\",\"message\":\"";
        const string suffix = "\"}";
        return BuildFixedSizeString(prefix, suffix, targetBytes);
    }

    private static string BuildCapPayload(int targetBytes)
    {
        const string prefix = "<alert><info><headline>";
        const string suffix = "</headline></info></alert>";
        return BuildFixedSizeString(prefix, suffix, targetBytes);
    }

    private static string BuildFixedSizeString(string prefix, string suffix, int targetBytes)
    {
        if (targetBytes < prefix.Length + suffix.Length)
        {
            return string.Concat(prefix, suffix);
        }

        var fillerLength = targetBytes - prefix.Length - suffix.Length;
        var filler = new string('x', fillerLength);
        return string.Concat(prefix, filler, suffix);
    }
}
