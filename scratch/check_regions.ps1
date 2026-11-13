Add-Type -AssemblyName System.Drawing
$b1 = New-Object System.Drawing.Bitmap('Assets/Bos4/ImageBos4/C1Ing.png')
$b2 = New-Object System.Drawing.Bitmap('Assets/Bos4/ImageBos4/C2Ing.png')
$b3 = New-Object System.Drawing.Bitmap('Assets/Bos4/ImageBos4/C3Ing.png')

$lines = @(
    @{ Name="Title"; Y1=50; Y2=200 },
    @{ Name="Item 1"; Y1=200; Y2=420 },
    @{ Name="Item 2"; Y1=420; Y2=680 },
    @{ Name="Item 3"; Y1=680; Y2=930 },
    @{ Name="Item 4"; Y1=930; Y2=1250 }
)

foreach ($l in $lines) {
    $diff12 = 0; $diff13 = 0
    for ($y = $l.Y1; $y -lt $l.Y2; $y += 3) {
        for ($x = 250; $x -lt 950; $x += 3) {
            if ($b1.GetPixel($x, $y) -ne $b2.GetPixel($x, $y)) { $diff12++ }
            if ($b1.GetPixel($x, $y) -ne $b3.GetPixel($x, $y)) { $diff13++ }
        }
    }
    Write-Output "$($l.Name): diff(C1, C2)=$diff12, diff(C1, C3)=$diff13"
}
