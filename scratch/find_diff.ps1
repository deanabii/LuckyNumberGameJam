Add-Type -AssemblyName System.Drawing
$b1 = New-Object System.Drawing.Bitmap('Assets/Bos4/ImageBos4/C1Ing.png')
$b2 = New-Object System.Drawing.Bitmap('Assets/Bos4/ImageBos4/C2Ing.png')

$minX = 9999; $maxX = 0; $minY = 9999; $maxY = 0

for ($y = 0; $y -lt $b1.Height; $y += 2) {
    for ($x = 0; $x -lt $b1.Width; $x += 2) {
        $p1 = $b1.GetPixel($x, $y)
        $p2 = $b2.GetPixel($x, $y)
        if ($p1 -ne $p2) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

Write-Output "Diff Bounding Box: X=[$minX, $maxX], Y=[$minY, $maxY]"

# Crop the difference region and save to scratch
$cropW = $maxX - $minX + 1
$cropH = $maxY - $minY + 1
$crop1 = New-Object System.Drawing.Bitmap($cropW, $cropH)
$crop2 = New-Object System.Drawing.Bitmap($cropW, $cropH)
$g1 = [System.Drawing.Graphics]::FromImage($crop1)
$g2 = [System.Drawing.Graphics]::FromImage($crop2)
$rect = New-Object System.Drawing.Rectangle($minX, $minY, $cropW, $cropH)
$destRect = New-Object System.Drawing.Rectangle(0, 0, $cropW, $cropH)
$g1.DrawImage($b1, $destRect, $rect, [System.Drawing.GraphicsUnit]::Pixel)
$g2.DrawImage($b2, $destRect, $rect, [System.Drawing.GraphicsUnit]::Pixel)
$crop1.Save('scratch/diff_c1.png')
$crop2.Save('scratch/diff_c2.png')
Write-Output "Saved diff_c1.png and diff_c2.png"
