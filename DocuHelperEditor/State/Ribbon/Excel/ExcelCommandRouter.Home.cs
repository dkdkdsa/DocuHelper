using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DocuHelper.Editors;
using DocuHelper.Tools;
using DocuHelperEditor.Services;
using DocuHelperEditor.State.Papers;

namespace DocuHelperEditor.State.Ribbon.Excel
{
    public sealed partial class ExcelCommandRouter
    {
        private static readonly string[] s_fonts = { "맑은 고딕", "굴림", "돋움", "바탕", "궁서", "Arial", "Calibri", "Times New Roman", "Consolas" };

        private static readonly double[] s_sizes = { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };

        private static readonly (string Value, string Label)[] s_borders =
        {
            ("bottom", "아래쪽 테두리"),
            ("top", "위쪽 테두리"),
            ("left", "왼쪽 테두리"),
            ("right", "오른쪽 테두리"),
            ("none", "테두리 없음"),
            ("all", "모든 테두리"),
            ("outline", "바깥쪽 테두리"),
            ("thick_outline", "굵은 바깥쪽 테두리"),
            ("inside", "안쪽 테두리"),
            ("double_bottom", "아래쪽 이중 테두리"),
            ("thick_bottom", "굵은 아래쪽 테두리"),
            ("top_bottom", "위쪽/아래쪽 테두리"),
        };

        private static readonly (string Value, string Label, string Detail)[] s_numberFormats =
        {
            ("general", "일반", "1234.5"),
            ("number", "숫자", "1234.50"),
            ("currency", "통화", "₩1,235"),
            ("accounting_krw", "회계", "₩ 1,235"),
            ("short_date", "간단한 날짜", "2026-06-30"),
            ("long_date", "자세한 날짜", "2026년 6월 30일 화요일"),
            ("time", "시간", "14:30:00"),
            ("percent2", "백분율", "12.00%"),
            ("fraction", "분수", "1 1/2"),
            ("scientific", "지수", "1.23E+03"),
            ("text", "텍스트", "@"),
        };

        private static readonly (string Value, string Label)[] s_currencies =
        {
            ("accounting_krw", "₩ 한국어"),
            ("accounting_usd", "$ 영어(미국)"),
            ("accounting_eur", "€ 유로"),
            ("accounting_jpy", "¥ 일본어"),
        };

        private static readonly (string Heading, (string Value, string Detail)[] Items)[] s_cellStyles =
        {
            ("좋음, 나쁨 및 보통", new[] { ("표준", "Normal"), ("좋음", "Good"), ("나쁨", "Bad"), ("보통", "Neutral") }),
            ("데이터 및 모델", new[] { ("계산", "Calculation"), ("확인 셀", "Check Cell"), ("설명 텍스트", "Explanatory Text"), ("입력", "Input"), ("연결된 셀", "Linked Cell"), ("메모", "Note"), ("출력", "Output"), ("경고 텍스트", "Warning Text") }),
            ("제목 및 표제", new[] { ("제목", "Title"), ("제목 1", "Heading 1"), ("제목 2", "Heading 2"), ("제목 3", "Heading 3"), ("제목 4", "Heading 4"), ("요약", "Total") }),
            ("테마 셀 스타일", new[] { ("강조색1", "Accent1"), ("강조색2", "Accent2"), ("강조색3", "Accent3"), ("강조색4", "Accent4"), ("강조색5", "Accent5"), ("강조색6", "Accent6") }),
            ("숫자 서식", new[] { ("백분율", "Percent"), ("쉼표", "Comma"), ("통화", "Currency"), ("하이퍼링크", "Hyperlink") }),
        };

        private static readonly (string Value, string Label)[] s_tableStyles =
        {
            ("TableStyleLight9", "밝게 9 (파랑)"),
            ("TableStyleLight10", "밝게 10 (주황)"),
            ("TableStyleLight11", "밝게 11 (회색)"),
            ("TableStyleLight12", "밝게 12 (노랑)"),
            ("TableStyleLight13", "밝게 13 (하늘)"),
            ("TableStyleLight14", "밝게 14 (초록)"),
            ("TableStyleLight1", "밝게 1 (검정 줄무늬)"),
            ("TableStyleMedium2", "보통 2 (파랑)"),
            ("TableStyleMedium3", "보통 3 (주황)"),
            ("TableStyleMedium4", "보통 4 (회색)"),
            ("TableStyleMedium5", "보통 5 (노랑)"),
            ("TableStyleMedium6", "보통 6 (하늘)"),
            ("TableStyleMedium7", "보통 7 (초록)"),
            ("TableStyleMedium9", "보통 9 (진한 파랑)"),
            ("TableStyleDark1", "어둡게 1 (검정)"),
            ("TableStyleDark2", "어둡게 2 (파랑)"),
        };

