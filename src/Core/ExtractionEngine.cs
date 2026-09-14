using System.IO.Compression;
namespace ZipSelect {
    public class ExtractionOptions {
        public bool SearchSubfolders = true;
        public bool IncludeArchiveSubfolders = true;
        public bool PreserveFolders = true;
        public bool RenameConflicts = false;
        public bool FolderPerArchive = false;
    }
    public class Update {
        public int Archives, Done, Extracted, Skipped, Issues, Renamed;
        public string Message, Status;
        public bool Scanning;
        public Update Copy() { return (Update)MemberwiseClone(); }
    }
    public static class Engine {
        public static HashSet<string> ParseExtensions(string input) {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in (input ?? "").Split(new[] { ' ', ',', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                string value = raw.Trim();
                if (value == "*" || value == "*.*") { result.Add("*"); continue; }
                if (value.StartsWith("*.")) value = value.Substring(1);
                if (!value.StartsWith(".")) value = "." + value;
                if (value.Length < 2 || value.Substring(1).IndexOf('.') >= 0 || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.IndexOfAny(['\\', '/', '*', '?', ':', '"', '<', '>', '|']) >= 0 || value.EndsWith("."))
                    throw new ArgumentException(Texts.Get("InvalidExtension") + raw + Texts.Get("ExampleMpPdfJpgToExtractAll"));
                result.Add(value);
            }
            if (result.Count == 0) throw new ArgumentException(Texts.Get("EnterAtLeastOneExtensionOrFor"));
            return result;
        }

        static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        // Archive paths must stay relative on both Windows and Unix, including Windows paths inside a Linux ZIP.
        static string TargetPath(string destination, string entryName) {
            string relative = entryName.Replace('\\', '/');
            if (relative.StartsWith('/') || System.Text.RegularExpressions.Regex.IsMatch(relative, @"^[A-Za-z]:"))
                throw new IOException(Texts.Get("AbsolutePathsAreNotAllowedInsideZip"));
            string[] segments = relative.Split('/');
            foreach (string segment in segments) {
                if (String.IsNullOrEmpty(segment) || segment == "." || segment == ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new IOException(Texts.Get("InvalidPathInsideZipFile"));
                if (OperatingSystem.IsWindows()) {
                    if (segment.EndsWith('.') || segment.EndsWith(' ')) throw new IOException(Texts.Get("AmbiguousFileNameOnWindows"));
                    string stem = segment.Split('.')[0].TrimEnd().ToUpperInvariant();
                    if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || System.Text.RegularExpressions.Regex.IsMatch(stem, @"^(COM|LPT)[1-9¹²³]$"))
                        throw new IOException(Texts.Get("FileNameReservedByWindows"));
                }
            }
            string root = Path.GetFullPath(destination);
            string prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
            if (!target.StartsWith(prefix, PathComparison)) throw new IOException(Texts.Get("PathOutsideTheExtractionFolder"));
            EnsureNoLinks(target);
            return target;
        }

        static void EnsureNoLinks(string path) {
            for (string current = Path.GetFullPath(path); !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current)) {
                try {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException(Texts.Get("TheDestinationContainsASymbolicLinkOr"));
                } catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { }
            }
        }
        public static Update Run(string source, string destination, string extensions, CancellationToken token, Action<Update> report) {
            return Run(source, destination, extensions, new ExtractionOptions(), token, report);
        }

