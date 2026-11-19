using System.IO;
using Sector.Ops;
using UnityEngine;

namespace Sector.UI
{
    // One save slot for the operations game, under persistentDataPath.
    public static class OpsSaveStore
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "sector_ops_save.txt");

        public static bool Exists => File.Exists(FilePath);

        public static OpsSession Load()
        {
            try
            {
                return Exists ? OpsSession.Load(File.ReadAllText(FilePath)) : null;
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"Save could not be read: {exception.Message}");
                return null;
            }
        }

        public static bool Save(OpsSession session)
        {
            try
            {
                File.WriteAllText(FilePath, session.Save());
                return true;
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"Save could not be written: {exception.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            try
            {
                if (Exists)
                    File.Delete(FilePath);
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"Save could not be deleted: {exception.Message}");
            }
        }
    }
}