        private static readonly (string Value, string Label)[] s_highlightRules =
        {
            ("cell_value:greater_than", "보다 큼…"),
            ("cell_value:less_than", "보다 작음…"),
            ("cell_value:between", "중간값…"),
            ("cell_value:equal", "같음…"),
            ("text_contains", "텍스트 포함…"),
            ("date", "발생 날짜…"),
            ("duplicate", "중복 값…"),
        };

        private static readonly (string Value, string Label)[] s_rankRules =
        {
            ("top:10", "상위 10개 항목…"),
            ("top:10%", "상위 10%…"),
            ("bottom:10", "하위 10개 항목…"),
            ("bottom:10%", "하위 10%…"),
            ("above_average", "평균 초과…"),
            ("below_average", "평균 미만…"),
        };

        private static readonly (string Value, string Label)[] s_dataBars =
        {
            ("638EC6", "파랑 데이터 막대"),
            ("63C384", "녹색 데이터 막대"),
            ("FF555A", "빨강 데이터 막대"),
            ("FFB628", "주황 데이터 막대"),
            ("008AEF", "하늘 데이터 막대"),
            ("D6007B", "보라 데이터 막대"),
        };

        private static readonly (string Value, string Label)[] s_colorScales =
        {
            ("F8696B,FFEB84,63BE7B", "녹색 - 노랑 - 빨강 색조"),
            ("63BE7B,FFEB84,F8696B", "빨강 - 노랑 - 녹색 색조"),
            ("F8696B,FCFCFF,63BE7B", "녹색 - 흰색 - 빨강 색조"),
            ("5A8AC6,FCFCFF,F8696B", "파랑 - 흰색 - 빨강 색조"),
            ("FCFCFF,F8696B", "흰색 - 빨강 색조"),
            ("FCFCFF,63BE7B", "흰색 - 녹색 색조"),
            ("63BE7B,FFEB84", "녹색 - 노랑 색조"),
            ("FFEB84,F8696B", "노랑 - 빨강 색조"),
        };

        private static readonly (string Value, string Label)[] s_iconSets =
        {
            ("3Arrows", "3 화살표 (색상)"),
            ("3ArrowsGray", "3 화살표 (회색)"),
            ("3TrafficLights1", "3 신호등"),
            ("3Symbols", "3 기호"),
            ("3Signs", "3 도형"),
            ("3Flags", "3 깃발"),
            ("4Arrows", "4 화살표 (색상)"),
            ("4TrafficLights", "4 신호등"),
            ("5Arrows", "5 화살표 (색상)"),
            ("5Rating", "5 등급"),
            ("5Quarters", "5 사분원"),
        };

        private static readonly (string Function, string Label)[] s_sumFunctions =
        {
            ("SUM", "합계"),
            ("AVERAGE", "평균"),
            ("COUNT", "숫자 개수"),
            ("MAX", "최대값"),
            ("MIN", "최소값"),
        };

        private void RegisterHome()
        {
            RegisterClipboard();
            RegisterFont();
            RegisterAlignment();
            RegisterNumber();
            RegisterStyles();
            RegisterCells();
            RegisterEditing();
        }

        private void RegisterClipboard()
        {
            AddClipboard("excel.home.paste", arg => PasteAsync(arg))
                .MenuFrom(PasteMenu)
                .EnabledWhen(CanPaste, "붙여넣을 내용이 없어요.");
            AddClipboard("excel.home.cut", _ => _surface.CopyAsync(true));
            AddClipboard("excel.home.copy", _ => _surface.CopyAsync(false)).AllowWhileLocked();
            AddFile("excel.home.formatpainter", _ => TogglePainter())
                .Needs("copy_range")
                .PressedWhen(() => _surface.IsPainting);
        }

        private void RegisterFont()
        {
            Format("excel.home.fontname", arg => string.IsNullOrWhiteSpace(arg) ? null : new (string, object)[] { ("font_name", arg) })
                .ValueFrom(() => State.FontName)
                .MenuFrom(() => s_fonts.Select(f => Entry(f, f, check: f == State.FontName, font: f)).ToArray());
            Format("excel.home.fontsize", arg => new (string, object)[] { ("font_size", ParseSize(arg)) })
                .ValueFrom(() => ExcelRibbonState.SizeText(State.FontSize))
                .MenuFrom(SizeMenu);
            Format("excel.home.fontgrow", _ => new (string, object)[] { ("font_size", State.NextLarger()) });
            Format("excel.home.fontshrink", _ => new (string, object)[] { ("font_size", State.NextSmaller()) });
            Format("excel.home.bold", _ => new (string, object)[] { ("bold", !State.IsBold) }).PressedWhen(() => State.IsBold);
            Format("excel.home.italic", _ => new (string, object)[] { ("italic", !State.IsItalic) }).PressedWhen(() => State.IsItalic);
            Format("excel.home.underline", _ => new (string, object)[] { ("underline", !State.IsUnderline) }).PressedWhen(() => State.IsUnderline);
            Format("excel.home.border", arg => string.IsNullOrEmpty(arg) ? null : new (string, object)[] { ("borders", arg) })
                .MenuFrom(() => s_borders.Select(b => Entry(b.Value, b.Label)).ToArray());
            Format("excel.home.fillcolor", arg => PickColor(arg, "fill_color", "none", color => State.FillColor = color))
                .ColorFrom(() => State.FillColor)
                .MenuFrom(() => ColorMenu("채우기 없음", null, State.FillColor));
            Format("excel.home.fontcolor", arg => PickColor(arg, "font_color", "auto", color => State.FontColor = color))
                .ColorFrom(() => State.FontColor)
                .MenuFrom(() => ColorMenu("자동 (검정)", "000000", State.FontColor));
        }

