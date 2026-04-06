using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace BetterMapTools.Features.MapDrawing.Multiplayer;

internal struct MapDrawingColorWireValue : IPacketSerializable
{
    public ulong PlayerId;
    public bool HasOverride;
    public string ColorRaw;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(PlayerId);
        writer.WriteBool(HasOverride);
        writer.WriteString(ColorRaw ?? string.Empty);
    }

    public void Deserialize(PacketReader reader)
    {
        PlayerId = reader.ReadULong();
        HasOverride = reader.ReadBool();
        ColorRaw = reader.ReadString();
    }
}

internal struct MapDrawingColorOverrideMessage : INetMessage, IPacketSerializable
{
    public bool HasOverride;
    public string ColorRaw;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.VeryDebug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteBool(HasOverride);
        writer.WriteString(ColorRaw ?? string.Empty);
    }

    public void Deserialize(PacketReader reader)
    {
        HasOverride = reader.ReadBool();
        ColorRaw = reader.ReadString();
    }
}

internal struct MapDrawingColorSnapshotMessage : INetMessage, IPacketSerializable
{
    public List<MapDrawingColorWireValue> Values;

    public bool ShouldBroadcast => false;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.VeryDebug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteList(Values ?? []);
    }

    public void Deserialize(PacketReader reader)
    {
        Values = reader.ReadList<MapDrawingColorWireValue>();
    }
}

internal struct MapDrawingUndoMessage : INetMessage, IPacketSerializable
{
    public int LineCount;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.VeryDebug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(LineCount, 8);
    }

    public void Deserialize(PacketReader reader)
    {
        LineCount = reader.ReadInt(8);
    }
}
