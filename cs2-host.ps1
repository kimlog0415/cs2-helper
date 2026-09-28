Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()

$csgo     = 'C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo'
$cfgDir   = Join-Path $csgo 'cfg'
$mapsDir  = Join-Path $csgo 'maps'
$logFile  = Join-Path $csgo 'console.log'
$settings = Join-Path $PSScriptRoot 'settings.json'
$marker   = '// CS2 방장 도우미가 만든 파일'

# 게임 안 '연습' 탭과 같은 이름/순서
$classicMaps = [ordered]@{
    de_cache = '무기창고'; de_anubis = '아누비스'; de_inferno = '인페르노'; de_mirage = '신기루'
    de_dust2 = '더스트2'; de_nuke = '뉴크'; de_ancient = '고대'; de_train = '열차'
    de_vertigo = '버티고'; de_overpass = '오버패스'; de_boulder = '볼더'; de_fachwerk = '파흐베르크'
    cs_shelter = '쉘터'; cs_office = '사무실'; cs_italy = '이탈리아'
}
$retakeMaps = [ordered]@{}
@($classicMaps.Keys)[0..9] | ForEach-Object { $retakeMaps[$_] = $classicMaps[$_] }
$armsMaps = [ordered]@{
    ar_baggage = '수하물'; ar_shoots = '재배소(낮)'; ar_shoots_night = '재배소(밤)'; ar_pool_day = '풀 데이'
}

# cfg: 맵 로딩 때 CS2가 읽는 gamemode_<cfg>_server.cfg (탈환은 봇을 모드 규칙이 정해서 없음)
$modes = [ordered]@{
    '캐주얼'     = @{ type = 0; mode = 0; cfg = 'casual';     maps = $classicMaps }
    '데스매치'   = @{ type = 1; mode = 2; cfg = 'deathmatch'; maps = $classicMaps }
    '무기 레이스' = @{ type = 1; mode = 0; cfg = 'armsrace';   maps = $armsMaps }
    '탈환'       = @{ type = 0; mode = 5; cfg = $null;        maps = $retakeMaps }
}
$serverCfgs = @('casual', 'deathmatch', 'armsrace', 'competitive')   # competitive: 예전 버전이 만든 파일 정리용

$botCounts = @('게임 기본값') + (0..10 | ForEach-Object { "$_ 명" })
$botLevels = [ordered]@{ '쉬움' = 0; '보통' = 1; '어려움' = 2; '전문가' = 3 }
$botTeams  = [ordered]@{ '양쪽에 섞기' = 'any'; '테러리스트 팀만' = 't'; '대테러 팀만' = 'ct' }

# ---------- 화면 ----------
$form = New-Object Windows.Forms.Form
$form.Text = 'CS2 방장 도우미'
$form.Font = New-Object Drawing.Font('맑은 고딕', 10)
$form.ClientSize = New-Object Drawing.Size(380, 494)
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false
$form.StartPosition = 'CenterScreen'
try { $form.Icon = [Drawing.Icon]::ExtractAssociatedIcon((Join-Path (Split-Path $csgo) 'bin\win64\cs2.exe')) } catch {}

$banner = New-Object Windows.Forms.Label
$banner.Location = New-Object Drawing.Point(20, 14); $banner.Size = New-Object Drawing.Size(340, 32)
$banner.TextAlign = 'MiddleLeft'; $banner.Padding = New-Object Windows.Forms.Padding(8, 0, 0, 0)
$banner.Font = New-Object Drawing.Font('맑은 고딕', 10, [Drawing.FontStyle]::Bold)
$form.Controls.Add($banner)

$y = 62
function Add-Row($text) {
    $lbl = New-Object Windows.Forms.Label
    $lbl.Text = $text; $lbl.Location = New-Object Drawing.Point(20, ($script:y + 4)); $lbl.AutoSize = $true
    $cb = New-Object Windows.Forms.ComboBox
    $cb.DropDownStyle = 'DropDownList'; $cb.Location = New-Object Drawing.Point(120, $script:y); $cb.Width = 240
    $cb.MaxDropDownItems = 16
    $form.Controls.AddRange(@($lbl, $cb))
    $script:y += 40
    return $cb
}