        private void RegisterAlignment()
        {
            Group("excel.home.alignment.menu",
                ("excel.home.valign.top", "위쪽 맞춤"),
                ("excel.home.valign.middle", "세로 가운데 맞춤"),
                ("excel.home.valign.bottom", "아래쪽 맞춤"),
                ("excel.home.wrap", "자동 줄 바꿈"),
                ("excel.home.align.left", "왼쪽 맞춤"),
                ("excel.home.align.center", "가운데 맞춤"),
                ("excel.home.align.right", "오른쪽 맞춤"),
                ("excel.home.merge", "병합하고 가운데 맞춤"));
            Format("excel.home.valign.top", _ => new (string, object)[] { ("vertical_align", "top") }).PressedWhen(() => State.VerticalAlign == "top");
            Format("excel.home.valign.middle", _ => new (string, object)[] { ("vertical_align", "center") }).PressedWhen(() => State.VerticalAlign == "center");
            Format("excel.home.valign.bottom", _ => new (string, object)[] { ("vertical_align", "bottom") }).PressedWhen(() => State.VerticalAlign == "bottom");
            Format("excel.home.wrap", _ => new (string, object)[] { ("wrap", !State.IsWrapped) }).PressedWhen(() => State.IsWrapped);
            Format("excel.home.align.left", _ => Horizontal(SheetAlign.Left, "left")).PressedWhen(() => State.Align == SheetAlign.Left);
            Format("excel.home.align.center", _ => Horizontal(SheetAlign.Center, "center")).PressedWhen(() => State.Align == SheetAlign.Center);
            Format("excel.home.align.right", _ => Horizontal(SheetAlign.Right, "right")).PressedWhen(() => State.Align == SheetAlign.Right);
            AddFile("excel.home.merge", _ => MergeSelection())
                .Needs("merge_cells")
                .PressedWhen(() => State.IsMerged)
                .EnabledWhen(() => !State.Selection.IsSingle || State.IsMerged, "합칠 칸을 두 개 이상 골라 주세요.");
        }

        private void RegisterNumber()
        {
            Group("excel.home.number.menu",
                ("excel.home.numberformat", "표시 형식"),
                ("excel.home.accounting", "회계 표시 형식"),
                ("excel.home.percent", "백분율 스타일"),
                ("excel.home.comma", "쉼표 스타일"));
            Format("excel.home.numberformat", arg => string.IsNullOrWhiteSpace(arg) ? null : new (string, object)[] { ("number_format", arg.Trim()) })
                .ValueFrom(() => State.NumberCategory)
                .MenuFrom(NumberFormatMenu);
            Format("excel.home.accounting", arg => string.IsNullOrEmpty(arg) ? null : new (string, object)[] { ("number_format", arg) })
                .MenuFrom(() => s_currencies.Select(c => Entry(c.Value, c.Label)).ToArray());
            Format("excel.home.percent", _ => new (string, object)[] { ("number_format", State.Cell?.NumberFormat == SheetNumberFormat.Percent ? "general" : "percent") })
                .PressedWhen(() => State.Cell?.NumberFormat == SheetNumberFormat.Percent);
            Format("excel.home.comma", _ => new (string, object)[] { ("number_format", State.Cell?.NumberFormat == SheetNumberFormat.Thousands ? "general" : "thousands") })
                .PressedWhen(() => State.Cell?.NumberFormat == SheetNumberFormat.Thousands);
        }

        private void RegisterStyles()
        {
            Group("excel.home.styles.menu",
                ("excel.home.condformat", "조건부 서식"),
                ("excel.home.formattable", "표 서식"),
                ("excel.home.cellstyles", "셀 스타일"));
            AddFile("excel.home.condformat", arg => RunConditional(arg))
                .Needs("set_conditional_format")
                .MenuFrom(ConditionalMenu);
            AddFile("excel.home.formattable", arg => RunTableStyle(arg))
                .Needs("manage_table")
                .MenuFrom(TableStyleMenu);
            Format("excel.home.cellstyles", arg => string.IsNullOrEmpty(arg) ? null : new (string, object)[] { ("cell_style", arg) })
                .MenuFrom(() => CellStyleMenu());
        }

