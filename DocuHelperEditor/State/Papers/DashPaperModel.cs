using System;
using System.Collections.Generic;
using DocuHelper.Dash;
using DocuHelper.Dash.Logic;
using DocuHelper.Dash.Runtime;

namespace DocuHelperEditor.State.Papers
{
    public class DashPaperModel : PaperModel
    {
        private readonly Func<DashState, DashRuntime> _create;

        public DashPaperModel(DashDocument document, Func<DashState, DashRuntime> create, DashState previous)
        {
            Document = document;
            _create = create;
            Runtime = create(previous);
            Render();
        }

        public DashDocument Document { get; }

        public DashRuntime Runtime { get; private set; }

        public ElementResult Root { get; private set; }

        public int Version { get; private set; }

        public IReadOnlyList<string> Problems => Runtime?.Problems ?? Array.Empty<string>();

        public bool IsPreviewing => Runtime != null && !Runtime.State.IsInitial(Document);

        public double PageWidth => Document?.Root?.Width > 0 ? Document.Root.Width : 1123;

        public double PageHeight => Document?.Root?.Height > 0 ? Document.Root.Height : 794;

        public double WidthCm => Document?.Root?.WidthCm ?? 29.71;

        public double HeightCm => Document?.Root?.HeightCm ?? 21.01;

        public double AspectRatio => PageWidth / PageHeight;

        public override bool IsBlank => Document?.Root == null || Document.Root.Children.Count == 0;

        public void Render()
        {
            Root = Runtime.Render();
            Version++;
        }

        public void SetParam(string name, object value)
        {
            Runtime.Set(name, value);
            Render();
        }

        public void RunAction(DashAction action, IReadOnlyDictionary<string, object> item)
        {
            Runtime.Apply(action, item);
            Render();
        }

        public void ResetParams()
        {
            Runtime.Reset();
            Render();
        }

        public void Reload()
        {
            Runtime = _create(Runtime?.State);
            Render();
        }
    }
}
