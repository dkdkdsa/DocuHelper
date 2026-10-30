using System;
using System.Collections.Generic;
using System.Globalization;

namespace DocuHelper.Tools.Ppt
{
    public class PptDeckInfo
    {
        public long SlideWidth { get; set; } = 12192000;

        public long SlideHeight { get; set; } = 6858000;

        public List<PptSlideInfo> Slides { get; set; } = new();

        public string AspectText
        {
            get
            {
                if (SlideWidth <= 0 || SlideHeight <= 0)
                    return "unknown size";

                var ratio = (double)SlideWidth / SlideHeight;
                if (Math.Abs(ratio - 16.0 / 9) < 0.02)
                    return "16:9";

                if (Math.Abs(ratio - 4.0 / 3) < 0.02)
                    return "4:3";

                if (Math.Abs(ratio - 16.0 / 10) < 0.02)
                    return "16:10";

                return string.Format(CultureInfo.InvariantCulture, "{0:0.#} x {1:0.#} cm", SlideWidth / 360000.0, SlideHeight / 360000.0);
            }
        }
    }
}
