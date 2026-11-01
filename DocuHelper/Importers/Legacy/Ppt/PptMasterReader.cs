using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptMasterReader
    {
        private readonly PptSource _source;
        private readonly Dictionary<int, PptMasterInfo> _cache = new();
        private readonly PptRecord _main;

        public PptMasterReader(PptSource source, PptRecord main)
        {
            _source = source;
            _main = main;
        }

        public PptMasterInfo Get(PptRecord master)
        {
            master ??= _main;
            if (master == null)
                return new PptMasterInfo();

            if (_cache.TryGetValue(master.Start, out var cached))
                return cached;

            var info = new PptMasterInfo { Styles = _source.Styles.Copy() };
            _cache[master.Start] = info;
            if (_main != null)
                info.Styles.Read(_main);

            if (master != _main)
                info.Styles.Read(master);

            info.Scheme = PptColors.FindScheme(master) ?? (_main != null ? PptColors.FindScheme(_main) : null);
            var context = new PptSlideContext
            {
                Pictures = _source.Pictures,
                Defaults = _source.Defaults,
                Scheme = info.Scheme,
                Styles = info.Styles,
            };

            var parser = new PptDrawingParser(context);
            parser.Parse(master.Find(PptTypes.PpDrawing));
            info.Background = parser.Background;
            foreach (var shape in parser.Shapes)
            {
                if (shape.PlaceholderId >= 1 && shape.PlaceholderId <= 4 && shape.Anchor != null)
                    info.Anchors.TryAdd(shape.PlaceholderId, shape.Anchor.Value);
            }

            return info;
        }
    }
}
