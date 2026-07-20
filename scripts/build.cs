#:property PublishAot=false
#:property TargetFramework=net10.0
#:package Spectre.Console@*

using Spectre.Console;
using System.Diagnostics;
using System.Runtime.InteropServices;

// ── Repo root detection ────────────────────────────────────────────────────
string? root = null;
for (var d = Environment.CurrentDirectory; d != null; d = Path.GetDirectoryName(d))
{
    if (Directory.GetFiles(d, "ME221.slnx").Length > 0) { root = d; break; }
}
if (root == null)
{
    AnsiConsole.MarkupLine("[red]✗ Could not find ME221.slnx — run from within the repo.[/]");
    return 1;
}

var svelteApp = Path.Combine(root, "src", "ME221Dashboard", "SvelteApp");
var csproj = Path.Combine(root, "src", "ME221Dashboard", "ME221Dashboard.csproj");
var wwwroot = Path.Combine(root, "src", "ME221Dashboard", "wwwroot");

// ── Parse CLI args (bypass interactive) ────────────────────────────────────
string? forcePlatform = null, forceConfig = null, forceVersion = null;
bool? forceClean = null, forcePublish = null, forceInstall = null;
var extraMsbuild = new List<string>();

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-p" or "--platform" when i + 1 < args.Length: forcePlatform = args[++i]; break;
        case "-c" or "--config"   when i + 1 < args.Length: forceConfig = args[++i]; break;
        case "--version"          when i + 1 < args.Length: forceVersion = args[++i]; break;
        case "--clean":       forceClean = true; break;
        case "--no-clean":    forceClean = false; break;
        case "--publish":     forcePublish = true; break;
        case "--no-publish":  forcePublish = false; break;
        case "--install":     forceInstall = true; break;
        case "--no-install":  forceInstall = false; break;
        case "--msbuild" when i + 1 < args.Length: extraMsbuild.Add(args[++i]); break;
        case "-h" or "--help": PrintHelp(); return 0;
    }
}

// ── Banner ─────────────────────────────────────────────────────────────────
try { AnsiConsole.Clear(); } catch { /* not an interactive terminal */ }
AnsiConsole.Write(new Panel(new Markup("[cyan]ME221 Dashboard Builder[/]"))
    .Border(BoxBorder.Double)
    .BorderStyle(new Style(Color.Cyan))
    .Padding(2, 0)
    .Expand());
AnsiConsole.WriteLine();

// ── Version detection ──────────────────────────────────────────────────────
string displayVersion;
string appVersion;

if (forceVersion != null)
{
    displayVersion = forceVersion.TrimStart('v');
    appVersion = RunGit("rev-list --count HEAD", root).ToString().Trim();
    AnsiConsole.MarkupLine($"[dim]Using explicit version: {displayVersion} (build {appVersion})[/]");
}
else
{
    var tag = RunGit("describe --tags --abbrev=0", root).ToString().Trim();
    displayVersion = tag.TrimStart('v');
    appVersion = RunGit("rev-list --count HEAD", root).ToString().Trim();

    if (string.IsNullOrEmpty(displayVersion) || displayVersion.Contains("fatal"))
    {
        AnsiConsole.MarkupLine("[yellow]⚠ No git tags found — falling back to 1.0.0. Use --version to set explicitly.[/]");
        displayVersion = "1.0.0";
    }
    AnsiConsole.MarkupLine($"[dim]Auto-detected version: {displayVersion} (build {appVersion})[/]");
}
AnsiConsole.WriteLine();

// ── Platform ───────────────────────────────────────────────────────────────
var platforms = new (string Key, string Label, string Tfm, string? Rid)[]
{
    ("android-arm64", "Android  (arm64)",  "net11.0-android37.0", "android-arm64"),
    ("android-armv7", "Android  (armv7)",  "net11.0-android37.0", "android-armv7"),
    ("android-all",   "Android  (all)",    "net11.0-android37.0", null),
    ("windows",       "Windows",           "net11.0-windows10.0.19041.0", null),
    ("ios",           "iOS",               "net11.0-ios", null),
    ("macos",         "macOS  (Catalyst)", "net11.0-maccatalyst", "maccatalyst-arm64"),
};

string platformKey;
string tfm;
string? platformRid;
if (forcePlatform != null)
{
    platformKey = forcePlatform.ToLowerInvariant().Replace(" ", "");
    var match = platforms.FirstOrDefault(p => p.Key == platformKey);
    if (match.Key == null)
    {
        AnsiConsole.MarkupLine($"[red]✗ Unknown platform: {forcePlatform}[/]");
        return 1;
    }
    tfm = match.Tfm;
    platformRid = match.Rid;
}
else
{
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold]Target platform[/]")
            .PageSize(10)
            .AddChoices(platforms.Select(p => $"  {p.Label}  [dim]({p.Key})[/]")));
    var match = platforms.First(p => choice.Contains(p.Key));
    platformKey = match.Key;
    tfm = match.Tfm;
    platformRid = match.Rid;
}

