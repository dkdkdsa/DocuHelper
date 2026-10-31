namespace DocuHelperEditor.Services
{
    public sealed record ClipboardContent
    {
        public string Text { get; init; }

        public string Html { get; init; }

        public byte[] Image { get; init; }

        public bool HasImage => Image != null && Image.Length > 0;

        public bool IsEmpty => string.IsNullOrEmpty(Text) && string.IsNullOrEmpty(Html) && !HasImage;

        public ClipboardKinds Kinds
        {
            get
            {
                var kinds = ClipboardKinds.None;
                if (!string.IsNullOrEmpty(Text))
                    kinds |= ClipboardKinds.Text;

                if (!string.IsNullOrEmpty(Html))
                    kinds |= ClipboardKinds.Html;

                if (HasImage)
                    kinds |= ClipboardKinds.Image;

                return kinds;
            }
        }
    }
}
