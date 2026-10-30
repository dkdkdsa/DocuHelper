using System.Text.Json;

namespace DocuHelper.Editors
{
    public record EditCommand(string Name, JsonElement Arguments)
    {
        public static EditCommand Create(string name, object arguments) =>
            new(name, JsonSerializer.SerializeToElement(arguments));
    }
}
