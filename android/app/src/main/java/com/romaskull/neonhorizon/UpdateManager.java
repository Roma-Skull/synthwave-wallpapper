package com.romaskull.neonhorizon;

import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;

import androidx.core.content.FileProvider;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.Locale;

public final class UpdateManager {
    public static final String PLATFORM_ID = "android";
    public static final String REPO_OWNER = "Roma-Skull";
    public static final String REPO_NAME = "synthwave-wallpapper";
    public static final String RELEASES_API_URL =
            "https://api.github.com/repos/" + REPO_OWNER + "/" + REPO_NAME + "/releases?per_page=25";
    public static final long AUTO_CHECK_INTERVAL_MS = 6L * 60L * 60L * 1000L;

    private static final String PREF_LAST_AUTO_CHECK = "last_auto_update_check_ms";

    public static final class SemVer implements Comparable<SemVer> {
        public final int major;
        public final int minor;
        public final int patch;
        public final String prerelease;
        public final String normalized;

        private SemVer(int major, int minor, int patch, String prerelease) {
            this.major = major;
            this.minor = minor;
            this.patch = patch;
            this.prerelease = prerelease == null ? "" : prerelease;
            this.normalized = this.prerelease.isEmpty()
                    ? String.format(Locale.US, "%d.%d.%d", major, minor, patch)
                    : String.format(Locale.US, "%d.%d.%d-%s", major, minor, patch, this.prerelease);
        }

        public static SemVer tryParse(String raw) {
            if (raw == null) return null;
            String s = raw.trim();
            if (s.isEmpty()) return null;
            if (s.startsWith("v") || s.startsWith("V")) {
                s = s.substring(1);
            }
            int plus = s.indexOf('+');
            if (plus >= 0) s = s.substring(0, plus);
            String pre = "";
            int dash = s.indexOf('-');
            if (dash >= 0) {
                pre = s.substring(dash + 1).trim();
                s = s.substring(0, dash);
            }
            String[] parts = s.split("\\.");
            if (parts.length < 1 || parts.length > 4) return null;
            try {
                int maj = Integer.parseInt(parts[0]);
                int min = parts.length >= 2 ? Integer.parseInt(parts[1]) : 0;
                int pat = parts.length >= 3 ? Integer.parseInt(parts[2]) : 0;
                return new SemVer(maj, min, pat, pre);
            } catch (NumberFormatException e) {
                return null;
            }
        }

        @Override
        public int compareTo(SemVer o) {
            if (o == null) return 1;
            int c = Integer.compare(major, o.major);
            if (c != 0) return c;
            c = Integer.compare(minor, o.minor);
            if (c != 0) return c;
            c = Integer.compare(patch, o.patch);
            if (c != 0) return c;
            if (prerelease.isEmpty() && o.prerelease.isEmpty()) return 0;
            if (prerelease.isEmpty()) return 1;
            if (o.prerelease.isEmpty()) return -1;
            return prerelease.compareToIgnoreCase(o.prerelease);
        }
    }

    public static final class AssetInfo {
        public final String name;
        public final String downloadUrl;
        public final long sizeBytes;
        public String expectedSha256;

        public AssetInfo(String name, String downloadUrl, long sizeBytes) {
            this.name = name;
            this.downloadUrl = downloadUrl;
            this.sizeBytes = sizeBytes;
        }
    }

    public static final class ReleaseInfo {
        public final String tagName;
        public final SemVer version;
        public final String htmlUrl;
        public final AssetInfo apkAsset;
        public final AssetInfo checksumsAsset;
        public final AssetInfo manifestAsset;

        public ReleaseInfo(
                String tagName,
                SemVer version,
                String htmlUrl,
                AssetInfo apkAsset,
                AssetInfo checksumsAsset,
                AssetInfo manifestAsset) {
            this.tagName = tagName;
            this.version = version;
            this.htmlUrl = htmlUrl;
            this.apkAsset = apkAsset;
            this.checksumsAsset = checksumsAsset;
            this.manifestAsset = manifestAsset;
        }
    }

    public static final class CheckResult {
        public boolean success;
        public boolean updateAvailable;
        public SemVer currentVersion;
        public ReleaseInfo latestRelease;
        public String errorMessage;
    }

    public interface ProgressCallback {
        void onProgress(int percent, String statusText);
    }

    private UpdateManager() {}

