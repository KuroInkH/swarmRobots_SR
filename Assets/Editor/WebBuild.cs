using UnityEditor;
using UnityEditor.Build.Reporting;

// Builds the web app from the command line (tools/run-simulator.sh runs this):
//   Unity -batchmode -quit -projectPath <swarm> -executeMethod WebBuild.BuildSimulator
// Opens on the Simulator, with the Control screen reachable from it. Output goes to SimPreview/.
public static class WebBuild
{
    const string Scene = "Assets/development/SingleCubeInteraction.unity";

    public static void BuildSimulator()
    {
        var report = Build();
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // Same build from inside the Unity editor (any OS, e.g. Windows without the tools/ scripts):
    // menu Swarm > Build web app. Output goes to SimPreview/ in the project folder.
    [MenuItem("Swarm/Build web app")]
    public static void BuildFromMenu()
    {
        var report = Build();
        bool ok = report.summary.result == BuildResult.Succeeded;
        EditorUtility.DisplayDialog("Build web app",
            ok ? "Done. The web app is in the SimPreview folder of the project."
               : "The build failed (" + report.summary.totalErrors + " errors). See the Console window.",
            "OK");
    }

    static BuildReport Build()
    {
        return BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { CreateSimScene.ScenePath, Scene }, // Simulator first, Control reachable from it
            locationPathName = "SimPreview",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
    }
}