$cbMode  = Add-Row '모드';      $modes.Keys     | ForEach-Object { [void]$cbMode.Items.Add($_) }
$cbMap   = Add-Row '맵'
$cbBots  = Add-Row '봇 수';     $botCounts      | ForEach-Object { [void]$cbBots.Items.Add($_) }
$cbLevel = Add-Row '봇 난이도'; $botLevels.Keys | ForEach-Object { [void]$cbLevel.Items.Add($_) }
$cbTeam  = Add-Row '봇 팀';     $botTeams.Keys  | ForEach-Object { [void]$cbTeam.Items.Add($_) }

$status = New-Object Windows.Forms.Label
$status.Location = New-Object Drawing.Point(20, ($y + 2)); $status.Size = New-Object Drawing.Size(340, 110)
$status.ForeColor = [Drawing.Color]::DarkGreen
$form.Controls.Add($status)

$btnLaunch = New-Object Windows.Forms.Button
$btnLaunch.Text = '▶  CS2 켜고 서버 열기'
$btnLaunch.Font = New-Object Drawing.Font('맑은 고딕', 11, [Drawing.FontStyle]::Bold)
$btnLaunch.Location = New-Object Drawing.Point(20, 384); $btnLaunch.Size = New-Object Drawing.Size(340, 46)
$form.Controls.Add($btnLaunch)

$btnCopy = New-Object Windows.Forms.Button
$btnCopy.Text = '서버 주소 복사 (친구들에게 보내기)'
$btnCopy.Location = New-Object Drawing.Point(20, 438); $btnCopy.Size = New-Object Drawing.Size(340, 40)
$form.Controls.Add($btnCopy)

$timer = New-Object Windows.Forms.Timer
$timer.Interval = 2000

# ---------- 맵 목록 ----------
$script:mapIds = @()
$script:loading = $true

function Fill-Maps($wantId) {
    $list = $modes[$cbMode.SelectedItem].maps
    $script:mapIds = @($list.Keys | Where-Object { Test-Path (Join-Path $mapsDir "$_.vpk") })
    $cbMap.Items.Clear()
    $script:mapIds | ForEach-Object { [void]$cbMap.Items.Add($list[$_]) }
    $idx = [array]::IndexOf($script:mapIds, $wantId)
    if ($idx -lt 0) { $idx = [array]::IndexOf($script:mapIds, 'de_dust2') }
    $cbMap.SelectedIndex = [Math]::Max(0, $idx)
}

# ---------- 저장 ----------
function Write-Cfg($name, $lines) {
    [IO.File]::WriteAllText((Join-Path $cfgDir $name), (($lines -join "`r`n") + "`r`n"), (New-Object Text.UTF8Encoding $false))
}

function Save-All {
    if ($script:loading -or $cbMode.SelectedIndex -lt 0 -or $cbMap.SelectedIndex -lt 0) { return }

    $m        = $modes[$cbMode.SelectedItem]
    $map      = $script:mapIds[$cbMap.SelectedIndex]
    $botsFree = [bool]$m.cfg
    foreach ($cb in @($cbBots, $cbLevel, $cbTeam)) { $cb.Enabled = $botsFree }

    Write-Cfg 'host.cfg'   @($marker, "game_type $($m.type)", "game_mode $($m.mode)", "map $map")
    Write-Cfg 'change.cfg' @($marker, "game_type $($m.type)", "game_mode $($m.mode)", "changelevel $map")

    $botLines = $null
    if ($cbBots.SelectedIndex -gt 0) {
        $n = $cbBots.SelectedIndex - 1
        $botLines = @($marker, 'bot_quota_mode normal', "bot_quota $n",
                      "bot_difficulty $($botLevels[$cbLevel.SelectedItem])",
                      "bot_join_team $($botTeams[$cbTeam.SelectedItem])")
        if ($n -eq 0) { $botLines += 'bot_kick' }
    }
    foreach ($c in $serverCfgs) {
        $path = Join-Path $cfgDir "gamemode_$($c)_server.cfg"
        $ours = -not (Test-Path $path) -or ((Get-Content $path -TotalCount 1 -Encoding UTF8) -eq $marker)
        if (-not $ours) { continue }   # 직접 만든 파일은 건드리지 않음
        if ($botLines -and $c -ne 'competitive') { Write-Cfg (Split-Path $path -Leaf) $botLines }
        elseif (Test-Path $path) { Remove-Item $path }
    }

    @{ mode = $cbMode.SelectedItem; map = $map; bots = $cbBots.SelectedIndex
       level = $cbLevel.SelectedItem; team = $cbTeam.SelectedItem } |
        ConvertTo-Json | Set-Content $settings -Encoding UTF8

    if ($timer.Enabled) { return }   # 서버 여는 중에는 진행 상황 문구 유지
    $note = if ($botsFree) { '' } else { "`n※ 탈환은 봇을 게임이 정해요 (수비 테러리스트 봇)" }
    $status.Text = "저장됐어요!  ($($cbMode.SelectedItem) / $($cbMap.SelectedItem))$note`n`n" +
                   "▶ 버튼 : CS2 켜고 이 설정으로 서버 열기`n" +
                   "F9  : (게임 중) 이 설정으로 서버 새로 열기`n" +
                   "F10 : (게임 중) 맵/모드만 바꾸기, 친구들 그대로"
}

