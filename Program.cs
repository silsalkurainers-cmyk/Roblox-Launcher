using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Windows.Forms;

namespace RobloxLauncher;

internal static class Program
{
    internal const string BuildId = "20261120-startup-trace-02";
    private static readonly object LogLock = new();
    private static string TempLogPath => Path.Combine(Path.GetTempPath(), $"Roblox-Launcher-{BuildId}.log");
    private static string AppLogPath => Path.Combine(AppContext.BaseDirectory, $"Roblox-Launcher-{BuildId}.log");

    [STAThread]
    private static int Main(string[] args)
    {
        WriteLog("BUILD", $"BUILD-ID={BuildId}; executable={Environment.ProcessPath}; assembly={typeof(Program).Assembly.Location}");
        WriteLog("ENTRY", $"Process started. Windows={Environment.OSVersion}; 64-bit={Environment.Is64BitProcess}; BaseDirectory={AppContext.BaseDirectory}");
        AppDomain.CurrentDomain.FirstChanceException += (_, e) => WriteLog("FIRST CHANCE EXCEPTION", e.Exception.ToString());
        AppDomain.CurrentDomain.ProcessExit += (_, _) => WriteLog("PROCESS EXIT", "The operating system is terminating the launcher process.");
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAccessibilityAssembly;
        LogAccessibilityFile();

        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                _ = Assembly.Load(new AssemblyName("Accessibility, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));
                WriteLog("SELF-TEST STAGE", "Accessibility assembly load starting.");
                ApplicationConfiguration.Initialize();
                WriteLog("SELF-TEST STAGE", "WinForms configuration completed.");
                using var testForm = new LauncherForm();
                WriteLog("SELF-TEST", "WinForms initialization and main window construction succeeded.");
                return 0;
            }
            catch (Exception ex)
            {
                WriteLog("SELF-TEST FAILED", ex.ToString());
                return 1;
            }
        }

        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                WriteLog("UI THREAD ERROR", e.Exception.ToString());
                ShowFatalError(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                WriteLog("UNHANDLED ERROR", e.ExceptionObject?.ToString() ?? "Unknown fatal exception");
                if (e.ExceptionObject is Exception ex) ShowFatalError(ex);
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                WriteLog("UNOBSERVED TASK ERROR", e.Exception.ToString());
                e.SetObserved();
            };

            WriteLog("STAGE", "Entering ApplicationConfiguration.Initialize.");
            ApplicationConfiguration.Initialize();
            WriteLog("WINFORMS", "ApplicationConfiguration.Initialize completed.");

            WriteLog("STAGE", "Entering LauncherForm constructor.");
            using var form = new LauncherForm();
            WriteLog("FORM", "Main window constructed; starting message loop.");
            Application.Run(form);
            WriteLog("EXIT", "Windows Forms message loop ended normally.");
            return 0;
        }
        catch (Exception ex)
        {
            WriteLog("FATAL STARTUP ERROR", ex.ToString());
            ShowFatalError(ex);
            return 1;
        }
    }

    private static void LogAccessibilityFile()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Accessibility.dll");
        WriteLog("DEPENDENCY CHECK", $"Accessibility.dll exists={File.Exists(path)}; path={path}");
    }

    private static Assembly? ResolveAccessibilityAssembly(object? sender, ResolveEventArgs args)
    {
        AssemblyName requested;
        try { requested = new AssemblyName(args.Name); }
        catch { return null; }

        if (!string.Equals(requested.Name, "Accessibility", StringComparison.OrdinalIgnoreCase))
            return null;

        string path = Path.Combine(AppContext.BaseDirectory, "Accessibility.dll");
        WriteLog("ASSEMBLY RESOLVE", $"Requested={args.Name}; candidate={path}; exists={File.Exists(path)}");
        if (!File.Exists(path)) return null;

        try
        {
            Assembly loaded = Assembly.LoadFrom(path);
            WriteLog("ASSEMBLY RESOLVE", $"Loaded Accessibility from {path}; identity={loaded.FullName}");
            return loaded;
        }
        catch (Exception ex)
        {
            WriteLog("ASSEMBLY RESOLVE FAILED", ex.ToString());
            return null;
        }
    }

    internal static void WriteLog(string stage, string message)
    {
        string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {stage}: {message}{Environment.NewLine}";
        lock (LogLock)
        {
            try { File.AppendAllText(TempLogPath, entry, Encoding.UTF8); } catch { }
            try { File.AppendAllText(AppLogPath, entry, Encoding.UTF8); } catch { }
        }
    }

    private static void ShowFatalError(Exception ex)
    {
        try
        {
            MessageBox.Show(
                $"Roblox Launcher could not continue.{Environment.NewLine}{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}Log file:{Environment.NewLine}{TempLogPath}",
                "Roblox Launcher - startup error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch { }
    }
}

internal sealed class LauncherForm : Form
{
    private const string PlayerInstallerUrl = "https://setup.rbxcdn.com/RobloxPlayerInstaller.exe";
    private const string StudioInstallerUrl = "https://setup.rbxcdn.com/RobloxStudioInstaller.exe";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(8) };

    private readonly string _root = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    private readonly string _portableRoblox;
    private readonly string _localRoblox = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox");

    private readonly Label _status;
    private readonly Button _playerButton;
    private readonly Button _studioButton;
    private bool _busy;
    private bool _setupStarted;

    public LauncherForm()
    {
        Program.WriteLog("FORM CONSTRUCTOR", "Entered LauncherForm constructor.");
        _portableRoblox = Path.Combine(_root, "Roblox");

        Text = $"Roblox Launcher [{Program.BuildId}]";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        ClientSize = new Size(460, 290);
        BackColor = Color.FromArgb(14, 14, 16);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        ShowInTaskbar = true;

        var heading = new Label
        {
            Text = "ROBLOX LAUNCHER",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 78
        };

        _playerButton = CreateButton("LAUNCH ROBLOX");
        _studioButton = CreateButton("LAUNCH ROBLOX STUDIO");

        _status = new Label
        {
            Text = "Window opened. Checking installation...",
            ForeColor = Color.FromArgb(170, 170, 178),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Bottom,
            Height = 52,
            Padding = new Padding(12, 0, 12, 8)
        };

        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(42, 8, 42, 8),
            BackColor = BackColor
        };
        buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        buttonPanel.Controls.Add(_playerButton, 0, 0);
        buttonPanel.Controls.Add(_studioButton, 0, 1);

        Program.WriteLog("FORM STAGE", "Controls created; adding controls to form.");
        Controls.Add(buttonPanel);
        Controls.Add(_status);
        Controls.Add(heading);
        Program.WriteLog("FORM STAGE", $"Controls added. Count={Controls.Count}; client={ClientSize}.");

        _playerButton.Click += async (_, _) => await LaunchAsync(Product.Player);
        _studioButton.Click += async (_, _) => await LaunchAsync(Product.Studio);

        Shown += (_, _) =>
        {
            Program.WriteLog("WINDOW SHOWN", "The main launcher window is visible.");
            if (!_setupStarted)
            {
                _setupStarted = true;
                BeginInvoke(new Action(async () => await PrepareBothAsync()));
            }
        };

        Program.WriteLog("FORM CONSTRUCTOR", "Constructor completed successfully.");
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 8, 0, 8),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(38, 38, 44),
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
        Cursor = Cursors.Hand,
        UseVisualStyleBackColor = false
    };

    private async Task PrepareBothAsync()
    {
        if (_busy) return;
        SetBusy(true);

        try
        {
            Program.WriteLog("SETUP", "Automatic first-run setup started.");
            SetStatus("Preparing portable Roblox folder...");
            await Task.Run(EnsurePortableRedirect);

            await EnsureInstalledAsync(Product.Player);
            await EnsureInstalledAsync(Product.Studio);

            SetStatus("Ready. Select Roblox or Roblox Studio.");
            Program.WriteLog("SETUP", "Both products were detected; setup completed.");
        }
        catch (Exception ex)
        {
            Program.WriteLog("SETUP FAILED", ex.ToString());
            SetStatus("Setup did not finish. Check the startup log.");
            MessageBox.Show(this,
                $"{ex.Message}{Environment.NewLine}{Environment.NewLine}Log: {Path.Combine(Path.GetTempPath(), "Roblox-Launcher-startup.log")}",
                "Roblox Launcher - setup error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LaunchAsync(Product product)
    {
        if (_busy) return;
        SetBusy(true);

        try
        {
            Program.WriteLog("LAUNCH", $"Requested {GetProductName(product)}.");
            SetStatus("Checking portable folder...");
            await Task.Run(EnsurePortableRedirect);
            await EnsureInstalledAsync(product);

            string executable = FindExecutable(product)
                ?? throw new FileNotFoundException($"The {GetProductName(product)} executable was not found after installation.");

            Program.WriteLog("LAUNCH", $"Starting executable: {executable}");
            var start = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = true
            };

            using var process = Process.Start(start);
            if (process is null)
                throw new InvalidOperationException($"Windows did not create a process for {GetProductName(product)}.");

            SetStatus($"{GetProductName(product)} launch request sent.");
            Program.WriteLog("LAUNCH", $"Process created. PID={process.Id}.");
        }
        catch (Exception ex)
        {
            Program.WriteLog($"LAUNCH FAILED - {GetProductName(product)}", ex.ToString());
            SetStatus("Could not launch. See the startup log.");
            MessageBox.Show(this,
                $"{ex.Message}{Environment.NewLine}{Environment.NewLine}Log: {Path.Combine(Path.GetTempPath(), "Roblox-Launcher-startup.log")}",
                "Roblox Launcher - launch error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task EnsureInstalledAsync(Product product)
    {
        string? installed = FindExecutable(product);
        if (installed is not null)
        {
            Program.WriteLog("INSTALL CHECK", $"{GetProductName(product)} already installed at {installed}");
            return;
        }

        string installerName = product == Product.Player ? "RobloxPlayerInstaller.exe" : "RobloxStudioInstaller.exe";
        string downloadUrl = product == Product.Player ? PlayerInstallerUrl : StudioInstallerUrl;
        string installerDirectory = Path.Combine(_root, "Installers");
        Directory.CreateDirectory(installerDirectory);
        string installerPath = Path.Combine(installerDirectory, installerName);

        Program.WriteLog("DOWNLOAD", $"Downloading {GetProductName(product)} from official Roblox CDN.");
        SetStatus($"Downloading {GetProductName(product)}...");
        using (var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var output = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(output);
        }

        var installerInfo = new FileInfo(installerPath);
        Program.WriteLog("DOWNLOAD", $"Downloaded {installerInfo.Length} bytes to {installerPath}");
        if (installerInfo.Length < 100_000)
            throw new InvalidDataException($"The downloaded {GetProductName(product)} installer is unexpectedly small.");

        SetStatus($"Verifying {GetProductName(product)} installer...");
        if (!HasValidRobloxSignature(installerPath))
        {
            TryDelete(installerPath);
            throw new SecurityException($"The downloaded {GetProductName(product)} installer does not have a valid Roblox Corporation signature. It was not run.");
        }

        Program.WriteLog("INSTALL", $"Authenticode signature verified for {installerPath}");
        SetStatus($"Opening {GetProductName(product)} installer...");
        using var installer = Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            WorkingDirectory = installerDirectory,
            UseShellExecute = true
        });

        if (installer is null)
            throw new InvalidOperationException($"Windows could not start the {GetProductName(product)} installer.");

        Program.WriteLog("INSTALL", $"Installer process started for {GetProductName(product)}. PID={installer.Id}");

        var deadline = DateTime.UtcNow.AddMinutes(8);
        var nextStatus = DateTime.UtcNow;
        while (DateTime.UtcNow < deadline)
        {
            installed = FindExecutable(product);
            if (installed is not null)
            {
                Program.WriteLog("INSTALL", $"{GetProductName(product)} detected at {installed}");
                TryDelete(installerPath);
                return;
            }

            if (DateTime.UtcNow >= nextStatus)
            {
                SetStatus($"Waiting for {GetProductName(product)} installer...");
                nextStatus = DateTime.UtcNow.AddSeconds(15);
            }

            await Task.Delay(2000);
        }

        throw new TimeoutException(
            $"{GetProductName(product)} was not detected after 8 minutes. Check whether its official installer is still open, then click its button again.");
    }

    private static bool HasValidRobloxSignature(string file)
    {
        string escaped = file.Replace("'", "''");
        string script = "$s=Get-AuthenticodeSignature -LiteralPath '" + escaped +
            "'; if ($s.Status -ne 'Valid' -or $s.SignerCertificate.Subject -notmatch 'Roblox Corporation') { exit 1 }";

        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(script);

        using var process = Process.Start(start);
        if (process is null) return false;
        if (!process.WaitForExit(30_000))
        {
            try { process.Kill(true); } catch { }
            return false;
        }
        return process.ExitCode == 0;
    }

    private string? FindExecutable(Product product)
    {
        string versions = Path.Combine(_localRoblox, "Versions");
        if (!Directory.Exists(versions)) return null;

        string[] names = product == Product.Player
            ? new[] { "RobloxPlayerLauncher.exe", "RobloxPlayerBeta.exe" }
            : new[] { "RobloxStudioBeta.exe", "RobloxStudioLauncherBeta.exe" };

        try
        {
            var candidates = Directory.GetDirectories(versions)
                .SelectMany(directory => names.Select(name => Path.Combine(directory, name)))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc);

            return candidates.FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Program.WriteLog("FIND EXECUTABLE", ex.ToString());
            return null;
        }
    }

    private void EnsurePortableRedirect()
    {
        Program.WriteLog("PORTABLE", $"Preparing link {_localRoblox} -> {_portableRoblox}");
        Directory.CreateDirectory(_root);

        if (File.Exists(_localRoblox))
            throw new IOException($"A file exists at {_localRoblox}; it cannot be used as the Roblox data directory.");

        if (Directory.Exists(_localRoblox))
        {
            var existing = new DirectoryInfo(_localRoblox);
            if ((existing.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                string? resolved = existing.ResolveLinkTarget(true)?.FullName;
                if (resolved is not null && PathsEqual(resolved, _portableRoblox))
                {
                    Directory.CreateDirectory(_portableRoblox);
                    Program.WriteLog("PORTABLE", "Existing junction already points to the portable data folder.");
                    return;
                }

                throw new IOException("The standard Roblox folder is already a junction to a different location. The launcher left it unchanged.");
            }

            if (IsRobloxRunning())
                throw new IOException("Close Roblox Player and Roblox Studio before the launcher moves the existing Roblox folder.");

            Directory.CreateDirectory(_portableRoblox);
            CopyDirectoryContents(_localRoblox, _portableRoblox);

            string backup = _localRoblox + ".portable-backup-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            Directory.Move(_localRoblox, backup);
            try
            {
                CreateJunction(_localRoblox, _portableRoblox);
            }
            catch
            {
                if (!Directory.Exists(_localRoblox)) Directory.Move(backup, _localRoblox);
                throw;
            }

            try { Directory.Delete(backup, true); }
            catch (Exception ex) { Program.WriteLog("PORTABLE BACKUP CLEANUP", ex.ToString()); }

            Program.WriteLog("PORTABLE", "Existing Roblox data migrated and junction created.");
            return;
        }

        Directory.CreateDirectory(_portableRoblox);
        CreateJunction(_localRoblox, _portableRoblox);
        Program.WriteLog("PORTABLE", "New portable Roblox data folder and junction created.");
    }

    private static void CopyDirectoryContents(string source, string destination)
    {
        foreach (string directory in Directory.GetDirectories(source))
        {
            var info = new DirectoryInfo(directory);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;

            string target = Path.Combine(destination, info.Name);
            Directory.CreateDirectory(target);
            CopyDirectoryContents(directory, target);
        }

        foreach (string file in Directory.GetFiles(source))
        {
            var info = new FileInfo(file);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            File.Copy(file, Path.Combine(destination, info.Name), true);
        }
    }

    private static void CreateJunction(string link, string target)
    {
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("mklink");
        start.ArgumentList.Add("/J");
        start.ArgumentList.Add(link);
        start.ArgumentList.Add(target);

        using var process = Process.Start(start)
            ?? throw new IOException("Windows could not start the command needed to create the portable folder junction.");

        if (!process.WaitForExit(30_000))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("Windows timed out while creating the portable Roblox folder junction.");
        }

        if (process.ExitCode != 0)
            throw new IOException($"Windows could not create the portable Roblox folder junction (exit code {process.ExitCode}).");
    }

    private static bool IsRobloxRunning()
    {
        string[] names = { "RobloxPlayerBeta", "RobloxPlayerLauncher", "RobloxStudioBeta", "RobloxStudioLauncherBeta" };
        return names.Any(name => Process.GetProcessesByName(name).Length > 0);
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static string GetProductName(Product product) =>
        product == Product.Player ? "Roblox Player" : "Roblox Studio";

    private void SetStatus(string message) => _status.Text = message;

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _playerButton.Enabled = !busy;
        _studioButton.Enabled = !busy;
    }

    private enum Product
    {
        Player,
        Studio
    }
}
