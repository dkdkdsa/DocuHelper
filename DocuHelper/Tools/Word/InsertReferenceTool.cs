using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocuHelper.Importers.Hwp;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public class InsertReferenceTool : WordTool
    {
        private static readonly string[] s_actions = { "insert", "edit", "delete" };
        private static readonly string[] s_kinds = { "hyperlink", "bookmark", "footnote", "endnote", "caption", "cross_reference", "index_entry" };
        private static readonly string[] s_crossTypes = { "caption", "heading", "bookmark", "footnote", "endnote" };
        private static readonly string[] s_shows = { "label_and_number", "only_label", "number", "text", "page_number", "above_below" };

        private sealed record Spot(Paragraph Paragraph, int Start, int End);

        public override string Name => "insert_reference";

        public override string Description =>
            "Add or remove hyperlinks, bookmarks, footnotes, endnotes, captions (Table 1, Figure 2), cross-references to those, and index entries. "
            + "kind picks the thing; at says where. Caption numbers and cross-references are live fields that renumber themselves. "
            + "Only http, https, mailto and tel links are allowed.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "action": {"type": "string", "enum": ["insert", "edit", "delete"], "description": "Default insert."},
                "kind": {"type": "string", "enum": ["hyperlink", "bookmark", "footnote", "endnote", "caption", "cross_reference", "index_entry"]},
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
                "hyperlink": {
                  "type": "object",
                  "properties": {
                    "url": {"type": "string"},
                    "email": {"type": "string"},
                    "bookmark": {"type": "string", "description": "Link to a bookmark in this document."},
                    "text": {"type": "string", "description": "Display text when `at` is a caret."},
                    "tooltip": {"type": "string"}
                  }
                },
                "bookmark": {
                  "type": "object",
                  "properties": {
                    "name": {"type": "string", "description": "Letters, digits and underscore, starts with a letter, max 40 characters."}
                  },
                  "required": ["name"]
                },
                "note": {
                  "type": "object",
                  "properties": {
                    "id": {"type": "string", "description": "edit, delete: note id like \"fn:2\" from read_document."},
                    "text": {"type": "string"}
                  }
                },
                "caption": {
                  "type": "object",
                  "properties": {
                    "label": {"type": "string", "description": "표, 그림, 수식, Table, Figure, Equation or another label."},
                    "text": {"type": "string", "description": "Caption words after the number."},
                    "position": {"type": "string", "enum": ["above", "below"]},
                    "target": {"type": "string", "description": "Table or paragraph id the caption belongs to; the caption is placed above/below it."},
                    "include_label": {"type": "boolean"}
                  }
                },
                "cross_reference": {
                  "type": "object",
                  "properties": {
                    "type": {"type": "string", "enum": ["caption", "heading", "bookmark", "footnote", "endnote"]},
                    "label": {"type": "string", "description": "For caption: the label (표, 그림 ...)."},
                    "target": {"type": "string", "description": "Caption/heading paragraph id, bookmark name or note id."},
                    "show": {"type": "string", "enum": ["label_and_number", "only_label", "number", "text", "page_number", "above_below"]},
                    "as_link": {"type": "boolean"}
                  }
                },
                "index_entry": {
                  "type": "object",
                  "properties": {
                    "entry": {"type": "string"},
                    "subentry": {"type": "string"},
                    "see": {"type": "string"},
                    "bold": {"type": "boolean"},
                    "italic": {"type": "boolean"}
                  }
                }
              },
              "required": ["kind"]
            }
            """;

        protected override WordHwpSupport Hwp => WordHwpSupport.Partial;

        private static void RequireInHwp(WordDocu docu, string kind)
        {
            switch (kind)
            {
                case "hyperlink":
                    HwpGuard.Require(docu, HwpFeature.Hyperlinks, "하이퍼링크");
                    return;
                case "bookmark":
                    HwpGuard.Require(docu, HwpFeature.Bookmarks, "책갈피");
                    return;
                case "footnote":
                case "endnote":
                    HwpGuard.Require(docu, HwpFeature.Notes, "각주와 미주");
                    return;
                case "cross_reference":
                    HwpGuard.Refuse(docu, "상호 참조");
                    return;
                case "caption":
                    HwpGuard.Refuse(docu, "캡션 번호");
                    return;
                default:
                    HwpGuard.Refuse(docu, "이 참조");
                    return;
            }
        }

        protected override ToolResult Run(WordDocu docu, ToolInput input)
        {
            var action = (input.GetOptionalString("action") ?? "insert").Trim().ToLowerInvariant();
            if (Array.IndexOf(s_actions, action) < 0)
                throw new ToolInputException("'action' 값은 insert, edit, delete 중 하나여야 해요.");

            var kind = input.GetString("kind").Trim().ToLowerInvariant();
            if (Array.IndexOf(s_kinds, kind) < 0)
                throw new ToolInputException($"'kind' 값은 {string.Join(", ", s_kinds)} 중 하나여야 해요.");

            if (docu is HwpDocu)
                RequireInHwp(docu, kind);

            var snapshot = docu.Capture();
            try
            {
                var scan = WordScan.Read(docu.Document);
                var ctx = WordRevisions.Active(docu);
                return kind switch
                {
                    "hyperlink" => LinkKind(docu, scan, input, action, ctx),
                    "bookmark" => BookmarkKind(docu, scan, input, action),
                    "footnote" => NoteKind(docu, scan, input, action, ctx, false),
                    "endnote" => NoteKind(docu, scan, input, action, ctx, true),
                    "caption" => CaptionKind(docu, scan, input, action, ctx),
                    "cross_reference" => CrossKind(docu, scan, input, action, ctx),
                    _ => IndexKind(docu, scan, input, action, ctx),
                };
            }
            catch
            {
                docu.Restore(snapshot);
                throw;
            }
        }

        private static ToolResult LinkKind(WordDocu docu, WordScan scan, ToolInput input, string action, RevisionContext ctx)
        {
            switch (action)
            {
                case "insert":
                    return InsertLink(docu, scan, input, ctx);
                case "edit":
                    return EditLink(docu, scan, input);
                default:
                    return DeleteLink(docu, scan, input, ctx);
            }
        }

        private static ToolResult InsertLink(WordDocu docu, WordScan scan, ToolInput input, RevisionContext ctx)
        {
            var link = Options(input, "hyperlink");
            var target = WordHyperlinks.Target(docu, link.GetOptionalString("url"), link.GetOptionalString("email"), link.GetOptionalString("bookmark"));
            var tooltip = link.GetOptionalString("tooltip");
            var text = WordText.Clean(link.GetOptionalString("text") ?? string.Empty).Trim();
            var styles = scan.Styles;
            if (IsRange(input.Get("at")))
            {
                var spot = Locate(scan, input);
                if (text.Length > 0)
                {
                    new WordTextEditor(docu, styles, ctx).Replace(spot.Paragraph, spot.Start, spot.End, WordRichText.Plain(text));
                    var replaced = WordTextMap.Build(spot.Paragraph).Isolate(spot.Start, spot.Start + text.Length);
                    WordHyperlinks.Wrap(docu, styles, replaced, target, tooltip, ctx);
                }
                else
                {
                    WordHyperlinks.Wrap(docu, styles, WordTextMap.Build(spot.Paragraph).Isolate(spot.Start, spot.End), target, tooltip, ctx);
                }

                return Done(docu, spot.Paragraph, "Inserted a hyperlink.", "링크 넣음", "link");
            }

            if (text.Length == 0)
                throw new ToolInputException("링크로 쓸 글('hyperlink.text')을 넣어 주세요. 기존 글에 걸려면 at을 글 범위({id, start, end})로 정해 주세요.");

            var point = WordRangeTarget.ResolveCaret(scan, input, "at");
            var run = WordTextMap.Build(point.Paragraph).InsertRunAt(point.Offset, text, null);
            WordHyperlinks.Wrap(docu, styles, new[] { run }, target, tooltip, null);
            WordInline.Track(ctx, new[] { run });
            return Done(docu, point.Paragraph, "Inserted a hyperlink.", "링크 넣음", "link");
        }

        private static ToolResult EditLink(WordDocu docu, WordScan scan, ToolInput input)
        {
            var link = Options(input, "hyperlink");
            var spot = Locate(scan, input);
            var span = WordTextMap.Build(spot.Paragraph).Spans
                .FirstOrDefault(s => s.Kind == WordSpanKind.Hyperlink && Overlaps(s, spot.Start, spot.End))
                ?? throw new ToolInputException("그 글에는 링크가 없어요. 링크를 먼저 넣어 주세요.");
            var hyperlink = (Hyperlink)span.Element;
            if (link.Has("url") || link.Has("email") || link.Has("bookmark"))
            {
                var target = WordHyperlinks.Target(docu, link.GetOptionalString("url"), link.GetOptionalString("email"), link.GetOptionalString("bookmark"));
                WordHyperlinks.Retarget(docu, hyperlink, target, link.GetOptionalString("tooltip"));
            }
            else if (link.Has("tooltip"))
            {
                hyperlink.Tooltip = WordText.Clean(link.GetOptionalString("tooltip")).Trim();
            }
            else
            {
                throw new ToolInputException("바꿀 링크 주소('hyperlink.url'), 메일('hyperlink.email'), 책갈피('hyperlink.bookmark')나 설명('hyperlink.tooltip')을 넣어 주세요.");
            }

            return Done(docu, spot.Paragraph, "Changed a hyperlink.", "링크 바꿈", "link");
        }

        private static ToolResult DeleteLink(WordDocu docu, WordScan scan, ToolInput input, RevisionContext ctx)
        {
            var spot = Locate(scan, input);
            var links = WordTextMap.Build(spot.Paragraph).Spans
                .Where(s => s.Kind == WordSpanKind.Hyperlink && Overlaps(s, spot.Start, spot.End))
                .Select(s => (Hyperlink)s.Element)
                .ToList();
            if (links.Count == 0)
                throw new ToolInputException("그 글에는 링크가 없어요.");

            foreach (var link in links)
                WordHyperlinks.Unwrap(scan.Styles, link, ctx);

            return Done(docu, spot.Paragraph, "Removed a hyperlink.", "링크 지움", "link");
        }

        private static ToolResult BookmarkKind(WordDocu docu, WordScan scan, ToolInput input, string action)
        {
            var bookmark = Options(input, "bookmark");
            switch (action)
            {
                case "insert":
                {
                    var name = WordBookmarks.CheckName(bookmark.GetString("name"));
                    if (WordBookmarks.Find(docu.Document, name) != null)
                        throw new ToolInputException($"‘{name}’ 책갈피가 이미 있어요. 다른 이름을 골라 주세요.");

                    var spot = Locate(scan, input);
                    WordBookmarks.Insert(docu, WordTextMap.Build(spot.Paragraph), spot.Start, spot.End, name);
                    return Done(docu, spot.Paragraph, $"Inserted bookmark {name}.", "책갈피 넣음", "bookmark-plus");
                }

                case "edit":
                {
                    var name = WordBookmarks.CheckName(bookmark.GetString("name"));
                    var start = BookmarkAt(docu, scan, input);
                    var existing = WordBookmarks.Find(docu.Document, name);
                    if (existing != null && !ReferenceEquals(existing, start))
                        throw new ToolInputException($"‘{name}’ 책갈피가 이미 있어요. 다른 이름을 골라 주세요.");

                    start.Name = name;
                    return Done(docu, start.Ancestors<Paragraph>().FirstOrDefault(), $"Renamed bookmark to {name}.", "책갈피 바꿈", "bookmark-plus");
                }

                default:
                {
                    var start = bookmark.Has("name")
                        ? WordBookmarks.Find(docu.Document, WordBookmarks.CheckName(bookmark.GetString("name")))
                            ?? throw new ToolInputException("그 이름의 책갈피를 찾지 못했어요.")
                        : BookmarkAt(docu, scan, input);
                    var host = start.Ancestors<Paragraph>().FirstOrDefault();
                    var name = start.Name?.Value;
                    if (name != null && name.StartsWith("_", StringComparison.Ordinal))
                        throw new ToolInputException("숨은 책갈피는 지울 수 없어요. 이 책갈피를 가리키는 링크나 참조가 깨질 수 있어요.");

                    WordBookmarks.Remove(docu.Document, start);
                    return Done(docu, host, $"Removed bookmark {name}.", "책갈피 지움", "bookmark-plus");
                }
            }
        }

        private static BookmarkStart BookmarkAt(WordDocu docu, WordScan scan, ToolInput input)
        {
            var spot = Locate(scan, input);
            var candidates = WordBookmarks.Starts(docu.Document)
                .Where(b => b.Name?.Value is string name && !name.StartsWith("_", StringComparison.Ordinal)
                    && ReferenceEquals(b.Ancestors<Paragraph>().FirstOrDefault(), spot.Paragraph))
                .ToList();
            if (candidates.Count == 0)
                throw new ToolInputException("이 자리에 책갈피가 없어요. 책갈피 이름('bookmark.name')을 함께 넣어 주세요.");

            if (candidates.Count > 1)
                throw new ToolInputException("이 자리에 책갈피가 여러 개예요. 책갈피 이름('bookmark.name')으로 하나를 골라 주세요.");

            return candidates[0];
        }

        private static ToolResult NoteKind(WordDocu docu, WordScan scan, ToolInput input, string action, RevisionContext ctx, bool endnote)
        {
            var note = Options(input, "note");
            var kind = endnote ? "endnote" : "footnote";
            switch (action)
            {
                case "insert":
                {
                    var spot = CaretAtEnd(scan, input);
                    var id = WordNotes.Insert(docu, scan.Styles, WordTextMap.Build(spot.Paragraph), spot.Start, endnote, note.GetString("text"), ctx);
                    Renumber(docu);
                    var number = NumberOf(docu, endnote, id);
                    return Done(docu, spot.Paragraph, $"Inserted {kind} {number}.", endnote ? "미주 넣음" : "각주 넣음", "footnote", WordAnchors.Note(kind, id));
                }

                case "edit":
                {
                    var id = WordNotes.NumberOf(note.GetString("id"), endnote);
                    WordNotes.Edit(docu, endnote, id, note.GetString("text"), ctx);
                    var host = WordNotes.FindReference(docu, endnote, id)?.Ancestors<Paragraph>().FirstOrDefault();
                    return Done(docu, host, $"Edited {kind} {NumberOf(docu, endnote, id)}.", endnote ? "미주 고침" : "각주 고침", "footnote", WordAnchors.Note(kind, id));
                }

                default:
                {
                    var id = WordNotes.NumberOf(note.GetString("id"), endnote);
                    var host = WordNotes.FindReference(docu, endnote, id)?.Ancestors<Paragraph>().FirstOrDefault();
                    var number = NumberOf(docu, endnote, id);
                    WordNotes.Delete(docu, endnote, id, ctx);
                    Renumber(docu);
                    return Done(docu, host, $"Deleted {kind} {number}.", endnote ? "미주 지움" : "각주 지움", "footnote", WordAnchors.Note(kind, id));
                }
            }
        }

        private static ToolResult CaptionKind(WordDocu docu, WordScan scan, ToolInput input, string action, RevisionContext ctx)
        {
            switch (action)
            {
                case "insert":
                    return InsertCaption(docu, scan, input, ctx);
                case "edit":
                    return EditCaption(docu, scan, input, ctx);
                default:
                    return DeleteCaption(docu, scan, input, ctx);
            }
        }

        private static ToolResult InsertCaption(WordDocu docu, WordScan scan, ToolInput input, RevisionContext ctx)
        {
            var caption = Options(input, "caption");
            var label = WordCaptions.CheckLabel(caption.GetString("label"));
            var block = CaptionTarget(scan, input, caption);
            var above = block.IsTable;
            if (caption.Has("position"))
            {
                var position = caption.GetString("position").Trim().ToLowerInvariant();
                if (position != "above" && position != "below")
                    throw new ToolInputException("'caption.position' 값은 above 또는 below여야 해요.");

                above = position == "above";
            }

            var paragraph = WordCaptions.Build(scan.Styles, label, caption.GetOptionalBool("include_label") ?? true, caption.GetOptionalString("text"), WordIds.Collect(docu.Document));
            WordCaptions.Place(block.Element, above, paragraph);
            WordInline.Track(ctx, paragraph.Descendants<Run>().ToList());
            if (ctx != null)
                WordRevisions.MarkParagraphMark(paragraph, true, ctx);

            Renumber(docu);
            var number = WordCaptions.SequenceOf(paragraph)?.Result ?? string.Empty;
            var anchors = block.IsTable && WordTables.IdOf(block.Table) is string tableId ? new[] { WordAnchors.Table(tableId) } : Array.Empty<string>();
            return Done(docu, paragraph, $"Inserted caption {label} {number}.", "캡션 넣음", "caption", anchors);
        }

        private static ToolResult EditCaption(WordDocu docu, WordScan scan, ToolInput input, RevisionContext ctx)
        {
            var caption = Options(input, "caption");
            var paragraph = Locate(scan, input).Paragraph;
            var sequence = WordCaptions.SequenceOf(paragraph) ?? throw new ToolInputException("그 문단은 캡션이 아니에요. 캡션 문단의 id를 골라 주세요.");
            if (caption.Has("label") && caption.GetString("label").Trim() != WordCaptions.LabelOf(sequence))
                throw new ToolInputException("캡션 이름은 바꿀 수 없어요. 캡션을 지우고 새 이름으로 다시 넣어 주세요.");

            if (!caption.Has("text"))
                throw new ToolInputException("고칠 캡션 글('caption.text')을 넣어 주세요.");

            WordCaptions.SetText(docu, scan.Styles, paragraph, sequence, caption.GetOptionalString("text") ?? string.Empty, ctx);
            return Done(docu, paragraph, "Edited a caption.", "캡션 고침", "caption");
        }

        private static ToolResult DeleteCaption(WordDocu docu, WordScan scan, ToolInput input, RevisionContext ctx)
        {
            var paragraph = Locate(scan, input).Paragraph;
            if (WordCaptions.SequenceOf(paragraph) == null)
                throw new ToolInputException("그 문단은 캡션이 아니에요. 캡션 문단의 id를 골라 주세요.");

            var location = WordInline.LocationOf(scan, paragraph);
            var id = WordIds.Of(paragraph);
            WordCaptions.Remove(paragraph, ctx);
            Renumber(docu);
            var anchors = id == null ? new List<string>() : new List<string> { WordAnchors.Paragraph(id) };
            return ToolResult.Changed("Deleted a caption.", Change(location, "caption", anchors), "캡션 지움");
        }

        private static ToolResult CrossKind(WordDocu docu, WordScan scan, ToolInput input, string action, RevisionContext ctx)
        {
            switch (action)
            {
                case "insert":
                {
                    var spec = CrossSpec(input);
                    var spot = CaretAtEnd(scan, input);
                    var runs = WordCrossReferences.Create(docu, scan, spec, spot.Paragraph);
                    WordInline.Place(WordTextMap.Build(spot.Paragraph), spot.Start, runs);
                    WordInline.Track(ctx, runs);
                    return Done(docu, spot.Paragraph, "Inserted a cross-reference.", "상호 참조 넣음", "link");
                }

                case "edit":
                {
                    var spec = CrossSpec(input);
                    var spot = Locate(scan, input);
                    var field = FieldAt(spot, "REF", "PAGEREF", "NOTEREF") ?? throw new ToolInputException("그 자리에 상호 참조가 없어요.");
                    var runs = WordCrossReferences.Create(docu, scan, spec, spot.Paragraph);
                    WordCrossReferences.Swap(field, runs, ctx);
                    return Done(docu, spot.Paragraph, "Edited a cross-reference.", "상호 참조 고침", "link");
                }

                default:
                {
                    var spot = Locate(scan, input);
                    var field = FieldAt(spot, "REF", "PAGEREF", "NOTEREF") ?? throw new ToolInputException("그 자리에 상호 참조가 없어요.");
                    WordInline.Drop(field, ctx);
                    return Done(docu, spot.Paragraph, "Removed a cross-reference.", "상호 참조 지움", "link");
                }
            }
        }

        private static ToolResult IndexKind(WordDocu docu, WordScan scan, ToolInput input, string action, RevisionContext ctx)
        {
            var entry = Options(input, "index_entry");
            switch (action)
            {
                case "insert":
                {
                    var spot = CaretAtEnd(scan, input);
                    var runs = WordIndexEntries.Runs(IndexInstruction(entry));
                    WordInline.Place(WordTextMap.Build(spot.Paragraph), spot.Start, runs);
                    WordInline.Track(ctx, runs);
                    return Done(docu, spot.Paragraph, "Inserted an index entry.", "색인 항목 넣음", "link");
                }

                case "edit":
                {
                    var spot = Locate(scan, input);
                    var field = IndexFieldIn(spot.Paragraph, entry);
                    WordCrossReferences.Swap(field, WordIndexEntries.Runs(IndexInstruction(entry)), ctx);
                    return Done(docu, spot.Paragraph, "Edited an index entry.", "색인 항목 고침", "link");
                }

                default:
                {
                    var spot = Locate(scan, input);
                    WordInline.Drop(IndexFieldIn(spot.Paragraph, entry), ctx);
                    return Done(docu, spot.Paragraph, "Removed an index entry.", "색인 항목 지움", "link");
                }
            }
        }

        private static WordCrossReferenceSpec CrossSpec(ToolInput input)
        {
            var reference = Options(input, "cross_reference");
            var type = (reference.GetOptionalString("type") ?? string.Empty).Trim().ToLowerInvariant();
            if (type.Length == 0)
                throw new ToolInputException("상호 참조 대상 종류('cross_reference.type')를 정해 주세요. caption, heading, bookmark, footnote, endnote 중 하나예요.");

            if (Array.IndexOf(s_crossTypes, type) < 0)
                throw new ToolInputException("'cross_reference.type' 값은 caption, heading, bookmark, footnote, endnote 중 하나여야 해요.");

            var show = reference.GetOptionalString("show")?.Trim().ToLowerInvariant();
            if (show != null && Array.IndexOf(s_shows, show) < 0)
                throw new ToolInputException("'cross_reference.show' 값은 label_and_number, only_label, number, text, page_number, above_below 중 하나여야 해요.");

            var target = reference.GetOptionalString("target");
            if (string.IsNullOrWhiteSpace(target))
                throw new ToolInputException("참조할 대상('cross_reference.target')을 넣어 주세요.");

            return new WordCrossReferenceSpec(type, reference.GetOptionalString("label"), target.Trim(), show, reference.GetOptionalBool("as_link") ?? false);
        }

        private static string IndexInstruction(ToolInput entry)
        {
            var text = entry.GetString("entry");
            return WordIndexEntries.Instruction(text, entry.GetOptionalString("subentry"), entry.GetOptionalString("see"), entry.GetOptionalBool("bold") ?? false, entry.GetOptionalBool("italic") ?? false);
        }

        private static WordField IndexFieldIn(Paragraph paragraph, ToolInput entry)
        {
            var main = entry.GetOptionalString("entry")?.Trim();
            var candidates = WordFields.Read(paragraph)
                .Where(f => f.Name == "XE" && f.Simple == null && f.IsComplete && (main == null || MainOf(f.Instruction) == main))
                .ToList();
            if (candidates.Count == 0)
                throw new ToolInputException("이 문단에 그 색인 항목이 없어요. 항목 이름('index_entry.entry')과 위치를 다시 확인해 주세요.");

            if (candidates.Count > 1)
                throw new ToolInputException("이 문단에 색인 항목이 여러 개예요. 항목 이름('index_entry.entry')으로 하나를 골라 주세요.");

            return candidates[0];
        }

        private static string MainOf(string instruction)
        {
            var tokens = WordFields.Tokenize(instruction);
            if (tokens.Count < 2)
                return null;

            var text = tokens[1];
            var colon = text.IndexOf(':');
            return (colon < 0 ? text : text.Substring(0, colon)).Trim();
        }

        private static WordField FieldAt(Spot spot, params string[] names)
        {
            var span = WordInline.FieldAt(WordTextMap.Build(spot.Paragraph), spot.Start, names);
            return span == null ? null : WordFields.Read(spot.Paragraph).FirstOrDefault(f => ReferenceEquals(f.Begin, span.Element));
        }

        private static WordBlock CaptionTarget(WordScan scan, ToolInput input, ToolInput caption)
        {
            var id = caption.GetOptionalString("target") ?? AtId(input);
            if (string.IsNullOrWhiteSpace(id))
                throw new ToolInputException("캡션을 붙일 표나 문단을 정해 주세요. 'caption.target'이나 at에 id를 넣어 주세요.");

            return scan.Find(id.Trim()) ?? throw new ToolInputException($"‘{id.Trim()}’ 표나 문단을 찾지 못했어요. read_paragraphs로 id를 다시 확인해 주세요.");
        }

        private static string AtId(ToolInput input)
        {
            if (!input.Has("at"))
                return null;

            var at = input.Get("at");
            return at.ValueKind == JsonValueKind.String ? at.GetString() : new ToolInput(at).GetOptionalString("id");
        }

        private static Spot Locate(WordScan scan, ToolInput input)
        {
            if (IsRange(input.Get("at")))
            {
                var range = Single(WordRangeTarget.Resolve(scan, input, "at"));
                return new Spot(range.Paragraph, range.Start, range.End);
            }

            var point = WordRangeTarget.ResolveCaret(scan, input, "at");
            return new Spot(point.Paragraph, point.Offset, point.Offset);
        }

        private static Spot CaretAtEnd(WordScan scan, ToolInput input)
        {
            if (IsRange(input.Get("at")))
            {
                var range = Single(WordRangeTarget.Resolve(scan, input, "at"));
                return new Spot(range.Paragraph, range.End, range.End);
            }

            var point = WordRangeTarget.ResolveCaret(scan, input, "at");
            return new Spot(point.Paragraph, point.Offset, point.Offset);
        }

        private static WordRange Single(IReadOnlyList<WordRange> ranges)
        {
            if (ranges.Count > 1)
                throw new ToolInputException($"'at'이 {ranges.Count}곳이에요. occurrence로 하나를 골라 주세요.");

            return ranges[0];
        }

        private static bool IsRange(JsonElement at) =>
            at.ValueKind == JsonValueKind.Object
            && (at.TryGetProperty("start", out _) || at.TryGetProperty("end", out _) || at.TryGetProperty("match", out _));

        private static bool Overlaps(WordTextSpan span, int start, int end) =>
            start == end ? span.Start <= start && start <= span.End : span.Start < end && start < span.End;

        private static ToolInput Options(ToolInput input, string key) =>
            input.Has(key) ? new ToolInput(input.Get(key)) : ToolInput.From(new { });

        private static void Renumber(WordDocu docu)
        {
            WordFieldUpdater.Sequences(docu);
            WordFieldUpdater.References(docu, WordScan.Read(docu.Document));
        }

        private static int NumberOf(WordDocu docu, bool endnote, int id) =>
            WordInventory.Notes(docu, WordScan.Read(docu.Document))
                .Where(n => n.IsEndnote == endnote && n.Id == id)
                .Select(n => n.Number)
                .DefaultIfEmpty(id)
                .First();

        private static ToolResult Done(WordDocu docu, Paragraph host, string content, string detail, string icon, params string[] anchors)
        {
            var scan = WordScan.Read(docu.Document);
            var list = new List<string>();
            var id = host == null ? null : WordIds.Of(host);
            if (id != null)
                list.Add(WordAnchors.Paragraph(id));

            list.AddRange(anchors);
            return ToolResult.Changed(content, Change(WordInline.LocationOf(scan, host), icon, list), detail);
        }
    }
}
