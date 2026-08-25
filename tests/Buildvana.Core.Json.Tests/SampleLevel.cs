// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

// An enum for the defaults-emission tests: its default value must render through the serializer options
// (camel-cased string), never as a bare member name or a number.
internal enum SampleLevel
{
    One,
    Two,
}
