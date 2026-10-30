using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DocuHelperEditor.Services;

namespace DocuHelperEditor.WinUI
{
    public class DpapiSecretStore : ISecretStore
    {
        private static readonly string s_folder = AppPaths.DataFolder;
        private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("DocuHelper.Secret.v1");

        public void Save(string name, string secret)
        {
            Directory.CreateDirectory(s_folder);
            var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret ?? string.Empty), s_entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(PathFor(name), data);
        }

        public string Load(string name)
        {
            var path = PathFor(name);
            if (!File.Exists(path))
                return null;

            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), s_entropy, DataProtectionScope.CurrentUser));
            }
            catch (CryptographicException e)
            {
                AppLog.Write("Could not read the saved key", e);
                return null;
            }
        }

        public void Delete(string name)
        {
            var path = PathFor(name);
            if (File.Exists(path))
                File.Delete(path);
        }

        private static string PathFor(string name) => Path.Combine(s_folder, name + ".dat");
    }
}
