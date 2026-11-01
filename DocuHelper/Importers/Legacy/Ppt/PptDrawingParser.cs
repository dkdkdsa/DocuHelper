using System.Collections.Generic;

namespace DocuHelper.Importers.Legacy.Ppt
{
    internal sealed class PptDrawingParser
    {
        private const int MaxDepth = 12;
        private const int MaxShapes = 20000;

        private readonly PptSlideContext _context;
        private int _count;

        public PptDrawingParser(PptSlideContext context)
        {
            _context = context;
        }

        public List<PptShape> Shapes { get; } = new();

        public string Background { get; private set; }

        public void Parse(PptRecord drawing)
        {
            var dg = drawing?.Find(PptTypes.DgContainer);
            if (dg == null)
                return;

            foreach (var child in dg.Children)
            {
                if (child.Type == PptTypes.SpgrContainer)
                {
                    var root = ParseGroup(child, 0);
                    if (root != null)
                        Shapes.AddRange(root.Children);
                }
                else if (child.Type == PptTypes.SpContainer)
                {
                    var shape = ParseShape(child);
                    if (shape != null && shape.IsBackground)
                        Background = BackgroundColor(shape.Properties);
                }
            }
        }

        private PptShape ParseGroup(PptRecord container, int depth)
        {
            var group = new PptShape { Kind = PptShapeKind.Group };
            var children = container.Children;
            var first = 0;
            if (children.Count > 0 && children[0].Type == PptTypes.SpContainer)
            {
                var head = ParseShape(children[0]);
                if (head != null)
                {
                    group.Flags = head.Flags;
                    group.Anchor = head.Anchor;
                    group.Bounds = head.Bounds;
                    group.Properties = head.Properties;
                }

                first = 1;
            }

            for (var i = first; i < children.Count && _count < MaxShapes; i++)
            {
                PptShape shape = null;
                if (children[i].Type == PptTypes.SpContainer)
                    shape = ParseShape(children[i]);
                else if (children[i].Type == PptTypes.SpgrContainer && depth < MaxDepth)
                    shape = ParseGroup(children[i], depth + 1);

                if (shape == null || shape.IsDeleted || shape.IsBackground)
                    continue;

                group.Children.Add(shape);
            }

            return group.Children.Count > 0 || depth == 0 ? group : null;
        }

        private PptShape ParseShape(PptRecord container)
        {
            _count++;
            var shape = new PptShape();
            shape.Properties.Parent = _context.Defaults;
            PptRecord textbox = null;
            foreach (var child in container.Children)
            {
                switch (child.Type)
                {
                    case PptTypes.Fspgr:
                        shape.Bounds = new PptRect(child.Int32(0), child.Int32(4), child.Int32(8), child.Int32(12));
                        break;
                    case PptTypes.Fsp:
                        shape.Type = child.Instance;
                        shape.Flags = child.UInt32(4);
                        break;
                    case PptTypes.Fopt:
                    case PptTypes.Fopt2:
                    case PptTypes.Fopt3:
                        shape.Properties.Read(child);
                        break;
                    case PptTypes.ChildAnchor:
                        if (child.Length >= 16)
                            shape.Anchor = new PptRect(child.Int32(0), child.Int32(4), child.Int32(8), child.Int32(12));

                        break;
                    case PptTypes.ClientAnchor:
                        shape.Anchor = ClientAnchor(child) ?? shape.Anchor;
                        break;
                    case PptTypes.ClientData:
                        foreach (var data in child.Children)
                        {
                            if (data.Type == PptTypes.OePlaceholderAtom)
                                shape.PlaceholderId = data.Byte(4);
                        }

                        break;
                    case PptTypes.ClientTextbox:
                        textbox = child;
                        break;
                }
            }

            if (shape.IsDeleted)
                return shape;

            if (shape.Bounds != null && (shape.Flags & 0x1) != 0)
                shape.Kind = PptShapeKind.Group;

            Classify(shape);
            if (shape.Kind == PptShapeKind.Picture && shape.Image == null)
                return null;

            if (textbox != null && shape.Kind == PptShapeKind.Shape)
                shape.Text = ReadText(textbox);

            if (shape.Anchor == null && shape.PlaceholderId != 0 && shape.Kind != PptShapeKind.Group)
                shape.Anchor = MasterAnchor(shape.PlaceholderId);

            return shape;
        }