# ---------- 불러오기 ----------
$saved = $null
if (Test-Path $settings) { try { $saved = Get-Content $settings -Raw -Encoding UTF8 | ConvertFrom-Json } catch {} }

$cbMode.SelectedItem = if ($saved) { $saved.mode } else { '캐주얼' }
if ($cbMode.SelectedIndex -lt 0) { $cbMode.SelectedIndex = 0 }
Fill-Maps $(if ($saved) { $saved.map } else { 'de_dust2' })
$cbBots.SelectedIndex = if ($saved -and $saved.bots -lt $botCounts.Count) { [int]$saved.bots } else { 0 }
$cbLevel.SelectedItem = if ($saved) { $saved.level } else { '보통' }
$cbTeam.SelectedItem  = if ($saved) { $saved.team } else { '양쪽에 섞기' }
if ($cbLevel.SelectedIndex -lt 0) { $cbLevel.SelectedIndex = 1 }
if ($cbTeam.SelectedIndex -lt 0)  { $cbTeam.SelectedIndex = 0 }

$cbMode.Add_SelectedIndexChanged({
    $prev = if ($cbMap.SelectedIndex -ge 0) { $script:mapIds[$cbMap.SelectedIndex] } else { $null }
    $script:loading = $true; Fill-Maps $prev; $script:loading = $false
    Save-All
})
foreach ($cb in @($cbMap, $cbBots, $cbLevel, $cbTeam)) { $cb.Add_SelectedIndexChanged({ Save-All }) }
$script:loading = $false
Save-All

# ---------- 서버 주소 (-condebug 로 남는 console.log 에서 읽음) ----------
function Get-LogTime($line) {
    [datetime]::ParseExact("$((Get-Date).Year)/$($line.Substring(0, 14))", 'yyyy/MM/dd HH:mm:ss', $null)
}

function Get-LastServer {
    if (-not (Test-Path $logFile)) { return $null }
    $hit = Get-Content $logFile -Encoding UTF8 | Select-String 'ServerSteamID=(\[A:[^\]]+\])' | Select-Object -Last 1
    if (-not $hit) { return $null }
    [pscustomobject]@{ Cmd = "connect $($hit.Matches[0].Groups[1].Value)"; When = Get-LogTime $hit.Line }
}

# 서버가 지금 열려 있는지: 마지막 기록이 ServerSteamID(열림)인지, 메뉴로 나간 종료 기록인지
function Get-OpenServerTime($since) {
    if (-not (Test-Path $logFile)) { return $null }
    $last = Get-Content $logFile -Encoding UTF8 |
        Select-String 'ServerSteamID=|Server shutting down: NETWORK_DISCONNECT_(DISCONNECT_BY_USER|REQUEST_HOSTSTATE_IDLE)' |
        Select-Object -Last 1
    if (-not $last -or $last.Line -notmatch 'ServerSteamID=') { return $null }
    $t = Get-LogTime $last.Line
    if ($t -lt $since) { return $null }
    return $t
}

