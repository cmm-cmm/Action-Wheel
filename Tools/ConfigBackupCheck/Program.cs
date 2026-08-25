using System.IO.Compression;
using Action_Wheel.Core;

// A regression check for the resource-limit and entry-allowlist hardening added to
// ConfigBackup.TryImport after a project audit found it would extract an unlimited number of
// entries of unlimited size from an untrusted "backup" ZIP before validating anything - a zip
// bomb could fill the staging directory (or the disk) before TryImport ever gets to reject it.
//
// This only covers the REJECTION paths - see the scope note in ConfigBackupCheck.csproj for why
// a positive "well-formed backup restores" or "partial failure rolls back" case is not exercised
// here: both would need TryImport to reach its commit phase, which writes to the real
// %LOCALAPPDATA%\ActionWheel files with no way to redirect them in a test. Every scenario below
// is instead double-checked to prove it left those real files alone.
//
// There is deliberately no test project (see CLAUDE.md); run this by hand after touching
// ConfigBackup, with: dotnet run --project Tools\ConfigBackupCheck\ConfigBackupCheck.csproj

int failures = 0;

void Check(string label, bool passed)
{
    Console.WriteLine(passed ? $"  OK    {label}" : $"  FAIL  {label}");
    if (!passed)
        failures++;
}

static void WriteManifestEntry(ZipArchive zip)
{
    var entry = zip.CreateEntry("manifest.json");
    using var stream = entry.Open();
    using var writer = new StreamWriter(stream);
    writer.Write("""{"manifestVersion":1,"exportedAtUtc":"2026-01-01T00:00:00.0000000Z","appVersion":"2.7.0"}""");
}

string actionsPath = ActionConfig.ActiveConfigPath;
string preferencesPath = Path.Combine(AppDataPaths.DirectoryPath, "preferences.json");

byte[]? actionsBefore = File.Exists(actionsPath) ? File.ReadAllBytes(actionsPath) : null;
byte[]? preferencesBefore = File.Exists(preferencesPath) ? File.ReadAllBytes(preferencesPath) : null;

bool RealFilesUntouched()
{
    byte[]? actionsNow = File.Exists(actionsPath) ? File.ReadAllBytes(actionsPath) : null;
    byte[]? preferencesNow = File.Exists(preferencesPath) ? File.ReadAllBytes(preferencesPath) : null;
    return BytesEqual(actionsBefore, actionsNow) && BytesEqual(preferencesBefore, preferencesNow);
}

static bool BytesEqual(byte[]? a, byte[]? b)
{
    if (a == null || b == null)
        return a == null && b == null;
    return a.AsSpan().SequenceEqual(b);
}

string tempDir = Path.Combine(Path.GetTempPath(), $"ActionWheelBackupCheck-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempDir);

Console.WriteLine("ConfigBackup rejection-path check");
Console.WriteLine();

try
{
    // Scenario 1: more entries than a real backup ever has.
    {
        string zipPath = Path.Combine(tempDir, "too-many-entries.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteManifestEntry(zip);
            for (int i = 0; i < 600; i++)
                zip.CreateEntry($"Icons/extra{i}.png");
        }

        bool imported = ConfigBackup.TryImport(zipPath, out string error);
        Check("too many entries is rejected", !imported && !string.IsNullOrEmpty(error));
        Check("too many entries: real files untouched", RealFilesUntouched());
    }

    // Scenario 2: an entry larger than MaxEntryUncompressedBytes (64 MB) - a small compressed
    // size (all zero bytes compress to almost nothing) with a large declared/actual length, which
    // is exactly the zip-bomb shape this limit exists to catch.
    {
        string zipPath = Path.Combine(tempDir, "oversized-entry.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteManifestEntry(zip);
            var entry = zip.CreateEntry("Icons/huge.png", CompressionLevel.Fastest);
            using var stream = entry.Open();
            var buffer = new byte[1024 * 1024];
            for (int i = 0; i < 65; i++)
                stream.Write(buffer, 0, buffer.Length);
        }

        bool imported = ConfigBackup.TryImport(zipPath, out string error);
        Check("an oversized entry is rejected", !imported && !string.IsNullOrEmpty(error));
        Check("oversized entry: real files untouched", RealFilesUntouched());
    }

    // Scenario 3: an entry name outside the backup format - not one of the four fixed files, not
    // under Profiles/*.xml or Icons/*.png.
    {
        string zipPath = Path.Combine(tempDir, "unknown-entry.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteManifestEntry(zip);
            var entry = zip.CreateEntry("Startup/evil.dll");
            using var stream = entry.Open();
            stream.Write(new byte[] { 1, 2, 3 });
        }

        bool imported = ConfigBackup.TryImport(zipPath, out string error);
        Check("an unrecognised entry name is rejected", !imported && !string.IsNullOrEmpty(error));
        Check("unrecognised entry name: real files untouched", RealFilesUntouched());
    }

    // Scenario 4: zip-slip path traversal - pre-existing protection, re-checked here so a future
    // change to the entry loop cannot drop it unnoticed alongside the newer checks above.
    {
        string zipPath = Path.Combine(tempDir, "zip-slip.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteManifestEntry(zip);
            var entry = zip.CreateEntry("Profiles/../../../evil.xml");
            using var stream = entry.Open();
            stream.Write(new byte[] { 1, 2, 3 });
        }

        bool imported = ConfigBackup.TryImport(zipPath, out string error);
        Check("a zip-slip path is rejected", !imported && !string.IsNullOrEmpty(error));
        Check("zip-slip: real files untouched", RealFilesUntouched());
    }
}
finally
{
    try { Directory.Delete(tempDir, recursive: true); } catch { }
}

Console.WriteLine();
Console.WriteLine(failures == 0
    ? "All ConfigBackup rejection-path checks passed."
    : $"{failures} ConfigBackup check(s) failed.");

return failures == 0 ? 0 : 1;