        private void Classify(PptShape shape)
        {
            if (shape.Kind == PptShapeKind.Group)
                return;

            var picture = shape.Type == 75 || (shape.Flags & 0x10) != 0;
            if (picture && shape.Properties.Has(PptProperties.Pib))
            {
                shape.Image = _context.Pictures?.Get(shape.Properties.GetInt(PptProperties.Pib, 0));
                shape.Kind = PptShapeKind.Picture;
                return;
            }

            if (shape.Type == 20 || shape.IsConnector || (shape.Type >= 32 && shape.Type <= 40))
                shape.Kind = PptShapeKind.Line;
        }

        private PptRect? MasterAnchor(int placeholderId)
        {
            var master = placeholderId switch
            {
                0x0D => 1,
                0x10 => 3,
                0x11 => 4,
                0x0E or 0x0F or 0x14 => 2,
                _ => 0,
            };

            if (master != 0 && _context.MasterAnchors.TryGetValue(master, out var rect))
                return rect;

            if (master == 3 && _context.MasterAnchors.TryGetValue(1, out rect))
                return rect;

            if (master == 4 && _context.MasterAnchors.TryGetValue(2, out rect))
                return rect;

            return null;
        }

        private static PptRect? ClientAnchor(PptRecord anchor)
        {
            if (anchor.Length >= 16)
                return new PptRect(anchor.Int32(4), anchor.Int32(0), anchor.Int32(8), anchor.Int32(12));

            if (anchor.Length >= 8)
                return new PptRect(anchor.Int16(2), anchor.Int16(0), anchor.Int16(4), anchor.Int16(6));

            return null;
        }

        private PptTextBody ReadText(PptRecord textbox)
        {
            var textType = PptTextStyles.OtherType;
            string text = null;
            PptRecord style = null;
            PptOutlineText outline = null;
            var fields = new Dictionary<int, string>();
            foreach (var child in textbox.Children)
            {
                switch (child.Type)
                {
                    case 0x0FD8:
                        fields[(int)child.UInt32(0)] = "slidenum";
                        break;
                    case 0x0FF7:
                    case 0x0FF8:
                        fields[(int)child.UInt32(0)] = "datetime1";
                        break;
                    case 0x0FF9:
                    case 0x0FFA:
                        fields[(int)child.UInt32(0)] = string.Empty;
                        break;
                    case PptTypes.TextHeaderAtom:
                        textType = (int)child.UInt32(0);
                        break;
                    case PptTypes.TextCharsAtom:
                    case PptTypes.TextBytesAtom:
                        text = PptTextBuilder.ReadText(child);
                        break;
                    case PptTypes.StyleTextPropAtom:
                        style = child;
                        break;
                    case PptTypes.OutlineTextRefAtom:
                        var index = (int)child.UInt32(0);
                        if (index >= 0 && index < _context.Outline.Count)
                            outline = _context.Outline[index];

                        break;
                }
            }

            if (outline != null && text == null)
                return PptTextBuilder.Build(outline.TextType, outline.Text, outline.Style, _context.Styles, fields);

            return PptTextBuilder.Build(textType, text ?? string.Empty, style, _context.Styles, fields);
        }

        private string BackgroundColor(PptProperties properties)
        {
            if (properties.Flag(PptProperties.FillBooleans, 0x10) == false)
                return null;

            if (!properties.HasOwn(PptProperties.FillColor))
                return _context.Scheme != null ? PptColors.FromOfficeArt(0x08000000, _context.Scheme) : null;

            return PptColors.FromOfficeArt(properties.Get(PptProperties.FillColor, 0x00FFFFFF), _context.Scheme);
        }
    }
}
