using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DocuHelper.Editors;
using DocuHelper.Tools;
using DocuHelperEditor.Components.Paper.Ppt;
using DocuHelperEditor.State.Ribbon;
using DocuHelperEditor.State.Ribbon.Excel;
using Xunit;

namespace DocuHelper.Tests
{
    // 리본 배치(*Ribbon), 커맨드 스펙(*CommandTable, PptCommandData), 코어 도구 스키마가 서로 맞는지 검사해요.
    // 라우터(WordCommandRouter 등)는 MAUI·Blazor에 묶여 있어 여기서는 다루지 않아요.
    // Excel 커맨드는 라우터 코드에서 등록하니 배치만 검사해요.
    public class RibbonSpecTests
    {
        // 메뉴 항목 인자 중 라우터가 읽고 도구에는 넘기지 않는 키예요.
        private static readonly HashSet<string> s_menuControlKeys = new(StringComparer.Ordinal)
        {
            "dialog", "command", "picker", "value", "prompt",
        };

        // 도구 스키마에 없는 인자 키 중 이유를 아는 것들이에요. "커맨드 id → 도구.키" 형식이에요.
        // 고쳐서 더 이상 나오지 않으면 Known_gaps_are_still_real 테스트가 실패하니 여기서 지워 주세요.
        private static readonly Dictionary<string, string> s_knownGaps = new(StringComparer.Ordinal)
        {
            // 라우터가 직접 읽어 처리하는 키
            ["word.home.clipboard.paste -> edit_text.mode"] = "붙여넣기 방식은 WordCommandRouter.Clipboard가 읽어요",
            ["hwp.edit.clipboard.paste -> edit_text.mode"] = "붙여넣기 방식은 HwpCommandRouter.Clipboard가 읽어요",
            ["word.home.clipboard.formatpainter -> format_text.painter"] = "서식 복사는 Action=formatPainter로 라우터가 직접 처리해요",
            ["word.home.clipboard.formatpainter -> format_text.paragraph_painter"] = "서식 복사는 Action=formatPainter로 라우터가 직접 처리해요",
            ["word.insert.table.table -> insert_table.columns"] = "라우터가 rows×columns로 표 칸을 만들어 넘겨요",
            ["hwp.edit.insert.table -> insert_table.columns"] = "라우터가 rows×columns로 표 칸을 만들어 넘겨요",

            // 도구가 아직 받지 않아 리본이 스키마를 보고 버튼을 꺼 두는 키
            ["ppt.anim.trigger -> set_shape_animation.trigger"] = "도구가 트리거를 아직 지원하지 않아요. PptRibbonCommands가 스키마를 보고 꺼 둬요",
            ["ppt.trans.sound -> set_slide_transition.sound"] = "도구가 전환 소리를 아직 지원하지 않아요. PptRibbonCommands가 스키마를 보고 꺼 둬요",

            // 의심 버그: 아무도 읽지 않고 도구도 모르는 키라 조용히 무시돼요
            ["word.review.changes.accept -> manage_revisions.then"] = "TODO: '적용하고 다음으로 이동'·'적용하고 추적 중지'의 then을 처리하는 코드가 없어요",
            ["word.review.changes.reject -> manage_revisions.then"] = "TODO: '적용 안 하고 다음으로 이동'·'적용 안 하고 추적 중지'의 then을 처리하는 코드가 없어요",
            ["hwp.edit.format.char -> format_text.scale_percent"] = "TODO: 글자 모양의 장평을 format_text가 받지 않아 무시돼요",
        };

        private sealed record SpecArgs(string Format, string Id, string Tool, string Json, bool IsMenu);

        public static IEnumerable<object[]> Ribbons()
        {
            yield return new object[] { "word" };
            yield return new object[] { "hwp" };
            yield return new object[] { "ppt" };
            for (var level = 1; level <= ExcelRibbonDefinition.MaxLevel; level++)
                yield return new object[] { "excel" + level };
        }

