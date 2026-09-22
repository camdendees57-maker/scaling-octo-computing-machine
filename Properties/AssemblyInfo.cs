using System.Reflection;
using MelonLoader;

[assembly: AssemblyTitle(WallWalker.BuildInfo.Name)]
[assembly: AssemblyDescription(WallWalker.BuildInfo.Description)]
[assembly: AssemblyCompany("Camden Dees")]
[assembly: AssemblyProduct(WallWalker.BuildInfo.Name)]
[assembly: AssemblyVersion(WallWalker.BuildInfo.Version)]
[assembly: AssemblyFileVersion(WallWalker.BuildInfo.Version)]

[assembly: MelonInfo(typeof(WallWalker.Main), WallWalker.BuildInfo.Name, WallWalker.BuildInfo.Version, WallWalker.BuildInfo.Author)]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]
