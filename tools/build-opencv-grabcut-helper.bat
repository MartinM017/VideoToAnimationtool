@echo off
setlocal

set ROOT=%~dp0..
set OPENCV_DIR=%ROOT%\opencv
set OUT_DIR=%ROOT%\tools
set INCLUDE_DIR=%OPENCV_DIR%\build\include
set LIB_DIR=%OPENCV_DIR%\build\x64\vc16\lib
set BIN_DIR=%OPENCV_DIR%\build\x64\vc16\bin

if not exist "%INCLUDE_DIR%\opencv2\imgproc.hpp" (
  echo OpenCV include folder was not found: "%INCLUDE_DIR%"
  exit /b 1
)

if not exist "%LIB_DIR%\opencv_world500.lib" (
  echo OpenCV import library was not found: "%LIB_DIR%\opencv_world500.lib"
  exit /b 1
)

where cl.exe >nul 2>nul
if errorlevel 1 (
  echo cl.exe was not found. Open "x64 Native Tools Command Prompt for VS 2022" and run this script again.
  exit /b 2
)

cl.exe /nologo /EHsc /O2 /std:c++17 /I"%INCLUDE_DIR%" "%OUT_DIR%\opencv-grabcut-helper.cpp" /Fe"%OUT_DIR%\opencv-grabcut-helper.exe" /link /LIBPATH:"%LIB_DIR%" opencv_world500.lib
if errorlevel 1 exit /b %errorlevel%

cl.exe /nologo /EHsc /O2 /std:c++17 /I"%INCLUDE_DIR%" "%OUT_DIR%\opencv-inpaint-helper.cpp" /Fe"%OUT_DIR%\opencv-inpaint-helper.exe" /link /LIBPATH:"%LIB_DIR%" opencv_world500.lib
if errorlevel 1 exit /b %errorlevel%

copy /Y "%BIN_DIR%\opencv_world500.dll" "%OUT_DIR%\" >nul
copy /Y "%BIN_DIR%\opencv_videoio_ffmpeg500_64.dll" "%OUT_DIR%\" >nul 2>nul

echo Built "%OUT_DIR%\opencv-grabcut-helper.exe"
echo Built "%OUT_DIR%\opencv-inpaint-helper.exe"
endlocal
