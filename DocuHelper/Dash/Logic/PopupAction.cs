using System;
using System.Collections.Generic;
using DocuHelper.Dash.Elements;

namespace DocuHelper.Dash.Logic
{
    public sealed class PopupAction : DashAction
    {
        public string Id { get; set; }

        public bool Close { get; set; }

        public override IEnumerable<string> Uses() => Array.Empty<string>();

        public override void Validate(DashCheck check, Element owner)
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                if (!Close)
                    check.Fail($"‘{DashLabels.Of(owner)}’을 누르면 열 팝업 창이 정해지지 않았어요.");

                return;
            }

            if (check.Document?.Find(Id.Trim()) is not PopupElement)
                check.Fail($"‘{DashLabels.Of(owner)}’을 누르면 열 팝업 창 ‘{Id}’이 없어요.");
        }
    }
}