function Update-Banner {
    $cs2 = Get-Process cs2 -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $cs2) {
        $banner.BackColor = [Drawing.Color]::Gainsboro; $banner.ForeColor = [Drawing.Color]::DimGray
        $banner.Text = '○  CS2 꺼져 있음 · 아래 ▶ 버튼으로 시작'
        $btnLaunch.Text = '▶  CS2 켜고 서버 열기'
        return
    }
    $btnLaunch.Text = '▶  CS2 다시 켜고 서버 열기'
    $open = Get-OpenServerTime $cs2.StartTime.AddSeconds(-5)
    if ($open) {
        $banner.BackColor = [Drawing.Color]::FromArgb(212, 237, 218); $banner.ForeColor = [Drawing.Color]::DarkGreen
        $banner.Text = "●  서버 열림 ($($open.ToString('HH:mm'))) · 게임 중 F9 / F10"
    } else {
        $banner.BackColor = [Drawing.Color]::FromArgb(209, 231, 248); $banner.ForeColor = [Drawing.Color]::SteelBlue
        $banner.Text = '●  CS2 실행 중 · 서버 없음 (메인 화면)'
    }
}

$bannerTimer = New-Object Windows.Forms.Timer
$bannerTimer.Interval = 3000
$bannerTimer.Add_Tick({ Update-Banner })
$form.Add_Shown({ Update-Banner; $bannerTimer.Start() })

$btnCopy.Add_Click({
    $s = Get-LastServer
    if (-not $s) {
        [Windows.Forms.MessageBox]::Show("서버를 찾지 못했어요.`n먼저 [CS2 켜고 서버 열기]로 서버를 열어주세요.", 'CS2 방장 도우미') | Out-Null
        return
    }
    Set-Clipboard -Value $s.Cmd
    [Windows.Forms.MessageBox]::Show("복사됐어요! 카톡/디스코드에 Ctrl+V 로 붙여넣으세요.`n`n$($s.Cmd)`n`n(서버 연 시각: $($s.When.ToString('HH:mm:ss')))", 'CS2 방장 도우미') | Out-Null
})

# ---------- CS2 켜고 서버 열기 ----------
$steamExe = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamExe

$btnLaunch.Add_Click({
    if (-not $steamExe -or -not (Test-Path $steamExe)) {
        [Windows.Forms.MessageBox]::Show('Steam을 찾지 못했어요.', 'CS2 방장 도우미') | Out-Null
        return
    }
    $cs2 = @(Get-Process cs2 -ErrorAction SilentlyContinue)
    if ($cs2) {
        $ans = [Windows.Forms.MessageBox]::Show(
            "CS2가 이미 켜져 있어요.`n`n게임 안이라면 [아니요]를 누르고 게임에서 F9를 누르세요.`n메인 화면이라면 [예]를 누르면 CS2를 껐다가 새로 켜서 서버를 열어요.",
            'CS2 방장 도우미', 'YesNo', 'Question')
        if ($ans -ne 'Yes') { return }
        $form.Cursor = 'WaitCursor'
        foreach ($p in $cs2) { [void]$p.CloseMainWindow() }
        foreach ($p in $cs2) { if (-not $p.WaitForExit(15000)) { $p.Kill() } }
        Start-Sleep -Seconds 3   # Steam이 게임 종료를 알아챌 시간
        $form.Cursor = 'Default'
    }

    $m   = $modes[$cbMode.SelectedItem]
    $map = $script:mapIds[$cbMap.SelectedIndex]
    Start-Process $steamExe -ArgumentList '-applaunch', '730', '+game_type', $m.type, '+game_mode', $m.mode, '+map', $map

    $script:launchAt = Get-Date
    $btnLaunch.Enabled = $false
    $timer.Start()
    $status.Text = "CS2 켜는 중...  ($($cbMode.SelectedItem) / $($cbMap.SelectedItem))`n`n" +
                   "서버가 열리면 친구들에게 보낼 주소가`n자동으로 복사돼요. (1분 정도 걸려요)"
})

$timer.Add_Tick({
    $s = Get-LastServer
    if ($s -and $s.When -ge $script:launchAt.AddSeconds(-5)) {
        $timer.Stop(); $btnLaunch.Enabled = $true
        Set-Clipboard -Value $s.Cmd
        [Media.SystemSounds]::Asterisk.Play()
        $status.Text = "서버가 열렸어요! 주소가 복사됐어요.`n카톡/디스코드에 Ctrl+V 로 보내세요.`n`n$($s.Cmd)"
    }
    elseif (((Get-Date) - $script:launchAt).TotalMinutes -gt 4) {
        $timer.Stop(); $btnLaunch.Enabled = $true
        $status.Text = "서버를 찾지 못했어요.`nCS2가 켜졌는지 확인하고 다시 눌러주세요."
    }
})

[void]$form.ShowDialog()
