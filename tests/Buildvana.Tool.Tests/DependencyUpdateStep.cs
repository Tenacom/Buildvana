// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/// <summary>
/// An observable step of a <c>bv dependencies update</c> run, with what <c>global.json</c> stated when it
/// happened. What a step saw is how a test tells apart what ran before it from what ran after.
/// </summary>
/// <param name="Name">The name of the step.</param>
/// <param name="GlobalJson">The content of <c>global.json</c> at the time of the step.</param>
internal sealed record DependencyUpdateStep(string Name, string GlobalJson);
