using System;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.WinUI
{
    public class DpapiSecretStore : ISecretStore
    {
        public void Save(string name, string secret) => throw new NotImplementedException();

        public string Load(string name) => throw new NotImplementedException();

        public void Delete(string name) => throw new NotImplementedException();
    }
}