// ── Config ─────────────────────────────────────────────────────────────────
string config;
if (forceConfig != null)
{
    config = forceConfig;
}
else
{
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold]Configuration[/]")
            .AddChoices(["  Release  [dim](optimized, signed)[/]", "  Debug  [dim](symbols, dev)[/]"]));
    config = choice.Contains("Release") ? "Release" : "Debug";
}

// ── Build options ──────────────────────────────────────────────────────────
bool doClean   = forceClean   ?? AnsiConsole.Confirm("[bold]Clean before building?[/]", false);
bool doPublish = forcePublish ?? AnsiConsole.Confirm("[bold]Publish after build?[/]", false);
bool doInstall = false;
if (doPublish)
    doInstall = forceInstall ?? AnsiConsole.Confirm("[bold]Install on device after publish?[/]", false);

// ── Summary ────────────────────────────────────────────────────────────────
AnsiConsole.WriteLine();
AnsiConsole.Write(new Table()
    .Border(TableBorder.Rounded).BorderColor(Color.Gray)
    .AddColumn("[bold]Setting[/]").AddColumn("[bold]Value[/]")
    .AddRow("Platform", $"[cyan]{platforms.First(p => p.Key == platformKey).Label}[/]")
    .AddRow("Target",   $"[dim]{tfm}[/]")
    .AddRow("Config",   $"[cyan]{config}[/]")
    .AddRow("Version",  $"[cyan]{displayVersion}[/] [dim](build {appVersion})[/]")
    .AddRow("Clean",    doClean   ? "[green]Yes[/]" : "[dim]No[/]")
    .AddRow("Publish",  doPublish ? "[green]Yes[/]" : "[dim]No[/]")
    .AddRow("Install",  doInstall ? "[green]Yes[/]" : "[dim]No[/]"));
AnsiConsole.WriteLine();

if (forcePlatform == null && !AnsiConsole.Confirm("[bold]Proceed?[/]", true))
{
    AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
    return 0;
}

// ── Build ──────────────────────────────────────────────────────────────────
int exitCode = 0;

void Fail(ProgressTask t) { t.Increment(t.MaxValue - t.Value); }

await AnsiConsole.Progress()
    .AutoRefresh(false).AutoClear(false)
    .Columns([new SpinnerColumn(Spinner.Known.Dots2), new TaskDescriptionColumn(), new ProgressBarColumn()])
    .StartAsync(async ctx =>
    {
        if (doClean)
        {
            var t = ctx.AddTask("[yellow]Cleaning[/]");
            var dir = Path.Combine(wwwroot, "assets");
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            exitCode = Run("dotnet", $"clean \"{csproj}\" -f {tfm} -c {config}", root);
            if (exitCode != 0) { Fail(t); return; }
            t.Increment(100);
        }

        {
            var t = ctx.AddTask("[cyan]Svelte build[/]");
            var dir = Path.Combine(wwwroot, "assets");
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            exitCode = RunNpm("run build", svelteApp);
            if (exitCode != 0) { Fail(t); return; }
            t.Increment(100);
        }

        {
            var t = ctx.AddTask($"[green].NET build[/] [dim]{tfm} {config}[/]");
            var msbuildArgs = extraMsbuild.Count > 0 ? " " + string.Join(" ", extraMsbuild.Select(m => $"-p:{m}")) : "";
            msbuildArgs += $" -p:ApplicationDisplayVersion={displayVersion} -p:ApplicationVersion={appVersion}";
            exitCode = Run("dotnet", $"build \"{csproj}\" -f {tfm} -c {config}{msbuildArgs}", root);
            if (exitCode != 0) { Fail(t); return; }
            t.Increment(100);
        }

        if (doPublish && exitCode == 0)
        {
            var rids = platformKey == "android-all"
                ? Array.Empty<string>()
                : platformRid != null ? new[] { platformRid } : Array.Empty<string>();

            var msbuildArgs = extraMsbuild.Count > 0 ? " " + string.Join(" ", extraMsbuild.Select(m => $"-p:{m}")) : "";
            msbuildArgs += $" -p:ApplicationDisplayVersion={displayVersion} -p:ApplicationVersion={appVersion}";

            if (rids.Length == 0)
            {
                var t = ctx.AddTask("[magenta]Publish[/]");
                exitCode = Run("dotnet", $"publish \"{csproj}\" -f {tfm} -c Release{msbuildArgs}", root);
                if (exitCode != 0) { Fail(t); return; }
                t.Increment(100);
            }
            else
            {
                foreach (var rid in rids)
                {
                    var t = ctx.AddTask($"[magenta]Publish[/] [dim]{rid}[/]");
                    exitCode = Run("dotnet", $"publish \"{csproj}\" -f {tfm} -c Release -r {rid}{msbuildArgs}", root);
                    if (exitCode != 0) { Fail(t); return; }
                    t.Increment(100);
                }
            }

            if (doInstall && exitCode == 0)
            {
                var t2 = ctx.AddTask("[blue]Install[/]");
                var pubDir = Path.Combine(root, "src", "ME221Dashboard", "bin", "Release", tfm, "publish");
                var apk = Directory.GetFiles(pubDir, "*.apk", SearchOption.AllDirectories).LastOrDefault();
                if (apk != null)
                {
                    exitCode = Run("adb", $"install -r \"{apk}\"", root);
                    if (exitCode != 0) { Fail(t2); return; }
                }
                t2.Increment(100);
            }
        }
    });

