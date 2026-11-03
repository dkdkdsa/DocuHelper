namespace DocuHelper.Dash
{
    public sealed class DashTemplate
    {
        public string Id { get; init; }

        public string Title { get; init; }

        public string Description { get; init; }

        public string Json { get; init; }

        public DashDocument Create() => DashFile.Read(System.Text.Encoding.UTF8.GetBytes(Json));
    }
}
