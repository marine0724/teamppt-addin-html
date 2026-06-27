TEAMPPT Add-in 설치 안내
============================

[1] 설치
  1. 이 폴더의 install.bat 을 마우스 우클릭 → "관리자 권한으로 실행"
  2. PowerPoint 를 재시작
  3. 홈 탭 끝에 TEAMPPT 버튼이 보이면 성공

[2] API 키 넣기 (최초 1회, 필수)
  - 관리자(배포자)에게서 받은 api-keys.json 파일을 아래 폴더에 복사하세요:
      %LOCALAPPDATA%\TeampptAddin\app\Assets\api-keys.json
  - (탐색기 주소창에 %LOCALAPPDATA%\TeampptAddin\app\Assets 붙여넣기)
  - 키를 넣고 PowerPoint 를 재시작하면 AI 기능이 활성화됩니다.
  - ※ 보안상 키는 릴리스에 포함되지 않습니다. 비공개로 전달받으세요.

[3] 업데이트
  - 자동입니다. 새 버전이 나오면 PowerPoint 패널 상단에
    "업데이트 준비됨" 배너가 뜨고, 누르면 자동 적용됩니다.
  - 다시 다운로드할 필요 없고, 넣어둔 api-keys.json 도 그대로 유지됩니다.

[4] 제거
  - uninstall.bat 을 관리자 권한으로 실행
