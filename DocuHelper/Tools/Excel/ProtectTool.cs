using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocuHelper.Docus;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocuHelper.Tools.Excel
{
    public class ProtectTool : ExcelTool
    {
        private static readonly string[] s_actions = { "protect", "unprotect", "allow_range_add", "allow_range_remove" };

        private static readonly string[] s_defaultAllow = { "select_locked", "select_unlocked" };

        public override string Name => "protect";

        public override string Description =>
            "Protect or unprotect a sheet or the workbook structure, or define ranges that stay editable, like Excel 시트 보호 / 통합 문서 보호 / 범위 편집 허용. "
            + "A protected sheet refuses edits to locked cells (all cells are locked unless unlocked with format_range locked=false) and any action that allow does not list. "
            + "allow lists the actions every user may still do; when it is omitted, selecting locked and unlocked cells stays allowed. "
            + "A password is optional and, if used, cannot be recovered by this app. Never invent a password: use one only when the user typed it or asked for one. "
            + "allow_range_add and allow_range_remove work only while the sheet is not protected; ranges without a password stay editable after protection.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "scope": {
                  "type": "string",
                  "description": "sheet or workbook.",
                  "enum": ["sheet", "workbook"]
                },
                "sheet": {
                  "type": "string",
                  "description": "Sheet name (scope sheet)."
                },
                "action": {
                  "type": "string",
                  "description": "protect (default), unprotect, allow_range_add or allow_range_remove.",
                  "enum": ["protect", "unprotect", "allow_range_add", "allow_range_remove"]
                },
                "password": {
                  "type": "string",
                  "description": "Optional password."
                },
                "allow": {
                  "type": "array",
                  "description": "scope sheet: actions every user may still do.",
                  "items": {
                    "type": "string",
                    "description": "Action.",
                    "enum": ["select_locked", "select_unlocked", "format_cells", "format_columns", "format_rows", "insert_columns", "insert_rows", "insert_hyperlinks", "delete_columns", "delete_rows", "sort", "autofilter", "pivot", "objects", "scenarios"]
                  }
                },
                "structure": {
                  "type": "boolean",
                  "description": "scope workbook: block adding, deleting, moving, renaming and hiding sheets. Default true."
                },
                "windows": {
                  "type": "boolean",
                  "description": "scope workbook: keep the window size and position."
                },
                "allow_ranges": {
                  "type": "array",
                  "description": "allow_range_add / allow_range_remove.",
                  "items": {
                    "type": "object",
                    "description": "One range.",
                    "properties": {
                      "title": {
                        "type": "string",
                        "description": "Name of the range."
                      },
                      "range": {
                        "type": "string",
                        "description": "Cells."
                      },
                      "password": {
                        "type": "string",
                        "description": "Optional range password."
                      }
                    },
                    "required": ["title"]
                  }
                }
              },
              "required": ["scope"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var scope = input.GetString("scope");
            var action = input.GetOptionalString("action") ?? "protect";
            if (!s_actions.Contains(action))
                throw new ToolInputException("action은 protect, unprotect, allow_range_add, allow_range_remove 중 하나로 알려 주세요.");

            var password = input.GetOptionalString("password");
            return scope switch
            {
                "sheet" => SheetRun(book, action, input, password),
                "workbook" => WorkbookRun(book, action, input, password),
                _ => throw new ToolInputException("scope는 sheet나 workbook 중 하나로 알려 주세요."),
            };
        }

        private static ToolResult SheetRun(ExcelBook book, string action, ToolInput input, string password)
        {
            var sheet = ResolveSheet(book, input, null, forWrite: true);
            if (action is "allow_range_add" or "allow_range_remove")
                return AllowRanges(sheet, action, input);

            var protection = sheet.Worksheet.GetFirstChild<SheetProtection>();
            var protectedNow = protection?.Sheet?.Value == true;
            if (action == "protect")
            {
                if (protectedNow)
                    throw new ToolInputException($"‘{sheet.Name}’ 시트는 이미 보호되어 있어요.");

                var allowed = AllowedOf(input);
                WorksheetOrder.Insert(sheet.Worksheet, ProtectionFor(allowed, password));
                return Changed($"Protected sheet '{sheet.Name}'. Allowed actions: {(allowed.Count == 0 ? "none" : string.Join(", ", allowed))}. Sheet protection guards against accidental edits and is not a security feature.", "시트 보호", "시트 보호", "lock", Anchors(sheet, new[] { new CellAddress(0, 0) }));
            }

            if (!protectedNow)
                throw new ToolInputException($"‘{sheet.Name}’ 시트는 보호되어 있지 않아요.");

            CheckPassword(HasPassword(protection), candidate => MatchesSheet(protection, candidate), password);
            protection.Remove();
            return Changed($"Unprotected sheet '{sheet.Name}'.", "시트 보호 해제", "시트 보호", "lock", Anchors(sheet, new[] { new CellAddress(0, 0) }));
        }

        private static ToolResult WorkbookRun(ExcelBook book, string action, ToolInput input, string password)
        {
            if (action is "allow_range_add" or "allow_range_remove")
                throw new ToolInputException("편집 허용 범위는 시트마다 정해요. scope를 sheet로 바꿔 주세요.");

            if (input.Has("allow"))
                throw new ToolInputException("허용 작업(allow)은 시트 보호에만 쓸 수 있어요.");

            var workbook = book.WorkbookPart.Workbook;
            var protection = workbook.GetFirstChild<WorkbookProtection>();
            var protectedNow = protection?.LockStructure?.Value == true || protection?.LockWindows?.Value == true;
            if (action == "protect")
            {
                if (protectedNow)
                    throw new ToolInputException("이 통합 문서는 이미 보호되어 있어요.");

                var structure = input.GetOptionalBool("structure") ?? true;
                var windows = input.GetOptionalBool("windows") ?? false;
                if (!structure && !windows)
                    throw new ToolInputException("통합 문서에서 보호할 것을 알려 주세요. 구조(structure)나 창 위치(windows) 중 하나가 필요해요.");

                protection?.Remove();
                var created = new WorkbookProtection
                {
                    LockStructure = structure ? (BooleanValue)true : null,
                    LockWindows = windows ? (BooleanValue)true : null,
                };
                if (!string.IsNullOrEmpty(password))
                    created.WorkbookPassword = PasswordHash.Legacy(password);

                OpenXmlElement anchor = workbook.BookViews ?? (OpenXmlElement)workbook.Sheets;
                workbook.InsertBefore(created, anchor);
                return Changed($"Protected the workbook (structure: {structure}, windows: {windows}). Workbook protection guards against accidental changes and is not a security feature.", "통합 문서 보호", "통합 문서 보호", "booklock", new List<string>());
            }

            if (!protectedNow)
                throw new ToolInputException("이 통합 문서는 보호되어 있지 않아요.");

            CheckPassword(HasWorkbookPassword(protection), candidate => MatchesWorkbook(protection, candidate), password);
            protection.Remove();
            return Changed("Unprotected the workbook.", "통합 문서 보호 해제", "통합 문서 보호", "booklock", new List<string>());
        }

        private static ToolResult AllowRanges(ExcelSheet sheet, string action, ToolInput input)
        {
            var protection = sheet.Worksheet.GetFirstChild<SheetProtection>();
            if (protection?.Sheet?.Value == true)
                throw new ToolInputException("시트를 보호하기 전에 편집 허용 범위를 정해 주세요. 보호 중에는 바꿀 수 없어요.");

            var element = input.Get("allow_ranges");
            if (element.ValueKind != JsonValueKind.Array)
                throw new ToolInputException("allow_ranges에 범위 목록을 알려 주세요. 예: [{\"title\": \"입력칸\", \"range\": \"B2:C9\"}]");

            var ranges = sheet.Worksheet.GetFirstChild<ProtectedRanges>();
            var lastTitle = string.Empty;
            var lastRange = (CellRange)null;
            foreach (var item in element.EnumerateArray())
            {
                var title = TextOf(item, "title");
                if (string.IsNullOrWhiteSpace(title))
                    throw new ToolInputException("편집 허용 범위마다 이름(title)을 알려 주세요.");

                lastTitle = title.Trim();
                if (action == "allow_range_add")
                {
                    var text = TextOf(item, "range") ?? throw new ToolInputException($"‘{lastTitle}’ 범위의 칸(range)을 알려 주세요. 예: B2:C9");
                    lastRange = ParseRange(sheet, text);
                    ranges ??= WorksheetOrder.Insert(sheet.Worksheet, new ProtectedRanges());
                    if (ranges.Elements<ProtectedRange>().Any(r => r.Name?.Value == lastTitle))
                        throw new ToolInputException($"‘{lastTitle}’ 이름의 편집 허용 범위가 이미 있어요. 다른 이름을 써 주세요.");

                    var entry = new ProtectedRange { Name = lastTitle, SequenceOfReferences = new ListValue<StringValue> { InnerText = lastRange.ToString() } };
                    var rangePassword = TextOf(item, "password");
                    if (!string.IsNullOrEmpty(rangePassword))
                        entry.Password = PasswordHash.Legacy(rangePassword);

                    ranges.AppendChild(entry);
                }
                else
                {
                    var existing = ranges?.Elements<ProtectedRange>().FirstOrDefault(r => r.Name?.Value == lastTitle)
                        ?? throw new ToolInputException($"‘{lastTitle}’ 이름의 편집 허용 범위가 없어요.");
                    CheckPassword(!string.IsNullOrEmpty(existing.Password?.Value), candidate => PasswordHash.MatchesLegacy(candidate, existing.Password?.Value), TextOf(item, "password"));
                    lastRange = CellRange.TryParse(existing.SequenceOfReferences?.InnerText ?? string.Empty, out var parsed) ? parsed : null;
                    existing.Remove();
                }
            }

            if (ranges != null && !ranges.HasChildren)
                ranges.Remove();

            var anchor = lastRange == null ? new List<string>() : Anchors(sheet, new[] { lastRange.Start });
            var location = $"편집 허용 범위 · {lastRange?.ToString() ?? lastTitle}";
            return Changed(action == "allow_range_add" ? $"Added the editable range '{lastTitle}'." : $"Removed the editable range '{lastTitle}'.", action == "allow_range_add" ? "편집 허용 범위 추가" : "편집 허용 범위 삭제", location, "listedit", anchor);
        }

        private static IReadOnlyList<string> AllowedOf(ToolInput input)
        {
            if (!input.Has("allow"))
                return s_defaultAllow;

            var element = input.Get("allow");
            if (element.ValueKind != JsonValueKind.Array)
                throw new ToolInputException("allow는 허용할 작업 이름을 담은 목록으로 알려 주세요.");

            var names = new List<string>();
            foreach (var item in element.EnumerateArray())
            {
                var name = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                if (name == null || !ProtectionGuard.SheetActionNames.Contains(name))
                    throw new ToolInputException($"‘{name ?? string.Empty}’은 알 수 없는 허용 작업이에요. 가능한 작업: {string.Join(", ", ProtectionGuard.SheetActionNames)}");

                names.Add(name);
            }

            return names;
        }

        private static SheetProtection ProtectionFor(IReadOnlyList<string> allowed, string password)
        {
            var protection = new SheetProtection { Sheet = true };
            foreach (var name in ProtectionGuard.SheetActionNames)
                ProtectionGuard.Apply(protection, name, !allowed.Contains(name));

            if (!string.IsNullOrEmpty(password))
                protection.Password = PasswordHash.Legacy(password);

            return protection;
        }

        private static bool HasPassword(SheetProtection protection) =>
            !string.IsNullOrEmpty(protection?.Password?.Value) || !string.IsNullOrEmpty(protection?.HashValue?.Value);

        private static bool MatchesSheet(SheetProtection protection, string password) =>
            PasswordHash.MatchesLegacy(password, protection.Password?.Value)
            || PasswordHash.MatchesSha512(password, protection.AlgorithmName?.Value, protection.SaltValue?.Value, protection.HashValue?.Value, SpinOf(protection.SpinCount?.Value));

        private static bool HasWorkbookPassword(WorkbookProtection protection) =>
            !string.IsNullOrEmpty(protection?.WorkbookPassword?.Value) || !string.IsNullOrEmpty(protection?.WorkbookHashValue?.Value);

        private static bool MatchesWorkbook(WorkbookProtection protection, string password) =>
            PasswordHash.MatchesLegacy(password, protection.WorkbookPassword?.Value)
            || PasswordHash.MatchesSha512(password, protection.WorkbookAlgorithmName?.Value, protection.WorkbookSaltValue?.Value, protection.WorkbookHashValue?.Value, SpinOf(protection.WorkbookSpinCount?.Value));

        private static void CheckPassword(bool required, Func<string, bool> matches, string password)
        {
            if (!required)
                return;

            if (string.IsNullOrEmpty(password))
                throw new ToolInputException("이 보호에는 암호가 걸려 있어요. 암호를 알려 주세요. 암호를 모르면 이 도우미로는 풀 수 없고, 도우미는 암호를 지어내지 않아요.");

            if (!matches(password))
                throw new ToolInputException("암호가 맞지 않아요. 다시 확인해 주세요.");
        }

        private static int SpinOf(uint? value) => value.HasValue ? (int)value.Value : PasswordHash.DefaultSpinCount;

        private static string TextOf(JsonElement item, string name) =>
            item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static ToolResult Changed(string content, string detail, string location, string icon, List<string> anchors)
        {
            var change = new DocuChange
            {
                Location = location,
                Icon = icon,
            };
            change.Anchors.AddRange(anchors);
            return ToolResult.Changed(content, change, detail);
        }
    }
}
