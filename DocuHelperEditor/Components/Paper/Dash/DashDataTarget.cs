using System.Linq;
using DocuHelper.Dash;
using DocuHelper.Dash.Logic;

namespace DocuHelperEditor.Components.Paper.Dash
{
    public sealed record DashDataTarget(DashDataKind Kind, string Name, StepsTable Table, DashSource Source, DashValue Value, DashParam Param)
    {
        public int Last => Table?.Steps?.Count ?? 0;

        public static DashDataTarget Find(DashDocument document, string name)
        {
            if (document == null || string.IsNullOrWhiteSpace(name))
                return null;

            var source = document.FindSource(name);
            if (source != null)
                return new DashDataTarget(DashDataKind.Source, name, null, source, null, null);

            var value = document.FindValue(name);
            if (value is StepsTable table)
                return new DashDataTarget(DashDataKind.Table, name, table, null, null, null);

            if (value != null)
                return new DashDataTarget(DashDataKind.Value, name, null, null, value, null);

            var param = document.FindParam(name);
            return param == null ? null : new DashDataTarget(DashDataKind.Param, name, null, null, null, param);
        }

        public static DashDataTarget First(DashDocument document)
        {
            if (document == null)
                return null;

            var name = document.Values.OfType<StepsTable>().Select(table => table.Name).FirstOrDefault(Named)
                ?? document.Sources.Where(source => source != null).Select(source => source.Name).FirstOrDefault(Named)
                ?? document.Values.Where(value => value != null).Select(value => value.Name).FirstOrDefault(Named)
                ?? document.Params.Where(param => param != null).Select(param => param.Name).FirstOrDefault(Named);
            return Find(document, name);
        }

        private static bool Named(string name) => !string.IsNullOrWhiteSpace(name);
    }
}
