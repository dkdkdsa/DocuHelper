using System;
using System.Collections.Generic;

namespace DocuHelper.Docus
{
    public class DocuChange
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Label { get; set; }

        public string Location { get; set; }

        public string Icon { get; set; }

        public List<string> Anchors { get; set; } = new();

        public DocuChange Clone() => new()
        {
            Id = Id,
            Label = Label,
            Location = Location,
            Icon = Icon,
            Anchors = new List<string>(Anchors),
        };
    }
}
