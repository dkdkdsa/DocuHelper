using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocuHelper.Docus;
using DocuHelper.Importers.Hwp;
using DocuHelper.Tools.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Hwp
{
    internal static class HwpFormFields
    {
        internal const string Prefix = "f:";
        internal const string DefaultName = "누름틀";

        internal static List<SdtRun> All(WordDocu docu) =>
            docu.Document.MainDocumentPart.Document.Body.Descendants<SdtRun>().ToList();

        internal static string IdOf(WordDocu docu, SdtRun field) =>
            Prefix + (All(docu).FindIndex(f => ReferenceEquals(f, field)) + 1).ToString(CultureInfo.InvariantCulture);

        internal static SdtRun Find(WordDocu docu, string value)
        {
            var key = (value ?? string.Empty).Trim();
            var all = All(docu);
            if (key.StartsWith(Prefix, StringComparison.Ordinal) && int.TryParse(key.Substring(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                if (number >= 1 && number <= all.Count)
                    return all[number - 1];

                throw new ToolInputException($"‘{key}’ 누름틀은 없어요. 누름틀은 {all.Count}개예요.");
            }

            var named = all.Where(f => NameOf(f) == key).ToList();
            if (named.Count == 1)
                return named[0];

            if (named.Count > 1)
                throw new ToolInputException($"‘{key}’ 이름의 누름틀이 {named.Count}개예요. 'f:번호'로 골라 주세요.");

            throw new ToolInputException($"‘{key}’ 누름틀을 찾지 못했어요. action을 list로 해서 목록을 확인해 주세요.");
        }

        internal static string NameOf(SdtRun field) => field.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value ?? string.Empty;

        internal static string HintOf(SdtRun field) => field.SdtProperties?.GetFirstChild<SdtAlias>()?.Val?.Value ?? string.Empty;

        internal static bool IsEmpty(SdtRun field) => field.SdtProperties?.GetFirstChild<ShowingPlaceholder>() != null || string.IsNullOrEmpty(field.SdtContentRun?.InnerText);

        internal static string ValueOf(SdtRun field) => IsEmpty(field) ? string.Empty : field.SdtContentRun.InnerText;

        internal static SdtRun Create(string name, string hint, string text)
        {
            var properties = new SdtProperties();
            if (!string.IsNullOrEmpty(hint))
                properties.AppendChild(new SdtAlias { Val = hint });

            properties.AppendChild(new Tag { Val = string.IsNullOrEmpty(name) ? DefaultName : name });
            var content = new SdtContentRun();
            if (string.IsNullOrEmpty(text))
                properties.AppendChild(new ShowingPlaceholder());
            else
                content.AppendChild(TextRun(text, null));

            return new SdtRun(properties, content);
        }

        internal static void SetName(SdtRun field, string name) => Set<Tag>(field, tag => tag.Val = name, () => new Tag { Val = name });

        internal static void SetHint(SdtRun field, string hint) => Set<SdtAlias>(field, alias => alias.Val = hint, () => new SdtAlias { Val = hint });

        internal static void Fill(SdtRun field, string text)
        {
            var content = field.SdtContentRun ?? field.AppendChild(new SdtContentRun());
            var format = content.Descendants<RunProperties>().FirstOrDefault();
            content.RemoveAllChildren();
            var properties = field.SdtProperties ?? field.PrependChild(new SdtProperties());
            var placeholder = properties.GetFirstChild<ShowingPlaceholder>();
            if (string.IsNullOrEmpty(text))
            {
                if (placeholder == null)
                    properties.AppendChild(new ShowingPlaceholder());

                return;
            }

            placeholder?.Remove();
            content.AppendChild(TextRun(text, format));
        }

        internal static void Unwrap(SdtRun field)
        {
            var children = IsEmpty(field) ? new List<OpenXmlElement>() : field.SdtContentRun.ChildElements.ToList();
            foreach (var child in children)
            {
                child.Remove();
                field.InsertBeforeSelf(child);
            }

            field.Remove();
        }

        internal static Run TextRun(string text, RunProperties format)
        {
            var run = new Run();
            if (format != null)
                run.AppendChild((RunProperties)format.CloneNode(true));

            run.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return run;
        }

        private static void Set<T>(SdtRun field, Action<T> update, Func<T> create) where T : OpenXmlLeafElement
        {
            var properties = field.SdtProperties ?? field.PrependChild(new SdtProperties());
            var existing = properties.GetFirstChild<T>();
            if (existing != null)
                update(existing);
            else
                properties.AppendChild(create());
        }
    }
}
