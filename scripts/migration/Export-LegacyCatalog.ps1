[CmdletBinding()]
param(
    [string] $ConnectionString = $env:LEGACY_GYM_CONNECTION_STRING,
    [Parameter(Mandatory = $true)] [string] $MediaRoot,
    [string] $OutputRoot = (Join-Path $PSScriptRoot '..\..\data\legacy-catalog')
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ConnectionString)) { throw 'Set LEGACY_GYM_CONNECTION_STRING or pass -ConnectionString.' }
$MediaRoot = [IO.Path]::GetFullPath($MediaRoot)
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

function Read-Table([string] $query) {
    $table = New-Object System.Data.DataTable
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $query
        $reader = $command.ExecuteReader()
        $table.Load($reader)
    } finally { $connection.Dispose() }
    return ,$table
}
function Write-Json([string] $path, $value) {
    $json = $value | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText($path, $json + [Environment]::NewLine, (New-Object Text.UTF8Encoding($false)))
}
function Media-Info([string] $folder, [object] $imagePath) {
    if ($imagePath -is [DBNull] -or [string]::IsNullOrWhiteSpace([string]$imagePath)) { return $null }
    $name = [IO.Path]::GetFileName(([string]$imagePath).Replace('/', '\'))
    $path = Join-Path (Join-Path $MediaRoot $folder) $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return [ordered]@{ fileName=$name; exists=$false; sha256=$null; bytes=0 } }
    $file = Get-Item -LiteralPath $path
    return [ordered]@{ fileName=$name; exists=$true; sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant(); bytes=$file.Length }
}

$english = @{
  1='Egg Whites';2='Whole Egg';3='Potatoes (Cooked)';4='Cucumber';5='Lean Minced Beef';6='Chicken Breast (Cooked)';7='Rice (Cooked)';8='Green Salad';9='Banana';10='Watermelon';11='Peanuts';13='Almonds';15='Salmon';16='Semi-Skimmed Milk';17='Brown Toast';18='Mushrooms';19='Zucchini';20='Olive Oil';21='Grapes';22='Apple';23='Plain Greek Yogurt (Juhayna)';24='Cheddar Cheese Slices (President)';25='Walnuts';26='Green Beans';27='Red Beans';28='Cashews';29='Avocado';30='Broccoli';31='Chia Seeds';32='Tomatoes';33='Onions';34='Peas (Cooked)';35='Oat Pasta (Cooked)';36='Pasta (Cooked)';37='Molokhia';38='Philadelphia Light Cheese';39='Turkey Slices';40='Beef Cubes (Cooked)';41='Medium Tortilla Bread (50 g)';42='Mango';43='Cantaloupe';44='Pineapple';45='Soybeans (Cooked)';46='Tilapia (Cooked)';47='Lentils (Cooked)';48='Almond Milk';49='Lupini Beans (Cooked)';50='Light Tuna (Sunshine)';51='Strawberry';52='Whey Protein';53='Coffee';54='Soy Milk';55='Cooked Fava Beans';57='Coconut Oil';58='Peanut Butter';59='Whole Milk';60='Full-Fat Yogurt';61='Beetroot';62='Brown Rice (Cooked)';63='Okra (Cooked)';64='Baladi Bread';65='Yellow Cherries';66='Carrots';67='Spinach';68='Eggplant';69='Beef Liver (Cooked)';70='Shrimp';71='Whole-Grain White Flour';72='Celery';73='Parsley';74='Dates';75='Pear';76='Sweet Potato (Raw)';77='Green Bell Pepper';78='Lettuce';79='Kiwi';80='Pomegranate';81='Orange';82='Honey';83='Butter';84='Sardines';85='Guava';86='Cottage Cheese';87='Raisins';88='Light Mayonnaise';89='PAM Cooking Spray';90='Blueberries';91='Oats';1091='Celery and Parsley';1092='Sesame Seeds';1093='Cream of Rice'
}
$foodCategories = @{
  Protein=@(1,2,5,6,15,27,39,40,45,46,47,49,50,55,69,70,84,86)
  Carbohydrates=@(3,7,17,35,36,41,52,62,64,71,74,76,82,91,1093)
  Fats=@(11,13,20,24,25,28,29,31,38,57,58,83,88,89,1092)
  Fruit=@(9,10,21,22,42,43,44,51,65,75,79,80,81,85,87,90)
  Vegetables=@(4,8,18,19,26,30,32,33,34,37,61,63,66,67,68,72,73,77,78,1091)
  Dairy=@(16,23,48,54,59,60)
  Beverages=@(53)
}
function Food-Category([int] $id) { foreach($entry in $foodCategories.GetEnumerator()){if($entry.Value -contains $id){return $entry.Key}}; return 'Uncategorized' }
function Exercise-Category([int] $id) {
    if ($id -in @(1,9) -or ($id -ge 1003 -and $id -le 1018)) { return 'Biceps' }
    if ($id -ge 1019 -and $id -le 1035) { return 'Triceps' }
    if ($id -ge 1036 -and $id -le 1060) { return 'Chest' }
    if ($id -ge 1061 -and $id -le 1090) { return 'Back' }
    if ($id -ge 1091 -and $id -le 1120) { return 'Lower Body' }
    if ($id -ge 1121 -and $id -le 1135) { return 'Shoulders' }
    if ($id -ge 1136 -and $id -le 1140) { return 'Glutes' }
    if ($id -in @(1141,1142,1143,1144,1145,1146,1147,1156)) { return 'Cardio' }
    if ($id -in @(1149,1155)) { return 'Forearms' }
    if ($id -ge 1148 -and $id -le 1159) { return 'Core' }
    return 'Uncategorized'
}
function Normalize-YouTube([object] $value, [int] $id, [System.Collections.Generic.List[object]] $corrections) {
    if ($value -is [DBNull] -or [string]::IsNullOrWhiteSpace([string]$value)) { return $null }
    $url = ([string]$value).Trim()
    $second = $url.IndexOf('https://', 8, [StringComparison]::OrdinalIgnoreCase)
    if ($second -gt 0) { $fixed=$url.Substring(0,$second); $corrections.Add([ordered]@{catalog='exercise';legacyId=$id;field='youTubeLink';from=$url;to=$fixed;reason='Removed a duplicated concatenated URL.'}); return $fixed }
    return $url
}

$foodsTable = Read-Table 'SELECT Id, Name, Unit, CaloriesPer100Units, ProteinPer100Units, CarbsPer100Units, FatPer100Units, ImagePath FROM FoodItems ORDER BY Id'
$exercisesTable = Read-Table 'SELECT Id, Name, YouTubeLink, ImagePath FROM Exercises ORDER BY Id'
$operationsTable = Read-Table "SELECT t.name, SUM(p.rows) [Rows] FROM sys.tables t JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN (0,1) WHERE t.name NOT IN ('FoodItems','Exercises','__EFMigrationsHistory') GROUP BY t.name ORDER BY t.name"
$errors = New-Object 'System.Collections.Generic.List[object]'
$warnings = New-Object 'System.Collections.Generic.List[object]'
$corrections = New-Object 'System.Collections.Generic.List[object]'
$foods = @()
foreach($row in $foodsTable.Rows) {
    $id=[int]$row.Id; if(-not $english.ContainsKey($id)){ $errors.Add([ordered]@{catalog='food';legacyId=$id;message='Missing curated English name.'}); continue }
    $protein=[decimal]$row.ProteinPer100Units
    if($id -eq 38){$corrections.Add([ordered]@{catalog='food';legacyId=$id;field='proteinPer100Units';from=$protein;to=6.28;reason='628 g protein per 100 g is impossible; restored the evident missing decimal separator.'});$protein=6.28}
    $media=Media-Info 'FoodItems' $row.ImagePath
    if($null -ne $media -and -not $media.exists){$errors.Add([ordered]@{catalog='food';legacyId=$id;message="Missing media file $($media.fileName)."})}
    $foods += [ordered]@{ legacyId=$id; name=$english[$id]; unit=([string]$row.Unit).Trim().ToLowerInvariant(); caloriesPer100Units=[decimal]$row.CaloriesPer100Units; proteinPer100Units=$protein; carbsPer100Units=[decimal]$row.CarbsPer100Units; fatPer100Units=[decimal]$row.FatPer100Units; imagePath=if($row.ImagePath -is [DBNull]){$null}else{[string]$row.ImagePath}; mediaId=$null; nameAr=([string]$row.Name).Trim(); categoryName=(Food-Category $id); media=$media }
}
$exercises = @()
foreach($row in $exercisesTable.Rows) {
    $id=[int]$row.Id; $media=Media-Info 'Exercises' $row.ImagePath
    if($null -ne $media -and -not $media.exists){$errors.Add([ordered]@{catalog='exercise';legacyId=$id;message="Missing media file $($media.fileName)."})}
    $url=Normalize-YouTube $row.YouTubeLink $id $corrections
    if($null -ne $url){$uri=$null;if(-not [Uri]::TryCreate($url,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -ne 'https' -or $uri.Host -notin @('youtube.com','www.youtube.com','youtu.be')){$errors.Add([ordered]@{catalog='exercise';legacyId=$id;message='Invalid YouTube URL.'})}}
    $exercises += [ordered]@{ legacyId=$id; name=([string]$row.Name).Trim(); youTubeLink=$url; imagePath=if($row.ImagePath -is [DBNull]){$null}else{[string]$row.ImagePath}; mediaId=$null; nameAr=$null; categoryName=(Exercise-Category $id); media=$media }
}
$foodPayload = @($foods | ForEach-Object { [ordered]@{legacyId=$_.legacyId;name=$_.name;unit=$_.unit;caloriesPer100Units=$_.caloriesPer100Units;proteinPer100Units=$_.proteinPer100Units;carbsPer100Units=$_.carbsPer100Units;fatPer100Units=$_.fatPer100Units;imagePath=$_.imagePath;mediaId=$null;nameAr=$_.nameAr;categoryName=$_.categoryName} })
$exercisePayload = @($exercises | ForEach-Object { [ordered]@{legacyId=$_.legacyId;name=$_.name;youTubeLink=$_.youTubeLink;imagePath=$_.imagePath;mediaId=$null;nameAr=$_.nameAr;categoryName=$_.categoryName} })
$manifest = [ordered]@{schemaVersion=1;exportedAtUtc=[DateTimeOffset]::UtcNow.ToString('O');sourceDatabase='GYM';foodsCount=$foods.Count;exercisesCount=$exercises.Count;foodMediaCount=@($foods|Where-Object{$null-ne$_.media}).Count;exerciseMediaCount=@($exercises|Where-Object{$null-ne$_.media}).Count;excludedOperationalTables=@($operationsTable.Rows|ForEach-Object{[ordered]@{name=[string]$_.name;rows=[long]$_.Rows}});categoryPolicy='Curated deterministic mappings; unmatched records use Uncategorized.';errors=[object[]]$errors;warnings=[object[]]$warnings;corrections=[object[]]$corrections;foods=$foods;exercises=$exercises}
Write-Json (Join-Path $OutputRoot 'foods.import.json') $foodPayload
Write-Json (Join-Path $OutputRoot 'exercises.import.json') $exercisePayload
Write-Json (Join-Path $OutputRoot 'manifest.json') $manifest
Write-Output "Exported $($foods.Count) foods and $($exercises.Count) exercises. Errors: $($errors.Count). Corrections: $($corrections.Count)."
if($errors.Count -gt 0){exit 2}
