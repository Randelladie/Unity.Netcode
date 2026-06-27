using Unity.Collections;

public class PacketCodec
{
    public struct Packet
    {
        public byte type;
        public int connectionId;
        public byte[] payload;
    }

    // Helper
    private static byte[] ReadBytes(ref DataStreamReader reader, int length)
    {
        byte[] data = new byte[length];

        for (int i = 0; i < length; i++)
        {
            data[i] = reader.ReadByte();
        }

        return data;
    }

    // Writing
    public static void WriteSend(ref DataStreamWriter writer, int connectionId, byte[] payload)
    {
        writer.WriteByte(1);
        writer.WriteInt(connectionId);

        writer.WriteInt(payload.Length);
        writer.WriteBytes(payload);
    }

    public static void WriteSendToAll(ref DataStreamWriter writer, byte[] payload)
    {
        writer.WriteByte(2);

        writer.WriteInt(payload.Length);
        writer.WriteBytes(payload);
    }

    public static void WriteSendToAllExcept(ref DataStreamWriter writer, int excludedConnectionId, byte[] payload)
    {
        writer.WriteByte(3);
        writer.WriteInt(excludedConnectionId);

        writer.WriteInt(payload.Length);
        writer.WriteBytes(payload);
    }

    public static void WriteEnd(ref DataStreamWriter writer)
    {
        writer.WriteByte(0);
    }

    // Reading
    public static bool ReadNext(ref DataStreamReader reader, out Packet packet)
    {
        packet = default;

        if (reader.GetBytesRead() >= reader.Length)
            return false;

        byte type = reader.ReadByte();
        packet.type = type;

        if (type == 0)
            return false;

        if (type == 1)
        {
            packet.connectionId = reader.ReadInt();

            int size = reader.ReadInt();
            packet.payload = ReadBytes(ref reader, size);

            return true;
        }

        if (type == 2)
        {
            int size = reader.ReadInt();
            packet.payload = ReadBytes(ref reader, size);

            return true;
        }

        if (type == 3)
        {
            packet.connectionId = reader.ReadInt();

            int size = reader.ReadInt();
            packet.payload = ReadBytes(ref reader, size);

            return true;
        }

        return false;
    }
}