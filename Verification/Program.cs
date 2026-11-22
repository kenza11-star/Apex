using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using ApexUrpLab;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

string root = args.Length == 1 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string resources = Path.Combine(root, "Assets", "Resources", "APEX");
int passed = 0;
void Pass(bool condition, string result)
{
    if (!condition) throw new InvalidOperationException(result);
    Console.WriteLine("PASS " + result);
    passed++;
}

var sources = Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.cs", SearchOption.AllDirectories).ToArray();
var syntax = sources.SelectMany(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path),
    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp9), path).GetDiagnostics())
    .Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
Pass(syntax.Length == 0, $"{sources.Length} Unity C# sources parsed by Roslyn in C# 9 mode" +
    (syntax.Length != 0 ? string.Join(Environment.NewLine, syntax.Select(x => x.ToString())) : ""));

using var registry = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "Verification", "assets.json")));
int hashes = 0;
foreach (JsonProperty asset in registry.RootElement.EnumerateObject())
{
    string folder = asset.Name.EndsWith(".glb", StringComparison.Ordinal) ? Path.Combine(root, "SourceMeshes") : resources;
    byte[] bytes = File.ReadAllBytes(Path.Combine(folder, asset.Name));
    string actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    if (actual != asset.Value.GetProperty("sha256").GetString())
        throw new InvalidDataException("Unexpected asset digest " + asset.Name);
    hashes++;
}
Pass(hashes == 19, "19 resource hashes match the anchored migration inputs");

var forest = LapCourse.Decode(File.ReadAllBytes(Path.Combine(resources, "ForestRoute.bytes")));
var marina = LapCourse.Decode(File.ReadAllBytes(Path.Combine(resources, "MarinaRoute.bytes")));
Pass(forest.Points.Length == 1050 && MathF.Abs(forest.Length - 2300f) < 0.5f,
    $"Forest Ridge is a {forest.Length:F2} m loop with 1,050 points");
Pass(marina.Points.Length == 571 && marina.Length > 1_000f, $"Marina is a {marina.Length:F2} m loop with 571 points");
Pass(Vector3.Distance(forest.At(-4).Position, forest.At(forest.Length - 4).Position) < .001f,
    "negative grid offsets wrap across the finish line");
Pass(Vector3.Distance(forest.At(0, -1.25f).Position, forest.At(0).Position) > 1.24f,
    "lane offsets preserve a physically meaningful starting grid");
Pass(forest.Rails.All(r => r.Height > 0f && Vector3.Distance(r.A, r.B) > .01f) &&
    marina.Rails.All(r => r.Height > 0f && Vector3.Distance(r.A, r.B) > .01f),
    "rails retained with finite, nondegenerate dimensions");

byte[] binary = File.ReadAllBytes(Path.Combine(resources, "ground.bytes"));
Vector3[] vertices = Enumerable.Range(0, binary.Length / 12).Select(i =>
    new Vector3(-BitConverter.ToSingle(binary, i*12), BitConverter.ToSingle(binary, i*12+4),
        BitConverter.ToSingle(binary, i*12+8))).ToArray();
float Cross2(Vector3 a, Vector3 b) => a.X*b.Z-a.Z*b.X;
float GroundError(Vector3 target)
{
    float best = float.PositiveInfinity;
    for (int i = 0; i < vertices.Length; i += 3)
    {
        Vector3 a = vertices[i], b = vertices[i+1], c = vertices[i+2];
        float area = Cross2(b-a, c-a);
        if (MathF.Abs(area) < .0001f) continue;
        float u = Cross2(target-a,c-a)/area, v = Cross2(b-a,target-a)/area;
        if (u < -.0001f || v < -.0001f || u+v > 1.0001f) continue;
        float height = a.Y+u*(b.Y-a.Y)+v*(c.Y-a.Y);
        best = MathF.Min(best, MathF.Abs(height-target.Y));
    }
    return best;
}
var errors = Enumerable.Range(0, 72).SelectMany(i => new[] {-4f, 0f, 4f}
    .Select(lane => GroundError(forest.At(i * forest.Length / 72, lane).Position))).ToArray();
