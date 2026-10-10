using System.Globalization;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;
using System.Text.RegularExpressions;

namespace ZipSelect;

public record ExtractionOptions
{
    public bool SearchSubfolders { get; set; } = true;
    public bool IncludeArchiveSubfolders { get; set; } = true;
    public bool PreserveFolders { get; set; } = true;
    public bool RenameConflicts { get; set; }
    public bool FolderPerArchive { get; set; }
}

public record Update
{
    public int Archives { get; set; }
    public int Done { get; set; }
    public int Extracted { get; set; }
    public int Skipped { get; set; }
    public int Issues { get; set; }
    public int Renamed { get; set; }
    public string? Message { get; set; }
    public string? Status { get; set; }
    public bool Scanning { get; set; }
}

public static class Engine
{
    static readonly char[] Separators = [' ', ',', ';', '\t', '\r', '\n'];
    static readonly char[] Forbidden = ['\\', '/', '*', '?', ':', '"', '<', '>', '|'];
    static readonly Regex DriveLetter = new(@"^[A-Za-z]:");
    static readonly Regex NumberedDevice = new(@"^(COM|LPT)[1-9¹²³]$");
    static readonly Encoding NameEncoding = new ZipNameEncoding();

    static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static HashSet<string> ParseExtensions(string? input)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in (input ?? "").Split(Separators, StringSplitOptions.RemoveEmptyEntries)) {
            string value = raw.Trim();
            if (value is "*" or "*.*") { result.Add("*"); continue; }
            if (value.StartsWith("*.")) value = value[1..];
            if (!value.StartsWith('.')) value = "." + value;
            if (value.Length < 2 || value[1..].Contains('.') || value.EndsWith('.')
                || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.IndexOfAny(Forbidden) >= 0)
                throw new ArgumentException(Texts.Get("InvalidExtension") + raw + Texts.Get("ExtensionExample"));
            result.Add(value);
        }
        if (result.Count == 0) throw new ArgumentException(Texts.Get("NoExtension"));
        return result;
    }

    public static Update Run(string source, string destination, string extensions, CancellationToken token, Action<Update> report) =>
        Run(source, destination, extensions, new ExtractionOptions(), token, report);

    public static Update Run(string source, string destination, string extensions, ExtractionOptions options, CancellationToken token, Action<Update> report)
    {
        ArgumentNullException.ThrowIfNull(options);
        var filter = ParseExtensions(extensions);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(Texts.Get("SourceNotFound"));
        destination = Path.GetFullPath(destination);
        EnsureNoLinks(destination, destination);
        Directory.CreateDirectory(destination);
        return new Extraction(Path.GetFullPath(source), destination, filter, options, token, report).Run();
    }

    // Archive paths must stay relative on both Windows and Unix, including Windows paths inside a Linux ZIP.
    static string TargetPath(string destination, string entryName)
    {
        string relative = entryName.Replace('\\', '/');
        if (relative.StartsWith('/') || DriveLetter.IsMatch(relative)) throw new IOException(Texts.Get("AbsolutePath"));
        string[] segments = relative.Split('/');
        foreach (string segment in segments) {
            if (segment is "" or "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new IOException(Texts.Get("InvalidPath"));
            if (OperatingSystem.IsWindows()) {
                if (segment.EndsWith('.') || segment.EndsWith(' ')) throw new IOException(Texts.Get("AmbiguousName"));
                string stem = segment.Split('.')[0].TrimEnd().ToUpperInvariant();
                if (stem is "CON" or "PRN" or "AUX" or "NUL" || NumberedDevice.IsMatch(stem)) throw new IOException(Texts.Get("ReservedName"));
            }
        }
        string prefix = Path.EndsInDirectorySeparator(destination) ? destination : destination + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(Path.Combine(destination, Path.Combine(segments)));
        if (!target.StartsWith(prefix, PathComparison)) throw new IOException(Texts.Get("OutsideDestination"));
        EnsureNoLinks(destination, target);
        return target;
    }

    // Only the destination and what lies beneath it are checked: links above it (a relocated home folder…) were chosen by the user.
    static void EnsureNoLinks(string destination, string path)
    {
        string root = Path.TrimEndingDirectorySeparator(destination);
        for (string? current = path; current != null && current.Length >= root.Length; current = Path.GetDirectoryName(current))
            if (IsLink(current)) throw new IOException(Texts.Get("LinkInDestination"));
    }

    // OneDrive placeholders are reparse points too, but not links: only symbolic links and junctions are refused.
    static bool IsLink(string path)
    {
        try {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0) return false;
        } catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException) { return false; }
        return new FileInfo(path).LinkTarget != null;
    }

    sealed class Extraction(string source, string destination, HashSet<string> filter, ExtractionOptions options, CancellationToken token, Action<Update> report)
    {
        readonly Update state = new() { Scanning = true, Status = Texts.Get("SearchingForArchives") };
        readonly byte[] buffer = new byte[128 * 1024];

        void Report(string? message = null)
        {
            state.Message = message;
            report(state with { });
        }

        public Update Run()
        {
            Report();
            var archives = FindArchives();
            state.Archives = archives.Count; state.Scanning = false;
            Report();
            foreach (string archive in archives) {
                token.ThrowIfCancellationRequested();
                state.Status = Texts.Get("Reading") + Path.GetFileName(archive);
                Report();
                ExtractArchive(archive);
                state.Done++;
                Report();
            }
            state.Status = archives.Count == 0 ? Texts.Get("NoZipArchivesFound") : Texts.Get("ExtractionComplete");
            Report(Texts.Format("Summary", state.Extracted, state.Skipped, state.Issues, state.Renamed));
            return state with { };
        }

        List<string> FindArchives()
        {
            var archives = new List<string>();
            var folders = new Stack<string>([source]);
            while (folders.Count > 0) {
                token.ThrowIfCancellationRequested();
                string folder = folders.Pop();
                try {
                    foreach (string file in Directory.EnumerateFiles(folder))
                        if (Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase)) archives.Add(file);
                    if (!options.SearchSubfolders) continue;
                    // Links can form loops or leave the chosen tree.
                    foreach (string child in Directory.EnumerateDirectories(folder))
                        if (!IsLink(child)) folders.Push(child);
                } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                    state.Issues++;
                    Report(Texts.Get("Access") + folder + " : " + ex.Message);
                }
            }
            archives.Sort(PathComparer);
            return archives;
        }

        void ExtractArchive(string archive)
        {
            string root;
            try {
                // The archive's relative path keeps folder names stable when other ZIP files are added later.
                root = options.FolderPerArchive ? TargetPath(destination, Path.ChangeExtension(Path.GetRelativePath(source, archive), null)) : destination;
            } catch (IOException ex) {
                state.Issues++;
                Report(Texts.Get("Error") + archive + " : " + ex.Message);
                return;
            }
            try {
                using var zip = ZipFile.Open(archive, ZipArchiveMode.Read, NameEncoding);
                foreach (var entry in zip.Entries) {
                    token.ThrowIfCancellationRequested();
                    if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) continue;
                    try {
                        ExtractEntry(entry, archive, root);
                    } catch (Exception ex) when (ex is not OperationCanceledException) {
                        state.Issues++;
                        Report(Texts.Get("Error") + archive + " / " + entry.FullName + " : " + ex.Message);
                    }
                }
            } catch (Exception ex) when (ex is not OperationCanceledException) {
                state.Issues++;
                Report(Texts.Get("UnreadableZip") + archive + " : " + ex.Message);
            }
        }

        void ExtractEntry(ZipArchiveEntry entry, string archive, string root)
        {
            string name = entry.FullName;
            if (!filter.Contains("*") && !filter.Contains(Path.GetExtension(name))) return;
            if (!options.IncludeArchiveSubfolders && name.Replace('\\', '/').Contains('/')) return;
            // The full entry path is validated even when its folders are dropped.
            string target = TargetPath(root, name);
            if (!options.PreserveFolders) target = TargetPath(root, Path.GetFileName(name.Replace('\\', '/')));
            bool renamed = false;
            if (File.Exists(target) || Directory.Exists(target)) {
                if (IsSameFile(target, entry)) {
                    state.Skipped++;
                    Report(Texts.Get("AlreadyPresent") + name);
                    return;
                }
                if (!options.RenameConflicts) {
                    state.Issues++;
                    Report(Texts.Get("Conflict") + name + Texts.Get("ConflictDetails") + archive);
                    return;
                }
                string folder = Path.GetDirectoryName(target)!, stem = Path.GetFileNameWithoutExtension(target), suffix = Path.GetExtension(target);
                for (int index = 2; ; index++) {
                    token.ThrowIfCancellationRequested();
                    string candidate = Path.Combine(folder, $"{stem} ({index}){suffix}");
                    EnsureNoLinks(destination, candidate);
                    if (IsSameFile(candidate, entry)) {
                        state.Skipped++;
                        Report(Texts.Get("AlreadyPresent") + name + " → " + Path.GetFileName(candidate));
                        return;
                    }
                    if (!File.Exists(candidate) && !Directory.Exists(candidate)) { target = candidate; renamed = true; break; }
                }
            }
            state.Status = Texts.Get("Extracting") + name;
            Report();
            Write(entry, target);
            state.Extracted++;
            if (renamed) {
                state.Renamed++;
                Report(Texts.Get("ExtractedAndRenamed") + name + " → " + Path.GetFileName(target));
            } else Report(Texts.Get("Extracted") + name);
        }

        // Same size and same CRC-32 as recorded in the ZIP: the file is considered already extracted.
        bool IsSameFile(string path, ZipArchiveEntry entry)
        {
            if (!File.Exists(path) || new FileInfo(path).Length != entry.Length) return false;
            var crc = new Crc32();
            using var stream = File.OpenRead(path);
            int length;
            while ((length = stream.Read(buffer)) > 0) {
                token.ThrowIfCancellationRequested();
                crc.Append(buffer.AsSpan(0, length));
            }
            return crc.GetCurrentHashAsUInt32() == entry.Crc32;
        }

        void Write(ZipArchiveEntry entry, string target)
        {
            string parent = Path.GetDirectoryName(target)!;
            Directory.CreateDirectory(parent);
            EnsureNoLinks(destination, target);
            string? temporary = Path.Combine(parent, ".extraction-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                var crc = new Crc32();
                long written = 0;
                using (var input = entry.Open())
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)) {
                    int length;
                    while ((length = input.Read(buffer)) > 0) {
                        token.ThrowIfCancellationRequested();
                        // Stop as soon as the data exceeds the declared size instead of filling the disk.
                        written += length;
                        if (written > entry.Length) throw new IOException(Texts.Get("SizeMismatch"));
                        output.Write(buffer, 0, length);
                        crc.Append(buffer.AsSpan(0, length));
                    }
                }
                if (written != entry.Length) throw new IOException(Texts.Get("SizeMismatch"));
                if (crc.GetCurrentHashAsUInt32() != entry.Crc32) throw new IOException(Texts.Get("ChecksumMismatch"));
                token.ThrowIfCancellationRequested();
                EnsureNoLinks(destination, target);
                File.Move(temporary, target);
                temporary = null;
            } finally {
                if (temporary != null && File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    // Names without the ZIP UTF-8 flag: valid UTF-8 (macOS, Linux, 7-Zip) or else the OEM code page used by Windows Explorer.
    sealed class ZipNameEncoding : Encoding
    {
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        readonly Encoding legacy;

        public ZipNameEncoding()
        {
            RegisterProvider(CodePagesEncodingProvider.Instance);
            int page = CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
            try { legacy = GetEncoding(page > 1 ? page : 437); } catch (Exception e) when (e is ArgumentException or NotSupportedException) { legacy = GetEncoding(437); }
        }

        Encoding For(byte[] bytes, int index, int count)
        {
            try { Utf8.GetCharCount(bytes, index, count); return Utf8; } catch (DecoderFallbackException) { return legacy; }
        }

        public override int GetCharCount(byte[] bytes, int index, int count) => For(bytes, index, count).GetCharCount(bytes, index, count);
        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) =>
            For(bytes, byteIndex, byteCount).GetChars(bytes, byteIndex, byteCount, chars, charIndex);
        public override int GetMaxCharCount(int byteCount) => Math.Max(Utf8.GetMaxCharCount(byteCount), legacy.GetMaxCharCount(byteCount));
        public override int GetByteCount(char[] chars, int index, int count) => Utf8.GetByteCount(chars, index, count);
        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) =>
            Utf8.GetBytes(chars, charIndex, charCount, bytes, byteIndex);
        public override int GetMaxByteCount(int charCount) => Utf8.GetMaxByteCount(charCount);
    }
}
