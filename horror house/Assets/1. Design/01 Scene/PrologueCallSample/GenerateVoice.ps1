$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$voiceDir = Join-Path $PSScriptRoot 'Voice'
New-Item -ItemType Directory -Path $voiceDir -Force | Out-Null
$lines = @(
    '안녕하세요. 시설관리팀 파견 담당자입니다.',
    '갑자기 연락드려 죄송합니다. 야간 근무 자리가 하나 비어서요.',
    '현장은 철거를 앞둔 학교입니다.',
    '아직 반출하지 않은 비품이 남아 있어 철거 전까지는 사람이 필요합니다.',
    '원래 근무하던 분이 어젯밤 인수인계 없이 나가셨다고 해서요.',
    '남은 닷새만 대신 맡아 주시면 됩니다.',
    '자정부터 여섯 시까지 1층을 순찰하고, 시설 상태와 외부인 출입 흔적을 확인하는 일입니다.',
    '근무 조건과 수당을 보내드릴게요.',
    '보시고 결정하시면 됩니다.'
)
$tts = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $tts.SelectVoice('Microsoft Heami Desktop')
    $tts.Rate = 0
    $tts.Volume = 85
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $target = Join-Path $voiceDir ('Call_{0:D2}.wav' -f ($i + 1))
        if (Test-Path -LiteralPath $target) { throw "Refusing to overwrite $target" }
        $tts.SetOutputToWaveFile($target)
        $tts.Speak($lines[$i])
        $tts.SetOutputToNull()
    }
} finally { $tts.Dispose() }
Get-ChildItem -LiteralPath $voiceDir -Filter '*.wav' | Select-Object Name,Length
