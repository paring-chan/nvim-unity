using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace NeovimUnity
{
  public static class NeovimConfig
  {
    private static List<(string, string)> _presets;

    public static string CurrentPreset
    {
      get
      {
        var presets = GetPresets();

        var pref = EditorPrefs.GetString("unity-nvim.preset");
        if (pref == "Custom") return pref;

        var fallback = presets.Count > 0 ? presets[0].Item1 : "Custom";

        if (string.IsNullOrEmpty(pref)) return fallback;
        if (!presets.Any(x => x.Item1 == pref)) return fallback;
        return pref;
      }
      set => EditorPrefs.SetString("unity-nvim.preset", value);
    }

    public static string CustomTemplate
    {
      get => EditorPrefs.GetString("unity-nvim.custom-template");
      set => EditorPrefs.SetString("unity-nvim.custom-template", value);
    }

    public static string CurrentTemplate
    {
      get
      {
        if (CurrentPreset == "Custom")
        {
          return CustomTemplate;
        }

        var presets = GetPresets();
        var presetTemplate = presets.Where(x => x.Item1 == CurrentPreset).Select(x => x.Item2).FirstOrDefault();
        if (presetTemplate != null) return presetTemplate;

        if (presets.Count > 0) return presets[0].Item2;
        return string.Empty;
      }
    }

    public static List<(string, string)> GetPresets()
    {
      if (_presets != null) return _presets;

      var result = new List<(string, string)>();

      if (NeovimDiscovery.CheckExecutable("neovide", out _)) result.Add(("Neovide", "neovide --neovim-bin \"$(Nvim)\" -- $(Args)"));

      var iTermPath = "/Applications/iTerm.app/Contents/MacOS/iTerm2";
      if (File.Exists(iTermPath)) result.Add(("iTerm", $"osascript -e \"{NeovimTerminal.EscapeQuote($"tell application \"iTerm2\" to create window with default profile command \"sh $(NvimScript)\"")}\""));

      _presets = result;
      return result;
    }
  }
}
