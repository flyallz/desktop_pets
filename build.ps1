$ErrorActionPreference = 'Stop'
$sampleRoot = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$asset = Join-Path $sampleRoot 'assets\cat.png'
if (-not (Test-Path -LiteralPath $asset)) { throw 'Missing assets/cat.png' }
$output = Join-Path $sampleRoot '发布包'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$references = @('System.dll', 'System.Core.dll', 'System.Net.Http.dll', 'System.Web.Extensions.dll', 'System.Security.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll', 'System.Xaml.dll')
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001', ('/out:' + (Join-Path $output '猫咪桌宠.exe')), ('/resource:' + $asset + ',PhotoCat.cat.png'), ('/win32manifest:' + (Join-Path $sampleRoot 'src\app.manifest')))
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $framework $reference) }
foreach ($pose in @('stretch', 'rest', 'sleep')) {
    $photo = Join-Path $sampleRoot ('assets\cat-' + $pose + '.png')
    if (-not (Test-Path -LiteralPath $photo)) { throw ('Missing posture photo: ' + $photo) }
    $arguments += '/resource:' + $photo + ',PhotoCat.cat-' + $pose + '.png'
}
foreach ($frame in 0..7) {
    $photo = Join-Path $sampleRoot ('assets\cat-walk-' + $frame + '.png')
    if (-not (Test-Path -LiteralPath $photo)) { throw ('Missing walking frame: ' + $photo) }
    $arguments += '/resource:' + $photo + ',PhotoCat.cat-walk-' + $frame + '.png'
}
foreach ($photo in Get-ChildItem -LiteralPath (Join-Path $sampleRoot 'assets\looks') -Filter '*.png' -ErrorAction SilentlyContinue) {
    $arguments += '/resource:' + $photo.FullName + ',PhotoCat.looks.' + $photo.Name
}
$spriteAtlas = Join-Path $sampleRoot 'assets\sprites\cat-spritesheet.png'
if (-not (Test-Path -LiteralPath $spriteAtlas)) { throw 'Missing cat sprite atlas' }
$arguments += '/resource:' + $spriteAtlas + ',PhotoCat.sprites.cat-spritesheet.png'
foreach ($name in @('idle','walk-right','walk-left','wave','stretch','groom','look','lie-down','sleep')) {
    $strip = Join-Path $sampleRoot ('assets\sprites\smooth\' + $name + '.png')
    if (-not (Test-Path -LiteralPath $strip)) { throw ('Missing smooth animation: ' + $strip) }
    $arguments += '/resource:' + $strip + ',PhotoCat.sprites.smooth.' + $name + '.png'
}
$arguments += '/resource:' + (Join-Path $sampleRoot 'src\PhotoEditor.xaml') + ',PhotoCat.PhotoEditor.xaml'
$photoTools = Join-Path $sampleRoot '.build\photo-tools.zip'
if (Test-Path -LiteralPath $photoTools) {
    $arguments += '/resource:' + $photoTools + ',PhotoCat.photo-tools.zip'
    $arguments += '/resource:' + (Join-Path $sampleRoot '.build\photo-tools.sha256') + ',PhotoCat.photo-tools.sha256'
}
$arguments += '/win32icon:'  + (Join-Path $sampleRoot 'assets\cat.ico')
$arguments += Join-Path $sampleRoot 'src\CatPet.cs'
$arguments += Join-Path $sampleRoot 'src\PhotoMotion.cs'
foreach ($source in @('UserPhotoStore', 'PhotoCustomization', 'PhotoEditPixels', 'LocalPhotoCutout', 'PhotoEditorWindow', 'UserPhotoChecks', 'IdlePhotoMotion', 'PetPreferences', 'DeepSeekChat', 'ChatWindows', 'PetCompanion', 'CompanionChecks', 'PhotoMotionChecks', 'SpriteAnimation', 'SpriteChecks')) {
    $arguments += Join-Path $sampleRoot ('src\' + $source + '.cs')
}
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output (Join-Path $output '猫咪桌宠.exe')