    public static SemVer getCurrentVersion() {
        SemVer v = SemVer.tryParse(BuildConfig.VERSION_NAME);
        if (v != null) return v;
        return SemVer.tryParse("1.0.0");
    }

    public static boolean shouldAutoCheck(Context context) {
        long last = WallpaperSettings.getPrefs(context).getLong(PREF_LAST_AUTO_CHECK, 0L);
        return (System.currentTimeMillis() - last) >= AUTO_CHECK_INTERVAL_MS;
    }

    public static void markAutoChecked(Context context) {
        WallpaperSettings.getPrefs(context).edit().putLong(PREF_LAST_AUTO_CHECK, System.currentTimeMillis()).apply();
    }

    public static CheckResult checkForUpdates() {
        CheckResult res = new CheckResult();
        res.currentVersion = getCurrentVersion();
        try {
            String json = downloadString(RELEASES_API_URL, "application/vnd.github+json");
            ReleaseInfo latest = selectLatestAndroidRelease(json);
            if (latest != null && latest.manifestAsset != null) {
                enrichWithManifest(latest);
            }
            res.success = true;
            res.latestRelease = latest;
            res.updateAvailable = latest != null && latest.version != null && latest.version.compareTo(res.currentVersion) > 0;
            return res;
        } catch (Exception e) {
            res.success = false;
            res.errorMessage = e.getMessage() != null ? e.getMessage() : e.getClass().getSimpleName();
            return res;
        }
    }

    public static File downloadAndVerifyApk(
            Context context,
            ReleaseInfo release,
            ProgressCallback callback) throws Exception {
        if (release == null || release.apkAsset == null) {
            throw new IllegalStateException("No compatible Android APK asset found in release.");
        }

        if (callback != null) callback.onProgress(0, "Fetching checksums...");
        String expectedSha = release.apkAsset.expectedSha256;
        if ((expectedSha == null || expectedSha.isEmpty()) && release.checksumsAsset != null) {
            try {
                String txt = downloadString(release.checksumsAsset.downloadUrl, "text/plain");
                expectedSha = parseSha256ForAsset(txt, release.apkAsset.name);
            } catch (Exception ignored) {
            }
        }

        File updatesDir = new File(context.getCacheDir(), "updates");
        if (!updatesDir.exists() && !updatesDir.mkdirs()) {
            throw new IllegalStateException("Cannot create updates cache directory.");
        }
        cleanupOldApks(updatesDir);

        File partFile = new File(updatesDir, "NeonHorizonWallpaper-" + release.version.normalized + ".apk.part");
        File apkFile = new File(updatesDir, "NeonHorizonWallpaper-" + release.version.normalized + ".apk");
        if (partFile.exists()) partFile.delete();
        if (apkFile.exists()) apkFile.delete();

        downloadBinary(release.apkAsset.downloadUrl, partFile, release.apkAsset.sizeBytes, pct -> {
            if (callback != null) {
                callback.onProgress(pct, String.format(Locale.US, "Downloading v%s... %d%%", release.version.normalized, pct));
            }
        });

        if (callback != null) callback.onProgress(100, "Verifying APK integrity...");
        if (!partFile.exists() || partFile.length() < 65536) {
            partFile.delete();
            throw new IllegalStateException("Downloaded APK is incomplete.");
        }

        // Verify ZIP/PK magic bytes
        byte[] magic = new byte[2];
        try (FileInputStream fis = new FileInputStream(partFile)) {
            if (fis.read(magic) != 2 || magic[0] != 'P' || magic[1] != 'K') {
                partFile.delete();
                throw new IllegalStateException("Downloaded file is not a valid APK archive.");
            }
        }

        if (expectedSha != null && !expectedSha.isEmpty()) {
            String actualSha = computeSha256(partFile);
            if (!actualSha.equalsIgnoreCase(expectedSha)) {
                partFile.delete();
                throw new IllegalStateException("SHA-256 verification failed.");
            }
        }

        if (!partFile.renameTo(apkFile)) {
            throw new IllegalStateException("Failed to stage verified APK.");
        }
        return apkFile;
    }

