// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// An <see cref="AdditionalText"/> whose content is provided as a string.
/// </summary>
internal sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
{
    public override string Path => path;

    public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content, Encoding.UTF8);
}