        private void RegisterCells()
        {
            Group("excel.home.cells.menu",
                ("excel.home.insertcells", "삽입"),
                ("excel.home.deletecells", "삭제"),
                ("excel.home.formatcells", "서식"));
            AddFile("excel.home.insertcells", arg => RunCellsMenu(arg))
                .MenuFrom(() => new[]
                {
                    Entry("shift:right", "셀 삽입: 오른쪽으로 밀기", "고른 칸 자리에 빈 칸을 넣어요", needs: "shift_cells"),
                    Entry("shift:down", "셀 삽입: 아래로 밀기", "고른 칸 자리에 빈 칸을 넣어요", needs: "shift_cells"),
                    RibbonMenus.Separator(),
                    Entry(SheetCommands.InsertRowsAbove, "시트 행 삽입", needs: "insert_rows_columns"),
                    Entry(SheetCommands.InsertColumnsLeft, "시트 열 삽입", needs: "insert_rows_columns"),
                    Entry(SheetCommands.InsertRowsBelow, "아래에 행 삽입", needs: "insert_rows_columns"),
                    Entry(SheetCommands.InsertColumnsRight, "오른쪽에 열 삽입", needs: "insert_rows_columns"),
                    Entry("add-sheet", "시트 삽입", needs: "add_sheet"),
                });
            AddFile("excel.home.deletecells", arg => RunCellsMenu(arg))
                .MenuFrom(() => new[]
                {
                    Entry("shift:left", "셀 삭제: 왼쪽으로 당기기", "고른 칸을 지우고 옆 칸을 당겨요", needs: "shift_cells"),
                    Entry("shift:up", "셀 삭제: 위로 당기기", "고른 칸을 지우고 아래 칸을 당겨요", needs: "shift_cells"),
                    RibbonMenus.Separator(),
                    Entry(SheetCommands.DeleteRows, "시트 행 삭제", needs: "delete_rows_columns"),
                    Entry(SheetCommands.DeleteColumns, "시트 열 삭제", needs: "delete_rows_columns"),
                    Entry("delete-sheet", "시트 삭제", needs: "delete_sheet", blocked: State.Model.Sheets.Count(s => !s.IsHidden) <= 1 ? "시트가 하나뿐이라 지울 수 없어요." : null),
                });
            AddFile("excel.home.formatcells", arg => RunCellsMenu(arg))
                .MenuFrom(FormatCellsMenu);
        }

        private void RegisterEditing()
        {
            AddFile("excel.home.autosum", arg => AutoSum(arg))
                .MenuFrom(AutoSumMenu);
            AddFile("excel.home.sortfilter", arg => RunSortFilter(arg))
                .MenuFrom(SortFilterMenu);
            AddView("excel.home.findselect", arg => FindSelect(arg))
                .MenuFrom(FindSelectMenu);
        }

        private Task PasteAsync(string mode)
        {
            if (!_surface.Prepare())
                return Task.CompletedTask;

            if (ClipTool(mode, out var transpose, out var paste))
            {
                if (Apply(EditCommand.Create("copy_range", PasteArgs(paste, transpose))))
                    _surface.AfterPaste(transpose);

                return Task.CompletedTask;
            }

            if (string.IsNullOrEmpty(mode) || mode == "all")
                return _surface.PasteAsync();

            Notify(_surface.HasClip ? PendingReason : "이 앱에서 복사한 칸이 있어야 쓸 수 있어요.");
            return Task.CompletedTask;
        }

        public bool? PasteOwnClip()
        {
            if (!ClipTool("all", out var transpose, out var paste) || !_surface.Prepare())
                return null;

            return Apply(EditCommand.Create("copy_range", PasteArgs(paste, transpose)));
        }

        private bool ClipTool(string mode, out bool transpose, out string paste)
        {
            transpose = mode == "transpose";
            paste = string.IsNullOrEmpty(mode) || transpose ? "all" : mode;
            return ToolReady("copy_range") && _surface.HasClip && !IsLocked;
        }

        private Dictionary<string, object> PasteArgs(string paste, bool transpose)
        {
            var args = Workbook(
                ("sheet", _surface.ClipSheet),
                ("source", _surface.ClipRef),
                ("target", SheetRef.Make(State.Selection.Left, State.Selection.Top)),
                ("target_sheet", State.SheetName),
                ("paste", paste),
                ("move", _surface.ClipIsCut));
            if (transpose)
                args["transpose"] = true;

            return args;
        }

