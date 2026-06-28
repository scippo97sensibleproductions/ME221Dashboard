using System.Diagnostics;
using System.Text;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard;

public partial class MainPage
{
    private readonly HybridBridgeService _bridge;
    private readonly ILogger<MainPage> _logger;

#if DEBUG
    private Process? _viteProcess;
    private volatile bool _viteReady;
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

#if DEBUG
        AppDomain.CurrentDomain.ProcessExit += (_, _) => StopViteDevServer();
        _ = StartViteDevServerAsync();
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

#if DEBUG
    private async Task StartViteDevServerAsync()
    {
        KillStaleViteProcesses();

        var svelteAppDir = FindSvelteAppDir();
        if (svelteAppDir is null)
        {
            _logger.LogWarning("SvelteApp directory not found");
            return;
        }

#if WINDOWS
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
        _logger.LogInformation("Vite dev server assumed running on {Url} (start manually in {Dir})", ViteBaseUrl, svelteAppDir);
#endif

        using var probeClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        for (int i = 0; i < 15; i++)
        {
            try
            {
                var response = await probeClient.GetAsync(ViteBaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    _viteReady = true;
                    _logger.LogInformation("Vite dev server ready");
                    MainThread.BeginInvokeOnMainThread(() =>
                        _ = hybridWebView.EvaluateJavaScriptAsync("location.reload()"));
                    return;
                }
            }
            catch
            {
                // Not ready yet
            }
            await Task.Delay(500);
        }

        _logger.LogWarning("Vite dev server did not become ready after 7.5s");
    }

    private void OnWebResourceRequested(object? sender, WebViewWebResourceRequestedEventArgs e)
    {
        // Let HybridWebView runtime handle its internal paths natively
        var p = e.Uri.AbsolutePath;
        if (p.StartsWith("/_framework/") || p == "/__hwvInvokeDotNet")
            return;

        e.Handled = true;

        if (!_viteReady)
        {
            var html = "<html><body style='display:flex;justify-content:center;align-items:center;height:100vh;margin:0;font-family:sans-serif;background:#1a1b1e;color:#f8f9fa'><h2>Starting Vite dev server...</h2></body></html>";
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

    private static async Task<Stream?> ProxyToViteAsync(Uri uri)
    {
        var viteUrl = $"{ViteBaseUrl}{uri.PathAndQuery}";
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
            _viteProcess.Kill(entireProcessTree: true);
            _viteProcess.Dispose();
            _viteProcess = null;
            _logger.LogInformation("Vite dev server stopped");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopViteDevServer();
    }
#endif
}
