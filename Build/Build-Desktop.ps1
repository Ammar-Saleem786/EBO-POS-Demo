$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot -Parent
$cache = Join-Path $PSScriptRoot '.cache'
$referenceRoot = Join-Path $cache 'net40\build'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
if (!(Test-Path (Join-Path $referenceRoot '.NETFramework\v4.0\mscorlib.dll'))) {
    $package = Join-Path $cache 'net40.zip'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing 'https://api.nuget.org/v3-flatcontainer/microsoft.netframework.referenceassemblies.net40/1.0.3/microsoft.netframework.referenceassemblies.net40.1.0.3.nupkg' -OutFile $package
    Expand-Archive -LiteralPath $package -DestinationPath (Join-Path $cache 'net40') -Force
}
$msbuild = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\MSBuild.exe'
if (!(Test-Path $msbuild)) { throw 'Install .NET Framework 4.x build tools first.' }
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
try {
    $env:TEMP = $cache
    $env:TMP = $cache
    $logPath = Join-Path $PSScriptRoot 'desktop-build.log'
    $arguments = @(
        (Join-Path $sourceRoot 'Samba.Presentation\Samba.Presentation.csproj'),
        '/t:Build', '/p:Configuration=Release', '/p:Platform=x86',
        '/p:OutputPath=bin\Release\', '/p:PostBuildEvent=',
        ('/p:TargetFrameworkRootPath=' + $referenceRoot + '\'),
        ('/p:CustomAfterMicrosoftCommonTargets=' + (Join-Path $PSScriptRoot 'Desktop.targets')),
        '/m:1', '/nr:false', '/v:minimal', '/nologo'
    )
    & $msbuild @arguments 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) { throw "Build failed. See $logPath" }
    $binaryFolder = Join-Path $sourceRoot 'Samba.Presentation\bin\Release'
    if (!(Test-Path (Join-Path $binaryFolder 'Samba.Presentation.exe'))) { throw 'Executable missing.' }
    Write-Host "Built desktop application: $binaryFolder"
    Write-Host 'Keep all DLLs, language folders, configuration and Imports beside the EXE.'
} finally {
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}
