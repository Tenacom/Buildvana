// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Buildvana.Tool.Infrastructure.Options;
using Cake.Frosting;

namespace Buildvana.Tool.Infrastructure;

public static class CommandLineParser
{
    private const string ScriptName = "build.cmd";

    private static readonly string CallScriptName =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ScriptName : "./" + ScriptName;

    private static readonly IOption[] BaseOptions = [
        KnownOptions.Verbosity,
        KnownOptions.Exclusive,
        KnownOptions.Help,
        KnownOptions.Stable,
    ];

    public static string[]? Parse(string[]? args)
    {
        if (args == null || args.Length == 0 || (args.Length == 1 && Is(args[0], "help", "--help", "-h")))
        {
            PrintHelp();
            return null;
        }

        if (Is(args[0], "cake"))
        {
            return [..args.Skip(1)];
        }

        var argsToProcess = new Queue<string>(args);

        var taskName = argsToProcess.Dequeue();
        if (Is(taskName, "-t", "--target") && argsToProcess.Count > 0)
        {
            taskName = argsToProcess.Dequeue();
        }

        var taskNames = GetTaskNames();
        var matchedTaskName = taskNames.FirstOrDefault(name => string.Equals(
            name.Replace("-", string.Empty, StringComparison.Ordinal),
            taskName.Replace("-", string.Empty, StringComparison.Ordinal),
            StringComparison.OrdinalIgnoreCase));
        if (matchedTaskName == null)
        {
            PrintError($"'{taskName}' is not a task");
            return null;
        }

        taskName = matchedTaskName;

        if (argsToProcess.Count == 1 && Is(argsToProcess.Peek(), "-h", "--help"))
        {
            PrintTaskHelp(taskName);
            return null;
        }

        var cakeArgs = new List<string>
        {
            "--target",
            taskName,
        };
        while (argsToProcess.Count > 0)
        {
            var arg = argsToProcess.Dequeue();

            if (arg.StartsWith("/p:", StringComparison.Ordinal))
            {
                cakeArgs.Add("--msbuild");
                cakeArgs.Add(arg[3..]);
                continue;
            }

            if (arg.StartsWith('-'))
            {
                cakeArgs.Add(arg);
                if (argsToProcess.Count > 0 && !argsToProcess.Peek().StartsWith('-'))
                {
                    cakeArgs.Add(argsToProcess.Dequeue());
                }

                continue;
            }

            PrintError("Unknown option: " + arg);
            return null;
        }

        return [.. cakeArgs];
    }

    private static HashSet<string> GetTaskNames() => GetTasks().Select(task => task.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static List<(string Name, string Description, HelpInfo HelpInfo)> GetTasks()
        => [.. typeof(BuildContext).Assembly.GetTypes()
            .Where(type => (type.IsSubclassOf(typeof(FrostingTask<BuildContext>)) || type.IsSubclassOf(typeof(AsyncFrostingTask<BuildContext>))) && !type.IsAbstract)
            .Select(type => (
                Name: type.GetCustomAttribute<TaskNameAttribute>()?.Name ?? string.Empty,
                Description: type.GetCustomAttribute<TaskDescriptionAttribute>()?.Description ?? string.Empty,
                HelpInfo: GetHelpInfo(type)))
            .Where(task => task.Name.Length > 0)];

    private static HelpInfo GetHelpInfo(Type taskType) => Activator.CreateInstance(taskType) is IHelpProvider helpProvider
        ? helpProvider.GetHelp()
        : new HelpInfo();

    private static bool Is(string? arg, params string[] values)
        => values.Any(value => value.Equals(arg, StringComparison.OrdinalIgnoreCase));

    private static void PrintHelp()
    {
        WriteHeader("Description:");

        WritePrefix();
        WriteLine("BenchmarkDotNet build script");

        WriteLine();

        WriteHeader("Usage:");

        WritePrefix();
        Write(CallScriptName + " ");
        WriteTask("<TASK> ");
        WriteOption("[OPTIONS]");
        WriteLine();

        WriteLine();

        PrintExamples([.. GetTasks().SelectMany(task => task.HelpInfo.Examples)]);

        PrintOptions(BaseOptions);

        WriteHeader("Tasks:");
        var taskWidth = GetTaskNames().Max(name => name.Length) + 3;
        foreach (var (taskName, taskDescription, _) in GetTasks())
        {
            WriteTask("    " + taskName.PadRight(taskWidth));
            Write(taskDescription);

            WriteLine();
        }
    }

    private static void PrintTaskHelp(string taskName)
    {
        var taskType = typeof(BuildContext).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(FrostingTask<BuildContext>)) && !type.IsAbstract)
            .First(type => Is(type.GetCustomAttribute<TaskNameAttribute>()?.Name, taskName));
        taskName = taskType.GetCustomAttribute<TaskNameAttribute>()!.Name;
        var taskDescription = taskType.GetCustomAttribute<TaskDescriptionAttribute>()?.Description ?? string.Empty;
        var helpInfo = GetHelpInfo(taskType);

