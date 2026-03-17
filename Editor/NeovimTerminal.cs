using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Unity.CodeEditor;

namespace NeovimUnity
{
  public class NeovimTerminal
  {
    private Process _process;
    private bool Running => _process != null && !_process.HasExited;
    private string _baseTempPath;

    private string GetPidPath() => GetBasePath() + ".pid";
    private string GetSocketPath() => GetBasePath() + ".pipe";

    public void Start(string path, int line, int column)
    {
      if (!Running && File.Exists(GetPidPath()))
      {
        var pid = int.Parse(File.ReadAllText(GetPidPath()));

        try
        {
          _process = Process.GetProcessById(pid);
        }
        catch (ArgumentException) { }
      }

      if (Running) SendIPC(path, line, column);
      else StartEditor(path, line, column);
    }

    private string GetBasePath()
    {
      if (_baseTempPath != null) return _baseTempPath;
      string hash;

      {
        using var sha256 = new SHA256Managed();
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(Directory.GetCurrentDirectory()));

        hash = BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", "")[..16];
      }

      return _baseTempPath = Path.Combine(Path.GetTempPath(), "nvim-" + hash);
    }

    public static string EscapeQuote(string source)
    {
      return source.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\\n");
    }

    public static string EscapeShellPath(string path)
    {
      return EscapeQuote(path.Replace("$", "\\$"));
    }

    private void StartEditor(string path, int line, int column)
    {
      var editorPath = CodeEditor.CurrentEditorPath;
      var pidPath = GetPidPath();

      var luaCode = new List<string>
      {
        $"local f=io.open(\"{EscapeShellPath(pidPath)}\", 'w') if f then f:write(vim.fn.getpid()) f:close() end",
        $"vim.api.nvim_set_current_dir(\"{EscapeShellPath(Directory.GetCurrentDirectory())}\")"
      };

      if (!string.IsNullOrEmpty(path))
      {
        luaCode.Add($"vim.cmd(\"edit {EscapeShellPath(path)}\")");
      }

      if (line >= 0)
      {
        luaCode.Add($"vim.cmd(\"call cursor({line}, {Math.Max(column, 0)})\")");
      }

      var socketPath = GetSocketPath();

      var args = $"--listen \"{EscapeShellPath(socketPath)}\" -c \"lua {EscapeQuote(string.Join(";", luaCode))}\"";

      var template = NeovimConfig.CurrentTemplate.Replace("$(Nvim)", editorPath).Replace("$(Args)", args);

      if (template.Contains("$(NvimScript)"))
      {
        var tmpPath = Path.GetTempFileName();
        File.WriteAllText(tmpPath, $"\"{editorPath}\" {args}");
        template = template.Replace("$(NvimScript)", tmpPath);
      }

      var startInfo = new ProcessStartInfo
      {
        FileName = "bash",
        Arguments = $"-l -c \"{EscapeQuote(template)}\"",
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = Directory.GetCurrentDirectory(),
      };
      UnityEngine.Debug.Log(startInfo.Arguments);

      if (File.Exists(socketPath))
        File.Delete(socketPath);

      var proc = Process.Start(startInfo);

      proc.Exited += (_, _) =>
      {
        if (proc.ExitCode != 0) UnityEngine.Debug.LogError($"neovim process exited with code {_process.ExitCode}");
      };
    }

    private void SendIPC(string path, int line, int col)
    {
      var editorPath = CodeEditor.CurrentEditorPath;

      var cursorExpr = $"| call cursor({line}, {Math.Max(0, col)})";

      var remoteExpr = $"v:lua.vim.api.nvim_command('edit {path.Replace("|", "\\|")} {(line >= 0 ? cursorExpr : "")}')";

      var startInfo = new ProcessStartInfo
      {
        FileName = "sh",
        Arguments = $"-c \"$NVIM --server \\\"$SERVER_PATH\\\" --remote-expr \\\"$NVIM_EXPR\\\"\"",
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = Directory.GetCurrentDirectory(),
      };

      startInfo.EnvironmentVariables["NVIM"] = editorPath;
      startInfo.EnvironmentVariables["SERVER_PATH"] = GetSocketPath();
      startInfo.EnvironmentVariables["NVIM_EXPR"] = remoteExpr;

      Process.Start(startInfo);

    }
  }
}
