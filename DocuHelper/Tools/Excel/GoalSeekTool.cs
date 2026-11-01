using System;
using System.Collections.Generic;
using System.Globalization;
using DocumentFormat.OpenXml.Spreadsheet;
using DocuHelper.Docus;

namespace DocuHelper.Tools.Excel
{
    public class GoalSeekTool : ExcelTool
    {
        private const double DefaultTolerance = 0.001;
        private const int DefaultIterations = 100;
        private const int MaxIterations = 1000;
        private const int MaxEvaluations = 600;
        private const int BracketAttempts = 60;

        public override string Name => "goal_seek";

        public override string Description =>
            "Find the value of one input cell that makes a formula cell reach a target, like Excel's 목표값 찾기. "
            + "cell is the formula cell, target is the number it should reach, and changing_cell is the number cell it depends on (not a formula). "
            + "The changing cell is set to the answer and keeps it. tolerance (default 0.001) is how close counts as reached; max_iterations (default 100) limits the steps of each search stage. "
            + "When the target cannot be reached, the call is refused and the changing cell is left as it was.";

        public override string InputSchema => """
            {
              "type": "object",
              "properties": {
                "sheet": {"type": "string", "description": "Sheet name."},
                "cell": {"type": "string", "description": "Formula cell that should reach the target, e.g. \"D10\"."},
                "target": {"type": "number", "description": "The value the formula cell should reach."},
                "changing_cell": {"type": "string", "description": "Number cell to change, e.g. \"B3\"."},
                "tolerance": {"type": "number", "description": "How close to the target counts as reached. Default 0.001."},
                "max_iterations": {"type": "integer", "description": "Steps allowed for each search stage (1-1000). Default 100."}
              },
              "required": ["sheet", "cell", "target", "changing_cell"]
            }
            """;

