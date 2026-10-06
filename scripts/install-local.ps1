param([Parameter(Mandatory = $true)][string]$AppDockDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskHost = [IO.Path]::GetFullPath($AppDockDirectory)
if (-not (Test-Path -LiteralPath (Join-Path $taskHost 'AppDock.at365.exe') -PathType Leaf)) { throw 'AppDock.at365.exeがあるフォルダーを指定してください。' }
$taskSource = Join-Path $taskRoot 'publish\Applet.WebBrowserTools.at365'
$taskFiles = @('Applet.WebBrowserTools.at365.dll', 'Applet.WebBrowserTools.at365.deps.json', 'extension.json')
foreach ($taskFile in $taskFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $taskSource $taskFile) -PathType Leaf)) { throw '先にpublish.batを実行してください。' }
}
$taskDestination = Join-Path $taskHost 'extensions\Applet.WebBrowserTools.at365'
New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
# AppDock.SDK is supplied by the DLL host, not privately deployed by the applet.
foreach ($taskFile in $taskFiles) { Copy-Item -LiteralPath (Join-Path $taskSource $taskFile) -Destination (Join-Path $taskDestination $taskFile) -Force }
Write-Output "Appletを配置しました: $taskDestination"
Write-Output 'AppDockを起動し直し、Applet.WebBrowserTools.at365を有効にしてください。'
