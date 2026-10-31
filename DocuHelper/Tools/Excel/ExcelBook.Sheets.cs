using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Threaded = DocumentFormat.OpenXml.Office2019.Excel.ThreadedComments;

namespace DocuHelper.Tools.Excel
{
    public sealed partial class ExcelBook
    {
        private static readonly MethodInfo s_addNewPart = typeof(OpenXmlPartContainer).GetMethods()
            .First(m => m.Name == nameof(OpenXmlPartContainer.AddNewPart) && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 2 && m.GetParameters().All(p => p.ParameterType == typeof(string)));

        public int VisibleSheetCount => SheetEntries().Count(e => !IsHiddenEntry(e));

        internal WorkbookFormulas.Outcome RenameSheet(ExcelSheet sheet, string name)
        {
            var outcome = WorkbookFormulas.Apply(this, ReferenceRewriter.ForRename(sheet.Name, name));
            sheet.Entry.Name = name;
            return outcome;
        }

        internal WorkbookFormulas.Outcome DeleteSheet(ExcelSheet sheet)
        {
            var position = SheetEntries().IndexOf(sheet.Entry);
            var outcome = WorkbookFormulas.Apply(this, ReferenceRewriter.ForDeletedSheet(sheet.Name));
            var workbook = WorkbookPart.Workbook;
            var names = workbook.DefinedNames;
            if (names != null)
            {
                foreach (var definedName in names.Elements<DefinedName>().ToList())
                {
                    if (definedName.LocalSheetId?.Value is not uint local)
                        continue;

                    if (local == position)
                        definedName.Remove();
                    else if (local > position)
                        definedName.LocalSheetId = local - 1;
                }

                if (!names.HasChildren)
                    names.Remove();
            }

            foreach (var view in workbook.BookViews?.Elements<WorkbookView>() ?? Enumerable.Empty<WorkbookView>())
            {
                if (view.ActiveTab?.Value is uint active && active > position)
                    view.ActiveTab = active - 1;

                if (view.FirstSheet?.Value is uint first && first > position)
                    view.FirstSheet = first - 1;
            }

            sheet.Entry.Remove();
            WorkbookPart.DeletePart(sheet.Part);
            _sheets = null;
            FixActiveTab();
            return outcome;
        }

        internal void MoveSheet(ExcelSheet sheet, int position)
        {
            var entries = SheetEntries();
            var from = entries.IndexOf(sheet.Entry);
            position = Math.Clamp(position, 0, entries.Count - 1);
            if (from == position)
                return;

            var order = Enumerable.Range(0, entries.Count).ToList();
            order.RemoveAt(from);
            order.Insert(position, from);
            var moved = new uint[entries.Count];
            for (var i = 0; i < order.Count; i++)
                moved[order[i]] = (uint)i;

            var rest = entries.Where(e => e != sheet.Entry).ToList();
            sheet.Entry.Remove();
            if (position < rest.Count)
                rest[position].InsertBeforeSelf(sheet.Entry);
            else
                rest[^1].InsertAfterSelf(sheet.Entry);

            var workbook = WorkbookPart.Workbook;
            foreach (var definedName in workbook.DefinedNames?.Elements<DefinedName>() ?? Enumerable.Empty<DefinedName>())
            {
                if (definedName.LocalSheetId?.Value is uint local && local < moved.Length)
                    definedName.LocalSheetId = moved[local];
            }

            foreach (var view in workbook.BookViews?.Elements<WorkbookView>() ?? Enumerable.Empty<WorkbookView>())
            {
                if (view.ActiveTab?.Value is uint active && active < moved.Length)
                    view.ActiveTab = moved[active];

                view.FirstSheet = null;
            }

            _sheets = null;
            FixActiveTab();
        }

        internal void SetSheetHidden(ExcelSheet sheet, bool hidden)
        {
            sheet.Entry.State = hidden ? SheetStateValues.Hidden : null;
            FixActiveTab();
        }

