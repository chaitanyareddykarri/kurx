<#
.SYNOPSIS
    Prints the most recent dev one-time code - SMS or email - from the backend container log.

.DESCRIPTION
    In dev, SMS_PROVIDER=console and EMAIL_PROVIDER=console, so nothing is delivered anywhere: the
    code is written to the API log instead. Finding it by eye is impractical because the backend logs
    every HTTP request as a JSON line, so a real code is buried thousands of lines deep within minutes.

    Both channels are covered, and the channel is printed. That matters because they are not
    interchangeable: a phone code and an email code are minted for different purposes
    (PhoneVerification vs EmailVerification/EmailLogin) and the server matches on the pair, so an
    email code will never satisfy a phone prompt no matter how fresh it is. When you have just
    triggered both, "which one is this?" is the question you actually need answered.

    Four details this handles that a plain `docker logs | findstr` does not:

      * It searches the WHOLE log, not `--tail N`. A code five minutes old can already be hundreds of
        lines back, so a tail window silently returns nothing and looks like "no code was sent".
      * It prints the code's AGE. Codes expire in 5 minutes, and an expired one looks identical to a
        fresh one - which is the single most common reason "the code doesn't work".
      * It prints the CHANNEL and DESTINATION, so two concurrent flows cannot be confused.
      * It reads the code out of the message body, which is where both senders put it.

    Logs arrive on stdout, so no stream redirection is needed (`2>&1` on a native command in
    Windows PowerShell 5.1 wraps each line in an ErrorRecord and is worth avoiding).

.PARAMETER Channel
    Limit to one channel: Sms or Email. Default Any.

.PARAMETER Watch
    Follow the log and print each new code as it is issued. Leave this running while you sign up.

.EXAMPLE
    .\scripts\otp.ps1
    2026-08-09 05:22:41Z  [sms]  +919876500456  code 481920  (12s old - valid)

.EXAMPLE
    .\scripts\otp.ps1 -Channel Email
    2026-08-09 15:48:18Z  [email]  testuser456@example.com  code 194855  (7s old - valid)

.EXAMPLE
    .\scripts\otp.ps1 -Watch
#>
param(
    [switch]$Watch,
    [ValidateSet('Any', 'Sms', 'Email')]
    [string]$Channel = 'Any',
    [string]$Container = 'kurx-backend'
)

$ErrorActionPreference = 'Stop'

# Both senders embed the same sentence: ConsoleSmsProvider logs it as `msg`, ConsoleEmailSender as
# `body` (tags stripped). Matching the sentence rather than either field name keeps this working if a
# third channel is added.
$marker = 'Your Kurx code is'

# The channel prefix is matched WITHOUT the arrow in "[sms->console]": PowerShell 5.1 reads this file
# in the console codepage, and a non-ASCII literal here would silently never match.
function Get-Channel([string]$line) {
    if ($line -like '*[[]email*') { return 'email' }
    if ($line -like '*[[]sms*') { return 'sms' }
    return 'other'
}

function Get-Destination([string]$line) {
    # Email logs `To`, SMS logs `Phone`. Structured Serilog properties, so a plain regex is enough.
    $to = [regex]::Match($line, '(?<="To":")[^"]+').Value
    if ($to) { return $to }
    $phone = [regex]::Match($line, '(?<="Phone":")[^"]+').Value
    if ($phone) { return $phone }
    return '(unknown)'
}

function Get-Code([string]$line) {
    return [regex]::Match($line, '(?<=Your Kurx code is )\d{6}').Value
}

function Test-Wanted([string]$channel) {
    return ($Channel -eq 'Any') -or ($channel -eq $Channel.ToLowerInvariant())
}

if ($Watch) {
    $what = if ($Channel -eq 'Any') { 'codes' } else { "$($Channel.ToLowerInvariant()) codes" }
    Write-Host "Watching $Container for new $what - request one in the app. Ctrl+C to stop." -ForegroundColor Cyan
    docker logs -f --since 0s $Container |
        Select-String -SimpleMatch $marker |
        ForEach-Object {
            $ch = Get-Channel $_.Line
            if (Test-Wanted $ch) {
                Write-Host ("{0}  [{1}]  {2}  code {3}" -f (Get-Date -Format 'HH:mm:ss'),
                    $ch, (Get-Destination $_.Line), (Get-Code $_.Line)) -ForegroundColor Green
            }
        }
    return
}

$candidates = docker logs $Container | Select-String -SimpleMatch $marker
$line = $candidates | Where-Object { Test-Wanted (Get-Channel $_.Line) } | Select-Object -Last 1

if (-not $line) {
    $hint = if ($Channel -eq 'Any') { '' } else { " for channel '$Channel'" }
    Write-Host "No code found in $Container's log$hint. Request one in the app first, then re-run." -ForegroundColor Yellow
    # An email code with no matching line almost always means the console sender is not printing the
    # body - the state this script could not see through before ConsoleEmailSender logged it.
    if ($Channel -eq 'Email') {
        Write-Host "If you requested one, check EMAIL_PROVIDER=console and that ConsoleEmailSender logs body=." -ForegroundColor DarkYellow
    }
    return
}

$ch = Get-Channel $line.Line
$code = Get-Code $line.Line
$dest = Get-Destination $line.Line

# The structured log carries its own UTC timestamp in `@t`; trust that over the wall clock, because
# `docker logs` prints historical lines with no indication of how old they are.
$issuedAt = [datetime]::Parse(
    [regex]::Match($line.Line, '(?<="@t":")[^"]+').Value,
    [cultureinfo]::InvariantCulture,
    [System.Globalization.DateTimeStyles]::RoundtripKind
).ToUniversalTime()

$age = [int]((Get-Date).ToUniversalTime() - $issuedAt).TotalSeconds
$fresh = $age -lt 300      # matches the server's 5-minute expiry

$verdict = if ($fresh) { "$age`s old - valid" } else { "$([int]($age / 60))m old - EXPIRED, request a new one" }
Write-Host ("{0:yyyy-MM-dd HH:mm:ss}Z  [{1}]  {2}  code {3}  ({4})" -f $issuedAt, $ch, $dest, $code, $verdict) `
    -ForegroundColor $(if ($fresh) { 'Green' } else { 'Red' })
