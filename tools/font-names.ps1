Add-Type -AssemblyName PresentationCore
foreach ($f in @('Italiana-Regular.ttf', 'Marcellus-Regular.ttf')) {
    $p = (Resolve-Path $f).Path
    $gt = New-Object System.Windows.Media.GlyphTypeface($p)
    Write-Output ($f + ' => ' + ($gt.FamilyNames.Values -join ', '))
}