        internal ExcelSheet CopySheet(ExcelSheet source, string name, int position)
        {
            var workbookPart = WorkbookPart;
            var entries = SheetEntries();
            var sourcePosition = entries.IndexOf(source.Entry);
            position = Math.Clamp(position, 0, entries.Count);

            source.Worksheet.Save();
            var part = workbookPart.AddNewPart<WorksheetPart>();
            using (var stream = source.Part.GetStream(FileMode.Open, FileAccess.Read))
                part.FeedData(stream);

            CopyRelationships(source.Part, part);
            CopyChildren(source.Part, part);
            RenameTables(part);
            RenewThreadedComments(part);
            RetargetCharts(part, source.Name, name);
            foreach (var view in part.Worksheet.SheetViews?.Elements<SheetView>() ?? Enumerable.Empty<SheetView>())
                view.TabSelected = null;

            var sheetId = entries.Select(s => s.SheetId?.Value ?? 0u).DefaultIfEmpty(0u).Max() + 1;
            var entry = new Sheet { Name = name, SheetId = sheetId, Id = workbookPart.GetIdOfPart(part) };
            if (position < entries.Count)
            {
                entries[position].InsertBeforeSelf(entry);
                ShiftSheetPositions(position);
                if (sourcePosition >= position)
                    sourcePosition++;
            }
            else
            {
                entries[^1].InsertAfterSelf(entry);
            }

            CopyLocalNames(source.Name, name, sourcePosition, position);
            _sheets = null;
            return Sheets.First(s => s.Part == part);
        }

        private List<Sheet> SheetEntries() =>
            WorkbookPart?.Workbook?.Sheets?.Elements<Sheet>().ToList() ?? new List<Sheet>();

        private static bool IsHiddenEntry(Sheet entry) => entry.State != null && entry.State.InnerText != "visible";

        private void FixActiveTab()
        {
            var entries = SheetEntries();
            if (entries.Count == 0)
                return;

            var view = WorkbookPart.Workbook.BookViews?.Elements<WorkbookView>().FirstOrDefault();
            var active = Math.Clamp((int)(view?.ActiveTab?.Value ?? 0), 0, entries.Count - 1);
            if (IsHiddenEntry(entries[active]))
            {
                var visible = Enumerable.Range(0, entries.Count).Where(i => !IsHiddenEntry(entries[i])).ToList();
                if (visible.Count > 0)
                    active = visible.OrderBy(i => Math.Abs(i - active)).ThenBy(i => i).First();
            }

            if (view != null)
            {
                view.ActiveTab = active == 0 ? null : (uint)active;
                if (view.FirstSheet?.Value is uint first && first >= entries.Count)
                    view.FirstSheet = null;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var id = entries[i].Id?.Value;
                if (id == null || !WorkbookPart.TryGetPartById(id, out var part) || part is not WorksheetPart worksheetPart)
                    continue;

                foreach (var sheetView in worksheetPart.Worksheet?.SheetViews?.Elements<SheetView>() ?? Enumerable.Empty<SheetView>())
                    sheetView.TabSelected = i == active ? true : null;
            }
        }

        private void CopyLocalNames(string sourceName, string name, int sourcePosition, int position)
        {
            var names = WorkbookPart.Workbook.DefinedNames;
            if (names == null)
                return;

            var rewriter = ReferenceRewriter.ForRename(sourceName, name);
            foreach (var definedName in names.Elements<DefinedName>().Where(d => d.LocalSheetId?.Value == (uint)sourcePosition).ToList())
            {
                var copy = (DefinedName)definedName.CloneNode(true);
                copy.LocalSheetId = (uint)position;
                copy.Text = Rewrite(copy.Text, rewriter);
                definedName.InsertAfterSelf(copy);
            }
        }

        private static void RetargetCharts(WorksheetPart part, string sourceName, string name)
        {
            var rewriter = ReferenceRewriter.ForRename(sourceName, name);
            foreach (var chart in part.DrawingsPart?.ChartParts ?? Enumerable.Empty<ChartPart>())
            {
                foreach (var formula in chart.ChartSpace?.Descendants<DocumentFormat.OpenXml.Drawing.Charts.Formula>() ?? Enumerable.Empty<DocumentFormat.OpenXml.Drawing.Charts.Formula>())
                    formula.Text = Rewrite(formula.Text, rewriter);
            }
        }

        private static string Rewrite(string text, ReferenceRewriter rewriter)
        {
            try
            {
                return FormulaText.Replace(text, r =>
                {
                    var dirty = false;
                    return rewriter.Rewrite(r, null, ref dirty);
                });
            }
            catch (FormulaException)
            {
                return text;
            }
        }

