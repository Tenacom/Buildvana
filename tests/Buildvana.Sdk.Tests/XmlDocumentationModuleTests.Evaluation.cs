// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

partial class XmlDocumentationModuleTests
{
    // What one evaluation leaves in the project: the properties the module reads or writes, and the
    // EvaluationWarning items it adds.
    private sealed record Evaluation(
        string XmlDocs,
        string GenerateDocumentationFile,
        string DocumentationFile,
        string NoWarn,
        IReadOnlyList<string> Warnings);
}