AnsiConsole.WriteLine();
AnsiConsole.Write(exitCode == 0
    ? new Panel(new Markup("[green]✓ Build complete[/]")).Border(BoxBorder.Rounded).BorderStyle(new Style(Color.Green)).Expand()
    : new Panel(new Markup($"[red]✗ Build failed (exit {exitCode})[/]")).Border(BoxBorder.Rounded).BorderStyle(new Style(Color.Red)).Expand());
AnsiConsole.WriteLine();
return exitCode;

// ── Helpers ────────────────────────────────────────────────────────────────
static int Run(string exe, string args, string workDir)
{
    var psi = new ProcessStartInfo(exe, args)
    {
        WorkingDirectory = workDir,
        RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
    };
    using var p = Process.Start(psi)!;
    p.OutputDataReceived += (_, e) => { if (e.Data != null) AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(e.Data)}[/]"); };
    p.ErrorDataReceived  += (_, e) => { if (e.Data != null) AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(e.Data)}[/]"); };
    p.BeginOutputReadLine(); p.BeginErrorReadLine();
    p.WaitForExit();
    return p.ExitCode;
}

static int RunNpm(string script, string workDir)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var psi = new ProcessStartInfo("cmd", $"/c npm {script}")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        p.OutputDataReceived += (_, e) => { if (e.Data != null) AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(e.Data)}[/]"); };
        p.ErrorDataReceived  += (_, e) => { if (e.Data != null) AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(e.Data)}[/]"); };
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        p.WaitForExit();
        return p.ExitCode;
    }
    return Run("npm", script, workDir);
}

static string RunGit(string args, string workDir)
{
    var psi = new ProcessStartInfo("git", args)
    {
        WorkingDirectory = workDir,
        RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
    };
    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEnd();
    p.WaitForExit();
    return stdout;
}

static void PrintHelp()
{
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("[bold]ME221 Dashboard Builder[/]");
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("Usage: [cyan]dotnet build.cs[/] [dim][[options]][/]");
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("[bold]Platforms:[/]  android-arm64, android-armv7, android-all, windows, ios, macos");
    AnsiConsole.MarkupLine("[bold]Config:[/]     Release (default), Debug");
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("[yellow]Flags:[/]");
    AnsiConsole.MarkupLine("  [cyan]-p[/], [cyan]--platform[/] NAME    Skip platform prompt");
    AnsiConsole.MarkupLine("  [cyan]-c[/], [cyan]--config[/] NAME      Skip config prompt");
    AnsiConsole.MarkupLine("  [cyan]--version[/] [dim]X.Y.Z[/]          Set app version (default: auto-detect from git tag)");
    AnsiConsole.MarkupLine("  [cyan]--clean[/] / [cyan]--no-clean[/]       Clean before build");
    AnsiConsole.MarkupLine("  [cyan]--publish[/] / [cyan]--no-publish[/]   Publish after build");
    AnsiConsole.MarkupLine("  [cyan]--install[/] / [cyan]--no-install[/]   Install on device after publish");
    AnsiConsole.MarkupLine("  [cyan]--msbuild[/] [dim]KEY=VALUE[/]         Pass MSBuild property to publish");
    AnsiConsole.MarkupLine("  [cyan]-h[/], [cyan]--help[/]               Show this help");
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("[dim]Without -p or -c, you get an interactive menu.[/]");
    AnsiConsole.MarkupLine("");
}
