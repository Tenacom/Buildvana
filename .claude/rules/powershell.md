# Shell usage (Windows PowerShell 5.1)

- To write files without a BOM, do NOT use `Set-Content`/`Out-File`:
  PS 5.1 has no `utf8NoBOM` encoding, and its `UTF8` means "with BOM".
  Use `[System.IO.File]::WriteAllText(path, content)` (UTF-8, no BOM) instead.
- Do not compose structured file content (JSON, XML, regex-bearing text) inside
  PowerShell string literals; escaping layers stack and fail silently. Use the
  Write/Edit tools for file content; use the shell for commands only.
- On a "file is being used by another process" build failure, first probe
  whether the lock still exists (open the file for exclusive write in a
  try/catch): IDE tooling (ReSharper worker, MSBuild language server) takes
  transient locks that clear on their own. Never kill `dotnet` processes
  without identifying them (`Get-CimInstance Win32_Process`) — some are the
  IDE's.
- IDE diagnostics delivered right after an edit may describe a state that
  later edits in the same batch already fixed (e.g. a partial class before its
  second part exists, or an unindexed new project reference). Treat them as
  advisory and let a build be the arbiter — but never dismiss analyzer hits
  (CAxxxx, SAxxxx) without checking, since all warnings are errors here.
