@echo off
chcp 65001 >nul
echo ===== 실전 테스트용 퍼블리시 (폴더 배포) =====
dotnet publish -p:PublishProfile=FolderProfile
if %ERRORLEVEL% neq 0 (
  echo 퍼블리시 실패.
  pause
  exit /b 1
)
echo.
echo 퍼블리시 완료: bin\Publish\
echo - MainPatchedImproved.exe 실행
echo - pic\kr\ 템플릿, wordlist.txt 포함 확인 후 배포
echo.
explorer "bin\Publish"
pause
