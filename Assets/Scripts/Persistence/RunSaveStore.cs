using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable] public sealed class RunSaveEnvelope
{
    public int format = 1;
    public string checksum;
    public string payload;
}

// Storage has no scene dependencies. Tests supply an isolated directory and explicit content catalog.
public sealed class RunSaveStore
{
    public string Path { get; }
    public string BackupPath => Path + ".bak";
    public string TemporaryPath => Path + ".tmp";
    public string DeletedPath => Path + ".deleted";
    public bool HasActiveFiles => !File.Exists(DeletedPath) && (File.Exists(Path) || File.Exists(BackupPath));
    public bool RecoveredFromBackup { get; private set; }
    const long MaximumBytes = 8 * 1024 * 1024;
    public RunSaveStore(string path) => Path = path ?? throw new ArgumentNullException(nameof(path));

    public bool TryRead(SaveContentCatalog catalog, out RunSaveData data, out string error)
    {
        data = null; RecoveredFromBackup = false;
        if (File.Exists(DeletedPath)) { error = "No active run save."; return false; }
        if (TryReadFile(Path, catalog, out data, out string primaryError)) { error = null; return true; }
        if (TryReadFile(BackupPath, catalog, out data, out string backupError))
        { RecoveredFromBackup = true; error = null; return true; }
        error = File.Exists(Path) || File.Exists(BackupPath)
            ? $"Unable to continue. {primaryError} Backup: {backupError}" : "No active run save.";
        return false;
    }

    bool TryReadFile(string path, SaveContentCatalog catalog, out RunSaveData data, out string error)
    {
        data = null;
        try
        {
            if (!File.Exists(path)) { error = "Save file missing."; return false; }
            if (new FileInfo(path).Length > MaximumBytes) { error = "Save file too large."; return false; }
            var envelope = JsonUtility.FromJson<RunSaveEnvelope>(File.ReadAllText(path, Encoding.UTF8));
            if (envelope == null || envelope.format != 1 || string.IsNullOrEmpty(envelope.payload) ||
                !string.Equals(envelope.checksum, Checksum(envelope.payload), StringComparison.Ordinal))
            { error = "Invalid save envelope/checksum."; return false; }
            var candidate = JsonUtility.FromJson<RunSaveData>(envelope.payload);
            if (!RunSaveMigrations.TryUpgrade(candidate, out error)) return false;
            if (!RunSaveValidation.Validate(candidate, catalog, out error)) return false;
            data = candidate; return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        { error = e.Message; return false; }
    }

    public bool Write(RunSaveData data, SaveContentCatalog catalog, out string error)
    {
        if (!RunSaveValidation.Validate(data, catalog, out error)) return false;
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            string payload = JsonUtility.ToJson(data);
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new RunSaveEnvelope { checksum = Checksum(payload), payload = payload }, true));
            if (bytes.LongLength > MaximumBytes) { error = "Save file too large."; return false; }
            using (var stream = new FileStream(TemporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(Path))
            {
                // Never overwrite a healthy recovery copy with a corrupted primary.
                bool validPrimary = TryReadFile(Path, catalog, out _, out _);
                File.Replace(TemporaryPath, Path, validPrimary ? BackupPath : null);
            }
            else
            {
                File.Move(TemporaryPath, Path);
                // The primary is durable before creating the first backup. If a primary
                // was externally lost, retain its existing recovery copy throughout.
                if (!File.Exists(BackupPath))
                {
                    using (var stream = new FileStream(TemporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    File.Move(TemporaryPath, BackupPath);
                }
            }
            if (File.Exists(DeletedPath)) File.Delete(DeletedPath);
            error = null; return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException || e is ArgumentException)
        { error = "Could not save run: " + e.Message; return false; }
    }

    public bool Delete(out string error)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            // A durable tombstone prevents backup resurrection if deletion is interrupted.
            using (var stream = new FileStream(DeletedPath, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.WriteByte(1); stream.Flush(true); }
            foreach (string path in new[] { Path, BackupPath, TemporaryPath }) if (File.Exists(path)) File.Delete(path);
            error = null; return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        { error = "Could not clear saved run: " + e.Message; return false; }
    }

    static string Checksum(string payload)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", string.Empty);
    }
}
