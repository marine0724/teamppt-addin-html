# ──────────────────────────────────────────────────────────────────
#  TEAMPPT 릴리스 패키징 스크립트
#  bin/Release 를 배포용 zip 으로 묶는다. (api-keys.json 자동 제외)
#
#  사용법 (관리자 권한 PowerShell):
#    1) AssemblyInfo.cs 에서 버전을 올린다 (예: 1.0.0 → 1.1.0)
#    2) Release 빌드한다 (CLAUDE.md 의 빌드 명령, Configuration=Release)
#    3) 이 스크립트 실행:  .\scripts\package-release.ps1
#       → dist\TeampptAddin-<버전>.zip 생성
#    4) 그 zip 을 GitHub Release 로 업로드 + docs\version.json 갱신
#
#  버전은 AssemblyInfo.cs 에서 자동으로 읽는다 (단일 출처).
# ──────────────────────────────────────────────────────────────────

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'src\TeampptAddin'
$bin  = Join-Path $proj 'bin\Release'
$dist = Join-Path $root 'dist'

# ── 버전 읽기 (AssemblyVersion) ──
$asmInfo = Get-Content (Join-Path $proj 'Properties\AssemblyInfo.cs') -Raw
if ($asmInfo -notmatch 'AssemblyVersion\("(\d+)\.(\d+)\.(\d+)') {
    throw "AssemblyInfo.cs 에서 AssemblyVersion 을 찾지 못했습니다."
}
$version = "$($Matches[1]).$($Matches[2]).$($Matches[3])"
Write-Host "버전: $version" -ForegroundColor Cyan

# ── 빌드 산출물 확인 ──
if (-not (Test-Path (Join-Path $bin 'TeampptAddin.dll'))) {
    throw "bin\Release\TeampptAddin.dll 이 없습니다. 먼저 Release 빌드를 하세요."
}

# ── 스테이징 폴더 구성 ──
$stage = Join-Path $env:TEMP "teamppt-pkg\TeampptAddin-$version"
if (Test-Path (Split-Path $stage)) { Remove-Item -Recurse -Force (Split-Path $stage) }
New-Item -ItemType Directory -Force $stage | Out-Null

Copy-Item -Recurse "$bin\*" $stage

# ── 키 파일 제거 (유출 방지) ──
$key = Join-Path $stage 'Assets\api-keys.json'
if (Test-Path $key) { Remove-Item -Force $key; Write-Host "api-keys.json 제외됨" -ForegroundColor Yellow }

# ── pdb 제거 (디버그 심볼, 배포 불필요) ──
Get-ChildItem $stage -Filter *.pdb | Remove-Item -Force

# ── 설치 스크립트 + README 동봉 ──
Copy-Item (Join-Path $proj 'install.bat')   $stage
Copy-Item (Join-Path $proj 'uninstall.bat') $stage
$readme = Join-Path $proj 'RELEASE-README.txt'
if (Test-Path $readme) { Copy-Item $readme (Join-Path $stage 'README.txt') }

# ── 키 파일이 남아있지 않은지 최종 검증 ──
if (Get-ChildItem $stage -Recurse -Filter 'api-keys.json') {
    throw "중단: api-keys.json 이 패키지에 남아있습니다!"
}

# ── 압축 ──
New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist "TeampptAddin-$version.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path "$stage\*" -DestinationPath $zip

$kb = [math]::Round((Get-Item $zip).Length / 1KB)
Write-Host ""
Write-Host "완료: $zip ($kb KB)" -ForegroundColor Green
Write-Host ""
Write-Host "다음 단계:" -ForegroundColor Cyan
Write-Host "  gh release create v$version `"$zip`" --repo marine0724/teamppt-addin-html --title `"TEAMPPT v$version`" --notes `"...`""
Write-Host "  그리고 docs\version.json 의 version/zipUrl 을 $version 으로 갱신 후 커밋·푸시"
