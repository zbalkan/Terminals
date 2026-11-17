$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $root 'Source'
$projectFiles = @(Get-ChildItem -Path $sourceRoot -Recurse -File -Filter '*.csproj')
$projects = @($projectFiles | ForEach-Object {
    [pscustomobject]@{
        Directory = $_.DirectoryName
        Content = Get-Content -LiteralPath $_.FullName -Raw
    }
})

# Check components with direct WinForms bases and the custom visual bases in this repository.
# Data classes, render helpers and runtime-generated ToolStripMenuItems
# are not stand-alone design-surface controls; only compiled sources are checked.
$visualBases = 'Form|UserControl|ContainerControl|Control|Panel|Button|TextBox|ComboBox|ToolStripContainer|TabControlItem|BaseStyledPanel'
$declaration = '(?m)\bclass\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?:global::)?(?:System\.Windows\.Forms\.)?(?:' + $visualBases + ')\b'
$problems = New-Object System.Collections.Generic.List[string]
$checked = 0

foreach ($file in Get-ChildItem -Path $sourceRoot -Recurse -File -Filter '*.cs') {
    $fileName = $file.FullName
    if ($fileName -match '[\\/](?:Tests|obj|bin)[\\/]' -or $fileName -match '(?i)\.designer\.cs$') {
        continue
    }
    $text = Get-Content -LiteralPath $fileName -Raw
    if (-not [regex]::IsMatch($text, $declaration)) {
        continue
    }
    $project = $projects |
        Where-Object { $fileName.StartsWith($_.Directory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object { $_.Directory.Length } -Descending |
        Select-Object -First 1
    if ($null -eq $project) {
        continue
    }
    $relativeSource = [IO.Path]::GetRelativePath($project.Directory, $fileName).Replace('/', '\')
    if ($project.Content -notmatch ('<Compile\s+Include="' + [regex]::Escape($relativeSource) + '"')) {
        # Historical sources excluded from current projects are not migration targets.
        continue
    }
    $checked++
    $designerPath = $fileName.Substring(0, $fileName.Length - 3) + '.Designer.cs'
    if (-not (Test-Path -LiteralPath $designerPath -PathType Leaf)) {
        $problems.Add("Missing Designer file: $fileName")
        continue
    }
    $designerText = Get-Content -LiteralPath $designerPath -Raw
    if ($designerText -notmatch '\bvoid\s+InitializeComponent\s*\(') {
        $problems.Add("Designer file has no InitializeComponent: $designerPath")
    }
    if ($text -match '\bvoid\s+InitializeComponent\s*\(') {
        $problems.Add("UI initialization remains in implementation file: $fileName")
    }
    $relativeDesigner = [IO.Path]::GetRelativePath($project.Directory, $designerPath).Replace('/', '\')
    if ($project.Content -notmatch ('<Compile\s+Include="' + [regex]::Escape($relativeDesigner) + '"')) {
        $problems.Add("Designer file not registered in project: $designerPath")
    }
}

if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Warning $_ }
    throw "WinForms Designer ownership check failed ($($problems.Count) issues)"
}
Write-Host "Verified Designer ownership and project registration for $checked direct WinForms component classes."
