using Unity.CodeEditor;
using System.Collections.Generic;
using System.Diagnostics;

namespace NeovimUnity
{
  public class NeovimDiscovery
  {
    private bool _initialized;
    private CodeEditor.Installation[] _foundExecutables;

    public static CodeEditor.Installation InstallationInfo(string nvimPath)
    {
      return new CodeEditor.Installation
      {
        Name = $"Neovim ({nvimPath.Replace("/", "  ̸")})",
        Path = nvimPath,
      };
    }

    public CodeEditor.Installation[] FindExecutables()
    {
#if UNITY_EDITOR_WIN
      return new CodeEditor.Installation[0];
#endif
      if (_initialized) return _foundExecutables;
      var results = new List<CodeEditor.Installation>();

      if (CheckExecutable("nvim", out var nvimPath))
      {
        results.Add(InstallationInfo(nvimPath));
      }

      _foundExecutables = results.ToArray();
      _initialized = true;

      return _foundExecutables;
    }

    public static bool CheckExecutable(string name, out string nvimPath)
    {
      nvimPath = default;

      var startInfo = new ProcessStartInfo
      {
        FileName = "sh",
        Arguments = $"-l -c \"which {name}\"",
        WindowStyle = ProcessWindowStyle.Hidden,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
      };

      var process = Process.Start(startInfo);
      process.WaitForExit();

      var exitCode = process.ExitCode;

      if (exitCode != 0)
      {
        return false;
      }

      nvimPath = process.StandardOutput.ReadToEnd().Trim();

      return true;
    }
  }
}
