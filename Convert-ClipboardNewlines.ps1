$ErrorActionPreference = 'Stop'

$text = Get-Clipboard -Raw
if ($null -eq $text) {
    throw 'クリップボードにテキストがありません。'
}

$originalLineBreaks = [regex]::Matches($text, "`r`n|`r|`n").Count
$converted = [regex]::Replace($text, "`r`n|`r|`n", "`r`n")
Set-Clipboard -Value $converted

Write-Output "クリップボードの改行をWindows形式（CRLF）に統一しました。改行数: $originalLineBreaks"
