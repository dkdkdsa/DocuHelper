using System.Collections.Generic;

namespace DocuHelperEditor.State.Ribbon
{
    public static class NamedDialogs
    {
        public static IEnumerable<KeyValuePair<string, DialogSpec>> All
        {
            get
            {
                foreach (var pair in WordNamedDialogs.All)
                    yield return pair;

                foreach (var pair in HwpNamedDialogs.All)
                    yield return pair;
            }
        }

        public static bool TryGet(string commandId, string dialogName, out DialogSpec dialog)
        {
            dialog = null;
            if (commandId == null || dialogName == null)
                return false;

            var key = DialogFields.Key(commandId, dialogName);
            return WordNamedDialogs.All.TryGetValue(key, out dialog) || HwpNamedDialogs.All.TryGetValue(key, out dialog);
        }
    }
}
