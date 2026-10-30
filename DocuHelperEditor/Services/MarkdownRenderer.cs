using System;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DocuHelperEditor.Services
{
    public static class MarkdownRenderer
    {
        private static readonly string[] s_safeSchemes = { "http", "https", "mailto" };

        private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
            .UseEmphasisExtras()
            .UsePipeTables()
            .UseGridTables()
            .UseListExtras()
            .UseTaskLists()
            .UseDefinitionLists()
            .UseAutoLinks()
            .UseSoftlineBreakAsHardlineBreak()
            .DisableHtml()
            .Build();

        public static string ToHtml(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            var document = Markdown.Parse(markdown, s_pipeline);
            foreach (var link in document.Descendants<LinkInline>().ToList())
            {
                if (!IsSafe(link.Url))
                    link.ReplaceBy(new ContainerInline());
            }
            foreach (var link in document.Descendants<AutolinkInline>().ToList())
            {
                if (!IsSafe(link.IsEmail ? "mailto:" + link.Url : link.Url))
                    link.ReplaceBy(new LiteralInline(link.Url));
            }

            using var writer = new StringWriter();
            var renderer = new HtmlRenderer(writer);
            s_pipeline.Setup(renderer);
            renderer.Render(document);
            writer.Flush();
            return writer.ToString();
        }

        private static bool IsSafe(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                && s_safeSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
        }
    }
}