        public static Update Run(string source, string destination, string extensions, ExtractionOptions options, CancellationToken token, Action<Update> report) {
            if (options == null) throw new ArgumentNullException("options");
            var filter = ParseExtensions(extensions);
            var state = new Update { Scanning = true, Status = Texts.Get("SearchingForArchives") };
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException(Texts.Get("TheSourceFolderCouldNotBeFound"));
            destination = Path.GetFullPath(destination);
            EnsureNoLinks(destination);
            Directory.CreateDirectory(destination);
            report(state.Copy());
            var archives = new List<string>();
            var folders = new Stack<string>();
            folders.Push(Path.GetFullPath(source));
            while (folders.Count > 0) {
                token.ThrowIfCancellationRequested();
                string folder = folders.Pop();
                try {
                    foreach (string file in Directory.EnumerateFiles(folder))
                        if (String.Equals(Path.GetExtension(file), ".zip", StringComparison.OrdinalIgnoreCase)) archives.Add(file);
                    foreach (string child in options.SearchSubfolders ? Directory.GetDirectories(folder) : new string[0]) {
                        // Les jonctions peuvent former des boucles ou sortir de l'arborescence choisie.
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) folders.Push(child);
                    }
                } catch (Exception ex) {
                    if (!(ex is IOException) && !(ex is UnauthorizedAccessException)) throw;
                    state.Issues++; state.Message = Texts.Get("Access") + folder + " : " + ex.Message;
                    report(state.Copy()); state.Message = null;
                }
            }
            state.Archives = archives.Count; state.Scanning = false;
            report(state.Copy());
            archives.Sort(PathComparer);
            var archiveFolders = new HashSet<string>(PathComparer);
            foreach (string archive in archives) {
                token.ThrowIfCancellationRequested();
                state.Status = Texts.Get("Reading") + Path.GetFileName(archive);
                state.Message = null; report(state.Copy());
                try {
                    string archiveDestination = destination;
                    if (options.FolderPerArchive) {
                        string stem = Path.GetFileNameWithoutExtension(archive);
                        string folderName = stem;
                        int number = 2;
                        while (!archiveFolders.Add(folderName)) folderName = stem + " (" + number++ + ")";
                        archiveDestination = TargetPath(destination, folderName);
                    }
                    using (var zip = ZipFile.OpenRead(archive)) {
                        foreach (var entry in zip.Entries) {
                            token.ThrowIfCancellationRequested();
                            string name = entry.FullName;
                            if (name.EndsWith("/") || name.EndsWith("\\")) continue;
                            string temporary = null;
                            try {
                                if (!filter.Contains("*") && !filter.Contains(Path.GetExtension(name))) continue;
                                if (!options.IncludeArchiveSubfolders && name.Replace('/', '\\').Contains("\\")) continue;
                                string target = TargetPath(archiveDestination, name);
                                if (!options.PreserveFolders) target = TargetPath(archiveDestination, name.Replace('\\', '/').Split('/').Last());
                                bool renamed = false;
                                if (File.Exists(target) || Directory.Exists(target)) {
                                    if (File.Exists(target) && new FileInfo(target).Length == entry.Length) {
                                        state.Skipped++; state.Message = Texts.Get("AlreadyPresent") + name;
                                        report(state.Copy()); continue;
                                    } else if (options.RenameConflicts) {
                                        string parentPath = Path.GetDirectoryName(target);
                                        string stem = Path.GetFileNameWithoutExtension(target);
                                        string suffix = Path.GetExtension(target);
                                        int index = 2;
                                        string candidate;
                                        do {
                                            token.ThrowIfCancellationRequested();
                                            candidate = Path.Combine(parentPath, stem + " (" + index++ + ")" + suffix);
                                            EnsureNoLinks(candidate);
                                            if (File.Exists(candidate) && new FileInfo(candidate).Length == entry.Length) break;
                                        } while (File.Exists(candidate) || Directory.Exists(candidate));
                                        if (File.Exists(candidate)) {
                                            state.Skipped++; state.Message = Texts.Get("AlreadyPresent") + name + " → " + Path.GetFileName(candidate);
                                            report(state.Copy()); continue;
                                        }
                                        target = candidate;
                                        renamed = true;
                                    } else {
                                        state.Issues++; state.Message = Texts.Get("Conflict") + name + Texts.Get("DifferentSizeOrExistingFolderZip") + archive;
                                        report(state.Copy()); continue;
                                    }
                                }
                                state.Status = Texts.Get("Extracting") + name;
                                state.Message = null; report(state.Copy());
                                string parent = Path.GetDirectoryName(target);
                                Directory.CreateDirectory(parent);
                                EnsureNoLinks(target);
                                temporary = Path.Combine(parent, ".extraction-" + Guid.NewGuid().ToString("N") + ".tmp");
                                using (var input = entry.Open())
                                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)) {
                                    byte[] buffer = new byte[1024 * 128]; int length;
                                    while ((length = input.Read(buffer, 0, buffer.Length)) > 0) {
                                        token.ThrowIfCancellationRequested(); output.Write(buffer, 0, length);
                                    }
                                }
                                if (new FileInfo(temporary).Length != entry.Length) throw new IOException(Texts.Get("IncorrectExtractedFileSize"));
                                token.ThrowIfCancellationRequested();
                                EnsureNoLinks(target);
                                File.Move(temporary, target); temporary = null;
                                state.Extracted++; state.Message = Texts.Get("Extracted") + name;
                                if (renamed) { state.Renamed++; state.Message = Texts.Get("ExtractedAndRenamed") + name + " → " + Path.GetFileName(target); }
                                report(state.Copy());
                            } catch (OperationCanceledException) { throw;
                            } catch (Exception ex) {
                                state.Issues++; state.Message = Texts.Get("Error") + archive + " / " + name + " : " + ex.Message; report(state.Copy());
                            } finally {
                                if (temporary != null && File.Exists(temporary)) File.Delete(temporary);
                            }
                        }
                    }
                } catch (OperationCanceledException) { throw;
                } catch (Exception ex) {
                    state.Issues++; state.Message = Texts.Get("UnreadableZip") + archive + " : " + ex.Message; report(state.Copy());
                }
                state.Done++; state.Message = null; report(state.Copy());
            }
            state.Status = archives.Count == 0 ? Texts.Get("NoZipArchivesFound") : Texts.Get("ExtractionComplete");
            state.Message = String.Format(Texts.Get("SummaryExtractedIncludingRenamedAlreadyPresentTo"), state.Extracted, state.Skipped, state.Issues, state.Renamed);
            report(state.Copy()); return state;
        }
    }
}
