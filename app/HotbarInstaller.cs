using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DfoHotbarCompanion;

public sealed record InstallResult(string Version, bool Rebuilt);

public sealed class HotbarInstaller
{
    private const string Repository = "https://github.com/Lattymoy/daggerfall-js-source.git";
    // The embedded patch was generated from this upstream snapshot. A shallow release clone
    // must fetch it so Git has the base blobs needed for a resilient three-way application.
    private const string PatchBaseCommit = "84d71ad52578fcddce85cc5236380deb54832b63";
    private readonly Action<string> log;
    private readonly string root;
    private readonly string source;
    private readonly string staging;
    private readonly string patchFile;
    private readonly string stateFile;
    private string gitExe = "git.exe";
    private string nodeExe = "node.exe";
    private string npmCli = "";
    private string npxCli = "";

    public string DfoExe { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "DFO Online", "DFO Online.exe");

    public HotbarInstaller(Action<string> log)
    {
        this.log = log;
        root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DFO Hotbar Companion");
        source = Path.Combine(root, "source");
        staging = Path.Combine(root, "package-staging");
        patchFile = Path.Combine(root, "hotbar-pages.patch");
        stateFile = Path.Combine(root, "installed.json");
    }

    public async Task<InstallResult> UpdateAsync()
    {
        if (!File.Exists(DfoExe)) throw new InvalidOperationException($"DFO Online was not found at {DfoExe}");
        if (Process.GetProcessesByName("DFO Online").Length > 0)
            throw new InvalidOperationException("Close DFO Online before updating the hotbar companion.");

        Directory.CreateDirectory(root);
        WriteEmbeddedPatch();
        LocateBuildTools();
        await RequireTool(gitExe, new[] { "--version" }, "Git");
        await RequireTool(nodeExe, new[] { "--version" }, "Node.js");
        await RequireTool(nodeExe, new[] { npmCli, "--version" }, "npm");

        if (!Directory.Exists(Path.Combine(source, ".git")))
        {
            if (Directory.Exists(source)) SafeDeleteDirectory(source);
            log("Downloading the official DFO source (first setup only)…");
            await Run(gitExe, new[] { "clone", "--depth", "1", "--no-tags", Repository, source }, root);
        }

        log("Checking the current official DFO release…");
        await Run(gitExe, new[] { "reset", "--hard" }, source);
        await Run(gitExe, new[] { "clean", "-fd" }, source);
        var tag = LatestRemoteReleaseTag(await Capture(gitExe, new[] { "ls-remote", "--tags", "--refs", "origin", "app-v*" }, source));
        await Run(gitExe, new[] { "fetch", "--depth", "1", "--force", "origin", $"refs/tags/{tag}:refs/tags/{tag}" }, source);
        var version = tag["app-v".Length..];
        await Run(gitExe, new[] { "checkout", "--detach", "--force", tag }, source);

        var asar = Path.Combine(Path.GetDirectoryName(DfoExe)!, "resources", "app.asar");
        if (!File.Exists(asar)) throw new InvalidOperationException("DFO's app.asar package is missing.");
        if (CanReuse(version, asar) && !HasDownloadedGameUpdate())
        {
            log($"DFO {version} is already patched. No rebuild was needed.");
            return new InstallResult(version, false);
        }

        log("Applying the four-page hotbar feature…");
        try
        {
            // Fetch just the patch's base snapshot rather than the repository's large history.
            await Run(gitExe, new[] { "fetch", "--depth", "1", "origin", PatchBaseCommit }, source, quiet: true);
            await Run(gitExe, new[] { "apply", "--3way", "--whitespace=nowarn", patchFile }, source);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("DFO changed around the hotbar code and the compatibility patch no longer applies. Your installed game was left untouched. The companion patch needs a compatibility update.", ex);
        }

        log("Preparing DFO's build tools…");
        await Run(nodeExe, new[] { npmCli, "install", "--no-audit", "--no-fund" }, source);
        log("Building the hotbar-enabled game…");
        await Run(nodeExe, new[] { npmCli, "run", "build" }, source);

        SafeDeleteDirectory(staging);
        Directory.CreateDirectory(staging);
        log("Opening DFO's installed package…");
        await Run(nodeExe, new[] { npxCli, "--yes", "@electron/asar", "extract", asar, staging }, root);

        var seedPath = Path.Combine(staging, "game-seed.json");
        var seed = JsonNode.Parse(await File.ReadAllTextAsync(seedPath))?.AsObject()
            ?? throw new InvalidOperationException("DFO's game index could not be read.");
        var installedShell = seed["shell"]?.GetValue<string>() ?? "";
        var sourceShell = (await Capture(nodeExe, new[] { "scripts/desktopRelease.mjs", "shell" }, source)).Trim();
        if (!string.Equals(installedShell, sourceShell, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This DFO release requires a newer official desktop shell. Launch DFO normally and allow its official installer update to finish, then run the companion again.");

        var builtDist = Path.Combine(source, "dist");
        var packageDist = Path.Combine(staging, "dist");
        if (!Directory.Exists(builtDist)) throw new InvalidOperationException("The DFO build completed without a dist folder.");
        SafeDeleteDirectory(packageDist);
        log("Installing the rebuilt game into a fresh package…");
        CopyDirectory(builtDist, packageDist);
        await WriteSeed(seed, seedPath, packageDist, version);

        var newAsar = Path.Combine(root, "app.asar.new");
        if (File.Exists(newAsar)) File.Delete(newAsar);
        await Run(nodeExe, new[] { npxCli, "--yes", "@electron/asar", "pack", staging, newAsar }, root);
        if (!File.Exists(newAsar) || new FileInfo(newAsar).Length < 1_000_000)
            throw new InvalidOperationException("The rebuilt DFO package did not pass validation.");

        var originalHash = HashFile(asar);
        var backupDir = Path.Combine(root, "backups");
        Directory.CreateDirectory(backupDir);
        var backup = Path.Combine(backupDir, $"app-{originalHash[..12]}.asar");
        if (!File.Exists(backup)) File.Copy(asar, backup);

        log("Activating the hotbar-enabled package…");
        File.Move(newAsar, asar, true);
        var patchedHash = HashFile(asar);
        await File.WriteAllTextAsync(stateFile, JsonSerializer.Serialize(new
        {
            version,
            patchedHash,
            originalHash,
            installedAt = DateTimeOffset.UtcNow,
        }, new JsonSerializerOptions { WriteIndented = true }));
        SafeDeleteDirectory(staging);
        log($"DFO {version} is ready with four hotbar pages.");
        return new InstallResult(version, true);
    }

    public void LaunchDfo()
    {
        if (!File.Exists(DfoExe)) throw new InvalidOperationException("DFO Online could not be found.");
        Process.Start(new ProcessStartInfo(DfoExe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(DfoExe)! });
    }

    private bool CanReuse(string version, string asar)
    {
        try
        {
            var state = JsonNode.Parse(File.ReadAllText(stateFile))?.AsObject();
            return state?["version"]?.GetValue<string>() == version
                && state?["patchedHash"]?.GetValue<string>() == HashFile(asar);
        }
        catch { return false; }
    }

    private static bool HasDownloadedGameUpdate()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Daggerfall JavaScript", "Game", "state.json");
            var state = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
            return state?["current"] is not null || state?["ready"] is not null;
        }
        catch { return false; }
    }

