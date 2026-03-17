using System.Globalization;
using System.Text.Json;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Security;
using ECP.Core.Token;

namespace ECP.BlackBoxTests;

public class VectorConformanceTests
{
    [Fact]
    public void UetVectorsJson_ConformsToSdkOutput()
    {
        using var document = LoadVectorDocument("uet-vectors.json");
        var vectors = document.RootElement.GetProperty("vectors");
        Assert.True(vectors.GetArrayLength() > 0);

        foreach (var vector in vectors.EnumerateArray())
        {
            var id = ReadString(vector, "id");
            var input = vector.GetProperty("input");

            var token = UniversalEmergencyToken.Create(
                (EmergencyType)ReadInt(input, "emergencyType"),
                (EcpPriority)ReadInt(input, "priority"),
                (ActionFlags)ReadInt(input, "actionFlags"),
                zoneHash: checked((ushort)ReadInt(input, "zoneHash")),
                timestampMinutes: checked((ushort)ReadInt(input, "timestampMinutes")),
                confirmHash: checked((uint)ReadInt(input, "confirmHash")));

            var actualHex = Convert.ToHexString(token.ToBytes());
            var expectedHex = ReadString(vector, "expectedHex");
            Assert.True(actualHex == expectedHex, $"Vector {id}: expected hex {expectedHex}, got {actualHex}.");

            var actualBase64 = token.ToBase64();
            var expectedBase64 = ReadString(vector, "expectedBase64");
            Assert.True(actualBase64 == expectedBase64, $"Vector {id}: expected base64 {expectedBase64}, got {actualBase64}.");
        }
    }

    [Fact]
    public void EnvelopeVectorsJson_ConformsToSdkOutput()
    {
        using var document = LoadVectorDocument("envelope-vectors.json");
        var vectors = document.RootElement.GetProperty("vectors");
        Assert.True(vectors.GetArrayLength() > 0);

        foreach (var vector in vectors.EnumerateArray())
        {
            var id = ReadString(vector, "id");
            var input = vector.GetProperty("input");

            var hmacLength = ReadInt(input, "hmacLength");
            var builder = Ecp.Envelope()
                .WithFlags((EcpFlags)ReadInt(input, "flags"))
                .WithPriority((EcpPriority)ReadInt(input, "priority"))
                .WithTtl(checked((byte)ReadInt(input, "ttl")))
                .WithKeyVersion(checked((byte)ReadInt(input, "keyVersion")))
                .WithMessageId(ParseUInt64Hex(ReadString(input, "messageId")))
                .WithTimestamp(checked((uint)input.GetProperty("timestampSeconds").GetInt64()))
                .WithPayloadType((EcpPayloadType)ReadInt(input, "payloadType"))
                .WithPayload(ParseHex(ReadString(input, "payloadHex")))
                .WithHmacLength(hmacLength);

            if (hmacLength > 0)
            {
                builder.WithHmacKey(ParseHex(ReadString(input, "hmacKeyHex")));
            }

            var envelope = builder.Build();
            var actualHex = Convert.ToHexString(envelope.ToBytes());
            var expectedHex = ReadString(vector, "expectedHex");
            Assert.True(actualHex == expectedHex, $"Vector {id}: expected hex {expectedHex}, got {actualHex}.");

            var actualBase64 = Convert.ToBase64String(envelope.ToBytes());
            var expectedBase64 = ReadString(vector, "expectedBase64");
            Assert.True(actualBase64 == expectedBase64, $"Vector {id}: expected base64 {expectedBase64}, got {actualBase64}.");
        }
    }

