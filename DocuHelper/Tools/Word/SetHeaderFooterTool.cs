using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class SetHeaderFooterTool : WordTool
    {
        private static readonly string[] s_presets = { "blank", "left", "center", "right", "three_column", "center_page", "page_x_of_y", "title_page" };
        private static readonly string[] s_formats = { "decimal", "upper_roman", "lower_roman", "upper_alpha", "lower_alpha" };

        public override string Name => "set_header_footer";

        public override string Description =>
            "Set the header or footer text, insert page numbers, switch 'different first page' / 'different odd and even pages', or add or remove a watermark. "
            + "Header and footer text can use {page}, {pages}, {date}, {time}, {file}, {title}, {author} which become live fields. left/center/right put text at the three positions of one line.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "part": {"type": "string", "enum": ["header", "footer"]},
                "section": {"description": "1-based section number or \"all\" (default).", "oneOf": [{"type": "integer", "minimum": 1}, {"type": "string", "enum": ["all"]}]},
                "variant": {"type": "string", "enum": ["default", "first", "even"], "description": "Which page the header/footer is for; default is every normal page."},
                "preset": {"type": "string", "enum": ["blank", "left", "center", "right", "three_column", "center_page", "page_x_of_y", "title_page"], "description": "Quick designs: left/center/right put `text` at that position; center_page = centered page number; page_x_of_y = centered \"1 / 3\"; title_page = document title left and page number right."},
                "text": {"type": "string"},
                "left": {"type": "string"},
                "center": {"type": "string"},
                "right": {"type": "string"},
                "page_number": {
                  "type": "object",
                  "properties": {
                    "position": {"type": "string", "enum": ["top", "bottom", "current", "none"], "description": "top = header, bottom = footer, current = field at the end of paragraph `at`, none = remove."},
                    "align": {"type": "string", "enum": ["left", "center", "right", "outside", "inside"]},
                    "style": {"type": "string", "enum": ["plain", "page_x_of_y", "dash", "brackets"], "description": "1 · 1 / 3 · - 1 - · [1]"},
                    "format": {"type": "string", "enum": ["decimal", "upper_roman", "lower_roman", "upper_alpha", "lower_alpha"]},
                    "start_at": {"type": "integer", "minimum": 0}
                  }
                },
                "at": {"type": "string", "description": "Paragraph id for page_number position current."},
                "different_first_page": {"type": "boolean"},
                "different_odd_even": {"type": "boolean"},
                "link_to_previous": {"type": "boolean", "description": "Sections 2+: use the previous section's header/footer."},
                "remove": {"type": "boolean", "description": "Delete the header/footer content for the chosen part/variant."},
                "watermark": {
                  "type": "object",
                  "properties": {
                    "action": {"type": "string", "enum": ["add", "remove"]},
                    "text": {"type": "string"},
                    "font": {"type": "string"},
                    "size_pt": {"type": "number", "description": "Omit for automatic size."},
                    "color": {"type": "string"},
                    "semi_transparent": {"type": "boolean"},
                    "layout": {"type": "string", "enum": ["diagonal", "horizontal"]}
                  },
                  "required": ["action"]
                }
              }
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.None;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var request = Request.Parse(input);
            var snapshot = docu.Capture();
            try
            {
                return new Session(docu, request).Apply();
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private sealed class Request
        {
            public bool? Header { get; private init; }

            public int? Section { get; private init; }

            public string Variant { get; private init; }

            public bool VariantGiven { get; private init; }

            public string Preset { get; private init; }

            public string Text { get; private init; }

            public string Left { get; private init; }

            public string Center { get; private init; }

            public string Right { get; private init; }

            public JsonElement? PageNumber { get; private init; }

            public string At { get; private init; }

            public bool? FirstPage { get; private init; }

            public bool? OddEven { get; private init; }

            public bool? Link { get; private init; }

            public bool Remove { get; private init; }

            public JsonElement? Watermark { get; private init; }

            public bool HasContent => Preset != null || Text != null || Left != null || Center != null || Right != null;

            public static Request Parse(ToolInput input)
            {
                var part = input.GetOptionalString("part")?.Trim().ToLowerInvariant();
                if (part != null && part != "header" && part != "footer")
                    throw new ToolInputException("'part' 값은 header(머리글) 또는 footer(바닥글)여야 해요.");

                int? section = null;
                if (input.Has("section"))
                {
                    var element = input.Get("section");
                    if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number) && number >= 1)
                        section = number;
                    else if (!(element.ValueKind == JsonValueKind.String && string.Equals(element.GetString(), "all", StringComparison.OrdinalIgnoreCase)))
                        throw new ToolInputException("'section' 값은 1 이상의 구역 번호나 \"all\"이어야 해요.");
                }

                var preset = input.GetOptionalString("preset")?.Trim().ToLowerInvariant();
                if (preset != null && !s_presets.Contains(preset))
                    throw new ToolInputException($"'preset' 값은 {string.Join(", ", s_presets)} 중 하나여야 해요.");

                var request = new Request
                {
                    Header = part == null ? null : part == "header",
                    Section = section,
                    Variant = (input.GetOptionalString("variant") ?? "default").Trim().ToLowerInvariant(),
                    VariantGiven = input.Has("variant"),
                    Preset = preset,
                    Text = Slot(input, "text"),
                    Left = Slot(input, "left"),
                    Center = Slot(input, "center"),
                    Right = Slot(input, "right"),
                    PageNumber = input.Has("page_number") ? Object(input, "page_number") : null,
                    At = input.GetOptionalString("at"),
                    FirstPage = input.GetOptionalBool("different_first_page"),
                    OddEven = input.GetOptionalBool("different_odd_even"),
                    Link = input.GetOptionalBool("link_to_previous"),
                    Remove = input.GetOptionalBool("remove") ?? false,
                    Watermark = input.Has("watermark") ? Object(input, "watermark") : null,
                };

                WordHeaderFooters.Type(request.Variant);
                if (request.Watermark != null)
                    request.CheckWatermark();

                if (request.PageNumber != null)
                    request.CheckPageNumber();

                if (!request.HasContent && !request.Remove && request.PageNumber == null && request.FirstPage == null && request.OddEven == null
                    && request.Link == null && request.Watermark == null)
                    throw new ToolInputException("바꿀 내용을 하나 이상 넣어 주세요. 예: preset, text, page_number, different_first_page, watermark.");

                if ((request.HasContent || request.Remove || request.Link != null) && request.Header == null)
                    throw new ToolInputException("'part'에 header(머리글)나 footer(바닥글)를 넣어 주세요.");

                if (request.HasContent && request.Remove)
                    throw new ToolInputException("내용을 넣는 것과 지우는 것(remove)은 한 번에 할 수 없어요.");

                if (preset is "left" or "center" or "right" && request.Text == null && request.SlotOf(preset) == null)
                    throw new ToolInputException($"preset이 {preset}이면 'text'에 넣을 글을 적어 주세요.");

                return request;
            }

            public string SlotOf(string name) => name switch
            {
                "left" => Left,
                "center" => Center,
                _ => Right,
            };

            public JsonElement Number => PageNumber.Value;

            public string NumberText(string name) =>
                PageNumber.Value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;

            public JsonElement Mark => Watermark.Value;

            private static JsonElement? Object(ToolInput input, string name)
            {
                var element = input.Get(name);
                if (element.ValueKind != JsonValueKind.Object)
                    throw new ToolInputException($"'{name}' 값은 항목이 들어 있는 객체여야 해요.");

                return element.Clone();
            }

            private static string Slot(ToolInput input, string name)
            {
                var text = input.GetOptionalString(name);
                if (text == null)
                    return null;

                text = WordText.Clean(text);
                if (text.Length > WordHeaderFooters.MaxSlotLength)
                    throw new ToolInputException($"'{name}'은 {WordHeaderFooters.MaxSlotLength}자까지만 쓸 수 있어요.");

                return text;
            }

            private void CheckWatermark()
            {
                var action = Mark.TryGetProperty("action", out var item) ? item.GetString() : null;
                if (action is not ("add" or "remove"))
                    throw new ToolInputException("'watermark.action' 값은 add 또는 remove여야 해요.");

                if (action == "add")
                {
                    var text = Mark.TryGetProperty("text", out var textItem) && textItem.ValueKind == JsonValueKind.String ? WordText.Clean(textItem.GetString()).Replace("\n", " ").Trim() : string.Empty;
                    if (text.Length == 0)
                        throw new ToolInputException("워터마크 글자('watermark.text')를 넣어 주세요. 예: 기밀, 초안");

                    if (text.Length > WordWatermark.MaxLength)
                        throw new ToolInputException($"워터마크 글자는 {WordWatermark.MaxLength}자까지만 쓸 수 있어요.");

                    if (Mark.TryGetProperty("size_pt", out var size) && size.ValueKind == JsonValueKind.Number)
                        WordUnits.Check(size.GetDouble(), 8, 500, "watermark.size_pt", "pt");

                    if (Mark.TryGetProperty("color", out var color) && color.ValueKind == JsonValueKind.String)
                        WordCellFormat.HexColor(color.GetString(), "watermark.color");

                    if (Mark.TryGetProperty("layout", out var layout) && layout.ValueKind == JsonValueKind.String && layout.GetString() is not ("diagonal" or "horizontal"))
                        throw new ToolInputException("'watermark.layout' 값은 diagonal 또는 horizontal이어야 해요.");
                }
            }

            private void CheckPageNumber()
            {
                var position = NumberText("position");
                if (position != null && position is not ("top" or "bottom" or "current" or "none"))
                    throw new ToolInputException("'page_number.position' 값은 top, bottom, current, none 중 하나여야 해요.");

                var align = NumberText("align");
                if (align != null && align is not ("left" or "center" or "right" or "outside" or "inside"))
                    throw new ToolInputException("'page_number.align' 값은 left, center, right, outside, inside 중 하나여야 해요.");

                var style = NumberText("style");
                if (style != null && style is not ("plain" or "page_x_of_y" or "dash" or "brackets"))
                    throw new ToolInputException("'page_number.style' 값은 plain, page_x_of_y, dash, brackets 중 하나여야 해요.");

                var format = NumberText("format");
                if (format != null && !s_formats.Contains(format))
                    throw new ToolInputException($"'page_number.format' 값은 {string.Join(", ", s_formats)} 중 하나여야 해요.");

                if (position == "current" && string.IsNullOrWhiteSpace(At))
                    throw new ToolInputException("page_number.position이 current이면 'at'에 쪽 번호를 넣을 문단 id를 적어 주세요.");

                if (position == null && format == null && !Number.TryGetProperty("start_at", out _))
                    throw new ToolInputException("'page_number'에 position, format, start_at 중 하나 이상을 넣어 주세요.");
            }
        }

        private sealed class Session
        {
            private readonly WordDocu _docu;
            private readonly Request _request;
            private readonly List<SectionProperties> _sections;
            private readonly HashSet<string> _used;
            private readonly List<string> _details = new();
            private readonly List<string> _anchors = new();
            private readonly HashSet<bool> _kinds = new();
            private readonly int _pages;

            public Session(WordDocu docu, Request request)
            {
                _docu = docu;
                _request = request;
                _sections = WordParts.Sections(docu).ToList();
                _used = WordIds.Collect(docu.Document);
                _pages = WordScan.Read(docu.Document).PageCount;
            }

            public ToolResult Apply()
            {
                if (_request.Section > _sections.Count)
                    throw new ToolInputException($"구역 {_request.Section}은(는) 없어요. 이 문서는 구역이 {_sections.Count}개예요.");

                if (_request.FirstPage != null)
                    FirstPage(_request.FirstPage.Value);

                if (_request.OddEven != null)
                    OddEven(_request.OddEven.Value);

                if (_request.Link != null)
                    Link(_request.Link.Value);

                if (_request.HasContent)
                    Content();

                if (_request.Remove)
                    Remove();

                if (_request.PageNumber != null)
                    PageNumber();

                if (_request.Watermark != null)
                    Watermark();

                if (_details.Count == 0)
                    throw new ToolInputException("바뀐 내용이 없어요. 지우려는 머리글·바닥글이 있는지, 구역 번호가 맞는지 확인해 주세요.");

                var location = _kinds.Count == 1 ? (_kinds.First() ? "머리글" : "바닥글") : "문서 전체";
                var anchors = _anchors.Distinct().ToList();
                var summary = string.Join(", ", _details);
                return ToolResult.Changed($"Updated header/footer: {summary}.", Change(location, "panel-top", anchors), summary);
            }

            private IEnumerable<int> All() => _request.Section == null ? Enumerable.Range(0, _sections.Count) : new[] { _request.Section.Value - 1 };

            private IEnumerable<int> Owners(bool header, HeaderFooterValues type)
            {
                if (_request.Section != null)
                    return new[] { _request.Section.Value - 1 };

                return Enumerable.Range(0, _sections.Count).Where(i => i == 0 || WordHeaderFooters.Own(_docu, _sections[i], header, type) != null).ToList();
            }

            private IEnumerable<int> OwnersOfAny(bool header) =>
                _request.Section != null
                    ? new[] { _request.Section.Value - 1 }
                    : Enumerable.Range(0, _sections.Count).Where(i => i == 0 || WordHeaderFooters.OwnsAny(_docu, _sections[i], header)).ToList();

            private void Touch(bool header, int index)
            {
                _kinds.Add(header);
                _anchors.Add(WordAnchors.HeaderFooter(header ? WordAnchors.Header : WordAnchors.Footer, index + 1));
            }

            private void Mark(int index) => _anchors.Add(WordAnchors.Section(index + 1));

            private string PageText(int index)
            {
                var numbers = _sections[index].GetFirstChild<PageNumberType>();
                var start = WordSections.Twips(numbers?.Start, 1);
                return WordHeaderFooters.Format(start, WordHeaderFooters.FormatName(_sections[index]));
            }

            private void FirstPage(bool on)
            {
                foreach (var index in All())
                {
                    var section = _sections[index];
                    if (on)
                    {
                        WordSections.Set(section, new TitlePage());
                        if (index == 0 || WordHeaderFooters.OwnsAny(_docu, section, true) || WordHeaderFooters.OwnsAny(_docu, section, false))
                            EnsureEmpty(section, HeaderFooterValues.First);
                    }
                    else
                    {
                        section.RemoveAllChildren<TitlePage>();
                    }

                    Mark(index);
                }

                _details.Add(on ? "첫 쪽 다르게" : "첫 쪽 같게");
            }

            private void OddEven(bool on)
            {
                var settings = WordParts.EnsureSettings(_docu);
                settings.RemoveAllChildren<EvenAndOddHeaders>();
                if (on)
                {
                    settings.AddChild(new EvenAndOddHeaders());
                    foreach (var index in All())
                    {
                        var section = _sections[index];
                        if (index == 0 || WordHeaderFooters.OwnsAny(_docu, section, true) || WordHeaderFooters.OwnsAny(_docu, section, false))
                            EnsureEmpty(section, HeaderFooterValues.Even);

                        Mark(index);
                    }
                }
                else
                {
                    foreach (var index in All())
                        Mark(index);
                }

                _details.Add(on ? "홀짝 쪽 다르게" : "홀짝 쪽 같게");
            }

            private void EnsureEmpty(SectionProperties section, HeaderFooterValues type)
            {
                foreach (var header in new[] { true, false })
                {
                    if (WordHeaderFooters.Effective(_docu, section, header, type) == null)
                    {
                        var root = WordHeaderFooters.Ensure(_docu, section, header, type);
                        WordHeaderFooters.Clear(root, _docu, header, _used);
                    }
                }
            }

            private void AutoSwitch(HeaderFooterValues type, IEnumerable<int> indices)
            {
                if (type == HeaderFooterValues.First && _request.FirstPage != false)
                {
                    foreach (var index in indices)
                    {
                        if (!WordStyles.IsOn(_sections[index].GetFirstChild<TitlePage>()))
                        {
                            WordSections.Set(_sections[index], new TitlePage());
                            Mark(index);
                            _details.Add("첫 쪽 다르게");
                        }
                    }
                }
                else if (type == HeaderFooterValues.Even && _request.OddEven != false)
                {
                    var settings = WordParts.EnsureSettings(_docu);
                    if (settings.GetFirstChild<EvenAndOddHeaders>() == null)
                    {
                        settings.AddChild(new EvenAndOddHeaders());
                        _details.Add("홀짝 쪽 다르게");
                    }
                }
            }

            private void Content()
            {
                var header = _request.Header.Value;
                var type = WordHeaderFooters.Type(_request.Variant);
                string left = null, center = null, right = null;
                switch (_request.Preset)
                {
                    case "left":
                        left = _request.Text;
                        break;
                    case "center":
                        center = _request.Text;
                        break;
                    case "right":
                        right = _request.Text;
                        break;
                    case "center_page":
                        center = "{page}";
                        break;
                    case "page_x_of_y":
                        center = "{page} / {pages}";
                        break;
                    case "title_page":
                        left = "{title}";
                        right = "{page}";
                        break;
                    case null:
                        left = _request.Text;
                        break;
                }

                left = _request.Left ?? left;
                center = _request.Center ?? center;
                right = _request.Right ?? right;
                var indices = Owners(header, type).ToList();
                AutoSwitch(type, indices);
                foreach (var index in indices)
                {
                    var section = _sections[index];
                    var root = WordHeaderFooters.Ensure(_docu, section, header, type);
                    var values = WordHeaderFooters.Values(_docu, _pages, PageText(index));
                    WordHeaderFooters.Replace(root, new[] { WordHeaderFooters.Line(_docu, section, header, left, center, right, values, _used) });
                    Touch(header, index);
                }

                _details.Add(header ? "머리글 고침" : "바닥글 고침");
            }

            private void Remove()
            {
                var header = _request.Header.Value;
                var type = WordHeaderFooters.Type(_request.Variant);
                var count = 0;
                foreach (var index in Owners(header, type))
                {
                    var root = WordHeaderFooters.Own(_docu, _sections[index], header, type);
                    if (root == null)
                        continue;

                    WordHeaderFooters.Clear(root, _docu, header, _used);
                    WordWatermark.Remove(root);
                    Touch(header, index);
                    count++;
                }

                if (count == 0)
                    throw new ToolInputException(header ? "지울 머리글이 없어요." : "지울 바닥글이 없어요.");

                _details.Add(header ? "머리글 지움" : "바닥글 지움");
            }

            private void Link(bool on)
            {
                var header = _request.Header.Value;
                var types = _request.VariantGiven ? new[] { WordHeaderFooters.Type(_request.Variant) } : WordHeaderFooters.Types.ToArray();
                var indices = All().ToList();
                if (indices.Contains(0))
                {
                    if (_request.Section == null)
                        indices.Remove(0);
                    else
                        throw new ToolInputException("첫 번째 구역은 이전 구역이 없어서 연결을 바꿀 수 없어요.");
                }

                var changed = false;
                foreach (var index in indices)
                {
                    if (on)
                    {
                        var owned = types.Where(t => WordHeaderFooters.Own(_docu, _sections[index], header, t) != null).ToList();
                        WordHeaderFooters.Unlink(_docu, _sections, index, header, owned);
                        changed |= owned.Count > 0;
                    }
                    else
                    {
                        changed |= WordHeaderFooters.Link(_docu, _sections, index, header, types, _used);
                    }

                    Touch(header, index);
                }

                if (!changed)
                    throw new ToolInputException(on ? "이미 이전 구역과 같은 내용을 쓰고 있어요." : "이전 구역에서 가져올 내용이 없거나 이미 따로 쓰고 있어요.");

                _details.Add(on ? "이전 구역과 연결" : "이전 구역과 연결 끊음");
            }

            private void PageNumber()
            {
                var position = _request.NumberText("position");
                var format = _request.NumberText("format");
                if (format != null || _request.Number.TryGetProperty("start_at", out _))
                    NumberFormat(format);

                if (position == null)
                    return;

                var align = _request.NumberText("align") ?? "center";
                var style = _request.NumberText("style") ?? "plain";
                switch (position)
                {
                    case "none":
                        RemoveNumbers();
                        break;
                    case "current":
                        Current(style);
                        break;
                    default:
                        Place(position == "top", align, style);
                        break;
                }
            }

            private void NumberFormat(string format)
            {
                var start = _request.Number.TryGetProperty("start_at", out var item) && item.ValueKind == JsonValueKind.Number ? item.GetInt32() : (int?)null;
                if (start is < 0)
                    throw new ToolInputException("'page_number.start_at' 값은 0 이상이어야 해요.");

                foreach (var index in All())
                {
                    var section = _sections[index];
                    var numbers = section.GetFirstChild<PageNumberType>() ?? new PageNumberType();
                    var changed = false;
                    if (format != null)
                    {
                        numbers.Format = WordHeaderFooters.FormatValue(format);
                        changed = true;
                    }

                    if (start != null && (_request.Section != null || index == 0))
                    {
                        numbers.Start = start.Value;
                        changed = true;
                    }

                    if (!changed)
                        continue;

                    if (numbers.Parent == null)
                        WordSections.Set(section, numbers);

                    Mark(index);
                }

                _details.Add(format != null ? "쪽 번호 모양" : "쪽 번호 시작");
            }

            private void Place(bool header, string align, string style)
            {
                var jobs = new List<(HeaderFooterValues Type, string Align)>();
                if (align is "outside" or "inside")
                {
                    var settings = WordParts.EnsureSettings(_docu);
                    if (settings.GetFirstChild<EvenAndOddHeaders>() == null)
                        settings.AddChild(new EvenAndOddHeaders());

                    var odd = align == "outside" ? "right" : "left";
                    jobs.Add((HeaderFooterValues.Default, odd));
                    jobs.Add((HeaderFooterValues.Even, odd == "right" ? "left" : "right"));
                }
                else
                {
                    var type = WordHeaderFooters.Type(_request.Variant);
                    AutoSwitch(type, Owners(header, type));
                    jobs.Add((type, align));
                }

                foreach (var (type, jobAlign) in jobs)
                {
                    foreach (var index in Owners(header, type).ToList())
                    {
                        var section = _sections[index];
                        var root = WordHeaderFooters.Ensure(_docu, section, header, type);
                        var values = WordHeaderFooters.Values(_docu, _pages, PageText(index));
                        var line = WordHeaderFooters.NumberLine(_docu, header, jobAlign, style, values, _used);
                        var existing = root.Elements<Paragraph>().FirstOrDefault(WordHeaderFooters.IsNumberLine);
                        if (existing != null)
                        {
                            existing.InsertAfterSelf(line);
                            existing.Remove();
                        }
                        else if (!WordHeaderFooters.HasText(root))
                        {
                            WordHeaderFooters.Replace(root, new[] { line });
                        }
                        else
                        {
                            root.AppendChild(line);
                        }

                        Touch(header, index);
                    }
                }

                _details.Add("쪽 번호 넣음");
            }

            private void RemoveNumbers()
            {
                var count = 0;
                foreach (var header in new[] { true, false })
                {
                    foreach (var index in OwnersOfAny(header))
                    {
                        foreach (var type in WordHeaderFooters.Types)
                        {
                            var root = WordHeaderFooters.Own(_docu, _sections[index], header, type);
                            if (root == null)
                                continue;

                            var lines = root.Elements<Paragraph>().Where(WordHeaderFooters.IsNumberLine).ToList();
                            if (lines.Count == 0)
                                continue;

                            foreach (var line in lines)
                                line.Remove();

                            if (!root.Elements<Paragraph>().Any())
                                root.AppendChild(WordHeaderFooters.Blank(_docu, header, _used));

                            Touch(header, index);
                            count++;
                        }
                    }
                }

                if (count == 0)
                    throw new ToolInputException("지울 쪽 번호가 없어요.");

                _details.Add("쪽 번호 뺌");
            }

            private void Current(string style)
            {
                var scan = WordScan.Read(_docu.Document);
                var block = scan.Find(_request.At) ?? throw NotFound(_request.At);
                if (block.IsTable)
                    throw new ToolInputException("쪽 번호는 문단에만 넣을 수 있어요. 표 안이 아닌 문단 id를 넣어 주세요.");

                var index = _sections.IndexOf(WordSections.Containing(scan.Body, block.Element));
                var values = WordHeaderFooters.Values(_docu, _pages, PageText(Math.Max(0, index)));
                foreach (var run in WordHeaderFooters.NumberRuns(style, values))
                    block.Paragraph.AppendChild(run);

                _anchors.Add(WordAnchors.Paragraph(block.Id));
                _details.Add("쪽 번호 넣음");
            }

            private void Watermark()
            {
                var action = _request.Mark.GetProperty("action").GetString();
                var roots = new List<OpenXmlCompositeElement>();
                if (action == "remove")
                {
                    if (_request.Section == null)
                    {
                        roots.AddRange(_docu.Document.MainDocumentPart.HeaderParts.Select(p => p.Header).Where(h => h != null));
                    }
                    else
                    {
                        foreach (var type in WordHeaderFooters.Types)
                        {
                            var root = WordHeaderFooters.Own(_docu, _sections[_request.Section.Value - 1], true, type);
                            if (root != null)
                                roots.Add(root);
                        }
                    }

                    var removed = roots.Count(WordWatermark.Remove);
                    if (removed == 0)
                        throw new ToolInputException("지울 워터마크가 없어요.");

                    foreach (var index in All())
                        Touch(true, index);

                    _details.Add("워터마크 뺌");
                    return;
                }

                var text = WordText.Clean(_request.Mark.GetProperty("text").GetString()).Replace("\n", " ").Trim();
                var font = _request.Mark.TryGetProperty("font", out var fontItem) && fontItem.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(fontItem.GetString()) ? fontItem.GetString().Trim() : "맑은 고딕";
                double? size = _request.Mark.TryGetProperty("size_pt", out var sizeItem) && sizeItem.ValueKind == JsonValueKind.Number ? sizeItem.GetDouble() : null;
                var color = _request.Mark.TryGetProperty("color", out var colorItem) && colorItem.ValueKind == JsonValueKind.String ? WordCellFormat.HexColor(colorItem.GetString(), "watermark.color") : "C0C0C0";
                var transparent = !(_request.Mark.TryGetProperty("semi_transparent", out var transparentItem) && transparentItem.ValueKind == JsonValueKind.False);
                var diagonal = !(_request.Mark.TryGetProperty("layout", out var layoutItem) && layoutItem.ValueKind == JsonValueKind.String && layoutItem.GetString() == "horizontal");
                var even = _docu.Document.MainDocumentPart.DocumentSettingsPart?.Settings?.GetFirstChild<EvenAndOddHeaders>() != null;
                foreach (var index in OwnersOfAny(true))
                {
                    var section = _sections[index];
                    var types = new List<HeaderFooterValues> { HeaderFooterValues.Default };
                    if (WordStyles.IsOn(section.GetFirstChild<TitlePage>()))
                        types.Add(HeaderFooterValues.First);

                    if (even)
                        types.Add(HeaderFooterValues.Even);

                    foreach (var type in types)
                    {
                        var root = WordHeaderFooters.Ensure(_docu, section, true, type);
                        WordWatermark.Add(_docu, root, text, font, size, color, transparent, diagonal, true);
                    }

                    Touch(true, index);
                }

                _details.Add("워터마크 넣음");
            }
        }
    }
}
