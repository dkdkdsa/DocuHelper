using System;
using System.Linq;

namespace DocuHelperEditor.State.Papers
{
    public readonly struct SheetRegion
    {
        public SheetRegion(int top, int left, int bottom, int right)
        {
            Top = top;
            Left = left;
            Bottom = bottom;
            Right = right;
        }

        public int Top { get; }

        public int Left { get; }

        public int Bottom { get; }

        public int Right { get; }

        public void Deconstruct(out int top, out int left, out int bottom, out int right)
        {
            top = Top;
            left = Left;
            bottom = Bottom;
            right = Right;
        }

        public bool TouchesEdge(Sheet sheet) => Bottom >= sheet.Rows.Count - 1 || Right >= sheet.Columns.Count - 2;

        public static SheetRegion? Around(Sheet sheet, int column, int row)
        {
            bool Filled(int r, int c) => r >= 0 && c >= 0 && HasValue(sheet.FindCell(r, c));
            bool RowHas(int r, int from, int to) => Enumerable.Range(Math.Max(0, from), Math.Max(0, to - Math.Max(0, from) + 1)).Any(c => Filled(r, c));
            bool ColumnHas(int c, int from, int to) => Enumerable.Range(Math.Max(0, from), Math.Max(0, to - Math.Max(0, from) + 1)).Any(r => Filled(r, c));

            int top = row, bottom = row, left = column, right = column;
            var changed = true;
            while (changed)
            {
                changed = false;
                if (top > 0 && RowHas(top - 1, left - 1, right + 1))
                {
                    top--;
                    changed = true;
                }

                if (RowHas(bottom + 1, left - 1, right + 1))
                {
                    bottom++;
                    changed = true;
                }

                if (left > 0 && ColumnHas(left - 1, top - 1, bottom + 1))
                {
                    left--;
                    changed = true;
                }

                if (ColumnHas(right + 1, top - 1, bottom + 1))
                {
                    right++;
                    changed = true;
                }
            }

            while (top < bottom && !RowHas(top, left, right))
                top++;

            while (bottom > top && !RowHas(bottom, left, right))
                bottom--;

            while (left < right && !ColumnHas(left, top, bottom))
                left++;

            while (right > left && !ColumnHas(right, top, bottom))
                right--;

            if (!RowHas(top, left, right))
                return null;

            return new SheetRegion(top, left, bottom, right);
        }

        public static bool StartsWithHeader(Sheet sheet, int top, int left, int right, int column)
        {
            if (sheet.FindRow(top)?.Kind == SheetRowKind.Header)
                return true;

            var cells = Enumerable.Range(left, right - left + 1).Select(c => sheet.FindCell(top, c)).Where(HasValue).ToList();
            if (cells.Count == 0 || cells.Any(c => c.IsNumber || !string.IsNullOrEmpty(c.Formula)))
                return false;

            if (cells.All(c => c.Bold == true))
                return true;

            var below = sheet.FindCell(top + 1, column);
            return below != null && below.IsNumber;
        }

        public static SumPlan PlanSum(Sheet sheet, int column, int row)
        {
            bool IsNumber(int r, int c) => r >= 0 && c >= 0 && sheet.FindCell(r, c) is { IsNumber: true } cell && HasValue(cell);

            var selected = sheet.FindCell(row, column);
            if (HasValue(selected))
            {
                if (!selected.IsNumber)
                    return SumPlan.Fail("숫자들 바로 아래나 오른쪽의 빈칸을 고른 뒤 눌러 주세요.");

                var top = row;
                while (IsNumber(top - 1, column))
                    top--;

                var bottom = row;
                while (IsNumber(bottom + 1, column))
                    bottom++;

                if (sheet.IsClipped && bottom >= sheet.Rows.Count - 1)
                    return SumPlan.Fail("표가 너무 커서 여기서는 합계를 낼 수 없어요. 도우미에게 부탁해 주세요.");

                if (HasValue(sheet.FindCell(bottom + 1, column)))
                    return SumPlan.Fail("합계를 넣을 아래 칸이 비어 있지 않아요. 숫자들 바로 아래 빈칸을 골라 주세요.");

                return SumPlan.At(column, bottom + 1, Range(column, top, column, bottom));
            }

            if (IsNumber(row - 1, column))
            {
                var top = row - 1;
                while (IsNumber(top - 1, column))
                    top--;

                return SumPlan.At(column, row, Range(column, top, column, row - 1));
            }

            if (IsNumber(row, column - 1))
            {
                var left = column - 1;
                while (IsNumber(row, left - 1))
                    left--;

                return SumPlan.At(column, row, Range(left, row, column - 1, row));
            }

            return SumPlan.Fail("더할 숫자를 찾지 못했어요. 숫자들 바로 아래나 오른쪽의 빈칸을 고른 뒤 눌러 주세요.");
        }

        private static string Range(int left, int top, int right, int bottom)
        {
            var start = SheetRef.Make(left, top);
            return left == right && top == bottom ? start : start + ":" + SheetRef.Make(right, bottom);
        }

        private static bool HasValue(SheetCell cell) =>
            cell != null && (!string.IsNullOrEmpty(cell.Text) || !string.IsNullOrEmpty(cell.Formula));
    }
}
