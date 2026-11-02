using System;

namespace DocuHelper.Importers.Legacy.Doc
{
    internal static class DocPapReader
    {
        public static void Apply(DocPap pap, byte[] data, int start, int end)
        {
            var position = start;
            while (DocSprmReader.Next(data, ref position, end, out var sprm))
            {
                var o = sprm.Offset;
                switch (sprm.Op)
                {
                    case 0x4600:
                        pap.Istd = Word(data, o);
                        break;
                    case 0x2403:
                    case 0x2461:
                        pap.Justification = data[o];
                        break;
                    case 0x2405:
                        pap.KeepLines = data[o] == 1;
                        break;
                    case 0x2406:
                        pap.KeepNext = data[o] == 1;
                        break;
                    case 0x2407:
                        pap.PageBreakBefore = data[o] == 1;
                        break;
                    case 0x2431:
                        pap.WidowControl = data[o] == 1;
                        break;
                    case 0x2416:
                        pap.InTable = data[o] == 1;
                        break;
                    case 0x2417:
                        pap.RowEnd = data[o] == 1;
                        break;
                    case 0x6649:
                        pap.TableDepth = BitConverter.ToInt32(data, o);
                        break;
                    case 0x244B:
                        pap.InnerCellEnd = data[o] == 1;
                        break;
                    case 0x244C:
                        pap.InnerRowEnd = data[o] == 1;
                        break;
                    case 0x840F:
                    case 0x845E:
                        pap.Left = (short)Word(data, o);
                        break;
                    case 0x840E:
                    case 0x845D:
                        pap.Right = (short)Word(data, o);
                        break;
                    case 0x8411:
                    case 0x8460:
                        pap.First = (short)Word(data, o);
                        break;
                    case 0xA413:
                        pap.Before = Word(data, o);
                        break;
                    case 0xA414:
                        pap.After = Word(data, o);
                        break;
                    case 0x245B:
                        pap.BeforeAuto = data[o] == 1;
                        break;
                    case 0x245C:
                        pap.AfterAuto = data[o] == 1;
                        break;
                    case 0x6412:
                        if (sprm.Length >= 4)
                        {
                            pap.Line = (short)Word(data, o);
                            pap.LineMultiple = Word(data, o + 2) == 1;
                        }

                        break;
                    case 0x246D:
                        pap.ContextualSpacing = data[o] == 1;
                        break;
                    case 0x260A:
                        pap.ListLevel = data[o];
                        break;
                    case 0x460B:
                        pap.ListOverride = (short)Word(data, o);
                        break;
                    case 0x2640:
                        pap.Outline = data[o];
                        break;
                    case 0x6424:
                    case 0x6425:
                    case 0x6426:
                    case 0x6427:
                        pap.Borders[sprm.Op - 0x6424] = Border(DocBrc.Read80(data, o));
                        break;
                    case 0xC64E:
                    case 0xC64F:
                    case 0xC650:
                    case 0xC651:
                        if (sprm.Length >= 8)
                            pap.Borders[sprm.Op - 0xC64E] = Border(DocBrc.Read(data, o));

                        break;
                    case 0x442F:
                        pap.Shading = DocShading.FromShd80(data, o);
                        break;
                    case 0xC64D:
                        if (sprm.Length >= 10)
                            pap.Shading = DocShading.FromShd(data, o);

                        break;
                    case 0xC60D:
                        ReadTabs(pap, data, sprm, false);
                        break;
                    case 0xC615:
                        ReadTabs(pap, data, sprm, true);
                        break;
                    default:
                        if (sprm.Op >= 0x3400 && sprm.Op < 0xE000)
                            DocTapReader.Apply(pap, data, sprm);

                        break;
                }
            }
        }

        private static DocBrc Border(DocBrc brc) => brc.IsSet ? brc : null;

        private static int Word(byte[] data, int offset) => data[offset] | (data[offset + 1] << 8);

        private static void ReadTabs(DocPap pap, byte[] data, DocSprm sprm, bool withClose)
        {
            var position = sprm.Offset;
            var end = sprm.Offset + sprm.Length;
            if (position >= end)
                return;

            var deleted = data[position++];
            var deleteStart = position;
            position += deleted * (withClose ? 4 : 2);
            if (position >= end)
                return;

            for (var i = 0; i < deleted && deleteStart + i * 2 + 2 <= end; i++)
                pap.Tabs.Add(new DocTab { Position = (short)Word(data, deleteStart + i * 2), Clear = true });

            var added = data[position++];
            var descriptors = position + added * 2;
            if (descriptors + added > end)
                return;

            for (var i = 0; i < added; i++)
            {
                var descriptor = data[descriptors + i];
                pap.Tabs.Add(new DocTab
                {
                    Position = (short)Word(data, position + i * 2),
                    Alignment = descriptor & 7,
                    Leader = (descriptor >> 3) & 7,
                });
            }
        }
    }
}
