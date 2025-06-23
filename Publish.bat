@echo off
echo 正在清理项目...

REM 获取当前时间戳
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Sec=%dt:~12,2%"
set "timestamp=%YYYY%%MM%%DD%_%HH%%Min%%Sec%"

REM 清理项目
dotnet clean

REM 删除 bin 和 obj 文件夹（如果存在）
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo 开始单文件发布...

REM 强制单文件发布到带时间戳的文件夹
dotnet publish -c Release -r win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false --output ./publish-%timestamp%

if %errorlevel% equ 0 (
    echo.
    echo 发布成功！
    echo 输出目录: %CD%\publish-%timestamp%
    echo 可执行文件: %CD%\publish-%timestamp%\SpineForge.exe
    echo.
    echo 是否打开输出文件夹？ (Y/N)
    set /p choice=
    if /i "%choice%"=="Y" explorer "%CD%\publish-%timestamp%"
    pause
) else (
    echo.
    echo 发布失败！
    echo.
    pause
)
