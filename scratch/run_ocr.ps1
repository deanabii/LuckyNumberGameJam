Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime] | Out-Null
[Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType = WindowsRuntime] | Out-Null
[Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime] | Out-Null

$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()

function Await-Task($asyncOperation) {
    $asTaskGeneric = [System.WindowsRuntimeSystemExtensions].GetMethods() | 
        Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -like 'IAsyncOperation*' } | 
        Select-Object -First 1
    
    # Return type of operation
    $interfaceType = $asyncOperation.GetType().GetInterfaces() | Where-Object { $_.Name -like 'IAsyncOperation`1' } | Select-Object -First 1
    if ($interfaceType) {
        $resultType = $interfaceType.GetGenericArguments()[0]
        $asTaskSpecific = $asTaskGeneric.MakeGenericMethod($resultType)
        $task = $asTaskSpecific.Invoke($null, @($asyncOperation))
        $task.Wait()
        return $task.Result
    }
    return $null
}

function Get-OcrText($filePath) {
    $fullPath = [System.IO.Path]::GetFullPath($filePath)
    $fileOp = [Windows.Storage.StorageFile]::GetFileFromPathAsync($fullPath)
    $file = Await-Task $fileOp
    
    $streamOp = $file.OpenAsync([Windows.Storage.FileAccessMode]::Read)
    $stream = Await-Task $streamOp
    
    $decoderOp = [Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)
    $decoder = Await-Task $decoderOp
    
    $bmpOp = $decoder.GetSoftwareBitmapAsync()
    $bitmap = Await-Task $bmpOp
    
    $ocrOp = $engine.RecognizeAsync($bitmap)
    $result = Await-Task $ocrOp
    
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
