using System.Collections.Generic;

namespace DocuHelperEditor.State.Papers
{
    public readonly record struct OutlineGroup(int Summary, int First, int Last, int Level);

    public static class SheetOutline
    {
        public static List<OutlineGroup> Groups(Sheet sheet)
        {
            var groups = new List<OutlineGroup>();
            var rows = sheet.Rows;
            for (var index = 0; index < rows.Count; index++)
            {
                if (sheet.SummaryBelow)
                {
                    if (index == 0 || rows[index - 1].OutlineLevel <= rows[index].OutlineLevel)
                        continue;

                    var first = index - 1;
                    while (first > 0 && rows[first - 1].OutlineLevel > rows[index].OutlineLevel)
                        first--;

                    groups.Add(new OutlineGroup(index, first, index - 1, rows[index - 1].OutlineLevel));
                }
                else
                {
                    if (index + 1 >= rows.Count || rows[index + 1].OutlineLevel <= rows[index].OutlineLevel)
                        continue;

                    var last = index + 1;
                    while (last + 1 < rows.Count && rows[last + 1].OutlineLevel > rows[index].OutlineLevel)
                        last++;

                    groups.Add(new OutlineGroup(index, index + 1, last, rows[index + 1].OutlineLevel));
                }
            }

            return groups;
        }

        public static bool IsExpanded(Sheet sheet, OutlineGroup group) => !sheet.Rows[group.Summary].Collapsed;
    }
}
