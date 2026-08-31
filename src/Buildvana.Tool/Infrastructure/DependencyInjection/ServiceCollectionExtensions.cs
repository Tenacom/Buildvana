// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Core.Versioning;
using Buildvana.Runtime;
using Buildvana.Tool.Build;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Dependencies;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Hooks;
using Buildvana.Tool.Services.PublicApiFiles;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Subcommands;
using Buildvana.Tool.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Infrastructure.DependencyInjection;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to add Buildvana Tool services.
/// </summary>
internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection @this)
    {
        /// <summary>
        /// Adds support for resolving <see cref="Lazy{T}"/> instances.
        /// </summary>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddLazySupport() => @this.AddTransient(typeof(Lazy<>), typeof(LazyResolver<>));

        /// <summary>
        /// Adds every service <c>bv</c> commands resolve, plus the commands themselves.
        /// </summary>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// <para>The ambient singletons a host decides for itself are deliberately not registered here:
        /// the console, the reporter, <see cref="GlobalSettings"/>, <see cref="CommandParameters"/>, and
        /// <see cref="IHomeDirectoryProvider"/>. Register those before calling this method.</para>
        /// <para>A host that fakes a boundary registers its fake <em>after</em> this call: the last
        /// registration of a service type is the one resolved. Only the last one, though - the registration
        /// it shadows stays in the collection, and something resolving <c>IEnumerable&lt;T&gt;</c> would get
        /// both. Nothing does today; a boundary that ever grows more than one implementation will have to
        /// be faked by replacing its registration rather than by adding one.</para>
        /// </remarks>
        public IServiceCollection AddBvServices()
        {
            _ = @this
                .AddLazySupport()
                .AddSingleton(static sp => ReleaseSettings.Parse(sp.GetRequiredService<CommandParameters>().Options))
                .AddSingleton(static sp => VersionAdvanceSettings.Parse(
                    sp.GetRequiredService<CommandParameters>().Positionals,
                    sp.GetRequiredService<CommandParameters>().Options))
                .AddSingleton(static sp => SelfUpdateSettings.Parse(sp.GetRequiredService<CommandParameters>().Options))

                // Lazy by design: the provider finds, parses, and validates the file on first read of what is asked
                // of it. A malformed buildvana.json stays inert until a consumer - typically the BuildvanaConfig
                // registration below - reads the configuration. Registering the resolved configuration separately
                // keeps consumers depending on the data alone, while the provider answers whoever needs the path.
                .AddSingleton<BuildvanaJsonConfigProvider>()
                .AddSingleton(static sp => CommandLineOverridesParser.Parse(sp.GetRequiredService<CommandParameters>()))
                .AddSingleton(static sp => BuildvanaConfigFactory.Create(
                    sp.GetRequiredService<BuildvanaJsonConfigProvider>().Config,
                    sp.GetRequiredService<CommandLineOverrides>()))
                .AddSingleton<IJsonHelper, JsonHelper>()
                .AddSingleton<IProcessRunner, ProcessRunner>()
                .AddSingleton<ISolutionContextFactory, HomeDirectorySolutionContextFactory>()
                .AddSingleton<SolutionContext>(static sp => sp.GetRequiredService<ISolutionContextFactory>().Create())
                .AddSingleton<GitService>()
                .AddSingleton<PublicApiFilesService>()
                .AddSingleton(ServerAdapter.Create)
                .AddSingleton<VersioningSettings>()
                .AddSingleton<VersionService>()
                .AddSingleton<ChangelogService>()
                .AddSingleton<DotNetService>()
                .AddSingleton<IFileBasedAppRunner>(static sp => sp.GetRequiredService<DotNetService>())
                .AddSingleton<HookRunner>()
                .AddSingleton<PostReleaseHookArgsFactory>()
                .AddSingleton<BuildPipeline>()
                .AddSingleton<SelfReferenceUpdater>()
                .AddSingleton<FamilyPinUpdater>()
                .AddSingleton<GlobalJsonPinReader>()
                .AddSingleton<ToolPinReader>()
                .AddSingleton<DirectivePinReader>()
                .AddSingleton<SolutionPinReader>()
                .AddSingleton<PackagePinReader>()
                .AddSingleton<AdditionalGroupPinReader>()
                .AddSingleton<DependencyDiscovery>()
                .AddSingleton(static sp => new EffectivePolicyResolver(sp.GetRequiredService<BuildvanaConfig>().Dependencies))
                .AddSingleton(static sp => new SelfVersionService(
                    sp.GetRequiredService<IReporter>(),
                    sp.GetRequiredService<IHomeDirectoryProvider>(),
                    sp.GetRequiredService<BuildvanaJsonConfigProvider>(),
                    sp.GetRequiredService<IJsonHelper>(),
                    sp.GetRequiredService<IProcessRunner>(),
                    sp.GetRequiredService<FamilyPinUpdater>(),
                    OwnVersion.Value));

            foreach (var registration in CommandRegistry.Commands)
            {
                _ = @this.AddSingleton(registration.CommandType);
            }

            return @this;
        }
    }
}
