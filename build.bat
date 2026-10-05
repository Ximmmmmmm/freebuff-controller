@echo off
rem Rebuild FreebuffController.exe (output lands in this folder as
rem FreebuffController.exe; rename to the Chinese display name if you like).
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
rem 会话接力脚本以 Base64 内嵌进 exe，编译前先刷新它（需要 python3）。
python "%~dp0tools\embed-handover.py" || (
  echo EMBED FAILED ^(需要 python3^)
  exit /b 1
)
rem 检查目标 exe 是否正在运行（避免报晦涩的 CS0016 文件占用错误）
tasklist /fi "imagename eq FreebuffController.exe" 2>nul | "%SystemRoot%\System32\find.exe" /i "FreebuffController.exe" >nul
if %errorlevel%==0 (
  echo [提示] 检测到 FreebuffController.exe 正在后台运行！
  echo 请在系统托盘右键退出控制器后再编译，以避免文件被锁定。
  exit /b 1
)

"%CSC%" -nologo -target:winexe -platform:anycpu -optimize+ -codepage:65001 ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Management.dll ^
  -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll ^
  -win32icon:"%~dp0app.ico" -out:"%~dp0FreebuffController.exe" "%~dp0FreebuffController.cs"
rem ...and propagate the exit code so CI smoke builds actually fail on error.
if %errorlevel%==0 (echo BUILD OK) else (
  echo BUILD FAILED
  exit /b 1
)
