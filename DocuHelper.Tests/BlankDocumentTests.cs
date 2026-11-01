using System;
using System.IO;
using System.Text.Json;
using DocuHelper.Docus;
using Xunit;

namespace DocuHelper.Tests
{
    public sealed class BlankDocumentTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "docuhelper-tests-" + Guid.NewGuid().ToString("N"));

        public BlankDocumentTests() => Directory.CreateDirectory(_folder);

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
            }
        }

        [Theory]
        [MemberData(nameof(Editors.Types), MemberType = typeof(Editors))]
        public void Blank_document_survives_a_save_and_load(DocuType type)
        {
            var editor = Editors.Of(type);
            using var docu = editor.CreateBlank(Path.Combine(_folder, "blank" + editor.Extension));

            Assert.Equal(type, docu.Type);
            Assert.False(string.IsNullOrWhiteSpace(editor.Describe(docu)));

            var bytes = editor.Export(docu, docu.FilePath);
            Assert.NotEmpty(bytes);

            using var reopened = editor.CreateBlank(Path.Combine(_folder, "reopened" + editor.Extension));
            reopened.Load(bytes);

            Assert.Equal(editor.Describe(docu), editor.Describe(reopened));
        }

        [Theory]
        [InlineData(DocuType.Word, "read_document", """{"what":"all"}""")]
        [InlineData(DocuType.Word, "read_paragraphs", "{}")]
        [InlineData(DocuType.Hwp, "read_document", """{"what":"all"}""")]
        [InlineData(DocuType.Hwp, "read_paragraphs", "{}")]
        [InlineData(DocuType.Ppt, "read_slides", "{}")]
        [InlineData(DocuType.Excel, "read_sheet", "{}")]
        public void Read_tool_works_on_a_blank_document(DocuType type, string tool, string arguments)
        {
            var editor = Editors.Of(type);
            using var docu = editor.CreateBlank(Path.Combine(_folder, "read" + editor.Extension));
            var before = editor.Describe(docu);

            var result = editor.Execute(docu, tool, JsonDocument.Parse(arguments).RootElement);

            Assert.False(result.IsError, result.Content);
            Assert.Equal(before, editor.Describe(docu));
        }
    }
}
