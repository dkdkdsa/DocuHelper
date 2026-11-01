using System;
using DocuHelper.Tools.Word;

namespace DocuHelperEditor.State.Papers
{
    public sealed record WordViewSegment(string Text, WordCharLook Look, string Kind, string RevisionId, int Tone, string CommentId)
    {
        public bool IsInserted => Kind == WordRevisionKinds.Insert;

        public bool IsDeleted => Kind == WordRevisionKinds.Delete;
    }

    public static class WordRevisionKinds
    {
        public const string Insert = "ins";

        public const string Delete = "del";
    }
}
