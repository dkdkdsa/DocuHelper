using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DocuHelper.Agents;
using DocuHelper.Docus;
using DocuHelper.Editors;

namespace DocuHelper
{
    public class Workspace
    {
        private readonly List<Docu> _docus = new();
        private readonly Dictionary<DocuType, DocuEditor> _editors = new();

        public IReadOnlyList<Docu> Docus => _docus;

        public IReadOnlyDictionary<DocuType, DocuEditor> Editors => _editors;

        public Agent Agent { get; set; }

        public void AddDocu(Docu docu) => _docus.Add(docu);

        public bool RemoveDocu(Docu docu) => _docus.Remove(docu);

        public void RegisterEditor(DocuEditor editor) => _editors[editor.TargetType] = editor;

        public DocuEditor GetEditor(Docu docu)
        {
            if (!_editors.TryGetValue(docu.Type, out var editor))
                throw new InvalidOperationException($"{docu.Type} 문서용 에디터가 등록되지 않았습니다.");

            return editor;
        }

        public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var agent = Agent ?? throw new InvalidOperationException("에이전트가 설정되지 않았습니다.");
            return agent.RunAsync(this, prompt, cancellationToken);
        }
    }
}
