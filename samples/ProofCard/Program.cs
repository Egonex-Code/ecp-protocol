// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ECP.Core;
using ECP.Core.Models;

const int MaxBarWidth = 34;
const int CardInnerWidth = 76;
const string RepositoryUrl = "github.com/Egonex-Code/ecp-protocol";
bool showPayload = HasFlag(args, "--show-payload");

FireScenario scenario = CreateCanonicalFireScenario();
string capXml = BuildCapXml(scenario);
string json = BuildJson(scenario);
ValidateGeneratedPayloads(capXml, json);
byte[] ecpAlert = Ecp.Alert(
    EmergencyType.Fire,
    zoneHash: scenario.ZoneHash,
    priority: EcpPriority.Critical,
    timestampMinutes: 12345,
    confirmHash: 0);

int capBytes = Encoding.UTF8.GetByteCount(capXml);
int jsonBytes = Encoding.UTF8.GetByteCount(json);
int ecpBytes = ecpAlert.Length;
int maxBytes = Math.Max(capBytes, Math.Max(jsonBytes, ecpBytes));

decimal reduction = CalculateReductionPercent(capBytes, ecpBytes);
string runId = BuildRunId();
string proofHash = ComputeProofHash(capXml, json, ecpAlert);
string verifiedOn = string.Concat(
    GetOsDisplayName(),
    " / ",
    RuntimeInformation.FrameworkDescription,
    " / ",
    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture));

string[] lines =
[
    "\"Why does it take 669 bytes to tell a computer there's a fire?\"",
    string.Empty,
    BuildMetricLine("CAP XML", capBytes, maxBytes, MaxBarWidth),
    BuildMetricLine("JSON", jsonBytes, maxBytes, MaxBarWidth),
    BuildMetricLine("ECP UET", ecpBytes, maxBytes, MaxBarWidth),
    string.Empty,
    string.Concat(
        "Same scenario. ",
        reduction.ToString("0.0", CultureInfo.InvariantCulture),
        "% less data (CAP XML vs ECP UET)."),
    "Method: all values are measured live from generated UTF-8 payloads.",
    "Vector: canonical FIRE profile aligned with public benchmark sizes.",
    string.Concat("Scenario: ", scenario.ScenarioName, " | ZoneHash: ", scenario.ZoneHash.ToString(CultureInfo.InvariantCulture)),
    string.Concat("Proof hash: ", proofHash),
    string.Empty,
    string.Concat("Verified on: ", verifiedOn),
    string.Concat("Run ID: ", runId),
    RepositoryUrl,
    string.Empty,
    "-- Copy-ready post ---------------------------------------------------",
    string.Concat(
        "I verified ",
        runId,
        ": CAP XML ",
        capBytes.ToString(CultureInfo.InvariantCulture),
        "B, JSON ",
        jsonBytes.ToString(CultureInfo.InvariantCulture),
        "B, ECP ",
        ecpBytes.ToString(CultureInfo.InvariantCulture),
        "B."),
    string.Concat(
        "Same FIRE scenario measured live in CLI. ",
        reduction.ToString("0.0", CultureInfo.InvariantCulture),
        "% less vs CAP. Proof ",
        proofHash,
        "."),
    "#ECP #OpenSource #DotNet",
    RepositoryUrl
];

WriteCard(lines, CardInnerWidth);

if (showPayload)
{
    WritePayloadEvidence(capXml, json, ecpAlert);
}

static FireScenario CreateCanonicalFireScenario()
{
    return new FireScenario(
        Identifier: "ECP-DEMO-20260319-0001",
        Sender: "ops@egonex.local",
        SentUtc: "2026-03-19T08:00:00Z",
        ScenarioName: "FIRE / Building A",
        ZoneHash: 1001,
        ZoneLabel: "Building A",
        Headline: "Building A fire - leave!",
        Description: "Smoke in Building A. Evacuate via route B now",
        Instruction: "Exit immediately. Go to North Gate assembly point. Ready.");
}

static string BuildCapXml(FireScenario scenario)
{
    return string.Concat(
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
        "<alert xmlns=\"urn:oasis:names:tc:emergency:cap:1.2\">",
        "<identifier>", scenario.Identifier, "</identifier>",
        "<sender>", scenario.Sender, "</sender>",
        "<sent>", scenario.SentUtc, "</sent>",
        "<status>Actual</status>",
        "<msgType>Alert</msgType>",
        "<scope>Public</scope>",
        "<info>",
        "<category>Safety</category>",
        "<event>Fire</event>",
        "<urgency>Immediate</urgency>",
        "<severity>Severe</severity>",
        "<certainty>Observed</certainty>",
        "<headline>", scenario.Headline, "</headline>",
        "<description>", scenario.Description, "</description>",
        "<instruction>", scenario.Instruction, "</instruction>",
        "<area><areaDesc>", scenario.ZoneLabel, "</areaDesc></area>",
        "</info>",
        "</alert>");
}

