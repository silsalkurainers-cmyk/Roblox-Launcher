using System.Diagnostics;
using System.Net.Http;
using System.Security;
using System.Windows.Forms;
using System.Text;

namespace RobloxLauncher;

internal static class Program
{
    private static readonly object LogLock = new();
    private static string LogPath => Path.Combine(AppContext.BaseDirectory, "Roblox-Launcher-error.log");

    [STAThread]
    private static void Main()
    {
        try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Log("FATAL", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
            Application.ThreadException += (_, e) =>
            {
                Log("UI THREAD", e.Exception);
                MessageBox.Show("The launcher encountered an error. Details were saved to:" + Environment.NewLine + "" + LogPath + "" + Environment.NewLine + "" + Environment.NewLine + "" + e.Exception.Message,
                    "Roblox Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            TaskScheduler.UnobservedTaskException += (_, e) => { Log("UNOBSERVED TASK", e.Exception); e.SetObserved(); };
            Log("START", new Exception("Launcher started."));
            ApplicationConfiguration.Initialize();
            Application.Run(new LauncherForm());
        }
        catch (Exception ex)
        {
            Log("STARTUP", ex);
            try { MessageBox.Show("Roblox Launcher could not start. Error log:" + Environment.NewLine + "" + LogPath + "" + Environment.NewLine + "" + Environment.NewLine + "" + ex, "Roblox Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }
    }

    internal static void Log(string stage, Exception ex)
    {
        try
        {
            lock (LogLock)
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {stage}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "Roblox-Launcher-error.log"), $"[{DateTime.Now:O}] {stage}: {ex}{Environment.NewLine}", Encoding.UTF8); } catch { }
        }
    }
}

internal sealed class LauncherForm : Form
{
    private const string PlayerInstallerUrl = "https://setup.rbxcdn.com/RobloxPlayerInstaller.exe";
    private const string StudioInstallerUrl = "https://setup.rbxcdn.com/RobloxStudioInstaller.exe";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private readonly string _root = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    private readonly string _portableRoblox;
    private readonly string _localRoblox = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox");

    private readonly Label _status;
    private readonly Button _playerButton;
    private readonly Button _studioButton;
    private bool _busy;

    public LauncherForm()
    {
        _portableRoblox = Path.Combine(_root, "Roblox");

        Text = "Roblox Launcher";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        ClientSize = new Size(440, 280);
        BackColor = Color.FromArgb(14, 14, 16);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        var heading = new Label
        {
            Text = "ROBLOX LAUNCHER",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 76
        };

        _playerButton = CreateButton("LAUNCH ROBLOX");
        _studioButton = CreateButton("LAUNCH ROBLOX STUDIO");

        _status = new Label
        {
            Text = "Preparing portable Roblox...",
            ForeColor = Color.FromArgb(160, 160, 168),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Bottom,
            Height = 48,
            Padding = new Padding(12, 0, 12, 8)
        };

        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(44, 8, 44, 8),
            BackColor = BackColor
        };
        buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        buttonPanel.Controls.Add(_playerButton, 0, 0);
        buttonPanel.Controls.Add(_studioButton, 0, 1);

        Controls.Add(buttonPanel);
        Controls.Add(_status);
        Controls.Add(heading);

        _playerButton.Click += async (_, _) => await LaunchAsync(Product.Player);
        _studioButton.Click += async (_, _) => await LaunchAsync(Product.Studio);
        Shown += async (_, _) => await PrepareBothAsync();
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 8, 0, 8),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(37, 37, 42),
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
        Cursor = Cursors.Hand,
        UseVisualStyleBackColor = false
    };

    private async Task PrepareBothAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            SetStatus("Setting up the portable folder...");
            await Task.Run(EnsurePortableRedirect);

            await EnsureInstalledAsync(Product.Player);
            await EnsureInstalledAsync(Product.Studio);

