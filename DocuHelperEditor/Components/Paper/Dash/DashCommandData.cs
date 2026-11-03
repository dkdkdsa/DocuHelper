using System.Collections.Generic;
using System.Linq;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.Components.Paper.Dash
{
    internal static class DashCommandData
    {
        private const string Later = "아직 준비 중이에요.";

        private static readonly string[] s_later =
        {
            "dash.home.paste", "dash.home.cut", "dash.home.copy",
            "dash.home.fontName", "dash.home.fontSize", "dash.home.fontGrow", "dash.home.fontShrink",
            "dash.home.bold", "dash.home.italic", "dash.home.underline", "dash.home.fontColor",
            "dash.home.alignLeft", "dash.home.alignCenter", "dash.home.alignRight", "dash.home.mark",
            "dash.home.shapes", "dash.home.arrange",
            "dash.insert.textbox", "dash.insert.shapes", "dash.insert.chart", "dash.insert.table", "dash.insert.repeat",
            "dash.insert.control", "dash.insert.meter", "dash.insert.row", "dash.insert.column", "dash.insert.canvas",
            "dash.data.source", "dash.data.param", "dash.data.table", "dash.data.formula",
            "dash.file.pdf", "dash.file.print",
        };

        public static IReadOnlyList<CommandSpec> All { get; } = new[]
        {
            new CommandSpec { Id = DashRibbonCommands.UndoId, Class = RibbonCommandClass.File, Action = "undo" },
            new CommandSpec { Id = DashRibbonCommands.RedoId, Class = RibbonCommandClass.File, Action = "redo" },
            new CommandSpec { Id = "dash.file.save", Class = RibbonCommandClass.File, Action = "save" },
            new CommandSpec { Id = "dash.file.saveAs", Class = RibbonCommandClass.File, Action = "saveAs" },
            new CommandSpec { Id = "dash.file.info", Class = RibbonCommandClass.File, Action = "showInfo", WhileLocked = true },
            new CommandSpec { Id = "dash.view.show", Class = RibbonCommandClass.View, Action = "mode:" + DashViewState.ViewMode, Pressed = "mode:" + DashViewState.ViewMode },
            new CommandSpec { Id = "dash.view.edit", Class = RibbonCommandClass.View, Action = "mode:" + DashViewState.EditMode, Pressed = "mode:" + DashViewState.EditMode },
            new CommandSpec { Id = "dash.view.data", Class = RibbonCommandClass.View, Action = "mode:" + DashViewState.DataMode, Pressed = "mode:" + DashViewState.DataMode },
            new CommandSpec { Id = "dash.view.gridlines", Class = RibbonCommandClass.View, Action = "gridlines", Pressed = "gridlines" },
            new CommandSpec { Id = "dash.view.guides", Class = RibbonCommandClass.View, Action = "guides", Pressed = "guides" },
            new CommandSpec
            {
                Id = "dash.view.zoom",
                Class = RibbonCommandClass.View,
                Action = "zoom",
                Dialog = new DialogSpec("확대/축소", new[] { new DialogField("value", "배율", "choice", "100", new string[] { "200", "150", "100", "75", "66", "50", "fit" }, new string[] { "200", "150", "100", "75", "66", "50", "창에 맞춤" }) }),
            },
            new CommandSpec { Id = "dash.view.fit", Class = RibbonCommandClass.View, Action = "fit" },
            new CommandSpec { Id = "dash.data.refresh", Class = RibbonCommandClass.View, Action = "refresh" },
            new CommandSpec { Id = "dash.data.resetParams", Class = RibbonCommandClass.View, Action = "resetParams" },
        }.Concat(s_later.Select(id => new CommandSpec { Id = id, Class = RibbonCommandClass.Disabled, Reason = Later })).ToList();
    }
}
