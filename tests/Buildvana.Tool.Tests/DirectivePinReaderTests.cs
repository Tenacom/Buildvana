// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Dependencies;

internal sealed class DirectivePinReaderTests
{
    private const string ToolApp = """
                                   #!/usr/bin/env dotnet
                                   #:package Serilog@4.0.0
                                   #:package Spectre.Console
                                   #:sdk Microsoft.Build.NoTargets@3.7.0
                                   #:sdk Microsoft.NET.Sdk

                                   Console.WriteLine("hello");
                                   """;

    [Test]
    public async Task Read_StatesOnePinPerVersionedDirective()
    {
        using var home = new TempHome();
        Write(home, "tools/report.cs", ToolApp);
        var pins = Read(home);
        await Assert.That(pins.Select(static pin => pin.Scope + " " + pin.Id + " " + pin.VersionText))
            .IsEquivalentTo(["Packages Serilog 4.0.0", "Sdks Microsoft.Build.NoTargets 3.7.0"]);
        await Assert.That(pins[0].DeclaringFile).IsEqualTo("tools/report.cs");
    }

    // A .cs file outside the declared scope is a source file, and its leading comments are nobody's pins.
    [Test]
    public async Task Read_IgnoresAFileOutsideTheScope()
    {
        using var home = new TempHome();
        Write(home, "src/App/Program.cs", ToolApp);
        await Assert.That(Read(home)).IsEmpty();
    }

    [Test]
    public async Task Read_ReadsTheBuiltInHooksScope()
    {
        using var home = new TempHome();
        Write(home, ".buildvana/hooks/release/post-release.cs", ToolApp);
        var pins = Read(home, new BuildvanaConfig());
        await Assert.That(pins.Select(static pin => pin.Id)).Contains("Serilog");
    }

    // The family moves in lockstep, and bv self-update is the command that moves it.
    [Test]
    public async Task Read_LeavesAFamilyDirectiveOut()
    {
        using var home = new TempHome();
        Write(home, "tools/report.cs", "#:sdk Buildvana.Sdk@2.1.0\n#:package Buildvana.Runtime@2.1.0\n");
        await Assert.That(Read(home)).IsEmpty();
    }

    // A directive whose '@' has nothing after it states a version of no version at all: it is a pin, and an
    // unmanaged one, so that the report says the file needs a hand rather than passing over it.
    [Test]
    public async Task Read_StatesADirectiveWithAnEmptyVersionAsUnmanaged()
    {
        using var home = new TempHome();
        Write(home, "tools/report.cs", "#:package Serilog@\n");
        await Assert.That(Read(home).Single().Management).IsEqualTo(PinManagement.UnreadableVersion);
    }

    // A versionless #:package resolves through central package management, so the id it names is a reference
    // to the central pin. A versionless #:sdk names no package and is no reference to one.
    [Test]
    public async Task Read_StatesAVersionlessPackageDirectiveAsAReference()
    {
        using var home = new TempHome();
        Write(home, "tools/report.cs", ToolApp);
        var read = ReadAll(home);
        await Assert.That(read.References).IsEquivalentTo(["Spectre.Console"]);
    }

    // Two apps may reference one package, and what the answer names is the package, not the mentions of it.
    [Test]
    public async Task Read_NamesAReferencedPackageOnce()
    {
        using var home = new TempHome();
        Write(home, "tools/first.cs", "#:package Spectre.Console\n");
        Write(home, "tools/second.cs", "#:package spectre.console\n");
        var read = ReadAll(home);
        await Assert.That(read.References.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Read_IgnoresBuildOutput()
    {
        using var home = new TempHome();
        Write(home, "tools/obj/generated.cs", ToolApp);
        await Assert.That(Read(home)).IsEmpty();
    }

    private static IReadOnlyList<DependencyPin> Read(TempHome home, BuildvanaConfig? config = null) => ReadAll(home, config).Pins;

    private static DirectivePins ReadAll(TempHome home, BuildvanaConfig? config = null)
        => new DirectivePinReader(home.Provider, config ?? new BuildvanaConfig { FileBasedApps = ["/tools/"] }).Read();

    private static void Write(TempHome home, string relativePath, string content)
    {
        var path = home.GetFullPath(relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
