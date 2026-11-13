Add-Type -AssemblyName System.Drawing
$b1 = New-Object System.Drawing.Bitmap('Assets/Bos4/ImageBos4/C1Ing.png')
$b2 = New-Object System.Drawing.Bitmap('Assets/Bos4/ImageBos4/C2Ing.png')
$b3 = New-Object System.Drawing.Bitmap('Assets/Bos4/ImageBos4/C3Ing.png')
Write-Output "b1: $($b1.Width)x$($b1.Height), b2: $($b2.Width)x$($b2.Height), b3: $($b3.Width)x$($b3.Height)"

$diffCount = 0
for ($y = 0; $y -lt $b1.Height; $y += 5) {
    for ($x = 0; $x -lt $b1.Width; $x += 5) {
        $p1 = $b1.GetPixel($x, $y)
        $p2 = $b2.GetPixel($x, $y)
        if ($p1 -ne $p2) { $diffCount++ }
    }
}
Write-Output "Sampled diff count between C1Ing and C2Ing: $diffCount"

$diffCount13 = 0
for ($y = 0; $y -lt $b1.Height; $y += 5) {
    for ($x = 0; $x -lt $b1.Width; $x += 5) {
        $p1 = $b1.GetPixel($x, $y)
        $p3 = $b3.GetPixel($x, $y)
        if ($p1 -ne $p3) { $diffCount13++ }
    }
}
Write-Output "Sampled diff count between C1Ing and C3Ing: $diffCount13"
