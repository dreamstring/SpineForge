@echo off
echo ����������Ŀ...

REM ��ȡ��ǰʱ���
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Sec=%dt:~12,2%"
set "timestamp=%YYYY%%MM%%DD%_%HH%%Min%%Sec%"

REM ������Ŀ
dotnet clean

REM ɾ�� bin �� obj �ļ��У�������ڣ�
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo ��ʼ���ļ�����...

REM ǿ�Ƶ��ļ���������ʱ������ļ���
dotnet publish -c Release -r win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false --output ./publish-%timestamp%

if %errorlevel% equ 0 (
    echo.
    echo �����ɹ���
    echo ���Ŀ¼: %CD%\publish-%timestamp%
    echo ��ִ���ļ�: %CD%\publish-%timestamp%\SpineForge.exe
    echo.
    echo �Ƿ������ļ��У� (Y/N)
    set /p choice=
    if /i "%choice%"=="Y" explorer "%CD%\publish-%timestamp%"
    pause
) else (
    echo.
    echo ����ʧ�ܣ�
    echo.
    pause
)