        [Fact]
        public void Spec_ids_are_unique()
        {
            var duplicates = WordCommandTable.All.Select(s => s.Id)
                .Concat(HwpCommandTable.All.Select(s => s.Id))
                .Concat(PptCommandData.All.Select(s => s.Id))
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.True(duplicates.Count == 0, "스펙 id가 겹쳐요: " + string.Join(", ", duplicates));
        }

        // 한글 리본처럼 같은 버튼이 여러 탭에 나오는 건 괜찮아요. 한 탭 안에서만 겹치지 않으면 돼요.
        [Theory]
        [MemberData(nameof(Ribbons))]
        public void Layout_ids_are_unique_within_a_tab(string ribbon)
        {
            var (tabs, files) = LayoutOf(ribbon);
            var duplicates = tabs
                .Select(tab => (Key: tab.Key, Ids: IdsOf(tab)))
                .Append((Key: "file", Ids: files.Select(f => f.Id).Where(id => !string.IsNullOrEmpty(id))))
                .SelectMany(tab => tab.Ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => $"{tab.Key}: {g.Key}"))
                .ToList();

            Assert.True(duplicates.Count == 0, $"{ribbon} 리본의 한 탭에 같은 id가 두 번 나와요: " + string.Join(", ", duplicates));
        }

        [Theory]
        [InlineData("word")]
        [InlineData("hwp")]
        public void Every_layout_item_has_a_spec(string ribbon)
        {
            var specs = (ribbon == "word" ? WordCommandTable.All.Select(s => s.Id) : HwpCommandTable.All.Select(s => s.Id)).ToHashSet();
            var missing = LayoutIds(ribbon).Where(id => !specs.Contains(id)).Distinct().ToList();

            Assert.True(missing.Count == 0, $"{ribbon} 리본에 있는데 스펙이 없는 버튼: " + string.Join(", ", missing));
        }

        [Fact]
        public void Spec_tools_exist_in_the_editor()
        {
            var missing = AllArgs()
                .Where(a => a.Tool != null && !EditorOf(a.Format).Tools.ContainsKey(a.Tool))
                .Select(a => $"{a.Id} -> {a.Tool}")
                .Distinct()
                .ToList();

            Assert.True(missing.Count == 0, "편집기에 없는 도구를 가리키는 스펙: " + string.Join(", ", missing));
        }

        [Fact]
        public void Spec_json_parses()
        {
            var problems = new List<string>();
            foreach (var (id, json) in AllJson())
            {
                try
                {
                    JsonNode.Parse(json);
                }
                catch (Exception e)
                {
                    problems.Add($"{id}: {e.Message}");
                }
            }

            Assert.True(problems.Count == 0, "JSON으로 읽히지 않는 스펙: " + string.Join(Environment.NewLine, problems));
        }

        [Fact]
        public void Spec_args_are_known_to_the_tool_schema()
        {
            var unknown = UnknownArgs().Where(key => !s_knownGaps.ContainsKey(key)).ToList();

            Assert.True(
                unknown.Count == 0,
                "도구 스키마에 없는 인자를 보내는 스펙이에요. 도구에 인자를 추가하거나, 라우터가 처리하는 키라면 s_knownGaps에 이유와 함께 적어 주세요:"
                    + Environment.NewLine + string.Join(Environment.NewLine, unknown));
        }

        [Fact]
        public void Known_gaps_are_still_real()
        {
            var unknown = UnknownArgs().ToHashSet();
            var stale = s_knownGaps.Keys.Where(key => !unknown.Contains(key)).ToList();

            Assert.True(stale.Count == 0, "이제 문제가 없는 항목이에요. s_knownGaps에서 지워 주세요: " + string.Join(", ", stale));
        }

        private static IEnumerable<string> UnknownArgs()
        {
            foreach (var args in AllArgs())
            {
                if (args.Tool == null || string.IsNullOrWhiteSpace(args.Json))
                    continue;

                if (!EditorOf(args.Format).Tools.TryGetValue(args.Tool, out var tool))
                    continue;

                if (Parse(args.Json) is not JsonObject json)
                    continue;

                var properties = PropertiesOf(tool);
                foreach (var key in json.Select(p => p.Key))
                {
                    if (args.IsMenu && s_menuControlKeys.Contains(key))
                        continue;

                    if (!properties.Contains(key))
                        yield return $"{args.Id} -> {args.Tool}.{key}";
                }
            }
        }

