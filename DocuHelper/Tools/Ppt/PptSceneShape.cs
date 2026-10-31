using System.Collections.Generic;

namespace DocuHelper.Tools.Ppt
{
    public class PptSceneShape
    {
        public uint Id { get; set; }

        public string Anchor { get; set; }

        public string Name { get; set; }

        public PptShapeKind Kind { get; set; }

        public PptInheritedFrom Inherited { get; set; }

        public bool IsPlaceholder { get; set; }

        public string PlaceholderType { get; set; }

        public long X { get; set; }

        public long Y { get; set; }

        public long W { get; set; }

        public long H { get; set; }

        public double RotationDeg { get; set; }

        public bool FlipH { get; set; }

        public bool FlipV { get; set; }

        public PptSceneGeometry Geometry { get; set; }

        public PptSceneFill Fill { get; set; }

        public PptSceneLine Line { get; set; }

        public PptSceneShadow Shadow { get; set; }

        public PptSceneText Text { get; set; }

        public PptScenePicture Picture { get; set; }

        public PptSceneTable Table { get; set; }

        public PptSceneChart Chart { get; set; }

        public List<PptSceneShape> Children { get; set; } = new();

        public string AltText { get; set; }

        public PptSceneLink Link { get; set; }
    }
}
