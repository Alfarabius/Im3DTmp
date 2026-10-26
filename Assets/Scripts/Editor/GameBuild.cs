using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class GameBuild
{
    [MenuItem("Game/Build/Windows Release")]
    public static void WindowsRelease() => Build(BuildTarget.StandaloneWindows64, "Build/Windows/Im3DTmp.exe", BuildOptions.None);

    [MenuItem("Game/Build/Windows Development (launcher tests)")]
    public static void WindowsDevelopment() => Build(BuildTarget.StandaloneWindows64, "Build/WindowsDevelopment/Im3DTmp.exe", BuildOptions.Development);

    [MenuItem("Game/Build/WebGL Development")]
    public static void WebGLDevelopment() => Build(BuildTarget.WebGL, "Build/WebGL", BuildOptions.Development);

    private static void Build(BuildTarget target, string output, BuildOptions options)
    {
        output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, output);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length == 0) throw new InvalidOperationException("No enabled build scenes.");

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            target = target,
            locationPathName = output,
            options = options
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Build failed: " + report.summary.result);
    }
}
