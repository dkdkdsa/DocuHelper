using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocuHelper.Dash;
using DocuHelper.Dash.Logic;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashDataForms
    {
        private const string FormulaExample = "SUM(기간표.금액)";

        public static DashDataForm Source(DashDocument document, string type, IReadOnlyList<string> files)
        {
            var form = new DashFormBuilder()
                .Text("name", "이름", DashNaming.Free(document, "원본"))
                .Choice("file", "파일", files.FirstOrDefault(), files.Select(file => (file, file)));
            var numbers = new List<(string Key, string Label)>();
            switch (type)
            {
                case DashSourceFiles.Excel:
                    form.Text("sheet", "시트 이름 (비우면 첫 시트)", string.Empty).Number("header", "머리글 줄", 1);
                    numbers.Add(("header", "머리글 줄"));
                    break;
                case DashSourceFiles.Word:
                    form.Number("table", "몇 번째 표", 1);
                    numbers.Add(("table", "몇 번째 표"));
                    break;
                case DashSourceFiles.PptTable:
                    form.Number("slide", "슬라이드", 1).Number("table", "몇 번째 표", 1);
                    numbers.Add(("slide", "슬라이드"));
                    numbers.Add(("table", "몇 번째 표"));
                    break;
                case DashSourceFiles.PptChart:
                    form.Number("slide", "슬라이드", 1).Number("chart", "몇 번째 차트", 1);
                    numbers.Add(("slide", "슬라이드"));
                    numbers.Add(("chart", "몇 번째 차트"));
                    break;
            }

            string Validate(IReadOnlyDictionary<string, JsonNode> values)
            {
                var problem = DashNaming.Problem(document, DashFormBuilder.TextOf(values, "name"), true);
                if (problem != null)
                    return problem;

                var wrong = numbers.FirstOrDefault(number => DashFormBuilder.NumberOf(values, number.Key) is not >= 1);
                return wrong.Key == null ? null : $"‘{wrong.Label}’에는 1 이상의 숫자를 써 주세요.";
            }

            JsonObject Build(IReadOnlyDictionary<string, JsonNode> values)
            {
                var item = new JsonObject
                {
                    ["type"] = type,
                    ["name"] = DashFormBuilder.TextOf(values, "name").Trim(),
                    ["file"] = DashFormBuilder.TextOf(values, "file"),
                };
                var sheet = DashFormBuilder.TextOf(values, "sheet").Trim();
                if (sheet.Length > 0)
                    item["sheet"] = sheet;

                foreach (var (key, _) in numbers)
                    item[key] = (int)Math.Round(DashFormBuilder.NumberOf(values, key) ?? 1);

                return item;
            }

            return new DashDataForm(Title(type), form.Fields, form.Defaults, Validate, Build);
        }

        public static DashDataForm Param(DashDocument document, string type)
        {
            var seed = DashInserts.NewParam(document, type);
            var start = DashParamForm.Of(JsonSerializer.Deserialize<DashParam>(seed.ToJsonString(), DashFile.Options));
            var form = new DashFormBuilder()
                .Text("name", "이름", start.Name)
                .Text("label", "화면에 보일 이름", start.Label);
            switch (start.Kind)
            {
                case DashParamForm.NumberKind:
                    form.Number("min", "가장 작은 값", Parse(start.Min))
                        .Number("max", "가장 큰 값", Parse(start.Max))
                        .Number("step", "한 칸 크기", Parse(start.Step))
                        .Number("initial", "처음 값", Parse(start.Initial));
                    break;
                case DashParamForm.RangeKind:
                    form.Number("min", "가장 작은 값", Parse(start.Min))
                        .Number("max", "가장 큰 값", Parse(start.Max))
                        .Number("from", "처음 시작", Parse(start.From))
                        .Number("to", "처음 끝", Parse(start.To));
                    break;
                case DashParamForm.PickKind:
                    form.Text("options", "고를 것들 (쉼표로 나눠 써요)", string.Join(", ", start.OptionList()))
                        .Text("all", "‘전체’ 항목 이름 (비우면 없음)", string.Empty)
                        .Text("choice", "처음 값", start.Choice);
                    break;
                default:
                    form.Flag("on", "처음에 켜 두기", start.On);
                    break;
            }

            string Validate(IReadOnlyDictionary<string, JsonNode> values)
            {
                var problem = DashNaming.Problem(document, DashFormBuilder.TextOf(values, "name"), false);
                return problem ?? DashParamForm.Read(start.Kind, values).Problem();
            }

            JsonObject Build(IReadOnlyDictionary<string, JsonNode> values) => DashParamForm.Read(start.Kind, values).Item();

            return new DashDataForm(DashParamForm.KindLabel(start.Kind) + " 조절값 추가", form.Fields, form.Defaults, Validate, Build);
        }

        public static DashDataForm Table(DashDocument document, string preferred)
        {
            var options = Origins(document);
            var first = options.Any(option => option.Value == preferred) ? preferred : options.Select(option => option.Value).FirstOrDefault();
            var form = new DashFormBuilder()
                .Text("name", "이름", DashNaming.Free(document, "표"))
                .Choice("source", "가져올 곳", first, options);

            string Validate(IReadOnlyDictionary<string, JsonNode> values) =>
                DashNaming.Problem(document, DashFormBuilder.TextOf(values, "name"), false);

            JsonObject Build(IReadOnlyDictionary<string, JsonNode> values) => new()
            {
                ["type"] = "table",
                ["name"] = DashFormBuilder.TextOf(values, "name").Trim(),
                ["source"] = DashFormBuilder.TextOf(values, "source"),
                ["steps"] = new JsonArray(),
            };

            return new DashDataForm("새 표", form.Fields, form.Defaults, Validate, Build);
        }

        public static DashDataForm Formula(DashDocument document)
        {
            var form = new DashFormBuilder()
                .Text("name", "이름", DashNaming.Free(document, "값"))
                .Text("expression", $"식 (예: {FormulaExample})", string.Empty)
                .Choice("format", "모양", DashValueFormats.Default, DashValueFormats.All);

            string Validate(IReadOnlyDictionary<string, JsonNode> values)
            {
                var problem = DashNaming.Problem(document, DashFormBuilder.TextOf(values, "name"), false);
                return problem ?? ExpressionProblem(DashFormBuilder.TextOf(values, "expression"));
            }

            JsonObject Build(IReadOnlyDictionary<string, JsonNode> values) => new()
            {
                ["type"] = "formula",
                ["name"] = DashFormBuilder.TextOf(values, "name").Trim(),
                ["expression"] = DashFormBuilder.TextOf(values, "expression").Trim(),
                ["format"] = DashFormBuilder.TextOf(values, "format"),
            };

            return new DashDataForm("새 값", form.Fields, form.Defaults, Validate, Build);
        }

        public static string ExpressionProblem(string expression) =>
            DashNames.TryParse(expression, out _) ? null : $"식을 읽을 수 없어요. 예: {FormulaExample}";

        private static IEnumerable<(string Value, string Label)> Origins(DashDocument document) =>
            document.Values.OfType<StepsTable>().Where(table => !string.IsNullOrWhiteSpace(table.Name)).Select(table => (table.Name, table.Name + " · 표"))
                .Concat(document.Sources.Where(source => source != null && !string.IsNullOrWhiteSpace(source.Name)).Select(source => (source.Name, source.Name + " · 원본")));

        private static double? Parse(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

        private static string Title(string type) => type switch
        {
            DashSourceFiles.Excel => "엑셀 시트 연결",
            DashSourceFiles.Word => "워드·한글 표 연결",
            DashSourceFiles.PptTable => "PPT 표 연결",
            _ => "PPT 차트 연결",
        };
    }
}
