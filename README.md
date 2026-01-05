# CympleFaceTracking
The VRCFT module for [Project Cymple](https://github.com/Dominocs/Project_Cymple), a low cost and open source DIY eye tracking solution.

## Cross-Platform Support
This module now supports both Windows and Linux platforms.

### Linux Dependencies
On Linux, you need to install `libgdiplus` for System.Drawing support:
- Ubuntu/Debian: `sudo apt-get install libgdiplus`
- CentOS/RHEL: `sudo yum install libgdiplus`
- Arch Linux: `sudo pacman -S libgdiplus`

### Configuration
- On Windows: The INI file is expected at `%ProgramFiles%\Cymple\iniFile.ini`
- On Linux: The module searches for the INI file in the following locations (in order):
  - `~/.wine/drive_c/Cymple/iniFile.ini` (for Wine)
  - `~/.steam/steam/steamapps/compatdata/*/pfx/drive_c/Cymple/iniFile.ini` (for Proton, scans all compatdata directories)