        protected override ToolResult Run(ExcelDocu docu, ExcelBook book, ToolInput input)
        {
            var sheet = ResolveSheet(book, input, null, true);
            var target = ParseCell(input.GetString("cell"));
            var changing = ParseCell(input.GetString("changing_cell"));
            var goal = input.GetOptionalNumber("target") ?? throw new ToolInputException("target에 찾을 값을 숫자로 알려 주세요.");
            var tolerance = input.GetOptionalNumber("tolerance") ?? DefaultTolerance;
            if (!(tolerance > 0))
                throw new ToolInputException("tolerance는 0보다 큰 숫자로 알려 주세요.");

            var iterations = input.GetOptionalInt("max_iterations") ?? DefaultIterations;
            if (iterations < 1 || iterations > MaxIterations)
                throw new ToolInputException($"max_iterations는 1부터 {MaxIterations} 사이로 알려 주세요.");

            if (target.Equals(changing))
                throw new ToolInputException("바꿀 칸과 목표 칸은 달라야 해요.");

            var targetCell = sheet.FindCell(target.Column, target.Row);
            if (targetCell?.CellFormula == null)
                throw new ToolInputException($"{target} 칸에는 수식이 있어야 해요. 목표값을 맞출 수식 칸을 알려 주세요.");

            var inputCell = sheet.FindCell(changing.Column, changing.Row);
            if (inputCell?.CellFormula != null)
                throw new ToolInputException($"{changing} 칸은 수식이라서 바꿀 수 없어요. 숫자가 들어 있는 칸을 알려 주세요.");

            var current = inputCell == null ? ExcelValue.Blank : book.ReadValue(inputCell);
            if (!current.IsBlank && !current.IsNumber)
                throw new ToolInputException($"{changing} 칸에는 숫자가 있어야 해요.");

            var origin = current.IsNumber ? current.Number : 0;
            sheet.PrepareForWrite();
            var backup = inputCell?.CloneNode(true) as Cell;
            var key = new CellKey(sheet.Index, changing.Column, changing.Row);
            var evaluations = 0;

            double Residual(double x)
            {
                evaluations++;
                if (evaluations > MaxEvaluations)
                    throw new ToolInputException($"계산을 {MaxEvaluations}번 넘게 해도 목표값을 찾지 못해서 멈췄어요. 목표값이나 바꿀 칸을 다시 확인해 주세요.");

                ExcelBook.SetNumber(sheet.GetOrCreateCell(changing.Column, changing.Row), x);
                var result = Recalculate(book, new[] { key }, false);
                if (result.Aborted)
                    throw new ToolInputException("통합 문서가 커서 이 도구로는 계산할 수 없어요.");

                var value = book.ReadValue(sheet.FindCell(target.Column, target.Row));
                if (!value.IsNumber)
                    return double.NaN;

                return value.Number - goal;
            }

            try
            {
                var answer = Solve(Residual, origin, tolerance, iterations);
                if (answer == null)
                    throw new ToolInputException($"{target} 칸을 목표값 {goal.ToString("0.######", CultureInfo.InvariantCulture)}에 맞출 수 없었어요. 목표값이 닿을 수 없는 범위일 수 있어요. {changing} 칸은 그대로 뒀어요.");

                Residual(answer.Value);
                var reached = book.ReadValue(sheet.FindCell(target.Column, target.Row)).Number;
                sheet.Invalidate();
                var content = $"Set ‘{sheet.Name}’ {changing} from {origin.ToString("0.######", CultureInfo.InvariantCulture)} to {answer.Value.ToString("0.######", CultureInfo.InvariantCulture)}, so {target} is now {reached.ToString("0.######", CultureInfo.InvariantCulture)} (target {goal.ToString("0.######", CultureInfo.InvariantCulture)}, {evaluations} calculations).";
                var change = new DocuChange
                {
                    Location = $"{changing} → {target}",
                    Icon = "target",
                    Anchors = Anchors(sheet, new[] { changing, target }),
                };
                return ToolResult.Changed(content, change, "목표값 찾기");
            }
            catch
            {
                Restore(sheet, changing, backup);
                Recalculate(book, new[] { key }, false);
                sheet.Invalidate();
                throw;
            }
        }

        private static void Restore(ExcelSheet sheet, CellAddress changing, Cell backup)
        {
            if (backup == null)
                sheet.RemoveCell(changing.Column, changing.Row);
            else
                sheet.ReplaceCell(changing.Column, changing.Row, backup);
        }

        private static double? Solve(Func<double, double> residual, double origin, double tolerance, int iterations)
        {
            var x0 = origin;
            var f0 = residual(x0);
            if (Math.Abs(f0) <= tolerance)
                return x0;

            var x1 = origin == 0 ? 1 : origin * 1.01;
            var f1 = residual(x1);
            for (var step = 0; step < iterations; step++)
            {
                if (Math.Abs(f1) <= tolerance)
                    return x1;

                if (f1 == f0)
                    break;

                var x2 = x1 - f1 * (x1 - x0) / (f1 - f0);
                if (double.IsNaN(x2) || double.IsInfinity(x2))
                    break;

                x0 = x1;
                f0 = f1;
                x1 = x2;
                f1 = residual(x1);
            }

            if (Math.Abs(f1) <= tolerance)
                return x1;

            return Bracket(residual, x1, tolerance, iterations);
        }

        private static double? Bracket(Func<double, double> residual, double center, double tolerance, int iterations)
        {
            var step = Math.Max(1, Math.Abs(center));
            for (var attempt = 0; attempt < BracketAttempts; attempt++)
            {
                var low = center - step;
                var high = center + step;
                var fLow = residual(low);
                if (Math.Abs(fLow) <= tolerance)
                    return low;

                var fHigh = residual(high);
                if (Math.Abs(fHigh) <= tolerance)
                    return high;

                if (!double.IsNaN(fLow) && !double.IsNaN(fHigh) && Math.Sign(fLow) != Math.Sign(fHigh))
                    return Bisect(residual, low, fLow, high, tolerance, iterations);

                step *= 2;
            }

            return null;
        }

        private static double? Bisect(Func<double, double> residual, double low, double fLow, double high, double tolerance, int iterations)
        {
            for (var step = 0; step < iterations; step++)
            {
                var middle = (low + high) / 2;
                var fMiddle = residual(middle);
                if (Math.Abs(fMiddle) <= tolerance)
                    return middle;

                if (Math.Abs(high - low) <= 1e-12 * Math.Max(1, Math.Abs(middle)) || double.IsNaN(fMiddle))
                    return null;

                if (Math.Sign(fMiddle) == Math.Sign(fLow))
                {
                    low = middle;
                    fLow = fMiddle;
                }
                else
                {
                    high = middle;
                }
            }

            var last = (low + high) / 2;
            return Math.Abs(residual(last)) <= tolerance ? last : null;
        }
    }
}
