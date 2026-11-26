@echo off
rem ProjectVTT Escritorio: abre el proyecto de Godot (4.7.2 .NET) en el editor.
rem El ejecutable de Godot Mono viene por WinGet; se puede sobreescribir
rem con la variable de entorno GODOT_PATH.
set "GODOT_EXE=%GODOT_PATH%"
if "%GODOT_EXE%"=="" set "GODOT_EXE=%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
start "" "%GODOT_EXE%" --editor --path "%~dp0..\apps\desktop"
