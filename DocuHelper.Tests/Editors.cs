using System.Collections.Generic;
using DocuHelper.Docus;
using DocuHelper.Editors;

namespace DocuHelper.Tests
{
    internal static class Editors
    {
        public static WordEditor Word { get; } = new WordEditor();

        public static HwpEditor Hwp { get; } = new HwpEditor(null);

        public static PptEditor Ppt { get; } = new PptEditor();

        public static ExcelEditor Excel { get; } = new ExcelEditor();

        public static IEnumerable<DocuEditor> All => new DocuEditor[] { Word, Hwp, Ppt, Excel };

        public static DocuEditor Of(DocuType type) => type switch
        {
            DocuType.Word => Word,
            DocuType.Hwp => Hwp,
            DocuType.Ppt => Ppt,
            _ => Excel,
        };

        public static IEnumerable<object[]> Types()
        {
            foreach (var editor in All)
                yield return new object[] { editor.TargetType };
        }
    }
}