    [Fact]
    public void HmacVectorsJson_ConformsToSdkOutput()
    {
        using var document = LoadVectorDocument("hmac-vectors.json");
        var vectors = document.RootElement.GetProperty("vectors");
        Assert.True(vectors.GetArrayLength() > 0);

        foreach (var vector in vectors.EnumerateArray())
        {
            var id = ReadString(vector, "id");
            var kind = ReadString(vector, "kind");
            var input = vector.GetProperty("input");

            if (kind == "hmac-compute")
            {
                var actualTag = EcpSecurity.ComputeHmac(
                    ParseHex(ReadString(input, "keyHex")),
                    ParseHex(ReadString(input, "dataHex")),
                    ReadInt(input, "hmacLength"));

                var actualHex = Convert.ToHexString(actualTag);
                var expectedHex = ReadString(vector, "expectedTagHex");
                Assert.True(actualHex == expectedHex, $"Vector {id}: expected tag {expectedHex}, got {actualHex}.");
                continue;
            }

            if (kind == "hmac-verify")
            {
                var actualVerify = EcpSecurity.VerifyHmac(
                    ParseHex(ReadString(input, "keyHex")),
                    ParseHex(ReadString(input, "dataHex")),
                    ParseHex(ReadString(input, "tagHex")));

                var expectedVerify = vector.GetProperty("expectedVerify").GetBoolean();
                Assert.True(actualVerify == expectedVerify, $"Vector {id}: expected verify={expectedVerify}, got {actualVerify}.");
                continue;
            }

            throw new InvalidOperationException($"Unsupported HMAC vector kind: {kind}.");
        }
    }

    [Fact]
    public void NegativeVectorsJson_ConformsToExpectedBehavior()
    {
        using var document = LoadVectorDocument("negative-vectors.json");
        var vectors = document.RootElement.GetProperty("vectors");
        Assert.True(vectors.GetArrayLength() > 0);

        foreach (var vector in vectors.EnumerateArray())
        {
            var id = ReadString(vector, "id");
            var kind = ReadString(vector, "kind");
            var bytes = ParseHex(ReadString(vector, "inputHex"));

            if (kind == "try-decode")
            {
                var expectedTryDecode = vector.GetProperty("expected").GetProperty("tryDecode").GetBoolean();
                var actualTryDecode = Ecp.TryDecode(bytes, out _);
                Assert.True(actualTryDecode == expectedTryDecode, $"Vector {id}: expected tryDecode={expectedTryDecode}, got {actualTryDecode}.");
                continue;
            }

            if (kind == "try-decode-envelope")
            {
                var input = vector.GetProperty("input");
                var expected = vector.GetProperty("expected");

                var expectedTryDecode = expected.GetProperty("tryDecodeEnvelope").GetBoolean();
                var expectedIsValid = expected.GetProperty("isValid").GetBoolean();

                var actualTryDecode = Ecp.TryDecodeEnvelope(
                    bytes,
                    ParseHex(ReadString(input, "hmacKeyHex")),
                    out EmergencyEnvelope envelope,
                    ReadInt(input, "hmacLength"));

                Assert.True(actualTryDecode == expectedTryDecode, $"Vector {id}: expected tryDecodeEnvelope={expectedTryDecode}, got {actualTryDecode}.");
                if (actualTryDecode)
                {
                    Assert.True(envelope.IsValid == expectedIsValid, $"Vector {id}: expected isValid={expectedIsValid}, got {envelope.IsValid}.");
                }

                continue;
            }

            throw new InvalidOperationException($"Unsupported negative vector kind: {kind}.");
        }
    }

    private static JsonDocument LoadVectorDocument(string fileName)
    {
        var path = ResolveVectorPath(fileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string ResolveVectorPath(string fileName)
    {
        var outputCopyPath = Path.Combine(AppContext.BaseDirectory, "test-vectors", fileName);
        if (File.Exists(outputCopyPath))
        {
            return outputCopyPath;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "test-vectors", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Unable to locate test vector file: {fileName}.");
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetString() ?? string.Empty;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetInt32();
    }

    private static byte[] ParseHex(string hex)
    {
        return string.IsNullOrEmpty(hex) ? Array.Empty<byte>() : Convert.FromHexString(hex);
    }

    private static ulong ParseUInt64Hex(string value)
    {
        var normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return ulong.Parse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
