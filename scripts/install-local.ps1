param([Parameter(Mandatory = $true)][string]$AppDockDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskHost = [IO.Path]::GetFullPath($AppDockDirectory)
if (-not (Test-Path -LiteralPath (Join-Path $taskHost 'AppDock.at365.exe') -PathType Leaf)) { throw 'AppDock.at365.exeがあるフォルダーを指定してください。' }
$taskSource = Join-Path $taskRoot 'publish\Applet.WebBrowserTools.at365'
$taskFiles = @('Applet.WebBrowserTools.at365.exe', 'extension.json')
foreach ($taskFile in $taskFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $taskSource $taskFile) -PathType Leaf)) { throw '先にpublish.batを実行してください。' }
}
$taskDestination = Join-Path $taskHost 'extensions\Applet.WebBrowserTools.at365'
New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
foreach ($taskFile in $taskFiles) { Copy-Item -LiteralPath (Join-Path $taskSource $taskFile) -Destination (Join-Path $taskDestination $taskFile) -Force }
foreach ($taskOldName in @('Applet.WebBrowserTools.at365.dll', 'Applet.WebBrowserTools.at365.deps.json')) {
    $taskOldFile = Join-Path $taskDestination $taskOldName
    if (Test-Path -LiteralPath $taskOldFile -PathType Leaf) { Remove-Item -LiteralPath $taskOldFile }
}
Write-Output "Appletを配置しました: $taskDestination"
Write-Output 'AppDockを起動し直し、Applet.WebBrowserTools.at365を有効にしてください。'