        private bool CanPaste() => _surface.HasClip || (_clipboard.Kinds & ClipboardKinds.Text) != 0;

        private IReadOnlyList<RibbonMenuItem> PasteMenu()
        {
            var tool = ToolReady("copy_range");
            var hasClip = _surface.HasClip;
            string Blocked(bool needsClip) => !tool ? PendingReason : needsClip && !hasClip ? "이 앱에서 복사한 칸이 있어야 쓸 수 있어요." : null;
            return new[]
            {
                Entry("all", "붙여넣기", "값, 수식, 서식 모두", key: "Ctrl+V"),
                Entry("formulas", "수식", "서식은 그대로 두고 수식과 값만", blocked: Blocked(true)),
                Entry("values", "값", "계산 결과만 넣어요", blocked: Blocked(true)),
                Entry("values_and_formats", "값 및 원본 서식", "수식은 값으로 바꾸고 서식은 따라와요", blocked: Blocked(true)),
                Entry("formats", "서식", "글자 모양, 색, 테두리만", blocked: Blocked(true)),
                Entry("column_widths", "열 너비 유지", "열 너비만 맞춰요", blocked: Blocked(true)),
                Entry("transpose", "행/열 바꿔 넣기", "가로와 세로를 서로 바꿔요", blocked: Blocked(true)),
                RibbonMenus.Separator(),
                DialogEntry("dialog", "선택하여 붙여넣기…", "excel.home.paste", "붙여넣기 방식을 골라요", "copy_range"),
            };
        }

        private Task TogglePainter()
        {
            if (_surface.IsPainting)
                _surface.DisarmPainter();
            else
                _surface.ArmPainter();

            return Task.CompletedTask;
        }

        public bool ApplyPainter(string sourceSheet, string sourceRange, string target)
        {
            return Apply(EditCommand.Create("copy_range", Workbook(
                ("sheet", sourceSheet),
                ("source", sourceRange),
                ("target", target),
                ("target_sheet", State.SheetName),
                ("paste", "formats"))));
        }

        private static double ParseSize(string text)
        {
            var cleaned = (text ?? string.Empty).Trim().Replace("pt", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            if (!double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) || size < 1 || size > 409)
                throw new ToolInputException("글자 크기는 1부터 409까지 숫자로 적어 주세요.");

            return Math.Round(size * 2, MidpointRounding.AwayFromZero) / 2;
        }

        private IReadOnlyList<RibbonMenuItem> SizeMenu()
        {
            var current = State.FontSize;
            var items = new List<RibbonMenuItem>
            {
                RibbonMenus.Input("글자 크기", ExcelRibbonState.SizeText(current), "pt"),
                RibbonMenus.Separator(),
            };
            items.AddRange(s_sizes.Select(s => Entry(ExcelRibbonState.SizeText(s), ExcelRibbonState.SizeText(s), check: Math.Abs(s - current) < 0.01)));
            return items;
        }

        private IReadOnlyList<RibbonMenuItem> ColorMenu(string resetLabel, string resetSwatch, string current)
        {
            return new[]
            {
                resetLabel == null ? null : Entry(resetSwatch == null ? "none" : "auto", resetLabel, swatch: resetSwatch),
                RibbonMenus.Separator(),
                RibbonMenus.Palette("색 고르기", RibbonPalette.Standard, current),
            }.Where(i => i != null).ToArray();
        }

        private (string Key, object Value)[] PickColor(string arg, string key, string reset, Action<string> remember)
        {
            if (string.IsNullOrWhiteSpace(arg))
                return null;

            var color = arg.Trim().TrimStart('#').ToUpperInvariant();
            if (color != reset)
                remember(color);

            return new (string, object)[] { (key, color == reset ? reset : color) };
        }

        private (string Key, object Value)[] Horizontal(SheetAlign align, string name)
        {
            return new (string, object)[] { ("align", State.Align == align ? "general" : name) };
        }

        private Task MergeSelection()
        {
            if (_surface.Prepare())
                _surface.ToggleMerge();

            return Task.CompletedTask;
        }

        private IReadOnlyList<RibbonMenuItem> NumberFormatMenu()
        {
            var items = new List<RibbonMenuItem>();
            items.AddRange(s_numberFormats.Select(f => Entry(f.Value, f.Label, f.Detail, check: State.NumberCategory == f.Label)));
            items.Add(RibbonMenus.Separator());
            items.Add(RibbonMenus.Input("서식 코드 직접 적기", "#,##0", "예: #,##0원  /  0.0%  /  yyyy-mm-dd"));
            return items;
        }

