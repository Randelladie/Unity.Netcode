using UnityEngine;
using System.Text;
using System;
using Unity.Collections;

public class StringPacker
{
    public static byte[] PackAscii(string message)
    {
        byte start = 0x02;
        byte end = 0x03;

        byte[] stringBytes = Encoding.ASCII.GetBytes(message);

        byte[] result = new byte[stringBytes.Length + 2];

        result[0] = start;
        Buffer.BlockCopy(stringBytes, 0, result, 1, stringBytes.Length);
        result[^1] = end;

        return result;
    }

    public static bool TryUnpackAscii(byte[] data, out string message)
    {
        message = null;

        if (data == null || data.Length < 2)
            return false;

        if (data[0] != 0x02 || data[^1] != 0x03)
            return false;

        int length = data.Length - 2;
        byte[] stringBytes = new byte[length];

        Buffer.BlockCopy(data, 1, stringBytes, 0, length);

        message = Encoding.ASCII.GetString(stringBytes);
        return true;
    }

    public static bool TryReadData(DataStreamReader reader, out string message)
    {
        message = null;

        if (reader.Length == 0)
            return false;

        byte b;
        bool reading = false;

        var buffer = new byte[256];
        int index = 0;

        while (reader.GetBytesRead() < reader.Length)
        {
            b = reader.ReadByte();

            if (b == 0x02)
            {
                reading = true;
                continue;
            }

            if (b == 0x03)
                break;

            if (reading)
            {
                buffer[index++] = b;
            }
        }

        message = Encoding.ASCII.GetString(buffer, 0, index);
        return index > 0;
    }
}