Pass(errors.Length == 216 && errors.Max() < .14f,
    $"216 route samples align with reflected ground triangles (max {errors.Max():F3} m)");

float CrossGate(LapCourse course, LapTimer timer, int index, float lane = 0, bool reverse = false)
{
    var frame = course.At(course.Length * index / LapTimer.GateCount, lane);
    Vector3 offset = frame.Forward * 2f * (reverse ? -1f : 1f);
    return timer.Observe(frame.Position - offset, frame.Position + offset, 1f / 60f);
}

foreach ((string Id, LapCourse Course) track in new[] { ("Forest", forest), ("Marina", marina) })
{
    var timer = new LapTimer(track.Course);
    CrossGate(track.Course, timer, 0, reverse:true);
    CrossGate(track.Course, timer, 0, lane:12);
    Pass(!timer.Active && timer.NextGate == 0, $"{track.Id} rejects reverse and off-road start crossings");
    CrossGate(track.Course, timer, 0);
    CrossGate(track.Course, timer, 2);
    Pass(timer.Active && timer.NextGate == 1, $"{track.Id} rejects a skipped checkpoint");
    for (int i = 1; i < LapTimer.GateCount; i++) CrossGate(track.Course, timer, i);
    Pass(CrossGate(track.Course, timer, 0) > 0 && timer.LapCount == 1, $"{track.Id} validates the 28-gate lap");
    timer.Invalidate();
    Pass(!timer.Active && timer.NextGate == 0 && timer.LapCount == 1,
        $"{track.Id} resets timing without discarding completed laps");
}

var drive = new DriveModel(12.7f, 67);
for (int i = 0; i < 120; i++) drive.Update(1, 0, false, 1f / 60);
Pass(drive.Speed is > 12 and < 30, "portable acceleration model reaches forward speed");
float before = drive.Speed;
drive.StopOnImpact();
Pass(drive.Speed < before, "impact reduces the requested longitudinal speed");
drive.Reset();
for (int i = 0; i < 12; i++) drive.Update(0, 1, false, 1f / 60);
Pass(drive.Speed == 0, "brake-to-reverse requires a hold while stopped");
for (int i = 0; i < 60; i++) drive.Update(0, 1, false, 1f / 60);
Pass(drive.Speed is < -1 and >= -8, "continued brake hold gives limited reverse speed");
const string bootstrapScene = "Assets/APEX/Scenes/ForestRidgeLab.unity";
string scene = File.ReadAllText(Path.Combine(root, bootstrapScene));
string AssetGuid(string path) => File.ReadLines(Path.Combine(root, path + ".meta"))
    .Single(line => line.StartsWith("guid: ", StringComparison.Ordinal)).Substring(6).Trim();
string scriptGuid = AssetGuid("Assets/APEX/Runtime/LabSession.cs");
Pass(scene.Contains("m_Script: {fileID: 11500000, guid: " + scriptGuid + ", type: 3}"),
    "prepared scene links to the committed LabSession script GUID");
string buildSettings = File.ReadAllText(Path.Combine(root, "ProjectSettings/EditorBuildSettings.asset"));
Pass(buildSettings.Contains("enabled: 1") && buildSettings.Contains("path: " + bootstrapScene)
    && buildSettings.Contains("guid: " + AssetGuid(bootstrapScene)),
    "committed Build Settings enable the same scene and GUID");
string cloudHook = File.ReadAllText(Path.Combine(root, "Assets/Editor/ApexCloudBuild.cs"));
string phoneGuide = File.ReadAllText(Path.Combine(root, "BUILD_FROM_PHONE.md"));
Pass(cloudHook.Contains("public static void PreExport()") && cloudHook.Contains("LabBuild.Prepare();")
    && phoneGuide.Contains("ApexUrpLab.Editor.ApexCloudBuild.PreExport"),
    "documented cloud hook matches the shared prepare entry point");
Console.WriteLine($"VERIFIED {passed} assertions");
