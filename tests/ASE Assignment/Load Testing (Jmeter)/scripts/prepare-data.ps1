param([ValidateRange(1,100)][int]$Users=2,[switch]$SeedNotifications)
. "$PSScriptRoot/common.ps1"
$root=Split-Path $PSScriptRoot -Parent;$config=Read-Properties (Join-Path $root 'config/local.properties');Assert-Local $config
$private=Join-Path $root 'config/local.private.json'
if(!(Test-Path -LiteralPath $private)){throw 'Copy config/local.private.example.json to local.private.json and fill local admin credentials.'}
$secrets=Get-Content -LiteralPath $private -Raw|ConvertFrom-Json
if(!$secrets.adminPassword -or $secrets.adminPassword -eq 'REPLACE_ME'){throw 'Fill your local admin credentials first.'}
$file=Join-Path $root 'data/local/accounts.private.json'
if(Test-Path -LiteralPath $file){throw 'Fixture already exists. Keep it for repeatability; clean it explicitly before preparing a replacement.'}
$admin=Invoke-LocalApi $config user POST '/api/auth/login' @{email=$secrets.adminEmail;password=$secrets.adminPassword}
if($admin.role -ne 'Admin' -or !$admin.token){throw 'Admin authentication failed.'}
$prefix='ase-'+[guid]::NewGuid().ToString('N').Substring(0,12)
$data=[ordered]@{prefix=$prefix;createdUtc=[DateTime]::UtcNow.ToString('o');accounts=@();resources=@();complete=$false}
function Save-Fixture { $data|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $file -Encoding utf8 }
function Track([string]$Kind,$Id,[string]$Name){$data.resources+=@{kind=$Kind;id=$Id;name=$Name};Save-Fixture}
Save-Fixture
try {
 $genre=Invoke-LocalApi $config inventory POST '/api/Genres' @{name=$prefix} $admin.token;Track genre $genre.id $prefix
 $author=Invoke-LocalApi $config inventory POST '/api/Authors' @{name=$prefix} $admin.token;Track author $author.id $prefix
 $list=Invoke-LocalApi $config inventory POST '/api/Lists' @{key=$prefix;name=$prefix;itemType='Books'} $admin.token;Track list $prefix $prefix
 for($i=1;$i -le $Users;$i++){
  $email="$prefix-$i@example.test";$password=[guid]::NewGuid().ToString('N')+'aA!9'
  $null=Invoke-LocalApi $config user POST '/api/auth/register' @{email=$email;password=$password;firstName='ASE';lastName="Load$i"}
  # Track email immediately, even if subsequent approval fails.
  Track user $email $email
  $pending=@(Invoke-LocalApi $config user GET '/api/Admin/users/pending' $null $admin.token)
  $account=$pending|Where-Object email -eq $email|Select-Object -First 1
  if(!$account){throw 'Registered fixture account not returned by pending API.'}
  $null=Invoke-LocalApi $config user POST "/api/Admin/users/$($account.id)/accept" $null $admin.token
  $book=Invoke-LocalApi $config inventory POST '/api/Books' @{title="$prefix-$i";author=$prefix;genre=$prefix;isbn="$prefix-$i";totalCopies=3} $admin.token
  Track book $book.id "$prefix-$i"
  $data.accounts+=@{userEmail=$email;userPassword=$password;bookId=$book.id;genreId=$genre.id;genre=$prefix;authorId=$author.id;authorName=$prefix;listKey=$prefix;searchTerm=$prefix}
  Save-Fixture
  if($SeedNotifications){
   $login=Invoke-LocalApi $config user POST '/api/auth/login' @{email=$email;password=$password}
   $reservation=Invoke-LocalApi $config lending POST '/api/reservations' @{bookId=$book.id} $login.token
   $null=Invoke-LocalApi $config lending POST "/api/my-borrowings/reservations/$($reservation.id)/cancel" $null $login.token
  }
 }
 $ids=@($data.accounts|ForEach-Object{[int]$_.bookId})
 $null=Invoke-LocalApi $config inventory PUT "/api/Lists/$prefix/items" @{itemIds=$ids} $admin.token
 $data.complete=$true;Save-Fixture
 Write-Host "Prepared $Users local customer fixtures; credentials saved only to ignored accounts.private.json."
 if($SeedNotifications){Write-Host 'Notification events were queued. Verify consumer delivery before notification read-action scenarios.'}
} catch { Save-Fixture;throw 'Fixture preparation stopped; the private manifest records created resources. Check service logs and Kafka, then use cleanup-data.ps1 or reconcile partial records. Do not rerun blindly.' }
