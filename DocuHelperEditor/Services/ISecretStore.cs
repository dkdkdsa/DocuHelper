namespace DocuHelperEditor.Services
{
    public interface ISecretStore
    {
        void Save(string name, string secret);

        string Load(string name);

        void Delete(string name);
    }
}
