using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Xls
{
    internal static class XlsRecordReader
    {
        public const int Continue = 0x003C;

        public static List<XlsRecord> Read(byte[] stream)
        {
            var records = new List<XlsRecord>();
            var position = 0;
            var currentId = -1;
            var currentPosition = 0;
            var segments = new List<(int Start, int Length)>();
            while (position + 4 <= stream.Length)
            {
                var id = BinaryPrimitives.ReadUInt16LittleEndian(stream.AsSpan(position));
                var length = BinaryPrimitives.ReadUInt16LittleEndian(stream.AsSpan(position + 2));
                if (position + 4 + length > stream.Length)
                    break;

                if (id == Continue && currentId >= 0)
                {
                    segments.Add((position + 4, length));
                }
                else
                {
                    Flush(records, stream, currentId, currentPosition, segments);
                    currentId = id;
                    currentPosition = position;
                    segments.Clear();
                    segments.Add((position + 4, length));
                }

                position += 4 + length;
            }

            Flush(records, stream, currentId, currentPosition, segments);
            return records;
        }

        private static void Flush(List<XlsRecord> records, byte[] stream, int id, int position, List<(int Start, int Length)> segments)
        {
            if (id < 0)
                return;

            var total = 0;
            foreach (var segment in segments)
                total += segment.Length;

            var data = new byte[total];
            var breaks = new List<int>();
            var offset = 0;
            for (var i = 0; i < segments.Count; i++)
            {
                if (i > 0 && segments[i].Length > 0)
                    breaks.Add(offset);

                Buffer.BlockCopy(stream, segments[i].Start, data, offset, segments[i].Length);
                offset += segments[i].Length;
            }

            records.Add(new XlsRecord(id, position, data, breaks.ToArray()));
        }
    }
}
