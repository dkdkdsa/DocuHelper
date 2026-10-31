namespace DocuHelperEditor.State.Papers
{
    public class SheetName
    {
        public string Name { get; set; }

        public string RefersTo { get; set; }

        public string Scope { get; set; }

        public string Comment { get; set; }

        public bool Hidden { get; set; }

        public bool IsWorkbookScope => string.IsNullOrEmpty(Scope);
    }
}
