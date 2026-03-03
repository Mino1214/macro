# 빌드 커맨드

## SafePal 버전 (Edge 캡처, data/pic/kr)
```bash
dotnet publish -c Release /p:PublishProfile=SingleFileProfile
```
→ 결과: `publish\` 폴더

## Trust Wallet 버전 (Chrome 캡처, data/pic/trustwallet)
```bash
dotnet publish /p:PublishProfile=TrustWalletProfile /p:Configuration=ReleaseTrustWallet
```
→ 결과: `publish-trustwallet\` 폴더  
(-c Release 쓰면 SafePal 쪽으로 빌드되므로 반드시 Configuration=ReleaseTrustWallet 로 실행)

## Tron CLI 버전 (터미널 전용)
```bash
dotnet publish /p:PublishProfile=TronCliProfile /p:Configuration=ReleaseTronCli
```
→ 결과: `publish-troncli\` 폴더  
실행 예: `MainPatchedImproved.exe --count 10`

Trust Wallet용 템플릿 이미지는 프로젝트 상위의 `pic\trustwallet\` 폴더에 두세요.  
필요 파일: `first.png`, `second.png`, `third.png`, `fourth.png`, `fifth.png`, `failbutton.png`, `successbutton.png`, `success.png`
