using System.Diagnostics;
using System.Text;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard;

public partial class MainPage
{
    private readonly HybridBridgeService _bridge;
    private readonly ILogger<MainPage> _logger;
    private volatile bool _shutdownStarted;

#if DEBUG
    private Process? _viteProcess;
    private volatile bool _viteReady;
    private static string? _loadingHtml;
    private static readonly HttpClient HttpClient = new();
    // In Vite dev mode, imported CSS and Svelte files are served as JavaScript modules
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html",
        [".js"] = "application/javascript",
        [".mjs"] = "application/javascript",
        [".ts"] = "application/javascript",
        [".svelte"] = "application/javascript",
        [".css"] = "application/javascript",
        [".json"] = "application/json",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".ico"] = "image/x-icon",
        [".woff2"] = "font/woff2",
        [".map"] = "application/json",
    };

    private static string ViteBaseUrl =>
#if ANDROID
        "http://10.0.2.2:5173";
#else
        "http://localhost:5173";
#endif
#endif

    public MainPage(HybridBridgeService bridge, ILogger<MainPage> logger)
    {
        InitializeComponent();
        _bridge = bridge;
        _logger = logger;

        // Gate before any web content: the SPA needs a WebView that parses
        // modern JS (optional chaining etc.), i.e. Chrome/WebView 100+. Old
        // providers (e.g. Chrome 69 on emulator images) break at parse time.
        CheckWebViewCompatibility();

#if DEBUG
        // Dev: the virtual host serves wwwroot natively (bridge included);
        // dev.html boots the app from the Vite dev server via the proxy.
        hybridWebView.DefaultFile = "dev.html";
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (_shutdownStarted) return;
            _shutdownStarted = true;
            _logger.LogCritical("SHUTDOWN: ProcessExit — disposing bridge before WebView2 COM teardown");
            _bridge.Dispose();
            StopViteDevServer();
            _logger.LogCritical("SHUTDOWN: ProcessExit — done, forcing exit");
            // Force exit to avoid WebView2 COM deadlock on shutdown
            Environment.Exit(0);
        };
        _ = StartViteDevServerAsync();
        _ = LoadLoadingHtmlAsync();
        hybridWebView.WebResourceRequested += OnWebResourceRequested;
#endif

        _bridge.Initialize(hybridWebView);
        hybridWebView.SetInvokeJavaScriptTarget(_bridge);

        _logger.LogInformation("HybridWebView initialized with Svelte app");
    }

    private void OnRawMessageReceived(object? sender, HybridWebViewRawMessageReceivedEventArgs e)
    {
        _bridge.HandleRawMessage(e.Message ?? string.Empty);
    }

    // ─── Native WebView compatibility gate ─────────────────────────────────

#if ANDROID
    private const int MinWebViewMajorVersion = 100;
    private bool _webViewBlocked;
#endif

    private void CheckWebViewCompatibility()
    {
#if ANDROID
        // GetCurrentWebViewPackage() / GetWebViewImplementationPackageName()
        // were removed from the modern Android bindings, so read the provider
        // version from the default WebView user agent ("... Chrome/69.0.3497.100 ...").
        var ua = Android.Webkit.WebSettings.GetDefaultUserAgent(Android.App.Application.Context) ?? string.Empty;
        var match = System.Text.RegularExpressions.Regex.Match(ua, @"Chrome/([\d.]+)");
        int major = 0;
        if (match.Success)
            int.TryParse(match.Groups[1].Value.Split('.')[0], out major);
        var versionName = match.Success ? match.Groups[1].Value : null;

        if (major >= MinWebViewMajorVersion)
            return;

        _webViewBlocked = true;
        WebViewVersionLabel.Text = versionName is null
            ? "unknown — no WebView provider detected"
            : $"{versionName} (Chrome/{major})";
        hybridWebView.IsVisible = false;
        WebViewWarningPanel.IsVisible = true;
        _logger.LogWarning(
            "Blocked app start: Android System WebView '{Version}' (Chrome/{Major}) is older than {Min}. The SPA requires a modern WebView. UA: {UserAgent}",
            versionName ?? "none", major, MinWebViewMajorVersion, ua);
#endif
    }

    private void OnWebViewRetryClicked(object? sender, EventArgs e)
    {
#if ANDROID
        CheckWebViewCompatibility();
        if (_webViewBlocked) return;

        WebViewWarningPanel.IsVisible = false;
        hybridWebView.IsVisible = true;
        _logger.LogInformation("WebView check passed — resuming app load");
        MainThread.BeginInvokeOnMainThread(() =>
            _ = hybridWebView.EvaluateJavaScriptAsync("location.reload()"));
#endif
    }

