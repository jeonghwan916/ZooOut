# [Knock Out]
> **물리 기반 넉백**  
> **개발 기간:** 2026.04.27 ~ 2026.05.08

---

## 1. 프로젝트 개요 (Abstract)
* **목표:** 1주차 기능 위주 MVP완성, 2주차 테스트 및 최적화
* **대상:** 타겟 플랫폼(Web, PC, 모바일) 및 사용자층
* **팀 멤버**

| 이름 | 이메일 | 
| :--- | :--- | 
| **윤창현** | ychcom0357@gmail.com |
| **서연석** | westkitestone@gmail.com | 
| **김정환** | jeonghwan916@gmail.com |

---

## 2. 기술 스택 (Tech Stack)
| 분류 | 기술 | 버전 / 비고 |
| :--- | :--- | :--- |
| **Engine** | Unity | 6000.4.4f1 |
| **Render Pipeline** | Universal 3D | PC기반 |
| **Language** | C# | .NET Standard 10 |
| **VCS** | Git | GitHub / Git Bash |
| **Communication** | Discoard / Notion | 칸반 보드 및 이슈 관리 |


 ---

## 3. 프로젝트 구조 (Architecture)

### 씬 구조
```text
Assets
 ┣ 🪟 Level   # 맵관련 요소
 ┣ 🪟 Player  # 사용자 인터렉션
 ┣ 🪟 NPC     # NPC 로직 FSM
 ┗ 🪟 UI      # 사용자 인터페이스
```



### 폴더 구조 (Folder Structure)
```text
Assets
 ┣ 📂 Animations   # 애니메이션 컨트롤러 및 클립
 ┣ 📂 Prefabs      # 재사용 가능한 프리팹
 ┣ 📂 Scripts      # 모든 C# 스크립트
 ┃ ┣ 📂 Core       # 싱글톤, 매니저 클래스
 ┃ ┣ 📂 UI         # UI 로직 및 이벤트 시스템
 ┃ ┗ 📂 Utils      # 공통 유틸리티
 ┣ 📂 Scenes       # 레벨 및 테스트 씬
 ┗ 📂 Settings     # URP 및 프로젝트 설정
```
---


##일정관리 및 세부내용##
* [노션](https://www.notion.so/KO-565ea6411a968248bd29010df7b27509)