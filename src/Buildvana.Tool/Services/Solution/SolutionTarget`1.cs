// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization.Metadata;

namespace Buildvana.Tool.Services.Solution;

/// <summary>
/// Describes a target of the Buildvana SDK that writes one JSON file per evaluation of a project, for
/// <see cref="SolutionTargetRunner"/> to run on every project of a solution.
/// </summary>
/// <typeparam name="T">The type of the content of each file the target writes.</typeparam>
/// <param name="Name">The name of the target, which the Buildvana SDK defines.</param>
/// <param name="OutputDirectory">The directory the target writes its files to, relative to the home
/// directory.</param>
/// <param name="OutputDirectoryProperty">The MSBuild property that names the output directory to the
/// target.</param>
/// <param name="ContentDescription">What the files hold, as a noun phrase for diagnostics, such as
/// <c>package pins</c>.</param>
/// <param name="TypeInfo">The source-generated serialization metadata for <typeparamref name="T"/>.</param>
internal sealed record SolutionTarget<T>(
    string Name,
    string OutputDirectory,
    string OutputDirectoryProperty,
    string ContentDescription,
    JsonTypeInfo<T> TypeInfo);
