Add-Type -AssemblyName System.Drawing
[Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime] | Out-Null
[Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType = WindowsRuntime] | Out-Null
[Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime] | Out-Null

$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()

function Get-OcrText($filePath) {
    $fullPath = [System.IO.Path]::GetFullPath($filePath)
    $asyncOp = [Windows.Storage.StorageFile]::GetFileFromPathAsync($fullPath)
    $file = $asyncOp.GetResults()
    if (-not $file) {
        $file = $asyncOp.AsTask().GetAwaiter().GetResult()
    }
    $streamAsync = $file.OpenAsync([Windows.Storage.FileAccessMode]::Read)
    $stream = $streamAsync.GetResults()
    if (-not $stream) { $stream = $streamAsync.AsTask().GetAwaiter().GetResult() }
    
    $decoderAsync = [Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)
    $decoder = $decoderAsync.GetResults()
    if (-not $decoder) { $decoder = $decoderAsync.AsTask().GetAwaiter().GetResult() }
    
    $bmpAsync = $decoder.GetSoftwareBitmapAsync()
    $bitmap = $bmpAsync.GetResults()
    if (-not $bitmap) { $bitmap = $bmpAsync.AsTask().GetAwaiter().GetResult() }
    
    $ocrAsync = $engine.RecognizeAsync($bitmap)
    $result = $ocrAsync.GetResults()
    if (-not $result) { $result = $ocrAsync.AsTask().GetAwaiter().GetResult() }
    
    return $result.Text
}

foreach ($img in @('Assets\Bos4\ImageBos4\C1.png', 'Assets\Bos4\ImageBos4\C1Ing.png', 'Assets\Bos4\ImageBos4\C2Ing.png', 'Assets\Bos4\ImageBos4\C3Ing.png')) {
    Write-Output "=== $img ==="
    try {
        $txt = Get-OcrText $img
        Write-Output $txt
    } catch {
        Write-Output "Error: $_"
    }
}