    public static void launchApkInstaller(Context context, File apkFile) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && !context.getPackageManager().canRequestPackageInstalls()) {
            Intent permIntent = new Intent(
                    Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                    Uri.parse("package:" + context.getPackageName()));
            permIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            context.startActivity(permIntent);
            return;
        }

        Uri apkUri = FileProvider.getUriForFile(
                context,
                context.getPackageName() + ".fileprovider",
                apkFile);
        Intent install = new Intent(Intent.ACTION_VIEW);
        install.setDataAndType(apkUri, "application/vnd.android.package-archive");
        install.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
        context.startActivity(install);
    }

    private static ReleaseInfo selectLatestAndroidRelease(String releasesJson) throws Exception {
        JSONArray list = new JSONArray(releasesJson);
        ReleaseInfo best = null;

        for (int i = 0; i < list.length(); i++) {
            JSONObject rel = list.getJSONObject(i);
            if (rel.optBoolean("draft", false) || rel.optBoolean("prerelease", false)) continue;

            String tag = rel.optString("tag_name", "");
            SemVer ver = parsePlatformTag(tag);
            if (ver == null) continue;
            boolean isPlatformSpecific = tag.toLowerCase(Locale.US).startsWith(PLATFORM_ID + "-v");

            JSONArray assets = rel.optJSONArray("assets");
            if (assets == null || assets.length() == 0) continue;

            AssetInfo bestApk = null;
            int bestApkScore = -1;
            AssetInfo bestChecksums = null;
            int bestCheckScore = -1;
            AssetInfo manifestAsset = null;

            for (int j = 0; j < assets.length(); j++) {
                JSONObject a = assets.getJSONObject(j);
                String name = a.optString("name", "");
                String url = a.optString("browser_download_url", "");
                long size = a.optLong("size", 0L);
                if (name.isEmpty() || url.isEmpty()) continue;

                int apkScore = scoreAndroidApkAsset(name);
                if (apkScore > bestApkScore) {
                    bestApkScore = apkScore;
                    bestApk = new AssetInfo(name, url, size);
                }

                int chkScore = scoreChecksumsAsset(name);
                if (chkScore > bestCheckScore) {
                    bestCheckScore = chkScore;
                    bestChecksums = new AssetInfo(name, url, size);
                }

                if ("manifest-android.json".equalsIgnoreCase(name)) {
                    manifestAsset = new AssetInfo(name, url, size);
                }
            }

            if (bestApk == null) continue;
            if (!isPlatformSpecific && manifestAsset == null && !bestApk.name.toLowerCase(Locale.US).contains("android")) {
                continue;
            }

            ReleaseInfo cand = new ReleaseInfo(
                    tag,
                    ver,
                    rel.optString("html_url", ""),
                    bestApk,
                    bestChecksums,
                    manifestAsset);
            if (best == null || cand.version.compareTo(best.version) > 0) {
                best = cand;
            }
        }
        return best;
    }

    private static SemVer parsePlatformTag(String tag) {
        if (tag == null) return null;
        String t = tag.trim();
        String prefix = PLATFORM_ID + "-v";
        if (t.toLowerCase(Locale.US).startsWith(prefix)) {
            return SemVer.tryParse(t.substring(PLATFORM_ID.length() + 1));
        }
        int dashV = t.toLowerCase(Locale.US).indexOf("-v");
        if (dashV > 0) {
            return null; // Reject tags scoped to other platforms like windows-v*
        }
        if ((t.startsWith("v") || t.startsWith("V")) && t.length() > 1 && Character.isDigit(t.charAt(1))) {
            return SemVer.tryParse(t);
        }
        return null;
    }

    private static int scoreAndroidApkAsset(String name) {
        String low = name.toLowerCase(Locale.US);
        if (!low.endsWith(".apk")) return -1;
        if ("neonhorizonwallpaper-android.apk".equals(low)) return 100;
        if (low.contains("android")) return 90;
        return 70;
    }

    private static int scoreChecksumsAsset(String name) {
        String low = name.toLowerCase(Locale.US);
        if ("checksums-android-sha256.txt".equals(low)) return 100;
        if (low.contains("android") && low.contains("sha256") && low.endsWith(".txt")) return 90;
        if ("checksums-sha256.txt".equals(low)) return 80;
        return -1;
    }

    private static void enrichWithManifest(ReleaseInfo rel) {
        try {
            String json = downloadString(rel.manifestAsset.downloadUrl, "application/json");
            JSONObject doc = new JSONObject(json);
            String plat = doc.optString("platform", "");
            if (!plat.isEmpty() && !PLATFORM_ID.equalsIgnoreCase(plat)) return;
            String sha = doc.optString("sha256", "");
            if (rel.apkAsset != null && sha.length() == 64) {
                rel.apkAsset.expectedSha256 = sha.toLowerCase(Locale.US);
            }
        } catch (Exception ignored) {
        }
    }

    private static String parseSha256ForAsset(String text, String assetName) {
        if (text == null || assetName == null) return null;
        String[] lines = text.split("\\r?\\n");
        for (String raw : lines) {
            String line = raw.trim();
            if (line.length() < 66) continue;
            String hex = line.substring(0, 64);
            String rest = line.substring(64).trim();
            if (rest.startsWith("*")) rest = rest.substring(1).trim();
            String fileName = new File(rest).getName();
            if (fileName.equalsIgnoreCase(assetName) && hex.matches("[0-9a-fA-F]{64}")) {
                return hex.toLowerCase(Locale.US);
            }
        }
        return null;
    }

    private static String computeSha256(File file) throws Exception {
        MessageDigest md = MessageDigest.getInstance("SHA-256");
        try (InputStream is = new BufferedInputStream(new FileInputStream(file))) {
            byte[] buf = new byte[16384];
            int n;
            while ((n = is.read(buf)) > 0) {
                md.update(buf, 0, n);
            }
        }
        byte[] digest = md.digest();
        StringBuilder sb = new StringBuilder(64);
        for (byte b : digest) {
            sb.append(String.format(Locale.US, "%02x", b & 0xff));
        }
        return sb.toString();
    }

    private static String downloadString(String urlStr, String accept) throws Exception {
        HttpURLConnection conn = openConn(urlStr, accept);
        try {
            int code = conn.getResponseCode();
            if (code < 200 || code >= 300) {
                throw new IllegalStateException("HTTP " + code);
            }
            try (InputStream is = new BufferedInputStream(conn.getInputStream())) {
                ByteArrayOutputStream out = new ByteArrayOutputStream();
                byte[] buf = new byte[8192];
                int n;
                while ((n = is.read(buf)) > 0) {
                    out.write(buf, 0, n);
                }
                return new String(out.toByteArray(), StandardCharsets.UTF_8);
            }
        } finally {
            conn.disconnect();
        }
    }

    private static void downloadBinary(
            String urlStr,
            File dest,
            long expectedSize,
            java.util.function.IntConsumer onPercent) throws Exception {
        HttpURLConnection conn = openConn(urlStr, "application/octet-stream");
        conn.setReadTimeout(30000);
        try {
            int code = conn.getResponseCode();
            if (code < 200 || code >= 300) {
                throw new IllegalStateException("HTTP " + code);
            }
            long total = conn.getContentLengthLong() > 0 ? conn.getContentLengthLong() : expectedSize;
            long received = 0;
            int lastPct = -1;
            try (InputStream is = new BufferedInputStream(conn.getInputStream());
                 BufferedOutputStream os = new BufferedOutputStream(new FileOutputStream(dest))) {
                byte[] buf = new byte[32768];
                int n;
                while ((n = is.read(buf)) > 0) {
                    os.write(buf, 0, n);
                    received += n;
                    if (total > 0 && onPercent != null) {
                        int pct = (int) Math.min(100, (received * 100L) / total);
                        if (pct != lastPct) {
                            lastPct = pct;
                            onPercent.accept(pct);
                        }
                    }
                }
            }
        } finally {
            conn.disconnect();
        }
    }

    private static HttpURLConnection openConn(String urlStr, String accept) throws Exception {
        HttpURLConnection conn = (HttpURLConnection) new URL(urlStr).openConnection();
        conn.setRequestMethod("GET");
        conn.setConnectTimeout(15000);
        conn.setReadTimeout(15000);
        conn.setInstanceFollowRedirects(true);
        conn.setRequestProperty("User-Agent", "NeonHorizonWallpaper/" + getCurrentVersion().normalized + " (Android)");
        if (accept != null && !accept.isEmpty()) {
            conn.setRequestProperty("Accept", accept);
        }
        return conn;
    }

    private static void cleanupOldApks(File dir) {
        File[] files = dir.listFiles();
        if (files == null) return;
        for (File f : files) {
            if (f.isFile()) {
                f.delete();
            }
        }
    }
}
