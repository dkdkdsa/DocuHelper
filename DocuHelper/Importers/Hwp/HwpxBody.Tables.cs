using System;
using System.Xml.Linq;

namespace DocuHelper.Importers.Hwp
{
    internal sealed partial class HwpxBody
    {
        private HwpTable ReadTable(XElement element)
        {
            var table = new HwpTable
            {
                RowCount = Math.Max(0, HwpxXml.Int(element, "rowCnt", 0)),
                ColumnCount = Math.Max(0, HwpxXml.Int(element, "colCnt", 0)),
            };
            var row = 0;
            foreach (var line in HwpxXml.Children(element, "tr"))
            {
                var column = 0;
                foreach (var item in HwpxXml.Children(line, "tc"))
                {
                    var address = HwpxXml.Child(item, "cellAddr");
                    var span = HwpxXml.Child(item, "cellSpan");
                    var cell = new HwpCell
                    {
                        Row = Math.Max(0, HwpxXml.Int(address, "rowAddr", row)),
                        Column = Math.Max(0, HwpxXml.Int(address, "colAddr", column)),
                        RowSpan = Math.Max(1, HwpxXml.Int(span, "rowSpan", 1)),
                        ColumnSpan = Math.Max(1, HwpxXml.Int(span, "colSpan", 1)),
                        Width = Math.Max(0, HwpxXml.Int(HwpxXml.Child(item, "cellSz"), "width", 0)),
                    };
                    ReadList(HwpxXml.Child(item, "subList"), cell.Blocks);
                    table.Cells.Add(cell);
                    table.RowCount = Math.Max(table.RowCount, cell.Row + cell.RowSpan);
                    table.ColumnCount = Math.Max(table.ColumnCount, cell.Column + cell.ColumnSpan);
                    column = cell.Column + cell.ColumnSpan;
                }

                row++;
            }

            return table;
        }
    }
}
