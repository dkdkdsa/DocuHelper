using System;

namespace DocuHelperEditor.State
{
    public class RecentWorkspace
    {
        public string Path { get; set; }

        public string Name { get; set; }

        public DateTime LastOpened { get; set; }
    }
}