        private static IEnumerable<SpecArgs> AllArgs()
        {
            foreach (var spec in WordCommandTable.All)
            {
                yield return new SpecArgs("word", spec.Id, spec.Tool, spec.Args, false);
                foreach (var menu in spec.Menu ?? Array.Empty<WordMenuSpec>())
                    yield return new SpecArgs("word", spec.Id, spec.Tool, menu.Args, true);
            }

            foreach (var spec in HwpCommandTable.All)
            {
                yield return new SpecArgs("hwp", spec.Id, spec.Tool, spec.Args, false);
                foreach (var menu in spec.Menu ?? Array.Empty<HwpMenuSpec>())
                {
                    // Gates가 있는 메뉴는 HwpCapabilityGate가 도구 스키마를 확인할 때까지 꺼 둬요.
                    if (menu.Gates.Count == 0)
                        yield return new SpecArgs("hwp", spec.Id, spec.Tool, menu.Args, true);
                }
            }

            foreach (var spec in PptCommandData.All)
            {
                yield return new SpecArgs("ppt", spec.Id, spec.Tool, spec.Args, false);
                yield return new SpecArgs("ppt", spec.Id, spec.Tool, spec.ArgsOff, false);
                foreach (var menu in spec.Menu ?? Array.Empty<PptMenuSpec>())
                    yield return new SpecArgs("ppt", spec.Id, menu.Tool ?? spec.Tool, menu.Args, true);
            }
        }

        private static IEnumerable<(string Id, string Json)> AllJson()
        {
            foreach (var args in AllArgs())
            {
                if (!string.IsNullOrWhiteSpace(args.Json))
                    yield return (args.Id, args.Json);
            }

            foreach (var spec in PptCommandData.All)
            {
                if (!string.IsNullOrWhiteSpace(spec.Dialog))
                    yield return (spec.Id + " (dialog)", spec.Dialog);

                foreach (var menu in spec.Menu ?? Array.Empty<PptMenuSpec>())
                {
                    if (!string.IsNullOrWhiteSpace(menu.Dialog))
                        yield return (spec.Id + " (menu dialog)", menu.Dialog);

                    if (!string.IsNullOrWhiteSpace(menu.Picker))
                        yield return (spec.Id + " (menu picker)", menu.Picker);
                }
            }
        }

        private static IEnumerable<string> LayoutIds(string ribbon)
        {
            var (tabs, files) = LayoutOf(ribbon);
            return tabs.SelectMany(IdsOf).Concat(files.Select(f => f.Id)).Where(id => !string.IsNullOrEmpty(id));
        }

        private static IEnumerable<string> IdsOf(RibbonTab tab) =>
            tab.Groups.SelectMany(g => g.Items.Concat(g.Row1).Concat(g.Row2)).Select(i => i.Id).Where(id => !string.IsNullOrEmpty(id));

        private static (IReadOnlyList<RibbonTab> Tabs, IReadOnlyList<RibbonFileItem> Files) LayoutOf(string ribbon) => ribbon switch
        {
            "word" => (WordRibbon.WithTable, WordRibbon.FileItems),
            "hwp" => (HwpRibbon.Tabs, HwpRibbon.FileItems),
            "ppt" => (PptRibbonData.Tabs, PptRibbonData.FileItems),
            _ => (ExcelRibbonDefinition.Tabs(int.Parse(ribbon.Substring("excel".Length))), ExcelRibbonDefinition.FileItems),
        };

        private static DocuEditor EditorOf(string format) => format switch
        {
            "word" => Editors.Word,
            "hwp" => Editors.Hwp,
            _ => Editors.Ppt,
        };

        private static HashSet<string> PropertiesOf(Tool tool) =>
            (JsonNode.Parse(tool.InputSchema)?["properties"] as JsonObject)?.Select(p => p.Key).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);

        private static JsonNode Parse(string json)
        {
            try
            {
                return JsonNode.Parse(json);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
