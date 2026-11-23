$origem  = "C:\Users\house\source\repos\ContaMobi"
$destino = "C:\Backups\ContaMobi_Win_Backup_$(Get-Date -Format 'yyyyMMdd_HHmm').zip"

New-Item -ItemType Directory -Force -Path "C:\Backups" | Out-Null

# Copia para uma pasta temporária ignorando bin, obj e .vs
$temp = "$env:TEMP\ContaMobi_Backup_Tmp"
if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
robocopy $origem $temp /E /XD bin obj .vs .git /NFL /NDL /NJH /NJS | Out-Null

# Compacta e limpa a temporária
Compress-Archive -Path "$temp\*" -DestinationPath $destino -Force
Remove-Item $temp -Recurse -Force

Write-Host "Backup criado em: $destino" -ForegroundColor Green