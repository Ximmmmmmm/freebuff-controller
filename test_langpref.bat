@echo off
rem LangPref behavior regression test: builds a console exe and runs it.
rem Keep this file ASCII-only - cmd reads .bat in the OEM codepage, so Chinese
rem comments here get mis-parsed as commands (build.bat follows the same rule).
rem Usage: test_langpref.bat  (exit 0 = all assertions passed)
>nul chcp 65001
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OUT=%TEMP%\langpref-test.exe
"%CSC%" -nologo -target:exe -platform:anycpu -optimize+ -codepage:65001 ^
  -out:"%OUT%" "%~dp0LangPref.cs" "%~dp0LangPrefTest.cs"
if not %errorlevel%==0 (
  echo BUILD FAILED
  exit /b 1
)
"%OUT%"
set RC=%errorlevel%
del "%OUT%" >nul 2>&1
if "%RC%"=="0" (echo TEST OK) else (echo TEST FAILED)
exit /b %RC%
