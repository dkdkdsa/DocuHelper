using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal static class PptPersistReader
    {
        private const uint EncryptedToken = 0xF3D1C4DF;
        private const int MaxEdits = 4096;
        private const int MaxPersistEntries = 1000000;

        public static PptPersist Read(byte[] stream, byte[] currentUser)
        {
            var persist = new PptPersist();
            var atom = currentUser == null ? null : PptRecord.Parse(currentUser, 0, currentUser.Length);
            if (atom == null || atom.Type != PptTypes.CurrentUserAtom)
                return persist;

            if (atom.UInt32(4) == EncryptedToken)
                persist.Encrypted = true;

            var position = (long)atom.UInt32(8);
            var visited = new HashSet<long>();
            var first = true;
            while (position > 0 && position < stream.Length && visited.Count < MaxEdits && visited.Add(position))
            {
                var edit = PptRecord.Parse(stream, (int)position, stream.Length);
                if (edit == null || edit.Type != PptTypes.UserEditAtom)
                    break;

                if (first)
                {
                    persist.DocumentId = edit.UInt32(16);
                    first = false;
                }

                if (edit.Length >= 32 && edit.UInt32(28) != 0)
                    persist.Encrypted = true;

                ReadDirectory(stream, (int)edit.UInt32(12), persist);
                position = edit.UInt32(8);
            }

            return persist;
        }

        private static void ReadDirectory(byte[] stream, int offset, PptPersist persist)
        {
            var directory = PptRecord.Parse(stream, offset, stream.Length);
            if (directory == null || directory.Type != PptTypes.PersistDirectoryAtom)
                return;

            var at = 0;
            var count = 0;
            while (at + 4 <= directory.Length && count < MaxPersistEntries)
            {
                var head = directory.UInt32(at);
                var first = head & 0xFFFFF;
                var entries = (int)(head >> 20);
                at += 4;
                for (var i = 0; i < entries && at + 4 <= directory.Length; i++)
                {
                    var id = first + (uint)i;
                    var target = directory.UInt32(at);
                    at += 4;
                    count++;
                    if (target < stream.Length)
                        persist.Offsets.TryAdd(id, (int)target);
                }
            }
        }
    }
}
