using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class WordNumbering
    {
        private static readonly string[] s_bullets = { "•", "◦", "▪" };

        private readonly WordprocessingDocument _document;
        private readonly WordStyles _styles;
        private readonly Dictionary<int, NumberingInstance> _instances = new();
        private readonly Dictionary<int, AbstractNum> _abstracts = new();

        public WordNumbering(WordprocessingDocument document, WordStyles styles)
        {
            _document = document;
            _styles = styles;
            Reload();
        }

        public Numbering Root => _document?.MainDocumentPart?.NumberingDefinitionsPart?.Numbering;

        public NumberingProperties Of(Paragraph paragraph, out int numId, out int level)
        {
            numId = 0;
            level = 0;
            var direct = paragraph?.ParagraphProperties?.NumberingProperties;
            var style = _styles.StyleNumbering(paragraph);
            var id = direct?.NumberingId?.Val?.Value ?? style?.NumberingId?.Val?.Value;
            if (id == null || id.Value <= 0)
                return null;

            numId = id.Value;
            level = direct?.NumberingLevelReference?.Val?.Value ?? style?.NumberingLevelReference?.Val?.Value ?? 0;
            return direct ?? style;
        }

        public WordKind ListKind(Paragraph paragraph, out int level)
        {
            if (Of(paragraph, out var numId, out level) == null)
                return WordKind.Body;

            var definition = FindLevel(numId, level);
            if (definition == null)
                return WordKind.Body;

            var format = definition.NumberingFormat?.Val;
            if (format == null)
                return WordKind.Number;

            if (format.Value == NumberFormatValues.Bullet)
                return WordKind.Bullet;

            return format.Value == NumberFormatValues.None ? WordKind.Body : WordKind.Number;
        }

        public Level FindLevel(int numId, int level)
        {
            if (!_instances.TryGetValue(numId, out var instance))
                return null;

            var overridden = instance.Elements<LevelOverride>().FirstOrDefault(o => o.LevelIndex?.Value == level)?.Level;
            if (overridden != null)
                return overridden;

            var definition = AbstractOf(instance);
            var link = definition?.NumberingStyleLink?.Val?.Value;
            if (link != null)
            {
                var linked = _styles.Find(link)?.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
                if (linked != null && linked.Value != numId && _instances.TryGetValue(linked.Value, out var target))
                    definition = AbstractOf(target) ?? definition;
            }

            return definition?.Elements<Level>().FirstOrDefault(l => l.LevelIndex?.Value == level);
        }

        public int BulletList()
        {
            foreach (var pair in _instances.OrderBy(p => p.Key))
            {
                if (pair.Value.Elements<LevelOverride>().Any())
                    continue;

                var definition = AbstractOf(pair.Value);
                if (!IsPlain(definition))
                    continue;

                var first = definition.Elements<Level>().FirstOrDefault(l => l.LevelIndex?.Value == 0);
                if (first?.NumberingFormat?.Val != null && first.NumberingFormat.Val.Value == NumberFormatValues.Bullet)
                    return pair.Key;
            }

            return AddInstance(AddAbstract(BulletLevels()), false);
        }

        public int NumberList()
        {
            var reusable = _abstracts.Values
                .Where(IsPlain)
                .FirstOrDefault(a =>
                {
                    var first = a.Elements<Level>().FirstOrDefault(l => l.LevelIndex?.Value == 0);
                    var format = first?.NumberingFormat?.Val;
                    var text = first?.LevelText?.Val?.Value;
                    return format != null && format.Value == NumberFormatValues.Decimal && (text == "%1." || text == "%1)");
                });

            var abstractId = reusable?.AbstractNumberId?.Value ?? AddAbstract(NumberLevels());
            return AddInstance(abstractId, true);
        }

        public static Level[] BulletLevels()
        {
            var levels = new Level[9];
            for (var i = 0; i < levels.Length; i++)
            {
                levels[i] = new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = NumberFormatValues.Bullet },
                    new LevelText { Val = s_bullets[i % s_bullets.Length] },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new PreviousParagraphProperties(new Indentation { Left = Indent(i), Hanging = "400" }))
                {
                    LevelIndex = i,
                };
            }

            return levels;
        }

        public static Level[] NumberLevels()
        {
            var levels = new Level[9];
            for (var i = 0; i < levels.Length; i++)
            {
                var format = i % 2 == 1 ? NumberFormatValues.Ganada : NumberFormatValues.Decimal;
                var suffix = i == 2 || i == 3 ? ")" : ".";
                levels[i] = new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = format },
                    new LevelText { Val = "%" + (i + 1).ToString(CultureInfo.InvariantCulture) + suffix },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new PreviousParagraphProperties(new Indentation { Left = Indent(i), Hanging = "400" }))
                {
                    LevelIndex = i,
                };
            }

            return levels;
        }

        public static AbstractNum NewAbstract(int id, IEnumerable<Level> levels)
        {
            var definition = new AbstractNum { AbstractNumberId = id };
            definition.AppendChild(new Nsid { Val = Random.Shared.Next(1, int.MaxValue).ToString("X8", CultureInfo.InvariantCulture) });
            definition.AppendChild(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel });
            foreach (var level in levels)
                definition.AppendChild(level);

            return definition;
        }

        public static NumberingInstance NewInstance(int numId, int abstractId, bool restart)
        {
            var instance = new NumberingInstance { NumberID = numId };
            instance.AppendChild(new AbstractNumId { Val = abstractId });
            if (restart)
                instance.AppendChild(new LevelOverride(new StartOverrideNumberingValue { Val = 1 }) { LevelIndex = 0 });

            return instance;
        }

        private static string Indent(int level) => (800 + 400 * level).ToString(CultureInfo.InvariantCulture);

        private static bool IsPlain(AbstractNum definition) =>
            definition != null
            && definition.NumberingStyleLink == null
            && definition.StyleLink == null
            && !definition.Elements<Level>().Any(l => l.ParagraphStyleIdInLevel != null);

        private AbstractNum AbstractOf(NumberingInstance instance)
        {
            var id = instance?.AbstractNumId?.Val?.Value;
            return id != null && _abstracts.TryGetValue(id.Value, out var definition) ? definition : null;
        }

        private int AddAbstract(IEnumerable<Level> levels)
        {
            var root = EnsureRoot();
            var id = _abstracts.Count == 0 ? 0 : _abstracts.Keys.Max() + 1;
            var definition = NewAbstract(id, levels);
            var last = root.Elements<AbstractNum>().LastOrDefault();
            if (last != null)
                last.InsertAfterSelf(definition);
            else
                root.AddChild(definition);

            _abstracts[id] = definition;
            return id;
        }

        private int AddInstance(int abstractId, bool restart)
        {
            var root = EnsureRoot();
            var numId = _instances.Count == 0 ? 1 : Math.Max(1, _instances.Keys.Max() + 1);
            var instance = NewInstance(numId, abstractId, restart);
            var last = root.Elements<NumberingInstance>().LastOrDefault();
            if (last != null)
                last.InsertAfterSelf(instance);
            else
                root.AddChild(instance);

            _instances[numId] = instance;
            return numId;
        }

        private Numbering EnsureRoot()
        {
            var main = _document.MainDocumentPart;
            var part = main.NumberingDefinitionsPart ?? main.AddNewPart<NumberingDefinitionsPart>();
            part.Numbering ??= new Numbering();
            return part.Numbering;
        }

        private void Reload()
        {
            _instances.Clear();
            _abstracts.Clear();
            var root = Root;
            if (root == null)
                return;

            foreach (var definition in root.Elements<AbstractNum>())
            {
                var id = definition.AbstractNumberId?.Value;
                if (id != null && !_abstracts.ContainsKey(id.Value))
                    _abstracts[id.Value] = definition;
            }

            foreach (var instance in root.Elements<NumberingInstance>())
            {
                var id = instance.NumberID?.Value;
                if (id != null && !_instances.ContainsKey(id.Value))
                    _instances[id.Value] = instance;
            }
        }
    }
}
