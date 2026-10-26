#Requires -Version 7.0
param([Parameter(Mandatory)][string]$Executable, [int]$TimeoutSeconds = 30)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path $Executable).Path
$logRoot = Join-Path ([IO.Path]::GetTempPath()) ('Im3DTmp-launcher-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $logRoot | Out-Null

# Run against the Windows DEVELOPMENT build: auto is intentionally absent from Release.
$cases = @(
    @{ Name = 'ru-early'; Json = '{"lang":"ru","auto":1}'; Code = 1; Language = 'ru' },
    @{ Name = 'en-completed'; Json = '{"lang":"en","auto":3}'; Code = 3; Language = 'en' },
    @{ Name = 'simulated-error'; Json = '{"lang":"en","auto":-1}'; Code = -1; Language = 'en' },
    @{ Name = 'unknown-language'; Json = '{"lang":"de","auto":1}'; Code = 1; Language = 'en' },
    @{ Name = 'invalid-json'; Json = 'not-json'; Code = 1; Language = 'en'; CloseWindow = $true }
)
foreach ($case in $cases) {
    $log = Join-Path $logRoot ($case.Name + '.log')
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $Executable
    $info.UseShellExecute = $false
    # ArgumentList preserves JSON as ONE argument, including its inner quotes.
    if ($case.ContainsKey('Json')) { $info.ArgumentList.Add($case.Json) }
    $info.ArgumentList.Add('-logFile')
    $info.ArgumentList.Add($log)
    $process = [Diagnostics.Process]::Start($info)
    try {
        if ($case.CloseWindow) {
            $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
            do {
                Start-Sleep -Milliseconds 200
                $process.Refresh()
                $initialized = (Test-Path $log) -and ((Get-Content $log -Raw) -match '\[GameApplication\] Language:')
            } until ($process.HasExited -or ($process.MainWindowHandle -ne 0 -and $initialized) -or [DateTime]::UtcNow -gt $deadline)
            if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null }
        }
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { throw "Timeout: $($case.Name)" }
        if ($process.ExitCode -ne $case.Code) { throw "$($case.Name): expected $($case.Code), got $($process.ExitCode)" }
        $text = Get-Content $log -Raw
        if ($text -notmatch ('\[GameApplication\] Language: ' + $case.Language)) { throw "Wrong language: $($case.Name)" }
        Write-Host "PASS $($case.Name)"
    }
    finally {
        if (-not $process.HasExited) { $process.Kill($true) }
        $process.Dispose()
    }
}
Write-Host "Logs: $logRoot"