            SetStatus("Ready.");
        }
        catch (Exception ex)
        {
            Program.Log("AUTO SETUP", ex);
            SetStatus("Setup needs attention. Click a launcher button to retry.");
            MessageBox.Show(this, ex.Message, "Roblox Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            await Task.Run(EnsurePortableRedirect);
            await EnsureInstalledAsync(product);

            string executable = FindExecutable(product)
                ?? throw new InvalidOperationException($"The {GetProductName(product)} executable was not found after installation.");

            var start = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = false
            };

            if (product == Product.Player)
                start.ArgumentList.Add("roblox-player:1+launchmode:app");

            Process.Start(start);
            SetStatus($"{GetProductName(product)} launched.");
        }
        catch (Exception ex)
        {
            Program.Log($"LAUNCH {GetProductName(product)}", ex);
            SetStatus("Could not launch. Click again to retry.");
            MessageBox.Show(this, ex.Message, "Roblox Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task EnsureInstalledAsync(Product product)
    {
        if (FindExecutable(product) is not null) return;

        string name = product == Product.Player ? "RobloxPlayerInstaller.exe" : "RobloxStudioInstaller.exe";
        string url = product == Product.Player ? PlayerInstallerUrl : StudioInstallerUrl;
        string installerDirectory = Path.Combine(_root, "Installers");
        Directory.CreateDirectory(installerDirectory);
        string installerPath = Path.Combine(installerDirectory, name);

        SetStatus($"Downloading {GetProductName(product)}...");
        using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var output = File.Create(installerPath);
            await response.Content.CopyToAsync(output);
        }

        SetStatus($"Verifying {GetProductName(product)} installer...");
        if (!HasValidRobloxSignature(installerPath))
        {
            TryDelete(installerPath);
            throw new SecurityException($"The downloaded {GetProductName(product)} installer did not have a valid Roblox Corporation signature. It was not run.");
        }

        SetStatus($"Installing {GetProductName(product)}...");
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            WorkingDirectory = installerDirectory,
            UseShellExecute = true
        });

        var deadline = DateTime.UtcNow.AddMinutes(10);
        while (DateTime.UtcNow < deadline)
        {
            if (FindExecutable(product) is not null)
            {
                TryDelete(installerPath);
                return;
            }
            await Task.Delay(2500);
        }

        throw new TimeoutException(
            $"{GetProductName(product)} was not detected after 10 minutes. If its official installer is still open, finish the installation and click the button again.");
    }

    private bool HasValidRobloxSignature(string file)
    {
        string escaped = file.Replace("'", "''");
        string script = "$s=Get-AuthenticodeSignature -LiteralPath '" + escaped +
            "'; if ($s.Status -ne 'Valid' -or $s.SignerCertificate.Subject -notmatch 'Roblox Corporation') { exit 1 }";
        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(script);

        using var process = Process.Start(start);
        if (process is null) return false;
        if (!process.WaitForExit(30000))
        {
            process.Kill(true);
            return false;
        }
        return process.ExitCode == 0;
    }

    private string? FindExecutable(Product product)
    {
        string versions = Path.Combine(_localRoblox, "Versions");
        if (!Directory.Exists(versions)) return null;

        string fileName = product == Product.Player ? "RobloxPlayerBeta.exe" : "RobloxStudioBeta.exe";
        try
        {
            return Directory.GetDirectories(versions)
                .Select(path => Path.Combine(path, fileName))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void EnsurePortableRedirect()
    {
        Directory.CreateDirectory(_root);

        if (File.Exists(_localRoblox))
            throw new IOException($"A file already exists at {_localRoblox}. It must be moved before portable setup can continue.");

        if (Directory.Exists(_localRoblox))
        {
            var existing = new DirectoryInfo(_localRoblox);
            if ((existing.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                string? resolved = existing.ResolveLinkTarget(true)?.FullName;
                if (resolved is not null && PathsEqual(resolved, _portableRoblox)) return;
                throw new IOException("Your existing Roblox folder is already a junction to a different location. The launcher left it unchanged.");
            }

            if (IsRobloxRunning())
                throw new IOException("Close Roblox Player and Roblox Studio before moving the existing installation into the portable folder.");

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

            try { Directory.Delete(backup, true); } catch { }
            return;
        }

        Directory.CreateDirectory(_portableRoblox);
        CreateJunction(_localRoblox, _portableRoblox);
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
            string target = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, target, true);
        }
    }

    private static void CreateJunction(string link, string target)
    {
        var start = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("mklink");
        start.ArgumentList.Add("/J");
        start.ArgumentList.Add(link);
        start.ArgumentList.Add(target);

        using var process = Process.Start(start)
            ?? throw new IOException("Windows could not start the junction command.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new IOException("Could not create the portable Roblox folder junction. " + output + " " + error);
    }

    private static bool IsRobloxRunning()
    {
        string[] names = { "RobloxPlayerBeta", "RobloxStudioBeta", "RobloxPlayerLauncher" };
        return names.Any(name => Process.GetProcessesByName(name).Length > 0);
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar),
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
