using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using P = DocumentFormat.OpenXml.Presentation;

namespace DocuHelper.Tools.Ppt
{
    public static class PptLayoutTemplates
    {
        private const long StandardWidth = 12192000;
        private const long StandardHeight = 6858000;
        private const long StandardBodyX = 838200;
        private const long StandardBodyY = 1825625;
        private const long StandardBodyWidth = 10515600;
        private const long StandardBodyHeight = 4351338;
        private const uint FirstLayoutId = 2147483648;

        private const string Namespaces =
            "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" "
            + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" "
            + "xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"";

        private const string GroupHeader =
            "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>"
            + "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>";

        private const string MutedFill = "<a:solidFill><a:schemeClr val=\"tx1\"><a:tint val=\"75000\"/></a:schemeClr></a:solidFill>";

        public static SlideLayoutPart Create(PresentationDocument document, SlideMasterPart master, string kind)
        {
            var presentationPart = document.PresentationPart;
            var korean = IsKorean(presentationPart);
            var frame = Frame.Of(presentationPart, master);
            var name = UniqueName(presentationPart, korean ? PptLayoutKinds.KoreanName(kind) : PptLayoutKinds.EnglishName(kind));
            var xml = new Builder(frame, FooterTypes(master), korean).Build(kind, name);

            var layout = master.AddNewPart<SlideLayoutPart>(FreeId(master));
            layout.SlideLayout = new P.SlideLayout(xml);
            layout.AddPart(master, "rId1");

            var list = master.SlideMaster.SlideLayoutIdList;
            if (list == null)
            {
                list = new P.SlideLayoutIdList();
                master.SlideMaster.SlideLayoutIdList = list;
            }

            list.AppendChild(new P.SlideLayoutId { Id = NextId(presentationPart), RelationshipId = master.GetIdOfPart(layout) });
            return layout;
        }

        private static string FreeId(OpenXmlPartContainer container)
        {
            for (var i = 1; ; i++)
            {
                var id = "rId" + i.ToString(CultureInfo.InvariantCulture);
                if (!container.TryGetPartById(id, out _) && !container.HyperlinkRelationships.Any(r => r.Id == id) && !container.ExternalRelationships.Any(r => r.Id == id) && !container.DataPartReferenceRelationships.Any(r => r.Id == id))
                    return id;
            }
        }

        public static uint NextId(PresentationPart presentationPart)
        {
            var max = FirstLayoutId - 1;
            foreach (var masterId in presentationPart.Presentation.SlideMasterIdList?.Elements<P.SlideMasterId>() ?? Enumerable.Empty<P.SlideMasterId>())
                max = Math.Max(max, masterId.Id?.Value ?? 0);

            foreach (var master in presentationPart.SlideMasterParts)
            {
                foreach (var layoutId in master.SlideMaster?.SlideLayoutIdList?.Elements<P.SlideLayoutId>() ?? Enumerable.Empty<P.SlideLayoutId>())
                    max = Math.Max(max, layoutId.Id?.Value ?? 0);
            }

            return max + 1;
        }

        public static bool IsKorean(PresentationPart presentationPart) =>
            presentationPart.SlideMasterParts
                .SelectMany(m => m.SlideLayoutParts)
                .Any(l => PptText.HasHangul(l.SlideLayout?.CommonSlideData?.Name?.Value));

