@echo off
REM =====================================================================
REM  ThaoCamVien - Script chay API + ngrok cho buoi demo
REM  Moi lan chay se mo 2 cua so:
REM    1. Cua so API (dotnet run ApiThaoCamVien tren cong 5281)
REM    2. Cua so ngrok (tunnel public https -> localhost:5281)
REM
REM  Yeu cau:
REM    - Cai .NET 8/9 SDK
REM    - Cai ngrok (https://ngrok.com/download) va da chay:
REM        ngrok config add-authtoken <your_token>
REM    - SQL Server local da co DB "web"
REM =====================================================================

setlocal enabledelayedexpansion
cd /d "%~dp0"

echo.
echo =====================================================================
echo  [1/3] Kiem tra moi truong
echo =====================================================================

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [X] Khong tim thay "dotnet". Hay cai .NET SDK truoc.
    pause
    exit /b 1
)

where ngrok >nul 2>nul
if errorlevel 1 (
    echo [X] Khong tim thay "ngrok". Tai ve tu https://ngrok.com/download
    echo     Sau khi cai, chay: ngrok config add-authtoken ^<token^>
    pause
    exit /b 1
)

echo [OK] dotnet + ngrok san sang.

echo.
echo =====================================================================
echo  [2/3] Khoi dong API (cua so rieng) tren http://0.0.0.0:5281
echo =====================================================================
start "ThaoCamVien API :5281" cmd /k "cd /d %~dp0ApiThaoCamVien && dotnet run --urls http://0.0.0.0:5281"

REM Doi API san sang (~8 giay)
echo Dang doi API khoi dong...
timeout /t 8 /nobreak >nul

echo.
echo =====================================================================
echo  [3/3] Khoi dong ngrok tunnel -> localhost:5281 (cua so rieng)
echo =====================================================================
echo.
echo Khi ngrok mo xong, nhin dong "Forwarding" kieu:
echo     https://abc-123-xyz.ngrok-free.app -^> http://localhost:5281
echo COPY URL https://... do, nhap vao man hinh "Cau hinh IP API" trong app.
echo.
echo De dung demo: dong ca hai cua so (Ctrl+C trong ngrok va API).
echo =====================================================================
echo.

REM Chay ngrok trong cua so rieng voi cmd /k de KHONG TU DONG TAT
REM kieu gi ngrok co loi (chua authtoken, mang die...) ban cung doc duoc loi.
start "ThaoCamVien ngrok tunnel" cmd /k "ngrok http 5281 || (echo. & echo [X] NGROK BI LOI - XEM THONG BAO PHIA TREN & echo Thuong gap: chua chay "ngrok config add-authtoken ^<token^>" & echo. & pause)"

echo.
echo Da mo 2 cua so: API va ngrok. Cua so nay co the dong.
echo.
pause

endlocal
