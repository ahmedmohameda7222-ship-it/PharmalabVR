using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace PharmaLabVR.Core
{
    public static class ScientificPackage
    {
        public const string DatabaseSha256 = "AB0A8F7C7375E1BD997990F4BC3A9AF497516F10E57F4AAA3CEFB55DCCAC5AE7";
        private const int MaxPackageBytes = 32 * 1024 * 1024;
        private const string RelativeDatabasePath = "science/minteq.v4.dat";

        public static string PrepareLocal(string streamingAssetsPath)
        {
            var path = Path.Combine(streamingAssetsPath, RelativeDatabasePath);
            Verify(File.ReadAllBytes(path));
            return path;
        }

        public static IEnumerator PrepareAndroid(Action<string> ready, Action<Exception> failed)
        {
            var directory = Path.Combine(Application.persistentDataPath, "science");
            var destination = Path.Combine(directory, "minteq.v4.dat");
            if (File.Exists(destination))
            {
                try { Verify(File.ReadAllBytes(destination)); ready(destination); yield break; }
                catch (Exception) { /* Recover from a previous incomplete or corrupt extraction. */ }
            }
            using var request = UnityWebRequest.Get(Application.streamingAssetsPath.TrimEnd('/') + "/" + RelativeDatabasePath);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                failed(new IOException("Scientific database asset could not be read: " + request.error));
                yield break;
            }
            var bytes = request.downloadHandler.data;
            try
            {
                Verify(bytes);
                Directory.CreateDirectory(directory);
                var temporary = destination + ".tmp";
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush(true);
                }
                if (File.Exists(destination)) File.Delete(destination);
                File.Move(temporary, destination);
                Verify(File.ReadAllBytes(destination));
                ready(destination);
            }
            catch (Exception exception) { failed(exception); }
        }

        public static void Verify(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxPackageBytes)
                throw new InvalidDataException("Scientific database package has an invalid size.");
            using var sha = SHA256.Create();
            var actual = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            if (!string.Equals(actual, DatabaseSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Scientific database package hash does not match the pinned minteq bytes.");
        }
    }
}
