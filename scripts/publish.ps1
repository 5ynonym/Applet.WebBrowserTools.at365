param([string]$OutputDirectory, [string]$AppDockRoot)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskOutput = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $taskRoot 'publish\Applet.WebBrowserTools.at365' }
$taskArguments = @('publish', (Join-Path $taskRoot 'Applet.WebBrowserTools\Applet.WebBrowserTools.csproj'), '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true', '-p:CopyOutputSymbolsToPublishDirectory=false', '-o', $taskOutput)
if ($AppDockRoot) { $taskArguments += ('-p:AppDockRoot=' + [IO.Path]::GetFullPath($AppDockRoot)) }
& dotnet @taskArguments
if ($LASTEXITCODE -ne 0) { throw "WebBrowserTools publish failed ($LASTEXITCODE)" }
Copy-Item -LiteralPath (Join-Path $taskRoot 'extension.json') -Destination (Join-Path $taskOutput 'extension.json') -Force
# Only the native executable and manifest belong to this distribution.
foreach ($taskOldName in @('Applet.WebBrowserTools.at365.dll', 'Applet.WebBrowserTools.at365.deps.json', 'Applet.WebBrowserTools.at365.runtimeconfig.json', 'AppDock.SDK.dll', 'AppDock.Runtime.dll', 'AppDock.SDK.pdb', 'AppDock.Runtime.pdb')) {
    $taskOldFile = Join-Path $taskOutput $taskOldName
    if (Test-Path -LiteralPath $taskOldFile -PathType Leaf) { Remove-Item -LiteralPath $taskOldFile }
}
Write-Output "Applet output: $taskOutput"

$taskUpdateHostRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\AppDock.at365'))
if ($AppDockRoot) { $taskUpdateHostRoot = [IO.Path]::GetFullPath($AppDockRoot) }
& (Join-Path $taskUpdateHostRoot 'scripts\pack-applet-update.ps1') -SourceDirectory $taskOutput -OutputDirectory (Join-Path (Split-Path $PSScriptRoot -Parent) 'publish')
