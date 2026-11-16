using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace NeonHorizonWallpaper
{
    public sealed class SemVer : IComparable<SemVer>
    {
        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }
        public string Prerelease { get; private set; }
        public string Normalized { get; private set; }

        private SemVer(int major, int minor, int patch, string prerelease)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Prerelease = prerelease ?? string.Empty;
            Normalized = string.IsNullOrEmpty(Prerelease)
                ? string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", major, minor, patch)
                : string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}-{3}", major, minor, patch, Prerelease);
        }

        public static bool TryParse(string raw, out SemVer version)
        {
            version = null;
            if (string.IsNullOrEmpty(raw)) return false;

            string s = raw.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(1);
            }

            int buildPlus = s.IndexOf('+');
            if (buildPlus >= 0) s = s.Substring(0, buildPlus);

            string pre = string.Empty;
            int dashIdx = s.IndexOf('-');
            if (dashIdx >= 0)
            {
                pre = s.Substring(dashIdx + 1).Trim();
                s = s.Substring(0, dashIdx);
            }

            string[] parts = s.Split('.');
            if (parts.Length < 1 || parts.Length > 4) return false;

            int maj, min = 0, pat = 0;
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out maj)) return false;
            if (parts.Length >= 2 && !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out min)) return false;
            if (parts.Length >= 3 && !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out pat)) return false;

            version = new SemVer(maj, min, pat, pre);
            return true;
        }

        public int CompareTo(SemVer other)
        {
            if (other == null) return 1;
            int c = Major.CompareTo(other.Major);
            if (c != 0) return c;
            c = Minor.CompareTo(other.Minor);
            if (c != 0) return c;
            c = Patch.CompareTo(other.Patch);
            if (c != 0) return c;

            bool selfEmpty = string.IsNullOrEmpty(Prerelease);
            bool otherEmpty = string.IsNullOrEmpty(other.Prerelease);
            if (selfEmpty && otherEmpty) return 0;
            if (selfEmpty) return 1;
            if (otherEmpty) return -1;
            return string.Compare(Prerelease, other.Prerelease, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return Normalized;
        }
    }

    public sealed class UpdateAssetInfo
    {
        public string Name { get; set; }
        public string DownloadUrl { get; set; }
        public long SizeBytes { get; set; }
        public string ExpectedSha256 { get; set; }
    }

    public sealed class UpdateReleaseInfo
    {
        public string Platform { get; set; }
        public string TagName { get; set; }
        public string ReleaseName { get; set; }
        public SemVer Version { get; set; }
        public string HtmlUrl { get; set; }
        public string Body { get; set; }
        public string PublishedAt { get; set; }
        public UpdateAssetInfo BinaryAsset { get; set; }
        public UpdateAssetInfo ChecksumsAsset { get; set; }
        public UpdateAssetInfo ManifestAsset { get; set; }
    }

    public sealed class UpdateCheckResult
    {
        public bool Success { get; set; }
        public bool UpdateAvailable { get; set; }
        public SemVer CurrentVersion { get; set; }
        public UpdateReleaseInfo LatestRelease { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsUserInitiated { get; set; }
    }

    public static class UpdateManager
    {
        public const string PlatformId = "windows";
        public const string RepoOwner = "Roma-Skull";
        public const string RepoName = "synthwave-wallpapper";
        public const string ReleasesApiUrl = "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases?per_page=25";
        public const string ReleasesPageUrl = "https://github.com/" + RepoOwner + "/" + RepoName + "/releases";

        private static bool _tlsInitialized;

        private static void EnsureTlsConfigured()
        {
            if (_tlsInitialized) return;
            _tlsInitialized = true;
            try
            {
                // .NET 4.0 headers omit SecurityProtocolType.Tls12 (3072) and Tls13 (12288),
                // so cast numeric flags to negotiate modern TLS cleanly on Windows 10/11.
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | (SecurityProtocolType)768;
            }
            catch
            {
            }
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288;
            }
            catch
            {
            }
        }

        public static SemVer GetCurrentVersion()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                object[] attrs = asm.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
                if (attrs != null && attrs.Length > 0)
                {
                    AssemblyInformationalVersionAttribute infoAttr = attrs[0] as AssemblyInformationalVersionAttribute;
                    SemVer parsed;
                    if (infoAttr != null && SemVer.TryParse(infoAttr.InformationalVersion, out parsed))
                    {
                        return parsed;
                    }
                }
                Version v = asm.GetName().Version;
                if (v != null && (v.Major > 0 || v.Minor > 0 || v.Build > 0))
                {
                    SemVer fromAsm;
                    if (SemVer.TryParse(string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", v.Major, v.Minor, Math.Max(0, v.Build)), out fromAsm))
                    {
                        return fromAsm;
                    }
                }
            }
            catch
            {
            }
            SemVer fallback;
            SemVer.TryParse("1.1.0", out fallback);
            return fallback;
        }

        /// <summary>
        /// Parses release tags using the multi-OS convention:
        /// - OS-scoped tags: "&lt;platform&gt;-v&lt;semver&gt;" (e.g. "windows-v1.2.0", "android-v1.0.0", "linux-v1.0.0").
        ///   Tags scoped to a different OS are strictly rejected for targetPlatform.
        /// - Shared/legacy tags: "v&lt;semver&gt;" (e.g. "v1.0.0") return isPlatformSpecific = false so the caller
        ///   also verifies matching platform assets inside the release.
        /// </summary>
        public static bool TryParsePlatformReleaseTag(
            string tagName,
            string targetPlatform,
            out bool isPlatformSpecific,
            out SemVer version)
        {
            isPlatformSpecific = false;
            version = null;
            if (string.IsNullOrEmpty(tagName) || string.IsNullOrEmpty(targetPlatform)) return false;

            string t = tagName.Trim();
            string osPrefix = targetPlatform + "-v";
            if (t.StartsWith(osPrefix, StringComparison.OrdinalIgnoreCase))
            {
                isPlatformSpecific = true;
                return SemVer.TryParse(t.Substring(targetPlatform.Length + 1), out version);
            }

            // Detect any "<otherOs>-v..." prefix and reject it cleanly
            int dashV = t.IndexOf("-v", StringComparison.OrdinalIgnoreCase);
            if (dashV > 0)
            {
                string prefix = t.Substring(0, dashV);
                bool looksLikeSlug = true;
                for (int i = 0; i < prefix.Length; i++)
                {
                    char ch = prefix[i];
                    if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '-')
                    {
                        looksLikeSlug = false;
                        break;
                    }
                }
                if (looksLikeSlug)
                {
                    return false;
                }
            }

            // Fallback: shared/legacy tag starting cleanly with "v" followed by a digit (e.g., "v1.0.0")
            if ((t.StartsWith("v", StringComparison.OrdinalIgnoreCase) && t.Length > 1 && char.IsDigit(t[1])) ||
                char.IsDigit(t[0]))
            {
                isPlatformSpecific = false;
                return SemVer.TryParse(t, out version);
            }

            return false;
        }

        private static string GetStr(Dictionary<string, object> map, string key)
        {
            object val;
            return map != null && map.TryGetValue(key, out val) && val != null ? val.ToString() : string.Empty;
        }

        private static bool GetBool(Dictionary<string, object> map, string key)
        {
            object val;
            if (map != null && map.TryGetValue(key, out val) && val is bool) return (bool)val;
            return false;
        }

        private static long GetLong(Dictionary<string, object> map, string key)
        {
            object val;
            if (map == null || !map.TryGetValue(key, out val) || val == null) return 0;
            try { return Convert.ToInt64(val, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static bool IsWindowsBinaryAssetName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string low = name.ToLowerInvariant();
            if (!low.EndsWith(".exe")) return false;
            if (low.Contains("windows") || low.Contains("-win") || low.Contains("_win")) return true;
            return string.Equals(name, "NeonHorizonWallpaper.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static int ScoreWindowsBinaryAsset(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            string low = name.ToLowerInvariant();
            if (!low.EndsWith(".exe")) return -1;
            if (string.Equals(low, "neonhorizonwallpaper-windows.exe", StringComparison.Ordinal)) return 100;
            if (low.Contains("windows")) return 90;
            if (low.Contains("-win") || low.Contains("_win")) return 80;
            if (string.Equals(low, "neonhorizonwallpaper.exe", StringComparison.Ordinal)) return 70;
            return -1;
        }

        private static int ScoreChecksumsAsset(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            string low = name.ToLowerInvariant();
            if (low == "checksums-windows-sha256.txt") return 100;
            if (low.Contains("windows") && low.Contains("sha256") && low.EndsWith(".txt")) return 90;
            if (low == "checksums-sha256.txt") return 80;
            return -1;
        }

        public static UpdateReleaseInfo SelectLatestWindowsReleaseFromJson(string releasesJson, out string parseError)
        {
            parseError = null;
            if (string.IsNullOrEmpty(releasesJson))
            {
                parseError = "Empty releases payload.";
                return null;
            }

            object parsedObj;
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = 8 * 1024 * 1024;
                parsedObj = ser.DeserializeObject(releasesJson);
            }
            catch (Exception ex)
            {
                parseError = "Failed to parse GitHub releases JSON: " + ex.Message;
                return null;
            }

            object[] list = parsedObj as object[];
            if (list == null)
            {
                parseError = "GitHub releases payload is not an array.";
                return null;
            }

            UpdateReleaseInfo best = null;
            for (int i = 0; i < list.Length; i++)
            {
                Dictionary<string, object> rel = list[i] as Dictionary<string, object>;
                if (rel == null) continue;
                if (GetBool(rel, "draft") || GetBool(rel, "prerelease")) continue;

                string tagName = GetStr(rel, "tag_name");
                bool isPlatformSpecific;
                SemVer ver;
                if (!TryParsePlatformReleaseTag(tagName, PlatformId, out isPlatformSpecific, out ver))
                {
                    continue;
                }

                object rawAssetsObj;
                object[] rawAssets = rel.TryGetValue("assets", out rawAssetsObj) ? rawAssetsObj as object[] : null;
                if (rawAssets == null || rawAssets.Length == 0) continue;

                UpdateAssetInfo bestBinary = null;
                int bestBinaryScore = -1;
                UpdateAssetInfo bestChecksums = null;
                int bestChecksumScore = -1;
                UpdateAssetInfo manifestAsset = null;

                for (int j = 0; j < rawAssets.Length; j++)
                {
                    Dictionary<string, object> a = rawAssets[j] as Dictionary<string, object>;
                    if (a == null) continue;
                    string aName = GetStr(a, "name");
                    string aUrl = GetStr(a, "browser_download_url");
                    long aSize = GetLong(a, "size");
                    if (string.IsNullOrEmpty(aName) || string.IsNullOrEmpty(aUrl)) continue;

                    int bScore = ScoreWindowsBinaryAsset(aName);
                    if (bScore > bestBinaryScore)
                    {
                        bestBinaryScore = bScore;
                        bestBinary = new UpdateAssetInfo { Name = aName, DownloadUrl = aUrl, SizeBytes = aSize };
                    }

                    int cScore = ScoreChecksumsAsset(aName);
                    if (cScore > bestChecksumScore)
                    {
                        bestChecksumScore = cScore;
                        bestChecksums = new UpdateAssetInfo { Name = aName, DownloadUrl = aUrl, SizeBytes = aSize };
                    }

                    if (string.Equals(aName, "manifest-windows.json", StringComparison.OrdinalIgnoreCase))
                    {
                        manifestAsset = new UpdateAssetInfo { Name = aName, DownloadUrl = aUrl, SizeBytes = aSize };
                    }
                }

                // Shared/legacy tags ("v1.0.0") must explicitly contain a Windows binary or manifest
                if (bestBinary == null && manifestAsset == null) continue;
                if (!isPlatformSpecific && bestBinary != null && !IsWindowsBinaryAssetName(bestBinary.Name) && manifestAsset == null)
                {
                    continue;
                }

                UpdateReleaseInfo candidate = new UpdateReleaseInfo
                {
                    Platform = PlatformId,
                    TagName = tagName,
                    ReleaseName = GetStr(rel, "name"),
                    Version = ver,
                    HtmlUrl = GetStr(rel, "html_url"),
                    Body = GetStr(rel, "body"),
                    PublishedAt = GetStr(rel, "published_at"),
                    BinaryAsset = bestBinary,
                    ChecksumsAsset = bestChecksums,
                    ManifestAsset = manifestAsset
                };

                if (best == null || candidate.Version.CompareTo(best.Version) > 0)
                {
                    best = candidate;
                }
            }

            return best;
        }

        public static string ParseSha256ForAsset(string checksumsText, string assetName)
        {
            if (string.IsNullOrEmpty(checksumsText) || string.IsNullOrEmpty(assetName)) return null;
            string[] lines = checksumsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length < 66) continue;
                int sep = line.IndexOfAny(new[] { ' ', '\t' });
                if (sep != 64) continue;
                string hex = line.Substring(0, 64);
                string filePart = line.Substring(sep).Trim().TrimStart('*');
                if (string.Equals(Path.GetFileName(filePart), assetName, StringComparison.OrdinalIgnoreCase) && IsHex64(hex))
                {
                    return hex.ToLowerInvariant();
                }
            }
            return null;
        }

        private static bool IsHex64(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 64) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok) return false;
            }
            return true;
        }

        public static string ComputeFileSha256(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256Managed sha = new SHA256Managed())
            {
                byte[] hash = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder(64);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }

        private static HttpWebRequest CreateGitHubRequest(string url, string accept)
        {
            EnsureTlsConfigured();
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 15000;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            req.UserAgent = "NeonHorizonWallpaper/" + GetCurrentVersion().Normalized + " (Windows)";
            if (!string.IsNullOrEmpty(accept))
            {
                req.Accept = accept;
            }
            return req;
        }

        private static string DownloadString(string url, string accept)
        {
            HttpWebRequest req = CreateGitHubRequest(url, accept);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream stream = resp.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static void DownloadBinaryFile(string url, string destPath, long expectedSize, Action<int> onProgressPercent)
        {
            HttpWebRequest req = CreateGitHubRequest(url, "application/octet-stream");
            req.Timeout = 30000;
            req.ReadWriteTimeout = 30000;

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream src = resp.GetResponseStream())
            using (FileStream dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                long total = resp.ContentLength > 0 ? resp.ContentLength : expectedSize;
                byte[] buf = new byte[65536];
                long received = 0;
                int lastPct = -1;
                while (true)
                {
                    int read = src.Read(buf, 0, buf.Length);
                    if (read <= 0) break;
                    dst.Write(buf, 0, read);
                    received += read;
                    if (total > 0 && onProgressPercent != null)
                    {
                        int pct = (int)Math.Min(100, (received * 100L) / total);
                        if (pct != lastPct)
                        {
                            lastPct = pct;
                            onProgressPercent(pct);
                        }
                    }
                }
            }
        }

        private static void EnrichReleaseWithManifest(UpdateReleaseInfo rel)
        {
            if (rel == null || rel.ManifestAsset == null || string.IsNullOrEmpty(rel.ManifestAsset.DownloadUrl)) return;
            try
            {
                string json = DownloadString(rel.ManifestAsset.DownloadUrl, "application/json");
                JavaScriptSerializer ser = new JavaScriptSerializer();
                Dictionary<string, object> doc = ser.DeserializeObject(json) as Dictionary<string, object>;
                if (doc == null) return;
                string plat = GetStr(doc, "platform");
                if (!string.IsNullOrEmpty(plat) && !string.Equals(plat, PlatformId, StringComparison.OrdinalIgnoreCase)) return;

                string mVerStr = GetStr(doc, "version");
                SemVer mVer;
                if (!string.IsNullOrEmpty(mVerStr) && SemVer.TryParse(mVerStr, out mVer))
                {
                    rel.Version = mVer;
                }
                string mSha = GetStr(doc, "sha256");
                if (rel.BinaryAsset != null && IsHex64(mSha))
                {
                    rel.BinaryAsset.ExpectedSha256 = mSha.ToLowerInvariant();
                }
            }
            catch
            {
                // Manifest enrichment is optional; checksums & release metadata remain valid
            }
        }

        public static UpdateCheckResult CheckForUpdates(bool userInitiated)
        {
            UpdateCheckResult res = new UpdateCheckResult
            {
                CurrentVersion = GetCurrentVersion(),
                IsUserInitiated = userInitiated
            };

            try
            {
                string json = DownloadString(ReleasesApiUrl, "application/vnd.github+json");
                string parseErr;
                UpdateReleaseInfo latest = SelectLatestWindowsReleaseFromJson(json, out parseErr);
                if (!string.IsNullOrEmpty(parseErr))
                {
                    res.ErrorMessage = parseErr;
                    return res;
                }

                if (latest != null && latest.ManifestAsset != null && latest.BinaryAsset == null)
                {
                    EnrichReleaseWithManifest(latest);
                }

                res.Success = true;
                res.LatestRelease = latest;
                res.UpdateAvailable = latest != null && latest.Version != null && latest.Version.CompareTo(res.CurrentVersion) > 0;
                if (res.UpdateAvailable && latest.ManifestAsset != null)
                {
                    EnrichReleaseWithManifest(latest);
                }
                return res;
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.ErrorMessage = ex.Message;
                return res;
            }
        }

        public static string GetUpdatesStagingRoot()
        {
            return Path.Combine(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NeonHorizonWallpaper"),
                "updates");
        }

        public static string DownloadAndStageUpdate(UpdateReleaseInfo release, Action<int, string> onStatus, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (release == null || release.BinaryAsset == null || string.IsNullOrEmpty(release.BinaryAsset.DownloadUrl))
            {
                errorMessage = "No compatible Windows executable asset was found in the release.";
                return null;
            }

            try
            {
                if (onStatus != null) onStatus(0, "Fetching checksums...");

                string expectedSha = release.BinaryAsset.ExpectedSha256;
                if (string.IsNullOrEmpty(expectedSha) && release.ChecksumsAsset != null && !string.IsNullOrEmpty(release.ChecksumsAsset.DownloadUrl))
                {
                    try
                    {
                        string checksumText = DownloadString(release.ChecksumsAsset.DownloadUrl, "text/plain");
                        expectedSha = ParseSha256ForAsset(checksumText, release.BinaryAsset.Name);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to download checksums: " + ex.Message);
                    }
                }

                string stageDir = Path.Combine(GetUpdatesStagingRoot(), "v" + release.Version.Normalized);
                if (!Directory.Exists(stageDir))
                {
                    Directory.CreateDirectory(stageDir);
                }

                string partPath = Path.Combine(stageDir, "NeonHorizonWallpaper-staged.exe.part");
                string finalPath = Path.Combine(stageDir, "NeonHorizonWallpaper-staged.exe");
                if (File.Exists(partPath)) File.Delete(partPath);
                if (File.Exists(finalPath)) File.Delete(finalPath);

                DownloadBinaryFile(release.BinaryAsset.DownloadUrl, partPath, release.BinaryAsset.SizeBytes, delegate(int pct)
                {
                    if (onStatus != null) onStatus(pct, string.Format(CultureInfo.InvariantCulture, "Downloading v{0}... {1}%", release.Version.Normalized, pct));
                });

                if (onStatus != null) onStatus(100, "Verifying package integrity...");

                FileInfo fi = new FileInfo(partPath);
                if (!fi.Exists || fi.Length < 65536)
                {
                    File.Delete(partPath);
                    errorMessage = "Downloaded update file is incomplete or corrupted.";
                    return null;
                }

                // Verify DOS/PE MZ header before accepting executable payload
                byte[] magic = new byte[2];
                using (FileStream fs = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs.Read(magic, 0, 2) != 2 || magic[0] != (byte)'M' || magic[1] != (byte)'Z')
                    {
                        fs.Close();
                        File.Delete(partPath);
                        errorMessage = "Downloaded asset is not a valid Windows PE executable.";
                        return null;
                    }
                }

                if (!string.IsNullOrEmpty(expectedSha))
                {
                    string actualSha = ComputeFileSha256(partPath);
                    if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(partPath);
                        errorMessage = string.Format(
                            CultureInfo.InvariantCulture,
                            "SHA-256 checksum verification failed (expected {0}, got {1}).",
                            expectedSha.Substring(0, 12),
                            actualSha.Substring(0, 12));
                        return null;
                    }
                    Logger.Info("Verified SHA-256 for update v" + release.Version.Normalized + ": " + actualSha);
                }

                File.Move(partPath, finalPath);
                return finalPath;
            }
            catch (Exception ex)
            {
                errorMessage = "Update download failed: " + ex.Message;
                return null;
            }
        }

        public static bool CanWriteTargetExecutable(string targetExePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(targetExePath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
                string probe = Path.Combine(dir, ".update_write_probe_" + Process.GetCurrentProcess().Id + ".tmp");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool LaunchStagedUpdaterAndPrepareExit(string stagedExePath, string currentExePath, bool reopenSettings, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                int myPid = Process.GetCurrentProcess().Id;
                StringBuilder args = new StringBuilder();
                args.Append("--apply-update ");
                args.Append(myPid.ToString(CultureInfo.InvariantCulture));
                args.Append(' ');
                args.Append('"').Append(currentExePath).Append('"');
                if (reopenSettings)
                {
                    args.Append(" --settings");
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = stagedExePath;
                psi.Arguments = args.ToString();
                psi.UseShellExecute = true;
                psi.WorkingDirectory = Path.GetDirectoryName(currentExePath) ?? Environment.CurrentDirectory;
                if (!CanWriteTargetExecutable(currentExePath))
                {
                    psi.Verb = "runas";
                }

                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Could not start update installer: " + ex.Message;
                return false;
            }
        }

        public static int RunApplyUpdateWorker(int parentPid, string targetExePath, bool reopenSettings)
        {
            Logger.Info("Updater worker running for PID " + parentPid + " -> " + targetExePath);

            // 1. Wait cleanly for previous process instance to release file handle & Mutex
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using (Process p = Process.GetProcessById(parentPid))
                    {
                        if (p.HasExited) break;
                        p.WaitForExit(250);
                    }
                }
                catch (ArgumentException)
                {
                    break; // Process already exited
                }
                catch
                {
                    Thread.Sleep(200);
                }
            }
            Thread.Sleep(250);

            string stagedExe = Assembly.GetExecutingAssembly().Location;
            string backupPath = targetExePath + ".bak";
            string tempPath = targetExePath + ".new";

            Exception lastError = null;
            bool copied = false;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                try
                {
                    File.Copy(stagedExe, tempPath, true);
                    if (File.Exists(targetExePath))
                    {
                        if (File.Exists(backupPath)) File.Delete(backupPath);
                        File.Move(targetExePath, backupPath);
                    }
                    File.Move(tempPath, targetExePath);
                    copied = true;
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Thread.Sleep(350);
                }
            }

            if (!copied)
            {
                try
                {
                    if (!File.Exists(targetExePath) && File.Exists(backupPath))
                    {
                        File.Move(backupPath, targetExePath);
                    }
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch
                {
                }
                Logger.Error("Apply update copy failed: " + (lastError != null ? lastError.Message : "unknown error"));
                return 1;
            }

            try
            {
                string args = "--updated " + GetCurrentVersion().Normalized + (reopenSettings ? " --settings" : string.Empty);
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = targetExePath;
                psi.Arguments = args;
                psi.UseShellExecute = true;
                psi.WorkingDirectory = Path.GetDirectoryName(targetExePath) ?? Environment.CurrentDirectory;
                Process.Start(psi);
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to relaunch updated app: " + ex.Message);
                return 2;
            }
        }

        public static void CleanupAfterStartup(string currentExePath)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Thread.Sleep(1500);
                    string bak = currentExePath + ".bak";
                    string tmp = currentExePath + ".new";
                    if (File.Exists(bak)) File.Delete(bak);
                    if (File.Exists(tmp)) File.Delete(tmp);

                    string updatesRoot = GetUpdatesStagingRoot();
                    string currentDir = Path.GetDirectoryName(Path.GetFullPath(currentExePath)) ?? string.Empty;
                    if (Directory.Exists(updatesRoot) &&
                        !currentDir.StartsWith(Path.GetFullPath(updatesRoot), StringComparison.OrdinalIgnoreCase))
                    {
                        string[] subdirs = Directory.GetDirectories(updatesRoot);
                        for (int i = 0; i < subdirs.Length; i++)
                        {
                            try { Directory.Delete(subdirs[i], true); }
                            catch { }
                        }
                    }
                }
                catch
                {
                }
            });
        }
    }
}
