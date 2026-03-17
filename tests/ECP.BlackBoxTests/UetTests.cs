using ECP.Core;
using ECP.Core.Models;
using ECP.Core.Token;

namespace ECP.BlackBoxTests;

public class UetTests
{
    [Fact]
    public void UetEncodeDecode_Fire_Critical_RoundTrip()
    {
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Fire,
            EcpPriority.Critical,
            ActionFlags.SoundAlarm | ActionFlags.FlashLights | ActionFlags.ShowMessage,
            zoneHash: 1001,
            timestampMinutes: 12345,
            confirmHash: 0);

        var decoded = UniversalEmergencyToken.FromBytes(token.ToBytes());

        Assert.Equal(token.EmergencyType, decoded.EmergencyType);
        Assert.Equal(token.Priority, decoded.Priority);
        Assert.Equal(token.ActionFlags, decoded.ActionFlags);
        Assert.Equal(token.ZoneHash, decoded.ZoneHash);
        Assert.Equal(token.TimestampMinutes, decoded.TimestampMinutes);
        Assert.Equal(token.ConfirmHash, decoded.ConfirmHash);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void UetEncodeDecode_AllEmergencyTypes_RoundTrip(byte emergencyTypeValue)
    {
        var emergencyType = (EmergencyType)emergencyTypeValue;
        var token = UniversalEmergencyToken.Create(
            emergencyType,
            EcpPriority.High,
            ActionFlags.None,
            zoneHash: 0xABCD,
            timestampMinutes: 12345,
            confirmHash: 0x2AAAA);

        var decoded = UniversalEmergencyToken.FromBytes(token.ToBytes());

        Assert.Equal(emergencyType, decoded.EmergencyType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void UetEncodeDecode_AllPriorities_RoundTrip(byte priorityValue)
    {
        var priority = (EcpPriority)priorityValue;
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Fire,
            priority,
            ActionFlags.None,
            zoneHash: 0xABCD,
            timestampMinutes: 12345,
            confirmHash: 0x2AAAA);

        var decoded = UniversalEmergencyToken.FromBytes(token.ToBytes());

        Assert.Equal(priority, decoded.Priority);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x01)]
    [InlineData(0x02)]
    [InlineData(0x13)]
    [InlineData(0xFF)]
    public void UetEncodeDecode_ActionFlags_Combinations_RoundTrip(byte flagsValue)
    {
        var flags = (ActionFlags)flagsValue;
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Security,
            EcpPriority.Medium,
            flags,
            zoneHash: 0xABCD,
            timestampMinutes: 12345,
            confirmHash: 0x2AAAA);

        var decoded = UniversalEmergencyToken.FromBytes(token.ToBytes());

        Assert.Equal(flags, decoded.ActionFlags);
    }

    [Fact]
    public void UetSize_IsExactly8Bytes()
    {
        var bytes = Ecp.Alert(EmergencyType.Fire, zoneHash: 1001, priority: EcpPriority.Critical, timestampMinutes: 12345, confirmHash: 0);
        Assert.Equal(UniversalEmergencyToken.Size, bytes.Length);
    }

    [Fact]
    public void UetBase64_MaxLengthIs12Chars()
    {
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Fire,
            EcpPriority.Critical,
            ActionFlags.None,
            zoneHash: 1001,
            timestampMinutes: 12345,
            confirmHash: 0);

        Assert.True(token.ToBase64().Length <= 12);
    }

    [Fact]
    public void UetKnownVector_BitLayoutMatchesExpectedHex()
    {
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Security,
            EcpPriority.High,
            (ActionFlags)0xAC,
            zoneHash: 0x1234,
            timestampMinutes: 0x5678,
            confirmHash: 0x2AAAA);

        var expectedHex = "5AB048D159E2AAAA";
        var actualHex = Convert.ToHexString(token.ToBytes());

        Assert.Equal(expectedHex, actualHex);
    }

    [Fact]
    public void UetCreate_ConfirmHashOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UniversalEmergencyToken.Create(
                EmergencyType.Fire,
                EcpPriority.Critical,
                ActionFlags.None,
                zoneHash: 1001,
                timestampMinutes: 12345,
                confirmHash: 0x40000));
    }

    [Fact]
    public void EcpAlert_OneLiner_Returns8Bytes()
    {
        var bytes = Ecp.Alert(
            EmergencyType.Fire,
            zoneHash: 1001,
            priority: EcpPriority.Critical,
            actionFlags: ActionFlags.SoundAlarm,
            timestampMinutes: 12345,
            confirmHash: 0);

        Assert.Equal(8, bytes.Length);
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)65535)]
    public void UetTimestamp_16BitWrapAround_PreservesModularValue(ushort timestampMinutes)
    {
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Fire,
            EcpPriority.Critical,
            ActionFlags.None,
            zoneHash: 1001,
            timestampMinutes: timestampMinutes,
            confirmHash: 0);

        var decoded = UniversalEmergencyToken.FromBytes(token.ToBytes());

        Assert.Equal(timestampMinutes, decoded.TimestampMinutes);
    }
}