static string BuildJson(FireScenario scenario)
{
    return string.Concat(
        "{\"id\":\"", EscapeJson(scenario.Identifier),
        "\",\"sent\":\"", EscapeJson(scenario.SentUtc),
        "\",\"event\":\"Fire\"",
        ",\"priority\":\"Critical\"",
        ",\"zone\":\"", EscapeJson(scenario.ZoneLabel),
        "\",\"zoneHash\":", scenario.ZoneHash.ToString(CultureInfo.InvariantCulture),
        ",\"description\":\"", EscapeJson(scenario.Description),
        "\",\"instruction\":\"", EscapeJson(scenario.Instruction),
        "\"}");
}

static string EscapeJson(string value)
{
    return value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);
}

static void ValidateGeneratedPayloads(string capXml, string json)
{
    _ = XDocument.Parse(capXml);
    using JsonDocument jsonDoc = JsonDocument.Parse(json);
    _ = jsonDoc.RootElement.ValueKind;
}

static bool HasFlag(string[] arguments, string flag)
{
    foreach (string argument in arguments)
    {
        if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static void WritePayloadEvidence(string capXml, string json, byte[] ecpAlert)
{
    Console.WriteLine();
    Console.WriteLine("Payload evidence (--show-payload)");
    Console.WriteLine(new string('=', 32));
    Console.WriteLine();

    Console.WriteLine(string.Concat(
        "CAP XML payload (",
        Encoding.UTF8.GetByteCount(capXml).ToString(CultureInfo.InvariantCulture),
        " bytes)"));
    Console.WriteLine(capXml);
    Console.WriteLine();

    Console.WriteLine(string.Concat(
        "JSON payload (",
        Encoding.UTF8.GetByteCount(json).ToString(CultureInfo.InvariantCulture),
        " bytes)"));
    Console.WriteLine(json);
    Console.WriteLine();

    Console.WriteLine(string.Concat(
        "ECP UET payload (",
        ecpAlert.Length.ToString(CultureInfo.InvariantCulture),
        " bytes, HEX)"));
    Console.WriteLine(Convert.ToHexString(ecpAlert));
}

static decimal CalculateReductionPercent(int baselineBytes, int optimizedBytes)
{
    if (baselineBytes <= 0)
    {
        return 0m;
    }

    decimal ratio = 1m - ((decimal)optimizedBytes / baselineBytes);
    return Math.Round(ratio * 100m, 1, MidpointRounding.AwayFromZero);
}

static string ComputeProofHash(string capXml, string json, ReadOnlySpan<byte> ecp)
{
    byte[] capBytes = Encoding.UTF8.GetBytes(capXml);
    byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

    byte[] combined = new byte[capBytes.Length + jsonBytes.Length + ecp.Length];
    capBytes.CopyTo(combined, 0);
    jsonBytes.CopyTo(combined, capBytes.Length);
    ecp.CopyTo(combined.AsSpan(capBytes.Length + jsonBytes.Length));

    byte[] hash = SHA256.HashData(combined);
    return Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant();
}

static string BuildMetricLine(string label, int bytes, int maxBytes, int maxBarWidth)
{
    int barLength = ScaleBar(bytes, maxBytes, maxBarWidth);
    return string.Concat(
        label.PadRight(7),
        ": ",
        bytes.ToString(CultureInfo.InvariantCulture).PadLeft(4),
        " bytes  ",
        new string('#', barLength));
}

static int ScaleBar(int value, int maxValue, int maxWidth)
{
    if (value <= 0 || maxValue <= 0 || maxWidth <= 0)
    {
        return 1;
    }

    int scaled = (int)Math.Round(
        (double)value / maxValue * maxWidth,
        MidpointRounding.AwayFromZero);

    return Math.Clamp(scaled, 1, maxWidth);
}

static string BuildRunId()
{
    Span<byte> bytes = stackalloc byte[3];
    RandomNumberGenerator.Fill(bytes);
    return string.Concat("#", Convert.ToHexString(bytes).ToLowerInvariant());
}

static string GetOsDisplayName()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        return "Windows";
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        return "Linux";
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        return "macOS";
    }

    return RuntimeInformation.OSDescription.Trim();
}

static void WriteCard(IEnumerable<string> lines, int innerWidth)
{
    string border = string.Concat("+", new string('-', innerWidth + 2), "+");
    Console.WriteLine(border);

    foreach (string line in lines)
    {
        foreach (string wrapped in WrapLine(line, innerWidth))
        {
            Console.Write("| ");
            Console.Write(wrapped.PadRight(innerWidth));
            Console.WriteLine(" |");
        }
    }

    Console.WriteLine(border);
}

static IEnumerable<string> WrapLine(string text, int width)
{
    if (string.IsNullOrEmpty(text))
    {
        yield return string.Empty;
        yield break;
    }

    string remaining = text;

    while (remaining.Length > width)
    {
        int split = remaining.LastIndexOf(' ', width);
        if (split <= 0)
        {
            split = width;
        }

        string segment = remaining[..split].TrimEnd();
        yield return segment;
        remaining = remaining[split..].TrimStart();
    }

    yield return remaining;
}

file sealed record FireScenario(
    string Identifier,
    string Sender,
    string SentUtc,
    string ScenarioName,
    ushort ZoneHash,
    string ZoneLabel,
    string Headline,
    string Description,
    string Instruction);
