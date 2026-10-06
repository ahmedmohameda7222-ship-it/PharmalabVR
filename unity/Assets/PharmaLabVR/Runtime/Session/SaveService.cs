using System;
using System.IO;
using PharmaLabVR.Core;

namespace PharmaLabVR.Session
{
    public sealed class SaveService
    {
        public string Save(CoreSession session, string directory, string name)
        {
            if (session == null || !session.IsOpen) throw new InvalidOperationException("No open native session.");
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, name + ".plv.json");
            var temporary = target + ".tmp";
            var bytes = System.Text.Encoding.UTF8.GetBytes(session.ExportSession());
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(target)) File.Replace(temporary, target, target + ".bak", true); else File.Move(temporary, target);
            return target;
        }
    }
}
