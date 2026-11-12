@echo off
rem Study data monitor: opens a window that shows the recording in StudyData while it is being written.
rem Unity starts this by itself when Play starts (Avian Voice > Study Monitor); this file is for starting it by hand.
cd /d "%~dp0"
where python >nul 2>nul
if %errorlevel%==0 (
  python study_monitor.py %*
) else (
  py -3 study_monitor.py %*
)
if errorlevel 1 pause