#if DEBUG
    private async Task StartViteDevServerAsync()
    {
        KillStaleViteProcesses();

#if WINDOWS
        var svelteAppDir = FindSvelteAppDir();
        if (svelteAppDir is null)
        {
            _logger.LogWarning("SvelteApp directory not found");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(svelteAppDir, "node_modules", ".bin", "vite.cmd"),
                Arguments = "--port 5173 --host",
                WorkingDirectory = svelteAppDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // Log Vite's output so we can see errors
            var process = Process.Start(psi)!;
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) _logger.LogDebug("Vite: {Line}", e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) _logger.LogError("Vite: {Line}", e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _viteProcess = process;
            _logger.LogInformation("Vite dev server starting on port 5173");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Vite dev server");
            return;
        }
#else
        _logger.LogInformation("Vite dev server assumed running on {Url} — start it on the host with 'npm run dev' in SvelteApp", ViteBaseUrl);
#endif

        using var probeClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        int attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                var response = await probeClient.GetAsync(ViteBaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    _viteReady = true;
                    _logger.LogInformation("Vite dev server ready (attempt {Attempt})", attempt);
                    MainThread.BeginInvokeOnMainThread(() =>
                        _ = hybridWebView.EvaluateJavaScriptAsync("location.reload()"));
                    return;
                }
            }
            catch
            {
                // Not ready yet — keep probing so the dev server can be started after the app
            }
            if (attempt % 10 == 0)
            {
                _logger.LogWarning("Vite dev server still not reachable at {Url} after {Attempt} attempts — start it on the host with 'npm run dev' in SvelteApp", ViteBaseUrl, attempt);
            }
            await Task.Delay(1500);
        }
    }

    private static async Task LoadLoadingHtmlAsync()
    {
        // The boot screen lives in Resources/Raw/loading.html (packaged as a
        // MauiAsset). Read it once and serve it while the Vite dev server is
        // starting. Falls back to a compact inline page if it can't be read.
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("loading.html");
            using var reader = new StreamReader(stream);
            _loadingHtml = await reader.ReadToEndAsync();
            return;
        }
        catch
        {
            // Unpackaged host — fall through to the output directory
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "loading.html");
            if (File.Exists(path))
            {
                _loadingHtml = await File.ReadAllTextAsync(path);
            }
        }
        catch
        {
            // Keep the inline fallback
        }
    }

    private const string FallbackLoadingHtml = "<html><body style='display:flex;justify-content:center;align-items:center;height:100vh;margin:0;font-family:sans-serif;background:#1a1b1e;color:#f8f9fa'><div style='text-align:center'><h2>Starting Vite dev server...</h2><p style='color:#9aa0a6;font-size:14px'>Start it on the host: <code style='background:#333;padding:2px 6px;border-radius:4px'>npm run dev</code> in <code style='background:#333;padding:2px 6px;border-radius:4px'>src/ME221Dashboard/SvelteApp</code></p></div></body></html>";

    private void OnWebResourceRequested(object? sender, WebViewWebResourceRequestedEventArgs e)
    {
        try
        {
            // Let HybridWebView runtime handle its internal paths natively
            var p = e.Uri.AbsolutePath;
            if (p.StartsWith("/_framework/") || p == "/__hwvInvokeDotNet")
            {
                return;
            }

            e.Handled = true;

            // Status endpoint for the loading page's poll — 200 once Vite is
            // reachable, so the page does a single reload into the real app.
            if (p == "/__viteReady")
            {
                var bytes = Encoding.UTF8.GetBytes(_viteReady ? "ready" : "waiting");
                e.SetResponse(_viteReady ? 200 : 503, _viteReady ? "OK" : "Service Unavailable",
                    "text/plain", Task.FromResult<Stream?>(new MemoryStream(bytes)));
                return;
            }

            if (!_viteReady)
            {
                // The first request usually beats the probe and the _loadingHtml
                // read, and EvaluateJavaScriptAsync("location.reload()") fails
                // silently while the page is still loading — so the page itself
                // drives the transition:
                //  • the plain fallback reloads once (2s) to hand over to the
                //    pretty boot screen (or straight to the app if Vite is up);
                //  • the pretty screen polls /__viteReady and reloads exactly
                //    once when the dev server answers — no reload loop, so the
                //    boot animations never restart while waiting.
                var isPretty = _loadingHtml is not null;
                var html = _loadingHtml ?? FallbackLoadingHtml;
                const string pollScript =
                    "<script>(function(){function p(){fetch('/__viteReady',{cache:'no-store'})" +
                    ".then(function(r){if(r.status===200){location.reload();return;}setTimeout(p,1000);})" +
                    ".catch(function(){setTimeout(p,1000);});}setTimeout(p,500);})();</script>";
                const string swapScript = "<script>setTimeout(function(){location.reload()},2000)</script>";
                var script = isPretty ? pollScript : swapScript;
                html = html.Contains("</body>", StringComparison.OrdinalIgnoreCase)
                    ? html.Replace("</body>", script + "</body>", StringComparison.OrdinalIgnoreCase)
                    : html + script;
                var bytes = Encoding.UTF8.GetBytes(html);
                e.SetResponse(200, "OK", "text/html", Task.FromResult<Stream?>(new MemoryStream(bytes)));
                return;
            }

            var ext2 = Path.GetExtension(e.Uri.AbsolutePath);
            string ct2;
            if (string.IsNullOrEmpty(ext2))
                ct2 = e.Uri.AbsolutePath.StartsWith("/@") ? "application/javascript" : "text/html";
            else
                ct2 = MimeTypes.TryGetValue(ext2, out var mime2) ? mime2 : "application/octet-stream";
            e.SetResponse(200, "OK", ct2, ProxyToViteAsync(e.Uri));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnWebResourceRequested EXCEPTION for {Uri}", e.Uri);
            System.Diagnostics.Debug.WriteLine($"[MainPage] EXCEPTION: {ex}");
        }
    }

    private static async Task<Stream?> ProxyToViteAsync(Uri uri)
    {
        // The HybridWebView navigates to its virtual host root (DefaultFile);
        // map that path to the Vite SPA root, which is the only entry Vite serves.
        var pathAndQuery = uri.PathAndQuery;
        if (pathAndQuery == "/dev.html")
            pathAndQuery = "/";
        var viteUrl = $"{ViteBaseUrl}{pathAndQuery}";
        try
        {
            var response = await HttpClient.GetAsync(viteUrl);
            return await response.Content.ReadAsStreamAsync();
        }
        catch
        {
            var error = Encoding.UTF8.GetBytes($"<html><body style=\"font-family:sans-serif;background:#1a1b1e;color:#f8f9fa;padding:2rem\"><h2>Vite proxy error</h2><p>Could not reach {uri}</p></body></html>");
            return new MemoryStream(error);
        }
    }

    private static string? FindSvelteAppDir()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            if (dir.Contains("\\bin\\") || dir.Contains("\\obj\\"))
            {
                var p = Directory.GetParent(dir);
                if (p is null) break;
                dir = p.FullName;
                continue;
            }
            if (Directory.Exists(Path.Combine(dir, "SvelteApp")))
                return Path.Combine(dir, "SvelteApp");
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        return null;
    }

    private void KillStaleViteProcesses()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            using var reader = new StringReader(output);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (!line.Contains(":5173") || !line.Contains("LISTENING"))
                    continue;

                var lastSpace = line.LastIndexOf(' ');
                if (lastSpace < 0 || !int.TryParse(line.AsSpan()[(lastSpace + 1)..], out var pid))
                    continue;

                try
                {
                    using var target = Process.GetProcessById(pid);
                    if (target.ProcessName.Equals("node", StringComparison.OrdinalIgnoreCase))
                    {
                        target.Kill(entireProcessTree: true);
                        _logger.LogWarning("Killed stale Vite process (PID {Pid})", pid);
                    }
                }
                catch
                {
                    // Already exited
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check for stale Vite processes");
        }
    }

    private void StopViteDevServer()
    {
        if (_viteProcess is not null && !_viteProcess.HasExited)
        {
            _logger.LogCritical("SHUTDOWN: Killing Vite dev server PID={Pid}", _viteProcess.Id);
            _viteProcess.Kill(entireProcessTree: true);
            _viteProcess.Dispose();
            _viteProcess = null;
            _logger.LogCritical("SHUTDOWN: Vite dev server stopped");
        }
    }
#endif

    protected override void OnDisappearing()
    {
        _logger.LogCritical("SHUTDOWN: MainPage.OnDisappearing");
        base.OnDisappearing();
#if DEBUG
        StopViteDevServer();
#endif
        // Dispose bridge before WebView2 native COM teardown can deadlock the process.
        // The DI container dispose runs too late — WebView2 COM blocks it.
        if (!_shutdownStarted)
        {
            _shutdownStarted = true;
            _logger.LogCritical("SHUTDOWN: Disposing bridge service");
            _bridge.Dispose();
        }
    }
}