    private void WriteEmbeddedPatch()
    {
        var resource = Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .Single(n => n.EndsWith("hotbar-pages.patch", StringComparison.Ordinal));
        using var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)!;
        using var output = File.Create(patchFile);
        input.CopyTo(output);
    }

    private void LocateBuildTools()
    {
        gitExe = FindInstalledFile("git.exe", new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "cmd", "git.exe"),
        }) ?? "git.exe";
        nodeExe = FindInstalledFile("node.exe", new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs", "node.exe"),
        }) ?? "node.exe";

        var nodeFolder = Path.GetDirectoryName(nodeExe) ?? "";
        npmCli = FindInstalledFile("npm-cli.js", new[] { Path.Combine(nodeFolder, "node_modules", "npm", "bin", "npm-cli.js") }) ?? "";
        npxCli = FindInstalledFile("npx-cli.js", new[] { Path.Combine(nodeFolder, "node_modules", "npm", "bin", "npx-cli.js") }) ?? "";
        if (npmCli.Length == 0 || npxCli.Length == 0)
            throw new InvalidOperationException("Node.js was found, but its npm tools are missing. Reinstall Node.js with npm enabled, then try again.");
        log($"Using Git at {gitExe}");
        log($"Using Node.js at {nodeExe}");
    }

    private static string? FindInstalledFile(string fileName, IEnumerable<string> preferred)
    {
        foreach (var path in preferred)
            if (File.Exists(path)) return path;
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var path = Path.Combine(folder.Trim('"'), fileName);
                if (File.Exists(path)) return path;
            }
            catch { }
        }
        return null;
    }

    private async Task RequireTool(string file, IEnumerable<string> args, string displayName)
    {
        try { await Run(file, args, root, quiet: true); }
        catch { throw new InvalidOperationException($"{displayName} is required for automatic DFO updates but was not found. Install Git and Node.js, then try again."); }
    }

    private async Task Run(string file, IEnumerable<string> args, string cwd, bool quiet = false)
    {
        var result = await RunProcess(file, args, cwd, quiet);
        if (result.ExitCode != 0) throw new InvalidOperationException($"{Path.GetFileName(file)} stopped with exit code {result.ExitCode}. {LastUsefulLine(result.Output)}");
    }

    private async Task<string> Capture(string file, IEnumerable<string> args, string cwd)
    {
        var result = await RunProcess(file, args, cwd, true);
        if (result.ExitCode != 0) throw new InvalidOperationException($"{Path.GetFileName(file)} stopped with exit code {result.ExitCode}. {LastUsefulLine(result.Output)}");
        return result.Output;
    }

    private async Task<(int ExitCode, string Output)> RunProcess(string file, IEnumerable<string> args, string cwd, bool quiet)
    {
        var psi = new ProcessStartInfo(file)
        {
            WorkingDirectory = cwd,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { output.AppendLine(e.Data); if (!quiet && WorthShowing(e.Data)) log(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { output.AppendLine(e.Data); if (!quiet && WorthShowing(e.Data)) log(e.Data); } };
        if (!process.Start()) throw new InvalidOperationException($"Could not start {file}.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        return (process.ExitCode, output.ToString());
    }

    private static bool WorthShowing(string line)
    {
        if (string.IsNullOrWhiteSpace(line)
            || line.Contains("npm WARN", StringComparison.OrdinalIgnoreCase)
            || line.Contains("funding", StringComparison.OrdinalIgnoreCase)) return false;
        // Vite prints one size row for every generated asset (well over ten thousand in DFO).
        // Sending every row synchronously through WPF can turn a one-minute build into a very long one.
        var clean = line.TrimStart('\u001b', '[', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ';', 'm', ' ');
        return !clean.StartsWith("dist/", StringComparison.OrdinalIgnoreCase)
            && !clean.StartsWith("dist\\", StringComparison.OrdinalIgnoreCase);
    }

    private static string LastUsefulLine(string output) => output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? "";

    private static string LatestRemoteReleaseTag(string refs)
    {
        var releases = refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('\t').LastOrDefault() ?? "")
            .Where(name => name.StartsWith("refs/tags/app-v", StringComparison.Ordinal))
            .Select(name => name["refs/tags/".Length..])
            .Select(tag => (Tag: tag, Parsed: Version.TryParse(tag[5..], out var v) ? v : null))
            .Where(x => x.Parsed is not null)
            .OrderByDescending(x => x.Parsed)
            .FirstOrDefault();
        return releases.Tag ?? throw new InvalidOperationException("No official DFO desktop release tag was found.");
    }

    private static async Task WriteSeed(JsonObject seed, string seedPath, string dist, string version)
    {
        var files = new JsonArray();
        foreach (var file in Directory.EnumerateFiles(dist, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(dist, file).Replace('\\', '/');
            await using var stream = File.OpenRead(file);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
            files.Add(new JsonObject { ["path"] = rel, ["size"] = stream.Length, ["sha256"] = hash });
        }
        seed["version"] = version;
        seed["files"] = files;
        await File.WriteAllTextAsync(seedPath, seed.ToJsonString());
    }

    private static string HashFile(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            File.Copy(file, target, true);
        }
    }

    private void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refused to remove a folder outside the companion workspace.");
        Directory.Delete(fullPath, true);
    }
}