        private IReadOnlyList<RibbonMenuItem> ConditionalMenu()
        {
            var dialog = ExcelDialogIds.ConditionalFormat;
            var items = new List<RibbonMenuItem> { RibbonMenus.Heading("셀 강조 규칙") };
            items.AddRange(s_highlightRules.Select(r => r.Value == "duplicate"
                ? QuickRuleEntry(r.Value, r.Label)
                : DialogEntry("dialog:" + r.Value, r.Label, dialog, "값을 적는 창이 떠요", "set_conditional_format")));
            items.Add(RibbonMenus.Heading("상위/하위 규칙"));
            items.AddRange(s_rankRules.Select(r => QuickRuleEntry(r.Value, r.Label)));
            items.Add(RibbonMenus.Heading("데이터 막대"));
            items.AddRange(s_dataBars.Select(b => Entry("bar:" + b.Value, b.Label, swatch: b.Value)));
            items.Add(RibbonMenus.Heading("색조"));
            items.AddRange(s_colorScales.Select(b => Entry("scale:" + b.Value, b.Label, swatch: b.Value.Split(',')[0])));
            items.Add(RibbonMenus.Heading("아이콘 집합"));
            items.AddRange(s_iconSets.Select(b => Entry("icon:" + b.Value, b.Label)));
            items.Add(RibbonMenus.Separator());
            items.Add(DialogEntry("dialog:new", "새 규칙…", dialog, "수식 규칙을 포함해 직접 만들어요", "set_conditional_format"));
            items.Add(Entry("clear", "선택한 셀에서 규칙 지우기"));
            items.Add(Entry("clear_sheet", "시트 전체에서 규칙 지우기"));
            items.Add(DialogEntry("dialog:manage", "규칙 관리…", dialog, "만든 규칙을 보고 지워요", "set_conditional_format"));
            return items;
        }

        private RibbonMenuItem QuickRuleEntry(string value, string label)
        {
            return State.HasDialog(ExcelDialogIds.ConditionalFormat)
                ? DialogEntry("dialog:" + value, label, ExcelDialogIds.ConditionalFormat, null, "set_conditional_format")
                : Entry("quick:" + value, label, "연한 빨강 채우기와 진한 빨강 글자로 보여 줘요", needs: "set_conditional_format");
        }

        private Dictionary<string, object> QuickRule(string value)
        {
            var args = Cells(("action", "add"), ("look", "light_red_fill_dark_red_text"));
            var percent = value.EndsWith("%", StringComparison.Ordinal);
            var parts = value.TrimEnd('%').Split(':');
            args["rule"] = parts[0];
            if (parts.Length > 1)
            {
                args["count"] = int.Parse(parts[1], CultureInfo.InvariantCulture);
                if (percent)
                    args["percent"] = true;
            }

            return args;
        }

        private Task RunConditional(string arg)
        {
            if (string.IsNullOrEmpty(arg) || !_surface.Prepare())
                return Task.CompletedTask;

            if (arg.StartsWith("dialog:", StringComparison.Ordinal))
            {
                State.OpenDialog(ExcelDialogIds.ConditionalFormat);
                return Task.CompletedTask;
            }

            var colon = arg.IndexOf(':');
            var kind = colon < 0 ? arg : arg.Substring(0, colon);
            var value = colon < 0 ? null : arg.Substring(colon + 1);
            Dictionary<string, object> args = kind switch
            {
                "quick" => QuickRule(value),
                "bar" => Cells(("action", "add"), ("rule", "data_bar"), ("bar_color", value)),
                "scale" => Cells(("action", "add"), ("rule", "color_scale"), ("colors", value.Split(','))),
                "icon" => Cells(("action", "add"), ("rule", "icon_set"), ("icon_set", value)),
                "clear" => Cells(("action", "clear")),
                "clear_sheet" => Sheetwide(("action", "clear_sheet")),
                _ => null,
            };

            if (args != null)
                Apply(EditCommand.Create("set_conditional_format", args));

            return Task.CompletedTask;
        }

        private IReadOnlyList<RibbonMenuItem> TableStyleMenu()
        {
            var items = s_tableStyles.Select(s => Entry(s.Value, s.Label, blocked: State.IsInTable ? "이미 표 안에 있어요." : null)).ToList();
            items.Add(RibbonMenus.Separator());
            items.Add(Entry("convert_to_range", "표 스타일 지우기", "표를 일반 범위로 바꿔요", blocked: State.IsInTable ? null : "표 안의 칸을 골라 주세요."));
            return items;
        }

        private Task RunTableStyle(string arg)
        {
            if (string.IsNullOrEmpty(arg) || !_surface.Prepare())
                return Task.CompletedTask;

            if (arg == "convert_to_range")
            {
                Apply(EditCommand.Create("manage_table", Sheetwide(("action", "convert_to_range"), ("range", State.ActiveRef))));
                return Task.CompletedTask;
            }

            CreateTable(arg);
            return Task.CompletedTask;
        }

