[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ApiBaseUrl,
    [string] $AccessToken = $env:COACHHUB_ACCESS_TOKEN,
    [Parameter(Mandatory = $true)] [string] $MediaRoot,
    [string] $ExportRoot = (Join-Path $PSScriptRoot '..\..\data\legacy-catalog'),
    [string] $ReceiptPath = (Join-Path $PSScriptRoot '..\..\artifacts\migration\legacy-catalog-receipt.json')
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($AccessToken)) { throw 'Set COACHHUB_ACCESS_TOKEN or pass -AccessToken.' }
$ApiBaseUrl = $ApiBaseUrl.TrimEnd('/')
$MediaRoot = [IO.Path]::GetFullPath($MediaRoot)
$ExportRoot = [IO.Path]::GetFullPath($ExportRoot)
$ReceiptPath = [IO.Path]::GetFullPath($ReceiptPath)
Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $AccessToken)
$client.Timeout = [TimeSpan]::FromMinutes(5)
$utf8 = New-Object Text.UTF8Encoding($false)

function Save-Receipt($receipt) {
    $parent=[IO.Path]::GetDirectoryName($ReceiptPath); [IO.Directory]::CreateDirectory($parent) | Out-Null
    [IO.File]::WriteAllText($ReceiptPath, (($receipt | ConvertTo-Json -Depth 10) + [Environment]::NewLine), $utf8)
}
function Send-Json([string] $url, $body) {
    $json=ConvertTo-Json -InputObject $body -Depth 10 -Compress
    $content=New-Object System.Net.Http.StringContent($json,$utf8,'application/json')
    try {
        $response=$client.PostAsync($url,$content).GetAwaiter().GetResult()
        $text=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if(-not $response.IsSuccessStatusCode){throw "HTTP $([int]$response.StatusCode) from $url`: $text"}
        return $text | ConvertFrom-Json
    } finally { $content.Dispose() }
}
function Content-Type([string] $name) {
    switch([IO.Path]::GetExtension($name).ToLowerInvariant()) { '.gif'{'image/gif'} '.png'{'image/png'} '.webp'{'image/webp'} '.jfif'{'image/jpeg'} '.jpeg'{'image/jpeg'} default{'image/jpeg'} }
}
function Upload-Media([string] $folder, [string] $imagePath) {
    $name=[IO.Path]::GetFileName($imagePath.Replace('/','\')); $path=Join-Path (Join-Path $MediaRoot $folder) $name
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)){throw "Media file not found: $path"}
    $multipart=New-Object System.Net.Http.MultipartFormDataContent
    $stream=[IO.File]::OpenRead($path); $fileContent=New-Object System.Net.Http.StreamContent($stream)
    $fileContent.Headers.ContentType=New-Object System.Net.Http.Headers.MediaTypeHeaderValue((Content-Type $name))
    $multipart.Add($fileContent,'file',$name)
    try {
        $response=$client.PostAsync("$ApiBaseUrl/api/media",$multipart).GetAwaiter().GetResult(); $text=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if(-not $response.IsSuccessStatusCode){throw "Media upload failed for $name`: HTTP $([int]$response.StatusCode) $text"}
        return ($text | ConvertFrom-Json).id
    } finally { $multipart.Dispose(); $fileContent.Dispose(); $stream.Dispose() }
}

$receipt=[ordered]@{schemaVersion=1;startedAtUtc=[DateTimeOffset]::UtcNow.ToString('O');apiBaseUrl=$ApiBaseUrl;items=@()}
if(Test-Path -LiteralPath $ReceiptPath){$existing=Get-Content -Raw -Encoding utf8 -LiteralPath $ReceiptPath | ConvertFrom-Json; foreach($item in $existing.items){$receipt.items += $item}}
foreach($catalog in @([ordered]@{name='food';file='foods.import.json';folder='FoodItems';endpoint='api/nutrition/foods/legacy-import'},[ordered]@{name='exercise';file='exercises.import.json';folder='Exercises';endpoint='api/training/exercises/legacy-import'})) {
    $rows=Get-Content -Raw -Encoding utf8 -LiteralPath (Join-Path $ExportRoot $catalog.file) | ConvertFrom-Json
    foreach($row in $rows) {
        if(@($receipt.items | Where-Object {$_.catalog -eq $catalog.name -and $_.legacyId -eq $row.legacyId -and $_.status -in @('Imported','AlreadyImported')}).Count -gt 0){continue}
        if($null -ne $row.imagePath -and -not [string]::IsNullOrWhiteSpace([string]$row.imagePath)){$row.mediaId=Upload-Media $catalog.folder $row.imagePath}
        $result=Send-Json "$ApiBaseUrl/$($catalog.endpoint)" (, $row)
        $outcome=$result.rows[0]
        $receipt.items += [ordered]@{catalog=$catalog.name;legacyId=[int]$row.legacyId;status=[string]$outcome.status;targetId=if($catalog.name -eq 'food'){$outcome.foodItemId}else{$outcome.exerciseId};mediaId=$row.mediaId;messages=@($outcome.messages)}
        Save-Receipt $receipt
        if($outcome.status -eq 'Invalid'){throw "$($catalog.name) $($row.legacyId) was rejected: $($outcome.messages -join '; ')"}
    }
}
$receipt.completedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')
Save-Receipt $receipt
$foods=@($receipt.items|Where-Object{$_.catalog -eq 'food' -and $_.status -in @('Imported','AlreadyImported')}).Count
$exercises=@($receipt.items|Where-Object{$_.catalog -eq 'exercise' -and $_.status -in @('Imported','AlreadyImported')}).Count
Write-Output "Migration completed: $foods foods and $exercises exercises recorded in $ReceiptPath."
$client.Dispose()
