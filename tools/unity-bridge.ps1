# Клиент TCP-моста Kitchen Designer (UnityTcpBridge, localhost:9337).
# Шлёт одну команду JSON-строкой и печатает ответ. Работает и с запущенным
# приложением (Build/KitchenDesigner.exe), и с Unity-редактором в Play Mode.
#
# Примеры:
#   .\tools\unity-bridge.ps1 ping
#   .\tools\unity-bridge.ps1 get_all_elements
#   .\tools\unity-bridge.ps1 snap_diagnose '{"name":"Board_1"}'
#   .\tools\unity-bridge.ps1 get_console_logs '{"count":100}'
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Method,
    [Parameter(Position = 1)][string]$Params = "{}",
    [int]$Port = 9337,
    [int]$TimeoutMs = 15000
)

$client = New-Object System.Net.Sockets.TcpClient
try {
    $connect = $client.BeginConnect("127.0.0.1", $Port, $null, $null)
    if (-not $connect.AsyncWaitHandle.WaitOne(3000)) {
        Write-Error "Не удалось подключиться к порту $Port — приложение запущено?"
        exit 1
    }
    $client.EndConnect($connect)
    $stream = $client.GetStream()

    $req = @{ id = [guid]::NewGuid().ToString(); method = $Method; parameters = $Params } |
        ConvertTo-Json -Compress
    $data = [Text.Encoding]::UTF8.GetBytes($req + "`n")
    $stream.Write($data, 0, $data.Length)
    $stream.Flush()

    # Читаем до первого перевода строки (ответ — одна JSON-строка).
    $stream.ReadTimeout = $TimeoutMs
    $buffer = New-Object byte[] 65536
    $response = ""
    while ($response -notmatch "`n") {
        $read = $stream.Read($buffer, 0, $buffer.Length)
        if ($read -le 0) { break }
        $response += [Text.Encoding]::UTF8.GetString($buffer, 0, $read)
    }

    $line = ($response -split "`n")[0].Trim()
    if (-not $line) { Write-Error "Пустой ответ от моста"; exit 1 }

    # Красивый вывод: разворачиваем JSON с отступами.
    $line | ConvertFrom-Json | ConvertTo-Json -Depth 10
}
finally {
    $client.Close()
}
