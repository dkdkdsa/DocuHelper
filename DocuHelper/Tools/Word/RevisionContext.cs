using System;
using System.Globalization;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocuHelper.Tools.Word
{
    public sealed class RevisionContext
    {
        private int _next;

        internal RevisionContext(string author, DateTime date, int first, bool assistant)
        {
            Author = author;
            Date = date;
            _next = first;
            IsAssistant = assistant;
        }

        public string Author { get; }

        public DateTime Date { get; }

        public bool IsAssistant { get; }

        public int NextId() => _next++;

        public T Stamp<T>(T change) where T : TrackChangeType
        {
            change.Id = NextId().ToString(CultureInfo.InvariantCulture);
            change.Author = Author;
            change.Date = Date;
            return change;
        }

        public T StampRun<T>(T change) where T : RunTrackChangeType
        {
            change.Id = NextId().ToString(CultureInfo.InvariantCulture);
            change.Author = Author;
            change.Date = Date;
            return change;
        }

        public RunPropertiesChange Stamp(RunPropertiesChange change)
        {
            change.Id = NextId().ToString(CultureInfo.InvariantCulture);
            change.Author = Author;
            change.Date = Date;
            return change;
        }
    }
}
