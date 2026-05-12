# LogAnalyzer 🔍

다양한 소스(로컬 파일, 원격 서버, 실행 중인 프로세스)의 로그를 실시간으로 모니터링하고 분석할 수 있는 종합 로그 분석 솔루션입니다.
WPF 기반의 직관적인 데스크톱 클라이언트와 가벼운 원격 에이전트로 구성되어 있습니다.

## ✨ 주요 기능 (Features)

- **멀티소스 로그 수집**
  - 🖥️ **로컬 파일:** 로컬 PC의 로그 파일(`.log`, `.txt`) 실시간 모니터링 (`tail -f` 방식)
  - 🌐 **TCP Agent (Windows):** 원격 Windows 서버에 `LogAnalyzerAgent`를 배포하여 네트워크 너머의 로그를 실시간 스트리밍
  - 🐧 **SSH (Linux):** 패스워드 또는 PEM 키 인증으로 Linux 서버에 접속하여 실시간 로그 캡처
  - ⚙️ **로컬 프로세스:** `.exe`, `.bat` 등 프로세스의 표준 출력(stdout)을 직접 캡처
- **다중 탭 지원:** 여러 대의 서버와 로그 파일을 동시에 탭으로 열어 모니터링 가능
- **실시간 필터링:** 텍스트 키워드 기반 실시간 필터 및 Watch 모드 지원
- **차트/분석:** 수집된 로그 데이터를 기반으로 한 시각적 차트 제공
- **로그 로테이션 감지:** 파일이 초기화(Truncate/Rotate)될 경우 자동으로 오프셋을 리셋하여 연속 모니터링 유지

## 🛠 기술 스택 (Tech Stack)

- **Client**: C#, .NET 8, WPF
- **Agent**: C#, .NET 8 (Console App, TCP Listener)
- **Libraries**: SSH.NET (SSH 연결), AvalonEdit (로그 텍스트 렌더링)
- **Build & Release**: GitHub Actions (자동 빌드 및 배포)

## 🚀 시작하기 (Getting Started)

### 1. 사전 요구 사항

- Windows 10 이상 (클라이언트 실행 환경)
- [GitHub Releases](../../releases)에서 최신 `LogAnalyzer-Release.zip`을 다운로드

### 2. 로컬 파일 모니터링

`LogAnalyzerWPF.exe`를 실행한 뒤, 단축키로 바로 사용할 수 있습니다.

| 단축키 | 기능 |
|--------|------|
| `Ctrl + T` | 로컬 로그 파일 열기 |
| `Ctrl + R` | 로컬 실행 파일(.exe/.bat) 구동 및 출력 캡처 |
| `Ctrl + W` | 현재 탭 닫기 |
| `Ctrl + Shift + T` | 차트 뷰 토글 |

### 3. 원격 Windows 서버 로그 모니터링 (TCP Agent)

1. `LogAnalyzerAgent.exe`를 원격 Windows 서버에 복사 후 실행합니다. (기본 포트: `5000`)
   ```cmd
   LogAnalyzerAgent.exe 5000
   ```
   > ⚠️ 필요한 경우 해당 포트를 방화벽에서 허용해야 합니다.

2. `LogAnalyzerWPF`에서 **원격 연결(Remote)** 버튼 → 모드를 `TCP Agent`로 선택 후 아래 정보를 입력하고 연결합니다.
   - **Host**: 원격 서버 IP
   - **Port**: `5000`
   - **RemoteFilePath**: 읽고자 하는 로그 파일의 절대 경로 (예: `C:\logs\app.log`)

### 4. 원격 Linux 서버 로그 모니터링 (SSH)

별도 에이전트 설치 없이 SSH로 직접 접속합니다.

1. `LogAnalyzerWPF`에서 **원격 연결(Remote)** 버튼 → 모드를 `LinuxSSH`로 선택합니다.
2. 아래 정보를 입력하고 연결합니다.
   - **Host**: Linux 서버 IP
   - **Port**: `22`
   - **Username**: SSH 계정명
   - **Password / PEM Key**: 비밀번호 또는 `.pem` 키 파일 경로
   - **RemoteFilePath**: 읽고자 하는 로그 파일의 절대 경로 (예: `/var/log/app.log`)

## 📝 시스템 아키텍처 흐름

1. **WPF 클라이언트 실행**: `LogAnalyzerWPF.exe`를 실행하고 원하는 로그 소스에 연결
2. **연결 수립**: 소스 타입에 따라 로컬 파일 스트림, TCP 소켓, 또는 SSH 채널 중 하나로 연결
3. **실시간 로그 수신**: 백그라운드 Task가 새 로그 라인을 지속적으로 수신하고 UI 스레드에 전달
4. **파싱 및 렌더링**: `LogParser`가 수신된 텍스트를 `LogEntry` 객체로 변환 후 화면에 렌더링
5. **필터 적용**: 사용자가 입력한 키워드 기준으로 실시간 필터링하여 표시
6. **연결 종료**: 탭 닫기 또는 프로그램 종료 시 모든 연결 및 리소스를 안전하게 해제 (`IDisposable`)

## ⚠️ 주의 사항 (Security Notice)

`LogAnalyzerAgent`는 별도의 클라이언트 인증 절차를 거치지 않습니다. **반드시 안전한 내부망(Private Network)이거나 방화벽으로 인가된 IP만 접근 가능한 환경에서만 Agent를 구동하시기 바랍니다.**
