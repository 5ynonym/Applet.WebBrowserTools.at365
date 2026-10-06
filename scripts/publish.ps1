param([string]$OutputDirectory, [string]$AppDockRoot)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskOutput = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $taskRoot 'publish\Applet.WebBrowserTools.at365' }
$taskArguments = @('publish', (Join-Path $taskRoot 'Applet.WebBrowserTools\Applet.WebBrowserTools.csproj'), '-c', 'Release', '-o', $taskOutput)
if ($AppDockRoot) { $taskArguments += ('-p:AppDockRoot=' + [IO.Path]::GetFullPath($AppDockRoot)) }
& dotnet @taskArguments
if ($LASTEXITCODE -ne 0) { throw "WebBrowserTools publish failed ($LASTEXITCODE)" }
Copy-Item -LiteralPath (Join-Path $taskRoot 'extension.json') -Destination (Join-Path $taskOutput 'extension.json') -Force
Write-Output "Applet output: $taskOutput"