        private static void CopyRelationships(OpenXmlPart from, OpenXmlPart to)
        {
            foreach (var relationship in from.HyperlinkRelationships)
                to.AddHyperlinkRelationship(relationship.Uri, relationship.IsExternal, relationship.Id);

            foreach (var relationship in from.ExternalRelationships)
                to.AddExternalRelationship(relationship.RelationshipType, relationship.Uri, relationship.Id);
        }

        private static void CopyChildren(OpenXmlPart from, OpenXmlPart to)
        {
            foreach (var pair in from.Parts.ToList())
            {
                var child = pair.OpenXmlPart;
                if (child is ImagePart || child is SpreadsheetPrinterSettingsPart)
                {
                    to.AddPart(child, pair.RelationshipId);
                    continue;
                }

                OpenXmlPart clone;
                try
                {
                    clone = (OpenXmlPart)s_addNewPart.MakeGenericMethod(child.GetType()).Invoke(to, new object[] { child.ContentType, pair.RelationshipId });
                }
                catch (Exception e) when (e is TargetInvocationException or ArgumentException or InvalidOperationException)
                {
                    to.AddPart(child, pair.RelationshipId);
                    continue;
                }

                if (child.RootElement is OpenXmlPartRootElement root)
                    root.Save();

                using (var stream = child.GetStream(FileMode.Open, FileAccess.Read))
                    clone.FeedData(stream);

                CopyRelationships(child, clone);
                CopyChildren(child, clone);
            }
        }

        private void RenameTables(WorksheetPart part)
        {
            var tables = part.TableDefinitionParts.Select(p => p.Table).Where(t => t != null).ToList();
            if (tables.Count == 0)
                return;

            var existing = WorkbookPart.WorksheetParts.Where(p => p != part).SelectMany(p => p.TableDefinitionParts).Select(p => p.Table).Where(t => t != null).ToList();
            var nextId = existing.Select(t => t.Id?.Value ?? 0u).DefaultIfEmpty(0u).Max();
            var taken = new HashSet<string>(existing.SelectMany(t => new[] { t.Name?.Value, t.DisplayName?.Value }).Where(n => n != null), StringComparer.OrdinalIgnoreCase);
            foreach (var definedName in WorkbookPart.Workbook.DefinedNames?.Elements<DefinedName>() ?? Enumerable.Empty<DefinedName>())
            {
                if (definedName.Name?.Value != null)
                    taken.Add(definedName.Name.Value);
            }

            foreach (var table in tables)
            {
                table.Id = ++nextId;
                var stem = table.DisplayName?.Value ?? table.Name?.Value ?? "Table";
                var number = 2;
                var candidate = stem + "_" + number;
                while (taken.Contains(candidate))
                    candidate = stem + "_" + ++number;

                taken.Add(candidate);
                table.Name = candidate;
                table.DisplayName = candidate;
            }
        }

        private static void RenewThreadedComments(WorksheetPart part)
        {
            var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var threadedPart in part.WorksheetThreadedCommentsParts)
            {
                foreach (var comment in threadedPart.ThreadedComments?.Elements<Threaded.ThreadedComment>() ?? Enumerable.Empty<Threaded.ThreadedComment>())
                {
                    if (comment.Id?.Value is string id && !ids.ContainsKey(id))
                        ids[id] = "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}";
                }
            }

            if (ids.Count == 0)
                return;

            foreach (var threadedPart in part.WorksheetThreadedCommentsParts)
            {
                foreach (var comment in threadedPart.ThreadedComments?.Elements<Threaded.ThreadedComment>() ?? Enumerable.Empty<Threaded.ThreadedComment>())
                {
                    if (comment.Id?.Value is string id && ids.TryGetValue(id, out var renewed))
                        comment.Id = renewed;

                    if (comment.ParentId?.Value is string parent && ids.TryGetValue(parent, out var renewedParent))
                        comment.ParentId = renewedParent;
                }
            }

            foreach (var author in part.WorksheetCommentsPart?.Comments?.Authors?.Elements<Author>() ?? Enumerable.Empty<Author>())
            {
                var text = author.Text ?? string.Empty;
                if (text.StartsWith("tc=", StringComparison.Ordinal) && ids.TryGetValue(text.Substring(3), out var renewed))
                    author.Text = "tc=" + renewed;
            }
        }
    }
}
