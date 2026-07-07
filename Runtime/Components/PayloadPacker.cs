using Unity.Collections;

public class PayloadPacker
{
    public static NativeArray<byte> PackPayload(NativeArray<byte> payload)
    {
        ushort size = (ushort)payload.Length;

        NativeArray<byte> packed = new NativeArray<byte>(
            sizeof(ushort) + payload.Length,
            Allocator.Temp);

        // Write length (little-endian)
        packed[0] = (byte)size;
        packed[1] = (byte)(size >> 8);

        // Copy payload
        NativeArray<byte>.Copy(payload, 0, packed, sizeof(ushort), payload.Length);

        return packed;
    }

    public static NativeArray<byte> UnpackPayload(ref DataStreamReader reader, Allocator allocator = Allocator.Temp)
    {
        ushort length = reader.ReadUShort();

        NativeArray<byte> payload = new NativeArray<byte>(length, allocator);
        reader.ReadBytes(payload);

        return payload;
    }
}