        private void CreateTable(string style)
        {
            var range = State.Region;
            _surface.Confirm(
                $"‘{range}’을 표로 만들까요? 맨 윗줄은 머리글로 써요.",
                "표 만들기",
                () => Apply(EditCommand.Create("manage_table", Sheetwide(("action", "create"), ("range", range), ("header", true), ("style", style)))));
        }

        private IReadOnlyList<RibbonMenuItem> CellStyleMenu()
        {
            var items = new List<RibbonMenuItem>();
            foreach (var (heading, styles) in s_cellStyles)
            {
                if (items.Count > 0)
                    items.Add(RibbonMenus.Separator());

                items.Add(RibbonMenus.Heading(heading));
                items.AddRange(styles.Select(s => Entry(s.Value, s.Value, s.Detail)));
            }

            return items;
        }

        private IReadOnlyList<RibbonMenuItem> FormatCellsMenu()
        {
            var hiddenSheets = State.Model.Sheets.Where(s => s.IsHidden).ToList();
            var visible = State.Model.Sheets.Count(s => !s.IsHidden);
            var items = new List<RibbonMenuItem>
            {
                DialogEntry("dialog:row-height", "행 높이…", "excel.home.formatcells", null, "format_rows_columns"),
                Entry(SheetCommands.FitRows, "행 높이 자동 맞춤", needs: "format_rows_columns"),
                DialogEntry("dialog:column-width", "열 너비…", "excel.home.formatcells", null, "format_rows_columns"),
                Entry(SheetCommands.FitColumns, "열 너비 자동 맞춤", needs: "format_rows_columns"),
                RibbonMenus.Separator(),
                RibbonMenus.Heading("숨기기"),
                Entry(SheetCommands.HideRows, "행 숨기기", needs: "format_rows_columns"),
                Entry(SheetCommands.HideColumns, "열 숨기기", needs: "format_rows_columns"),
                Entry(SheetCommands.HideSheet, "시트 숨기기", needs: "change_sheet", blocked: visible <= 1 ? "보이는 시트가 하나뿐이라 숨길 수 없어요." : null),
                Entry(SheetCommands.ShowRows, "행 숨기기 취소", needs: "format_rows_columns"),
                Entry(SheetCommands.ShowColumns, "열 숨기기 취소", needs: "format_rows_columns"),
            };
            items.AddRange(hiddenSheets.Select(s => Entry(SheetCommands.ShowSheet + s.Name, "시트 ‘" + s.Name + "’ 숨기기 취소", needs: "change_sheet")));
            items.Add(RibbonMenus.Separator());
            items.Add(Entry(SheetCommands.RenameSheet, "시트 이름 바꾸기", "시트 탭을 바로 고쳐요", needs: "change_sheet"));
            items.Add(Entry(SheetCommands.CopySheet, "시트 이동/복사", "복사본 만들기와 위치 옮기기", needs: "copy_sheet"));
            if (ToolHas("change_sheet", "tab_color"))
            {
                items.Add(RibbonMenus.Heading("탭 색"));
                items.Add(Entry("tab-color:none", "색 없음"));
                items.Add(RibbonMenus.Palette("탭 색 고르기", RibbonPalette.Standard, State.Sheet.TabColor));
            }
            else
            {
                items.Add(Entry("tab-color", "탭 색", blocked: PendingReason));
            }

            items.Add(RibbonMenus.Separator());
            items.Add(Entry("excel.review.protectsheet", "시트 보호", "검토 탭의 시트 보호와 같아요", blocked: DisabledReasonOf("excel.review.protectsheet")));
            items.Add(Entry("locked", "셀 잠금", check: State.IsLocked, needs: "format_range", blocked: ToolHas("format_range", "locked") ? null : PendingReason));
            items.Add(DialogEntry("dialog:cell-format", "셀 서식…", "excel.home.formatcells", "표시 형식, 맞춤, 글꼴, 테두리, 채우기, 보호를 한 창에서", "format_range"));
            return items;
        }

        private async Task RunCellsMenu(string arg)
        {
            if (string.IsNullOrEmpty(arg) || !_surface.Prepare())
                return;

            if (arg.StartsWith("dialog:", StringComparison.Ordinal))
            {
                State.OpenDialog("excel.home.formatcells");
                return;
            }

            if (arg.StartsWith("shift:", StringComparison.Ordinal))
            {
                await ShiftCells(arg.Substring("shift:".Length));
                return;
            }

            if (arg == "excel.review.protectsheet")
            {
                await InvokeAsync(arg, null);
                return;
            }

            switch (arg)
            {
                case "add-sheet":
                    Apply(EditCommand.Create("add_sheet", Workbook(("name", _surface.NextSheetName()))));
                    return;
                case "delete-sheet":
                    await _surface.RunCommandAsync(SheetCommands.DeleteSheet + ":" + State.Model.ActiveSheetIndex);
                    return;
                case "locked":
                    Apply(EditCommand.Create("format_range", Cells(("locked", !State.IsLocked))));
                    return;
                case SheetCommands.HideSheet:
                case SheetCommands.RenameSheet:
                case SheetCommands.CopySheet:
                    await _surface.RunCommandAsync(arg + ":" + State.Model.ActiveSheetIndex);
                    return;
            }

            if (arg.StartsWith("tab-color:", StringComparison.Ordinal))
            {
                Apply(EditCommand.Create("change_sheet", Sheetwide(("tab_color", "none"))));
                return;
            }

            if (RibbonPalette.Standard.Any(s => s.Value == arg))
            {
                Apply(EditCommand.Create("change_sheet", Sheetwide(("tab_color", arg))));
                return;
            }

            await _surface.RunCommandAsync(arg);
        }

