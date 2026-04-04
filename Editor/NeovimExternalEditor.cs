using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;
using System.IO;
using Microsoft.Unity.VisualStudio.Editor;
using System.Linq;
using System;

namespace NeovimUnity
{
  public class NeovimExternalEditor : IExternalCodeEditor
  {
    [InitializeOnLoadMethod]
    private static void Initialize() => CodeEditor.Register(new NeovimExternalEditor());
    private readonly IGenerator _generator;

    private NeovimExternalEditor()
    {
      var generatorFactoryType = typeof(ProjectGenerationFlag).Assembly.GetType("Microsoft.Unity.VisualStudio.Editor.GeneratorFactory");
      var getInstance = generatorFactoryType.GetMethod("GetInstance");
      _generator = (IGenerator)(getInstance.Invoke(null, new object[] { 1 }));
    }

    private readonly NeovimDiscovery _discovery = new();
    private readonly NeovimTerminal _term = new();

    public CodeEditor.Installation[] Installations => _discovery.FindExecutables();

    public void Initialize(string editorInstallationPath) { }

    private static readonly string[] BlacklistedExtensions = new string[] { "unity" }; 

    public bool OpenProject(string filePath, int line, int column)
    {
      if (!_generator.IsSupportedFile(filePath)) return false;
      _term.Start(filePath, line, column);

      return true;
    }

    public void SyncAll()
    {
      _generator.Sync();
    }

    public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
    {
      _generator.SyncIfNeeded(addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles).Union(importedFiles), importedFiles);
    }

    public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
    {
      installation = default;

      if (Path.GetFileName(editorPath) != "nvim") return false;

      installation = NeovimDiscovery.InstallationInfo(editorPath);
      return true;
    }

    public void OnGUI()
    {
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();

      var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(GetType().Assembly);

      var style = new GUIStyle
      {
        richText = true,
        margin = new RectOffset(0, 4, 0, 0)
      };

      GUILayout.Label($"<size=10><color=grey>{package.displayName} v{package.version} enabled</color></size>", style);
      GUILayout.EndHorizontal();

      var currentPreset = NeovimConfig.CurrentPreset;

      using (new EditorGUILayout.HorizontalScope())
      {
        EditorGUILayout.PrefixLabel("Neovim frontend");

        var presets = NeovimConfig.GetPresets().Select(x => x.Item1).Concat(new[] { "Custom" }).ToArray();


        EditorGUI.BeginChangeCheck();
        var changed = EditorGUILayout.Popup(Array.IndexOf(presets, currentPreset), presets);

        if (EditorGUI.EndChangeCheck() && changed >= 0)
        {
          NeovimConfig.CurrentPreset = presets[changed];
        }
      }

      using (new EditorGUILayout.HorizontalScope())
      {
        EditorGUILayout.PrefixLabel("Neovim launch command");
        var lastEnabled = GUI.enabled;

        GUI.enabled = currentPreset == "Custom";
        var last = NeovimConfig.CurrentTemplate;
        var changed = EditorGUILayout.TextField(last);

        if (last != changed) NeovimConfig.CustomTemplate = changed;

        GUI.enabled = lastEnabled;
      }

      EditorGUILayout.LabelField("Generate .csproj files for:");
      EditorGUI.indentLevel++;
      SettingsButton(ProjectGenerationFlag.Embedded, "Embedded packages", "", _generator);
      SettingsButton(ProjectGenerationFlag.Local, "Local packages", "", _generator);
      SettingsButton(ProjectGenerationFlag.Registry, "Registry packages", "", _generator);
      SettingsButton(ProjectGenerationFlag.Git, "Git packages", "", _generator);
      SettingsButton(ProjectGenerationFlag.BuiltIn, "Built-in packages", "", _generator);
      SettingsButton(ProjectGenerationFlag.LocalTarBall, "Local tarball", "", _generator);
      SettingsButton(ProjectGenerationFlag.Unknown, "Packages from unknown sources", "", _generator);
      SettingsButton(ProjectGenerationFlag.PlayerAssemblies, "Player projects", "For each player project generate an additional csproj with the name 'project-player.csproj'", _generator);
      RegenerateProjectFiles(_generator);
      EditorGUI.indentLevel--;
    }

    private static void SettingsButton(ProjectGenerationFlag preference, string guiMessage, string toolTip, IGenerator generator)
    {
      var prevValue = generator.AssemblyNameProvider.ProjectGenerationFlag.HasFlag(preference);

      var newValue = EditorGUILayout.Toggle(new GUIContent(guiMessage, toolTip), prevValue);
      if (newValue != prevValue)
        generator.AssemblyNameProvider.ToggleProjectGeneration(preference);
    }

    private static void RegenerateProjectFiles(IGenerator generator)
    {
      var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
      rect.width = 252;
      if (GUI.Button(rect, "Regenerate project files"))
      {
        generator.Sync();
      }
    }
  }
}
