using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptPictureStore
    {
        private readonly List<PptRecord> _entries = new();
        private readonly Dictionary<int, PptImage> _images = new();
        private readonly byte[] _pictures;

        public PptPictureStore(PptRecord drawingGroup, byte[] pictures)
        {
            _pictures = pictures;
            var dgg = drawingGroup?.Find(PptTypes.DggContainer);
            var store = dgg?.Find(PptTypes.BStoreContainer);
            if (store != null)
                _entries.AddRange(store.Children);
        }

        public PptImage Get(int index)
        {
            if (index < 1 || index > _entries.Count)
                return null;

            if (_images.TryGetValue(index, out var cached))
                return cached;

            var image = Load(_entries[index - 1]);
            _images[index] = image;
            return image;
        }

        private PptImage Load(PptRecord entry)
        {
            if (entry.Type != PptTypes.Fbse)
                return PptBlipDecoder.Decode(entry);

            var nameLength = entry.Byte(33);
            var embedded = 36 + nameLength;
            if (entry.Length > embedded + PptRecord.HeaderSize)
            {
                var blip = PptRecord.Parse(entry.Bytes(embedded, entry.Length - embedded), 0, entry.Length - embedded);
                var image = PptBlipDecoder.Decode(blip);
                if (image != null)
                    return image;
            }

            var offset = entry.UInt32(28);
            if (_pictures == null || offset >= _pictures.Length)
                return null;

            return PptBlipDecoder.Decode(PptRecord.Parse(_pictures, (int)offset, _pictures.Length));
        }
    }
}