        private Task ShiftCells(string shift)
        {
            var insert = shift == "right" || shift == "down";
            if (_surface.WholeRows || _surface.WholeColumns)
            {
                var rows = _surface.WholeRows;
                var command = insert
                    ? (rows ? SheetCommands.InsertRowsAbove : SheetCommands.InsertColumnsLeft)
                    : (rows ? SheetCommands.DeleteRows : SheetCommands.DeleteColumns);
                return _surface.RunCommandAsync(command);
            }

            Apply(EditCommand.Create("shift_cells", Cells(("action", insert ? "insert" : "delete"), ("shift", shift))));
            return Task.CompletedTask;
        }

        private Task AutoSum(string function)
        {
            if (!_surface.Prepare())
                return Task.CompletedTask;

            _surface.AutoSum(string.IsNullOrEmpty(function) ? "SUM" : function);
            return Task.CompletedTask;
        }

        private IReadOnlyList<RibbonMenuItem> AutoSumMenu()
        {
            var items = s_sumFunctions.Select(f => Entry(f.Function, f.Label)).ToList();
            items.Add(RibbonMenus.Separator());
            items.Add(Entry("more", "다른 함수…", "수식 입력줄에서 함수를 골라 적어요"));
            return items;
        }

        private IReadOnlyList<RibbonMenuItem> SortFilterMenu()
        {
            return new[]
            {
                Entry("asc", "텍스트 오름차순 정렬", needs: "sort_range"),
                Entry("desc", "텍스트 내림차순 정렬", needs: "sort_range"),
                DialogEntry("dialog:sort", "사용자 지정 정렬…", "excel.data.sort", "열을 여러 개 기준으로 정렬해요", "sort_range"),
                RibbonMenus.Separator(),
                Entry("filter", "필터", check: State.IsFiltered, needs: "filter_range"),
                Entry("clear_criteria", "지우기", needs: "filter_range", blocked: State.HasFilterCriteria ? null : "지울 필터 조건이 없어요."),
                Entry("reapply", "다시 적용", needs: "filter_range", blocked: State.HasFilterCriteria ? null : "다시 적용할 필터 조건이 없어요."),
            };
        }

        private Task RunSortFilter(string arg)
        {
            switch (arg)
            {
                case "asc":
                case "desc":
                    if (_surface.Prepare())
                        _surface.SortTable(arg == "desc");
                    return Task.CompletedTask;
                case "filter":
                case "clear_criteria":
                case "reapply":
                    return InvokeAsync(arg == "filter" ? "excel.data.filter" : arg == "clear_criteria" ? "excel.data.filter.clear" : "excel.data.filter.reapply", null);
                default:
                    if (arg != null && arg.StartsWith("dialog:", StringComparison.Ordinal))
                        return InvokeAsync("excel.data.sort", null);

                    return Task.CompletedTask;
            }
        }

        private IReadOnlyList<RibbonMenuItem> FindSelectMenu()
        {
            return new[]
            {
                Entry("find", "찾기…", key: "Ctrl+F"),
                Entry("replace", "바꾸기…", key: "Ctrl+H"),
                RibbonMenus.Separator(),
                Entry("goto", "이동…", "이름 상자로 가요", key: "Ctrl+G"),
                RibbonMenus.Separator(),
                Entry("select:formula", "수식"),
                Entry("select:constant", "상수"),
                Entry("select:blank", "빈 칸"),
                Entry("select:note", "메모"),
                Entry("select:conditional", "조건부 서식"),
                Entry("select:validation", "데이터 유효성 검사"),
            };
        }

        private void FindSelect(string arg)
        {
            switch (arg)
            {
                case "find":
                    _surface.OpenFind(false);
                    break;
                case "replace":
                    _surface.OpenFind(true);
                    break;
                case "goto":
                    _surface.FocusNameBox();
                    break;
                default:
                    if (arg != null && arg.StartsWith("select:", StringComparison.Ordinal))
                        _surface.SelectNext(arg.Substring("select:".Length));
                    break;
            }
        }
    }
}
