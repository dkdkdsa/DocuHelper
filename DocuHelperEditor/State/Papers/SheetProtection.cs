namespace DocuHelperEditor.State.Papers
{
    public class SheetProtection
    {
        public bool IsProtected { get; set; }

        public bool HasPassword { get; set; }

        public bool SelectLockedCells { get; set; } = true;

        public bool SelectUnlockedCells { get; set; } = true;

        public bool AllowFormatCells { get; set; }

        public bool AllowFormatColumns { get; set; }

        public bool AllowFormatRows { get; set; }

        public bool AllowInsertColumns { get; set; }

        public bool AllowInsertRows { get; set; }

        public bool AllowInsertHyperlinks { get; set; }

        public bool AllowDeleteColumns { get; set; }

        public bool AllowDeleteRows { get; set; }

        public bool AllowSort { get; set; }

        public bool AllowAutoFilter { get; set; }

        public bool AllowPivotTables { get; set; }

        public bool AllowObjects { get; set; }

        public bool AllowScenarios { get; set; }
    }
}
