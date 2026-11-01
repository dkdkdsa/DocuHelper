using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class InsertFieldTool : WordTool
    {
        private static readonly string[] s_actions = { "insert", "update" };
        private static readonly string[] s_kinds = { "date", "time", "date_time", "page", "num_pages", "section_pages", "section", "file_name", "file_path", "author", "title", "subject", "merge_field", "custom" };
        private static readonly string[] s_scopes = { "all", "toc", "page_refs", "sequences" };
        private static readonly string[] s_formats = { "decimal", "upper_roman", "lower_roman", "upper_alpha", "lower_alpha" };

        public override string Name => "insert_field";

        public override string Description =>
            "Insert a field (date, time, page number, page count, file name, author, title, merge field, or a SEQ/REF-style field) at a caret, "
            + "or update the stored results of all fields. A date or time with update_automatically false is inserted as plain text.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "action": {"type": "string", "enum": ["insert", "update"], "description": "Default insert."},
                "kind": {"type": "string", "enum": ["date", "time", "date_time", "page", "num_pages", "section_pages", "section", "file_name", "file_path", "author", "title", "subject", "merge_field", "custom"]},
                "at": {
                  "description": "Where: a paragraph id, {id, offset} for a caret, or {id, start, end} / {id, match, occurrence} for a text range.",
                  "oneOf": [
                    {"type": "string"},
                    {
                      "type": "object",
                      "properties": {
                        "id": {"type": "string"},
                        "offset": {"type": "integer", "minimum": 0},
                        "start": {"type": "integer", "minimum": 0},
                        "end": {"type": "integer", "minimum": 0},
                        "match": {"type": "string"},
                        "occurrence": {"type": "integer", "minimum": 0},
                        "row": {"type": "integer"},
                        "column": {"type": "integer"}
                      },
                      "required": ["id"]
                    }
                  ]
                },
                "format": {"type": "string", "description": "date/time picture such as \"yyyy. M. d.\", \"yyyy년 M월 d일\", \"tt h:mm\"; page numbers: \"decimal\", \"upper_roman\", \"lower_roman\", \"upper_alpha\", \"lower_alpha\"."},
                "update_automatically": {"type": "boolean", "description": "date/time: true inserts a DATE/TIME field that Word refreshes; default false (plain text)."},
                "name": {"type": "string", "description": "merge_field: the data column name."},
                "instruction": {"type": "string", "description": "custom: field code such as \"SEQ Chapter \\\\* ARABIC\". Allowed: SEQ, REF, PAGEREF, NOTEREF, STYLEREF, SECTION, SECTIONPAGES, DOCPROPERTY, DATE, TIME, PAGE, NUMPAGES, FILENAME, AUTHOR, TITLE, SUBJECT, MERGEFIELD, SET, IF with those only. INCLUDETEXT, INCLUDEPICTURE, LINK, DDE, DDEAUTO, MACROBUTTON, IMPORT, AUTOTEXT, ASK, FILLIN, DATABASE are refused."},
                "scope": {"type": "string", "enum": ["all", "toc", "page_refs", "sequences"], "description": "update only."}
              }
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.None;

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var action = (input.GetOptionalString("action") ?? "insert").Trim().ToLowerInvariant();
            if (Array.IndexOf(s_actions, action) < 0)
                throw new ToolInputException("'action' 값은 insert 또는 update여야 해요.");

            var snapshot = docu.Capture();
            try
            {
                return action == "update" ? Update(docu, input) : Insert(docu, input);
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static ToolResult Update(WordDocu docu, ToolInput input)
        {
            var scope = (input.GetOptionalString("scope") ?? "all").Trim().ToLowerInvariant();
            if (Array.IndexOf(s_scopes, scope) < 0)
                throw new ToolInputException("'scope' 값은 all, toc, page_refs, sequences 중 하나여야 해요.");

            var count = WordFieldUpdater.Update(docu, scope);
            if (count == 0)
                return ToolResult.Ok("No stored field results needed updating.", "바뀐 필드가 없어요.");

            var scan = WordScan.Read(docu.Document);
            return ToolResult.Changed($"Updated {count} field result(s).", Change(WordScan.PageLabel(scan.Blocks), "file-text", Array.Empty<string>()), $"필드 {count}개 갱신");
        }

        private static ToolResult Insert(WordDocu docu, ToolInput input)
        {
            var kind = input.GetString("kind").Trim().ToLowerInvariant();
            if (Array.IndexOf(s_kinds, kind) < 0)
                throw new ToolInputException($"'kind' 값은 {string.Join(", ", s_kinds)} 중 하나여야 해요.");

            var scan = WordScan.Read(docu.Document);
            var point = Point(scan, input);
            var paragraph = point.Paragraph;
            var map = WordTextMap.Build(paragraph);
            var ctx = WordRevisions.Active(docu);
            var temporal = kind == "date" || kind == "time" || kind == "date_time";
            if (temporal && !(input.GetOptionalBool("update_automatically") ?? false))
            {
                var value = WordFieldUpdater.Format(DateTime.Now, Picture(input, kind));
                WordInline.Track(ctx, new[] { map.InsertRunAt(point.Offset, value, null) });
            }
            else
            {
                WordInline.Track(ctx, WordInline.Place(map, point.Offset, Fields(docu, scan, input, kind, paragraph)));
            }

            var fresh = WordScan.Read(docu.Document);
            var id = WordIds.Of(paragraph);
            var anchors = id == null ? new List<string>() : new List<string> { WordAnchors.Paragraph(id) };
            return ToolResult.Changed(
                $"Inserted a {kind.Replace('_', ' ')} field.",
                Change(WordInline.LocationOf(fresh, paragraph), temporal ? "calendar" : "file-text", anchors),
                temporal ? "날짜 넣음" : "필드 넣음");
        }

        private static IReadOnlyList<OpenXmlElement> Fields(WordDocu docu, WordScan scan, ToolInput input, string kind, Paragraph paragraph)
        {
            switch (kind)
            {
                case "date":
                case "time":
                case "date_time":
                {
                    var picture = Picture(input, kind);
                    var code = kind == "time" ? "TIME" : "DATE";
                    return Complex($"{code} \\@ \"{picture}\"", WordFieldUpdater.Format(DateTime.Now, picture));
                }

                case "page":
                {
                    var format = PageFormat(input);
                    return Complex($"PAGE{NumberSwitch(format)}", Numeral(WordInline.PageOf(scan, paragraph), format));
                }

                case "num_pages":
                    return Complex("NUMPAGES", scan.PageCount.ToString(CultureInfo.InvariantCulture));

                case "section_pages":
                    return Complex("SECTIONPAGES", SectionPages(scan, paragraph).ToString(CultureInfo.InvariantCulture));

                case "section":
                    return Complex("SECTION", SectionNumber(scan, paragraph).ToString(CultureInfo.InvariantCulture));

                case "file_name":
                    return Complex("FILENAME \\* MERGEFORMAT", docu.Name);

                case "file_path":
                    return Complex("FILENAME \\p \\* MERGEFORMAT", docu.FilePath);

                case "author":
                    return Complex("AUTHOR \\* MERGEFORMAT", docu.Document.PackageProperties.Creator ?? string.Empty);

                case "title":
                    return Complex("TITLE \\* MERGEFORMAT", docu.Document.PackageProperties.Title ?? string.Empty);

                case "subject":
                    return Complex("SUBJECT \\* MERGEFORMAT", docu.Document.PackageProperties.Subject ?? string.Empty);

                case "merge_field":
                {
                    var name = input.GetString("name").Trim();
                    if (name.Length > 64 || name.Any(c => char.IsControl(c) || c == '"' || c == '\\'))
                        throw new ToolInputException("병합 필드 이름('name')은 큰따옴표와 역슬래시를 쓸 수 없고 64자 이내여야 해요.");

                    var code = name.Any(char.IsWhiteSpace) ? $"\"{name}\"" : name;
                    return Complex($"MERGEFIELD {code} \\* MERGEFORMAT", $"«{name}»");
                }

                default:
                {
                    var instruction = input.GetString("instruction").Trim();
                    if (!WordFields.IsAllowed(instruction, out var reason))
                        throw new ToolInputException(reason);

                    return Complex(instruction, string.Empty);
                }
            }
        }

        private static WordPoint Point(WordScan scan, ToolInput input)
        {
            if (input.Has("at"))
                return WordRangeTarget.ResolveCaret(scan, input, "at");

            var last = scan.Blocks.LastOrDefault(b => !b.IsTable && b.Paragraph != null)
                ?? throw new ToolInputException("필드를 넣을 문단이 없어요.");
            return new WordPoint(last, null, last.Paragraph, WordTextMap.Build(last.Paragraph).Length);
        }

        private static string Picture(ToolInput input, string kind)
        {
            var format = input.GetOptionalString("format")?.Trim();
            var picture = string.IsNullOrEmpty(format) ? DefaultPicture(kind) : format;
            if (picture.Length > 60 || picture.Contains('"'))
                throw new ToolInputException("날짜나 시간 형식('format')은 60자 이내이고 큰따옴표를 쓸 수 없어요.");

            return picture;
        }

        private static string DefaultPicture(string kind) => kind switch
        {
            "time" => "tt h:mm",
            "date_time" => "yyyy. M. d. tt h:mm",
            _ => "yyyy. M. d.",
        };

        private static string PageFormat(ToolInput input)
        {
            var format = (input.GetOptionalString("format") ?? "decimal").Trim().ToLowerInvariant();
            if (Array.IndexOf(s_formats, format) < 0)
                throw new ToolInputException("쪽 번호 형식('format')은 decimal, upper_roman, lower_roman, upper_alpha, lower_alpha 중 하나여야 해요.");

            return format;
        }

        private static string NumberSwitch(string format) => format switch
        {
            "upper_roman" => " \\* ROMAN",
            "lower_roman" => " \\* roman",
            "upper_alpha" => " \\* ALPHABETIC",
            "lower_alpha" => " \\* alphabetic",
            _ => " \\* ARABIC",
        };

        private static string Numeral(int value, string format) => format switch
        {
            "upper_roman" => Roman(value),
            "lower_roman" => Roman(value).ToLowerInvariant(),
            "upper_alpha" => Alpha(value),
            "lower_alpha" => Alpha(value).ToLowerInvariant(),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };

        private static string Roman(int value)
        {
            var numerals = new (int Value, string Text)[]
            {
                (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
            };
            var builder = new StringBuilder();
            var rest = Math.Max(1, value);
            foreach (var (number, text) in numerals)
            {
                while (rest >= number)
                {
                    builder.Append(text);
                    rest -= number;
                }
            }

            return builder.ToString();
        }

        private static string Alpha(int value)
        {
            var number = Math.Max(1, value);
            return new string((char)('A' + (number - 1) % 26), (number - 1) / 26 + 1);
        }

        private static int SectionPages(WordScan scan, Paragraph paragraph)
        {
            var page = WordInline.PageOf(scan, paragraph);
            var section = page >= 1 && page <= scan.PageSections.Count ? scan.PageSections[page - 1] : 0;
            return Math.Max(1, scan.PageSections.Count(s => s == section));
        }

        private static int SectionNumber(WordScan scan, Paragraph paragraph)
        {
            var number = 1;
            foreach (var element in scan.Body.Descendants())
            {
                if (ReferenceEquals(element, paragraph))
                    break;

                if (element is SectionProperties)
                    number++;
            }

            return number;
        }

        private static IReadOnlyList<OpenXmlElement> Complex(string instruction, string value) =>
            WordFields.Complex(instruction, value, null).ToList();
    }
}