        private static string UniqueName(PresentationPart presentationPart, string name)
        {
            var taken = new HashSet<string>(
                presentationPart.SlideMasterParts.SelectMany(m => m.SlideLayoutParts).Select(l => l.SlideLayout?.CommonSlideData?.Name?.Value ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            if (!taken.Contains(name))
                return name;

            for (var i = 2; ; i++)
            {
                var candidate = name + " " + i.ToString(CultureInfo.InvariantCulture);
                if (!taken.Contains(candidate))
                    return candidate;
            }
        }

        private static HashSet<string> FooterTypes(SlideMasterPart master)
        {
            var types = new HashSet<string>();
            var tree = master.SlideMaster?.CommonSlideData?.ShapeTree;
            if (tree == null)
                return types;

            foreach (var shape in tree.Elements<P.Shape>())
            {
                var type = PptPlaceholder.TypeOf(PptPlaceholder.Of(shape));
                if (type is "dt" or "ftr" or "sldNum")
                    types.Add(type);
            }

            return types;
        }

        private sealed class Frame
        {
            private Frame(long height, PptBox body)
            {
                Body = body;
                Kx = body.Width / (double)StandardBodyWidth;
                Ky = height / (double)StandardHeight;
                Kb = body.Height / (double)StandardBodyHeight;
            }

            public PptBox Body { get; }

            public double Kx { get; }

            public double Ky { get; }

            public double Kb { get; }

            public static Frame Of(PresentationPart presentationPart, SlideMasterPart master)
            {
                var size = presentationPart.Presentation.SlideSize;
                var width = size?.Cx != null && size.Cx.HasValue && size.Cx.Value > 0 ? size.Cx.Value : StandardWidth;
                var height = size?.Cy != null && size.Cy.HasValue && size.Cy.Value > 0 ? size.Cy.Value : StandardHeight;
                var scaleX = width / (double)StandardWidth;
                var scaleY = height / (double)StandardHeight;
                var body = new PptBox(
                    (long)Math.Round(StandardBodyX * scaleX),
                    (long)Math.Round(StandardBodyY * scaleY),
                    (long)Math.Round(StandardBodyWidth * scaleX),
                    (long)Math.Round(StandardBodyHeight * scaleY));

                var shape = master.SlideMaster?.CommonSlideData?.ShapeTree?.Elements<P.Shape>()
                    .FirstOrDefault(s => PptPlaceholder.RoleOf(PptPlaceholder.TypeOf(PptPlaceholder.Of(s))) == PptShapeRole.Body && PptPlaceholder.Of(s) != null);

                var box = shape == null ? null : PptXfrm.Read(shape);
                if (box != null && !box.Value.IsEmpty)
                    body = box.Value;

                return new Frame(height, body);
            }

            public long X(long standard) => Body.X + (long)Math.Round((standard - StandardBodyX) * Kx);

            public long W(long standard) => (long)Math.Round(standard * Kx);

            public long Y(long standard) => (long)Math.Round(standard * Ky);

            public long H(long standard) => (long)Math.Round(standard * Ky);
        }

        private sealed class Builder
        {
            private readonly Frame _frame;
            private readonly HashSet<string> _footers;
            private readonly string _lang;
            private readonly string _titleText;
            private readonly string _subtitleText;
            private readonly string[] _levelText;
            private readonly StringBuilder _shapes = new();
            private int _nextId = 2;

            public Builder(Frame frame, HashSet<string> footers, bool korean)
            {
                _frame = frame;
                _footers = footers;
                _lang = korean ? "ko-KR" : "en-US";
                _titleText = korean ? "마스터 제목 스타일 편집" : "Click to edit Master title style";
                _subtitleText = korean ? "마스터 부제목 스타일 편집" : "Click to edit Master subtitle style";
                _levelText = korean
                    ? new[] { "마스터 텍스트 스타일 편집", "둘째 수준", "셋째 수준", "넷째 수준", "다섯째 수준" }
                    : new[] { "Click to edit Master text styles", "Second level", "Third level", "Fourth level", "Fifth level" };
            }

            public string Build(string kind, string name)
            {
                var caption = Stepped(new[] { 1600, 1400, 1200, 1000, 1000 });
                switch (kind)
                {
                    case PptLayoutKinds.Title:
                        Title("ctrTitle", Xfrm(_frame.X(1524000), _frame.Y(1122363), _frame.W(9144000), _frame.H(2387600)), "anchor=\"b\"", Level(1, "algn=\"ctr\"", string.Empty, 6000, false, string.Empty));
                        Text("Subtitle", " type=\"subTitle\" idx=\"1\"", Xfrm(_frame.X(1524000), _frame.Y(3602038), _frame.W(9144000), _frame.H(1655762)), string.Empty, Stepped(new[] { 2400, 2000, 1800, 1800, 1800 }, "algn=\"ctr\""), _subtitleText);
                        break;
                    case PptLayoutKinds.Content:
                        Title("title", string.Empty, string.Empty, string.Empty);
                        Content(" idx=\"1\"", string.Empty);
                        break;
                    case PptLayoutKinds.Section:
                        Title("title", Xfrm(_frame.X(831850), _frame.Y(1709738), _frame.W(10515600), _frame.H(2852737)), "anchor=\"b\"", Level(1, string.Empty, string.Empty, 6000, false, string.Empty));
                        Text("Text Placeholder", " type=\"body\" sz=\"quarter\" idx=\"1\"", Xfrm(_frame.X(831850), _frame.Y(4589463), _frame.W(10515600), _frame.H(1500187)), string.Empty, Stepped(new[] { 2400, 2000, 1800, 1600, 1600 }, string.Empty, false, MutedFill), null);
                        break;
                    case PptLayoutKinds.TwoContent:
                        Title("title", string.Empty, string.Empty, string.Empty);
                        var gap = (long)Math.Round(152400 * _frame.Kx);
                        var half = (_frame.Body.Width - gap) / 2;
                        Content(" sz=\"half\" idx=\"1\"", Xfrm(_frame.Body.X, _frame.Body.Y, half, _frame.Body.Height));
                        Content(" sz=\"half\" idx=\"2\"", Xfrm(_frame.Body.X + half + gap, _frame.Body.Y, half, _frame.Body.Height));
                        break;
                    case PptLayoutKinds.Comparison:
                        Title("title", string.Empty, string.Empty, string.Empty);
                        var headerY = _frame.Body.Y - (long)Math.Round(144462 * _frame.Kb);
                        var headerHeight = (long)Math.Round(823912 * _frame.Kb);
                        var objectY = headerY + headerHeight;
                        var objectHeight = (long)Math.Round(3684588 * _frame.Kb);
                        var leftWidth = _frame.W(5157787);
                        var rightX = _frame.X(6172200);
                        var rightWidth = _frame.W(5183188);
                        var header = Stepped(new[] { 2400, 2000, 1800, 1600, 1600 }, string.Empty, true);
                        Text("Text Placeholder", " type=\"body\" idx=\"1\"", Xfrm(_frame.Body.X, headerY, leftWidth, headerHeight), "anchor=\"b\"", header, null);
                        Content(" sz=\"half\" idx=\"2\"", Xfrm(_frame.Body.X, objectY, leftWidth, objectHeight));
                        Text("Text Placeholder", " type=\"body\" sz=\"quarter\" idx=\"3\"", Xfrm(rightX, headerY, rightWidth, headerHeight), "anchor=\"b\"", header, null);
                        Content(" sz=\"quarter\" idx=\"4\"", Xfrm(rightX, objectY, rightWidth, objectHeight));
                        break;
                    case PptLayoutKinds.TitleOnly:
                        Title("title", string.Empty, string.Empty, string.Empty);
                        break;
                    case PptLayoutKinds.Blank:
                        break;
                    case PptLayoutKinds.ContentCaption:
                        CaptionTitle();
                        Content(" idx=\"1\"", Xfrm(_frame.X(5183188), _frame.Y(987425), _frame.W(6172200), _frame.H(4873625)), Sizes(new[] { 3200, 2800, 2400, 2000, 2000 }));
                        Text("Text Placeholder", " type=\"body\" sz=\"half\" idx=\"2\"", Xfrm(_frame.X(839788), _frame.Y(2057400), _frame.W(3932237), _frame.H(3811588)), string.Empty, caption, null);
                        break;
                    default:
                        CaptionTitle();
                        Picture(Xfrm(_frame.X(5183188), _frame.Y(987425), _frame.W(6172200), _frame.H(4873625)));
                        Text("Text Placeholder", " type=\"body\" sz=\"half\" idx=\"2\"", Xfrm(_frame.X(839788), _frame.Y(2057400), _frame.W(3932237), _frame.H(3811588)), string.Empty, caption, null);
                        break;
                }

                Footers();
                return $"<p:sldLayout {Namespaces} type=\"{TypeToken(kind)}\" preserve=\"1\"><p:cSld name=\"{SecurityElement.Escape(name)}\"><p:spTree>{GroupHeader}{_shapes}</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>";
            }

            private static string TypeToken(string kind) => kind switch
            {
                PptLayoutKinds.Title => "title",
                PptLayoutKinds.Content => "obj",
                PptLayoutKinds.Section => "secHead",
                PptLayoutKinds.TwoContent => "twoObj",
                PptLayoutKinds.Comparison => "twoTxTwoObj",
                PptLayoutKinds.TitleOnly => "titleOnly",
                PptLayoutKinds.Blank => "blank",
                PptLayoutKinds.ContentCaption => "objTx",
                _ => "picTx",
            };

            private static string Xfrm(long x, long y, long cx, long cy) =>
                $"<a:xfrm><a:off x=\"{x.ToString(CultureInfo.InvariantCulture)}\" y=\"{y.ToString(CultureInfo.InvariantCulture)}\"/><a:ext cx=\"{cx.ToString(CultureInfo.InvariantCulture)}\" cy=\"{cy.ToString(CultureInfo.InvariantCulture)}\"/></a:xfrm>";

            private static string Level(int level, string paragraphAttributes, string paragraphChildren, int size, bool bold, string runChildren)
            {
                var attributes = $"sz=\"{size.ToString(CultureInfo.InvariantCulture)}\"" + (bold ? " b=\"1\"" : string.Empty);
                var run = runChildren.Length == 0 ? $"<a:defRPr {attributes}/>" : $"<a:defRPr {attributes}>{runChildren}</a:defRPr>";
                var open = paragraphAttributes.Length == 0 ? $"<a:lvl{level}pPr>" : $"<a:lvl{level}pPr {paragraphAttributes}>";
                return $"{open}{paragraphChildren}{run}</a:lvl{level}pPr>";
            }

            private static string Sizes(int[] sizes)
            {
                var text = new StringBuilder();
                for (var i = 0; i < sizes.Length; i++)
                    text.Append(Level(i + 1, string.Empty, string.Empty, sizes[i], false, string.Empty));

                return text.ToString();
            }

            private static string Stepped(int[] sizes, string extraAttributes = "", bool bold = false, string runChildren = "")
            {
                var text = new StringBuilder();
                for (var i = 0; i < sizes.Length; i++)
                {
                    var attributes = $"marL=\"{(i * 457200).ToString(CultureInfo.InvariantCulture)}\" indent=\"0\"" + (extraAttributes.Length == 0 ? string.Empty : " " + extraAttributes);
                    text.Append(Level(i + 1, attributes, "<a:buNone/>", sizes[i], bold, runChildren));
                }

                return text.ToString();
            }

            private void Title(string type, string spPr, string bodyAttributes, string lstStyle)
            {
                var id = _nextId++;
                _shapes.Append(Shape(id, "Title " + (id - 1).ToString(CultureInfo.InvariantCulture), $" type=\"{type}\"", spPr, bodyAttributes, lstStyle, Paragraph(_titleText)));
            }

            private void CaptionTitle() =>
                Title("title", Xfrm(_frame.X(839788), _frame.Y(457200), _frame.W(3932237), _frame.H(1600200)), "anchor=\"b\"", Level(1, string.Empty, string.Empty, 3200, false, string.Empty));

            private void Content(string ph, string spPr, string lstStyle = "")
            {
                var id = _nextId++;
                _shapes.Append(Shape(id, "Content Placeholder " + (id - 1).ToString(CultureInfo.InvariantCulture), ph, spPr, string.Empty, lstStyle, LevelParagraphs()));
            }

            private void Text(string label, string ph, string spPr, string bodyAttributes, string lstStyle, string prompt)
            {
                var id = _nextId++;
                _shapes.Append(Shape(id, label + " " + (id - 1).ToString(CultureInfo.InvariantCulture), ph, spPr, bodyAttributes, lstStyle, prompt == null ? LevelParagraphs() : Paragraph(prompt)));
            }

            private void Picture(string spPr)
            {
                var id = _nextId++;
                _shapes.Append(Shape(id, "Picture Placeholder " + (id - 1).ToString(CultureInfo.InvariantCulture), " type=\"pic\" idx=\"1\"", spPr, string.Empty, Stepped(new[] { 3200, 2800, 2400, 2000, 2000 }), $"<a:p><a:endParaRPr lang=\"{_lang}\"/></a:p>"));
            }

            private void Footers()
            {
                if (_footers.Contains("dt"))
                {
                    var id = _nextId++;
                    _shapes.Append(Shape(id, "Date Placeholder " + (id - 1).ToString(CultureInfo.InvariantCulture), " type=\"dt\" sz=\"half\" idx=\"10\"", string.Empty, string.Empty, string.Empty, $"<a:p><a:fld id=\"{{6FCF9F07-3BC7-4570-B054-79111B0A380C}}\" type=\"datetimeFigureOut\"><a:rPr lang=\"{_lang}\"/><a:t>2000-01-01</a:t></a:fld><a:endParaRPr lang=\"{_lang}\"/></a:p>"));
                }

                if (_footers.Contains("ftr"))
                {
                    var id = _nextId++;
                    _shapes.Append(Shape(id, "Footer Placeholder " + (id - 1).ToString(CultureInfo.InvariantCulture), " type=\"ftr\" sz=\"quarter\" idx=\"11\"", string.Empty, string.Empty, string.Empty, $"<a:p><a:endParaRPr lang=\"{_lang}\"/></a:p>"));
                }

                if (_footers.Contains("sldNum"))
                {
                    var id = _nextId++;
                    _shapes.Append(Shape(id, "Slide Number Placeholder " + (id - 1).ToString(CultureInfo.InvariantCulture), " type=\"sldNum\" sz=\"quarter\" idx=\"12\"", string.Empty, string.Empty, string.Empty, $"<a:p><a:fld id=\"{{8F82E0A0-C266-4798-8C8F-B9F91E9DA37E}}\" type=\"slidenum\"><a:rPr lang=\"{_lang}\"/><a:t>‹#›</a:t></a:fld><a:endParaRPr lang=\"{_lang}\"/></a:p>"));
                }
            }

            private string Paragraph(string text) =>
                $"<a:p><a:r><a:rPr lang=\"{_lang}\"/><a:t>{SecurityElement.Escape(text)}</a:t></a:r><a:endParaRPr lang=\"{_lang}\"/></a:p>";

            private string LevelParagraphs()
            {
                var text = new StringBuilder();
                for (var i = 0; i < _levelText.Length; i++)
                {
                    text.Append($"<a:p><a:pPr lvl=\"{i.ToString(CultureInfo.InvariantCulture)}\"/><a:r><a:rPr lang=\"{_lang}\"/><a:t>{SecurityElement.Escape(_levelText[i])}</a:t></a:r>");
                    if (i == _levelText.Length - 1)
                        text.Append($"<a:endParaRPr lang=\"{_lang}\"/>");

                    text.Append("</a:p>");
                }

                return text.ToString();
            }

            private static string Shape(int id, string name, string ph, string spPr, string bodyAttributes, string lstStyle, string paragraphs)
            {
                var properties = spPr.Length == 0 ? "<p:spPr/>" : $"<p:spPr>{spPr}</p:spPr>";
                var body = bodyAttributes.Length == 0 ? "<a:bodyPr/>" : $"<a:bodyPr {bodyAttributes}/>";
                var styles = lstStyle.Length == 0 ? "<a:lstStyle/>" : $"<a:lstStyle>{lstStyle}</a:lstStyle>";
                return $"<p:sp><p:nvSpPr><p:cNvPr id=\"{id.ToString(CultureInfo.InvariantCulture)}\" name=\"{name}\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr><p:ph{ph}/></p:nvPr></p:nvSpPr>{properties}<p:txBody>{body}{styles}{paragraphs}</p:txBody></p:sp>";
            }
        }
    }
}