        WriteHeader("Description:");

        WritePrefix();
        WriteLine(!string.IsNullOrWhiteSpace(taskDescription)
            ? $"Task '{taskName}': {taskDescription}"
            : $"Task '{taskName}'");

        if (!string.IsNullOrWhiteSpace(helpInfo.Description))
        {
            foreach (var line in helpInfo.Description.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                WritePrefix();
                WriteLine(line.TrimEnd());
            }
        }

        WriteLine();

        WriteHeader("Usage:");

        WritePrefix();
        Write(CallScriptName + " ");
        WriteTask(taskName + " ");
        WriteOption("[OPTIONS]");
        WriteLine();

        WriteLine();

        PrintExamples(helpInfo.Examples);

        PrintOptions([.. helpInfo.Options, .. BaseOptions]);

        if (helpInfo.EnvironmentVariables.Any())
        {
            WriteHeader("Environment variables:");
            foreach (var envVar in helpInfo.EnvironmentVariables)
            {
                WritePrefix();
                WriteOption(envVar.Name);
                WriteLine();
            }
        }
    }

    private static void PrintOptions(IOption[] options)
    {
        const string valuePlaceholder = "<VALUE>";

        WriteLine("Options:", ConsoleColor.DarkCyan);

        static int GetWidth(IOption option)
        {
            var width = option.CommandLineName.Length + option.Aliases.Sum(alias => 1 + alias.Length);
            if (option is StringOption)
            {
                width += 1 + valuePlaceholder.Length;
            }

            return width;
        }

        const int descriptionGap = 3;
        var maxWidth = options.Max(GetWidth) + descriptionGap;

        foreach (var option in options)
        {
            var allNames = option.Aliases.Append(option.CommandLineName).OrderBy(name => name.Length);
            var joinName = string.Join(',', allNames);

            WritePrefix();
            WriteOption(joinName);
            if (option is StringOption)
            {
                Write(" ");
                WriteArg(valuePlaceholder);
            }

            Write(new string(
                ' ',
                maxWidth - joinName.Length - (option is StringOption ? valuePlaceholder.Length + 1 : 0)));
            var descriptionLines = option.Description.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
            Write(descriptionLines.FirstOrDefault(string.Empty));
            for (var i = 1; i < descriptionLines.Length; i++)
            {
                WriteLine();
                WritePrefix();
                Write(new string(' ', maxWidth));
                Write(descriptionLines[i]);
            }

            WriteLine();
        }

        WritePrefix();
        WriteOption("/p:");
        WriteArg("<KEY>");
        WriteOption("=");
        WriteArg(valuePlaceholder);
        Write(new string(' ', maxWidth - "/p:<KEY>=".Length - valuePlaceholder.Length));
        Write("Passes custom properties to MSBuild");
        WriteLine();

        WriteLine();
    }

    private static void PrintExamples(IReadOnlyList<Example> examples)
    {
        if (!examples.Any())
        {
            return;
        }

        WriteHeader("Examples:");

        foreach (var example in examples)
        {
            WritePrefix();
            Write(CallScriptName + " ");
            WriteTask(example.TaskName + " ");
            foreach (var (name, value, isMsBuild) in example.Arguments)
            {
                if (isMsBuild)
                {
                    WriteOption("/p:");
                    WriteArg(name);
                    WriteOption("=");
                    WriteArg(value + " ");
                }
                else
                {
                    WriteOption(name + " ");
                    if (value != null)
                    {
                        WriteArg(value + " ");
                    }
                }
            }

            WriteLine();
        }

        WriteLine();
    }

    private static void PrintError(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("ERROR: " + text);
        Console.WriteLine();
        Console.ResetColor();
        PrintHelp();
    }

    private static void WritePrefix() => Write("    ");
    private static void WriteTask(string message) => Write(message, ConsoleColor.Green);
    private static void WriteOption(string message) => Write(message, ConsoleColor.Blue);
    private static void WriteArg(string message) => Write(message, ConsoleColor.DarkYellow);
    private static void WriteObsolete(string message) => Write(message, ConsoleColor.Gray);

    private static void WriteHeader(string message) => WriteLine(message, ConsoleColor.DarkCyan);

    private static void Write(string message, ConsoleColor? color = null)
    {
        if (color != null)
        {
            Console.ForegroundColor = color.Value;
        }

        Console.Write(message);
        if (color != null)
        {
            Console.ResetColor();
        }
    }

    private static void WriteLine(string message = "", ConsoleColor? color = null)
    {
        Write(message, color);
        Console.WriteLine();
    }
}
