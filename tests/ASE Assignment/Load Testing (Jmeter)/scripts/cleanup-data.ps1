param([ValidateSet('local','azure')][string]$Environment='local',[switch]$ConfirmFixtureCleanup)
. "$PSScriptRoot/common.ps1"
if(!$ConfirmFixtureCleanup){throw 'Use -ConfirmFixtureCleanup to remove ONLY resources named in the fixture manifest.'}
$root=Split-Path $PSScriptRoot -Parent;$config=Read-Properties (Join-Path $root "config/$Environment.properties");Assert-Environment $config $Environment
$file=Join-Path $root "data/$Environment/accounts.private.json";$data=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json
if($data.prefix -notmatch '^ase-[a-f0-9]{12}$'){throw 'Unrecognized fixture ownership prefix.'}
$secrets=Get-Content (Join-Path $root "config/$Environment.private.json") -Raw|ConvertFrom-Json
$admin=Invoke-LocalApi $config user POST '/api/auth/login' @{email=$secrets.adminEmail;password=$secrets.adminPassword}
$failed=$false
# Reverse dependencies: books/list before genre/author; users removed only when their own email matches.
foreach($kind in @('book','list','author','genre','user')){
 foreach($r in @($data.resources|Where-Object kind -eq $kind)){
  if(!$r.name.StartsWith($data.prefix)){throw 'Resource ownership mismatch.'}
  try {
   switch($kind){
    'book' {
      $books=@(Invoke-LocalApi $config inventory GET '/api/Books');$b=$books|Where-Object id -eq $r.id|Select-Object -First 1
      if($b){if($b.title -ne $r.name){throw 'Book identity changed'};$null=Invoke-LocalApi $config inventory DELETE "/api/Books/$($r.id)" $null $admin.token}
    }
    'list' {
      $items=@(Invoke-LocalApi $config inventory GET '/api/Lists');$item=$items|Where-Object key -eq $r.id|Select-Object -First 1
      if($item){if($item.name -ne $r.name){throw 'List identity changed'};$null=Invoke-LocalApi $config inventory DELETE "/api/Lists/$($r.id)" $null $admin.token}
    }
    'author' {
      $items=@(Invoke-LocalApi $config inventory GET '/api/Authors');$item=$items|Where-Object id -eq $r.id|Select-Object -First 1
      if($item){if($item.name -ne $r.name){throw 'Author identity changed'};$null=Invoke-LocalApi $config inventory DELETE "/api/Authors/$($r.id)" $null $admin.token}
    }
    'genre' {
      $items=@(Invoke-LocalApi $config inventory GET '/api/Genres');$item=$items|Where-Object id -eq $r.id|Select-Object -First 1
      if($item){if($item.name -ne $r.name){throw 'Genre identity changed'};$null=Invoke-LocalApi $config inventory DELETE "/api/Genres/$($r.id)" $null $admin.token}
    }
    'user' {
      $active=@(Invoke-LocalApi $config user GET '/api/Admin/users/active' $null $admin.token)
      $u=$active|Where-Object email -eq $r.name|Select-Object -First 1
      if($u){$null=Invoke-LocalApi $config user POST "/api/Admin/users/$($u.id)/revoke" $null $admin.token}
      $pending=@(Invoke-LocalApi $config user GET '/api/Admin/users/pending' $null $admin.token)
      $u=$pending|Where-Object email -eq $r.name|Select-Object -First 1
      if($u){$null=Invoke-LocalApi $config user POST "/api/Admin/users/$($u.id)/reject" $null $admin.token}
    }
   }
  } catch { $failed=$true;Write-Warning "Could not clean a $kind fixture. Resolve through the application and retry; manifest retained." }
 }
}
if($failed){throw 'Partial cleanup; private manifest retained for retry.'}
# Rename rather than delete the recovery manifest. Path is fixed inside the suite's data directory.
#Move-Item -LiteralPath $file -Destination (Join-Path $root ('data/local/cleaned-'+(Get-Date -Format 'yyyyMMddHHmmss')+'.private.json'))
Move-Item -LiteralPath $file -Destination (Join-Path $root ('data/' + $Environment + '/cleaned-' + (Get-Date -Format 'yyyyMMddHHmmss') + '.private.json'))
Write-Host 'Fixture resources cleaned. Historical Lending/Notification records remain by design; use fresh dedicated databases for a complete reset.'
