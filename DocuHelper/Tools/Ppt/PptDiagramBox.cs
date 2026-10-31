namespace DocuHelper.Tools.Ppt
{
    public sealed record PptDiagramBox(int Index, string Preset, PptBox Box, int? Adjust, PptBox TextArea);
}
