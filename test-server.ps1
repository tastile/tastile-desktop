# Mock Tastile API Server for Desktop Testing
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:3140/")
$listener.Start()

Write-Host "Mock Tastile API Server on http://localhost:3140/"
Write-Host "Press Ctrl+C to stop"

$script:tiles = @(
    @{ id = "tile-001"; title = "Design API"; lifecycle = "ready"; next_action = "Draw diagram"; done_definition = "Reviewed"; worked_minutes = 0 },
    @{ id = "tile-002"; title = "Implement core"; lifecycle = "started"; next_action = "Write tests"; done_definition = "Passing"; worked_minutes = 45 },
    @{ id = "tile-003"; title = "Setup CI"; lifecycle = "ready"; next_action = "Config actions"; done_definition = "Deployed"; worked_minutes = 0 }
)
$script:activeId = "tile-002"
$script:phase = "work"
$script:phaseStart = [DateTime]::UtcNow.AddMinutes(-45).ToString("o")

function ToJson($obj) { $obj | ConvertTo-Json -Depth 10 -Compress }

while ($listener.IsListening) {
    $ctx = $listener.GetContext()
    $req = $ctx.Request
    $res = $ctx.Response
    $path = $req.Url.PathAndQuery
    
    switch ($path) {
        "/health" { 
            $content = '{"status":"ok"}' 
            $res.StatusCode = 200
        }
        "/status" {
            $content = ToJson @{ status = "running"; version = "0.1.0"; active_tile_id = $script:activeId; phase_kind = $script:phase; phase_started_at = $script:phaseStart; tile_count = $script:tiles.Count }
            $res.StatusCode = 200
        }
        "/read/tiles" {
            $content = ToJson @{ tiles = $script:tiles }
            $res.StatusCode = 200
        }
        "/read/active-tile" {
            $tile = $script:tiles | Where { $_.id -eq $script:activeId }
            $content = ToJson @{ tile = $tile; phase = $script:phase; phase_started_at = $script:phaseStart }
            $res.StatusCode = 200
        }
        "/read/execution" {
            $content = ToJson @{ active_tile_id = $script:activeId; phase_kind = $script:phase; phase_started_at = $script:phaseStart; phase_ends_at = $null }
            $res.StatusCode = 200
        }
        default {
            if ($path.StartsWith("/commands")) {
                $res.StatusCode = 200
                $content = '{"ok":true,"events":[]}'
            } else {
                $res.StatusCode = 404
                $content = '{"error":"not found"}'
            }
        }
    }
    
    $buffer = [System.Text.Encoding]::UTF8.GetBytes($content)
    $res.ContentType = "application/json"
    $res.ContentLength64 = $buffer.Length
    $res.OutputStream.Write($buffer, 0, $buffer.Length)
    $res.Close()
}

$listener.Stop()
