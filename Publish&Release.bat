@echo off
setlocal enabledelayedexpansion
echo Starting project build...

REM Get current timestamp
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YY=%dt:~2,2%" & set "YYYY=%dt:~0,4%" & set "MM=%dt:~4,2%" & set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%" & set "Min=%dt:~10,2%" & set "Sec=%dt:~12,2%"
set "timestamp=%YYYY%%MM%%DD%_%HH%%Min%%Sec%"

REM Read version from csproj file
set "version=1.0.0"
if exist "SpineForge.csproj" (
    echo Reading project version info...
    for /f "tokens=*" %%i in ('findstr "<Version>" SpineForge.csproj') do (
        set "line=%%i"
        for /f "tokens=2 delims=<>" %%j in ("!line!") do (
            set "version=%%j"
        )
    )
) else (
    echo Warning: SpineForge.csproj not found, using default version
)

echo Project version: !version!

REM Clean project
dotnet clean -c Release

REM Delete bin and obj folders if they exist
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

echo Starting file publishing...

REM Force single file publish with runtime and all files
dotnet publish -c Release -r win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false --output ./publish-!timestamp!

if !errorlevel! equ 0 (
    echo.
    echo Publish successful!
    echo Output directory: %CD%\publish-!timestamp!
    echo Executable file: %CD%\publish-!timestamp!\SpineForge.exe
    echo.
    
    REM Create final package directory
    set "package_dir=SpineForge_Package_!timestamp!"
    if exist "!package_dir!" rmdir /s /q "!package_dir!"
    mkdir "!package_dir!"
    
    echo Preparing package files...
    
    REM Copy published files
    xcopy "publish-!timestamp!\*" "!package_dir!\" /E /I /Y
    
    REM Copy TestSpine folder
    if exist "TestSpine" (
        echo Copying TestSpine folder...
        xcopy "TestSpine" "!package_dir!\TestSpine\" /E /I /Y
    ) else (
        echo Warning: TestSpine folder not found, skipping copy
    )
    
    REM Create zip file using PowerShell
    set "zip_name=SpineForge_!version!.zip"
    echo Creating zip file: !zip_name!
    
    REM Delete existing zip file if it exists
    if exist "!zip_name!" (
        echo Deleting existing zip file...
        del "!zip_name!"
    )
    
    powershell -Command "Compress-Archive -Path '!package_dir!\*' -DestinationPath '!zip_name!' -Force"
    
    if exist "!zip_name!" (
        echo.
        echo Packaging complete!
        echo ZIP file: %CD%\!zip_name!
        echo.
        
        REM Clean up temp files automatically
        echo Cleaning up temp files...
        rmdir /s /q "!package_dir!"
        rmdir /s /q "publish-!timestamp!"
        echo Temp files cleaned up
        
        echo.
        echo Build and packaging completed successfully!
        
    ) else (
        echo ZIP file creation failed
    )
    
    pause
) else (
    echo.
    echo Publish failed!
    echo.
    pause
)
