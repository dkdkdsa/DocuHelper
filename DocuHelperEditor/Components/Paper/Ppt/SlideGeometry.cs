using System;
using System.Collections.Generic;

namespace DocuHelperEditor.Components.Paper.Ppt
{
    public static class SlideGeometry
    {
        private const double Flat = 0.0001;

        private const string Rect ="<rect x=\"0\" y=\"0\" width=\"100\" height=\"100\" />";

        private static readonly Dictionary<string, string> s_markup = new(StringComparer.Ordinal)
        {
            ["rect"] = Rect,
            ["roundRect"] = "<rect x=\"0\" y=\"0\" width=\"100\" height=\"100\" rx=\"8\" ry=\"8\" />",
            ["ellipse"] = "<ellipse cx=\"50\" cy=\"50\" rx=\"50\" ry=\"50\" />",
            ["triangle"] = Polygon("50,0 100,100 0,100"),
            ["rtTriangle"] = Polygon("0,0 0,100 100,100"),
            ["diamond"] = Polygon("50,0 100,50 50,100 0,50"),
            ["pentagon"] = Polygon("50,0 100,38 82,100 18,100 0,38"),
            ["hexagon"] = Polygon("25,0 75,0 100,50 75,100 25,100 0,50"),
            ["parallelogram"] = Polygon("25,0 100,0 75,100 0,100"),
            ["trapezoid"] = Polygon("20,0 80,0 100,100 0,100"),
            ["plus"] = Polygon("35,0 65,0 65,35 100,35 100,65 65,65 65,100 35,100 35,65 0,65 0,35 35,35"),
            ["rightArrow"] = Polygon("0,25 60,25 60,0 100,50 60,100 60,75 0,75"),
            ["leftArrow"] = Polygon("100,25 40,25 40,0 0,50 40,100 40,75 100,75"),
            ["upArrow"] = Polygon("25,100 25,40 0,40 50,0 100,40 75,40 75,100"),
            ["downArrow"] = Polygon("25,0 25,60 0,60 50,100 100,60 75,60 75,0"),
            ["leftRightArrow"] = Polygon("0,50 25,15 25,35 75,35 75,15 100,50 75,85 75,65 25,65 25,85"),
            ["chevron"] = Polygon("0,0 70,0 100,50 70,100 0,100 30,50"),
            ["homePlate"] = Polygon("0,0 70,0 100,50 70,100 0,100"),
            ["star5"] = Polygon("50,0 61,35 98,35 68,57 79,91 50,70 21,91 32,57 2,35 39,35"),
            ["flowChartDecision"] = Polygon("50,0 100,50 50,100 0,50"),
            ["flowChartTerminator"] = "<rect x=\"0\" y=\"0\" width=\"100\" height=\"100\" rx=\"50\" ry=\"50\" />",
            ["heart"] = "<path d=\"M50,25 C50,10 35,0 25,0 C10,0 0,12 0,30 C0,60 30,80 50,100 C70,80 100,60 100,30 C100,12 90,0 75,0 C65,0 50,10 50,25 Z\" />",
            ["line"] = "<line x1=\"0\" y1=\"0\" x2=\"100\" y2=\"100\" />",
        };

        public static string Markup(string preset) => preset != null && s_markup.TryGetValue(preset, out var markup) ? markup : Rect;

        public static string LineMarkup(double width, double height)
        {
            if (height <= Flat)
                return "<line x1=\"0\" y1=\"50\" x2=\"100\" y2=\"50\" />";

            if (width <= Flat)
                return "<line x1=\"50\" y1=\"0\" x2=\"50\" y2=\"100\" />";

            return Markup("line");
        }

        public static bool IsLine(string preset) => preset == "line";

        private static string Polygon(string points) => "<polygon points=\"" + points + "\" />";
    }
}
