<div align="justify">

## About
Video Converter is a command line tool that utilizes ffmpeg and configuration files to simplify encoding video (and audio) files.  
The goal is to heavily simplify the usage of ffmpeg, by offering fewer, more consolidated options, providing good defaults, and re-using configuration files.

## Setup
* Download and unzip one of the [latest releases](https://github.com/TornOne/Video-Converter/releases/latest).
  * The default download is platform-independent, but requires .NET 9 installed. (Will probably work with similar .NET versions if you change the version in the .json file.)
  * The "Win" option is a Windows-only executable that also requries .NET 9 to be installed.
  * The "Win Standalone" option is a Windows-only executable that does not require .NET to be installed.
* Find a version of ffmpeg and ffprobe. (ffprobe is optional, but recommended)
  * [Some options here](https://ffmpeg.org/download.html)
  * Keeping this up-to-date is optional, but will provide newer and better encoder versions.
* Point `defaults.cfg` to ffmpeg and ffprobe and configure any other settings you want.

## Usage
Video Converter accepts 0 or more file and folder paths as arguments. Each path can be to a configuration file (must end in `.cfg`), to a video file, or to a folder containing only video files.  
The configuration files act as overrides to `defaults.cfg` and are applied over each other in the order they appear as arguments.  
Video files specify videos to convert and override any videos specified in a configuration file.

Each configuration file must be a newline separated list of `key=value` pairs. Empty lines and lines starting with a `#` are ignored.  
Every key has to be the name of an option, but every option does not need to be defined in a configuration file.  
`defaults.cfg` has information on each possible option, along with their default values. But these can be changed, or even deleted if you want. Aside from its location, it is not a special configuration file.

Relative paths specified as arguments are relative to the current working directory.  
Relative paths specified in a configuration file are relative to the directory of the configuration file.

## Usage tips
Start by reading over `defaults.cfg`, seeing what options there are, and set values to what you feel are good defaults for you.  
Video and audio option sections are the most likely ones that you will want to change regularly, depending on the purpose for which you are encoding. I would recommend making a new configuration file with just those options (and any other you foresee yourself changing often), and stick to just editing that file in the future.  
If you switch back and forth between a few different encoding settings, make an override file for each and pass the corresponding one in with the videos you want to convert.

### Future plans
* Add sample configuration override files for various use cases.
* Add reference info on speed and quality options.
* Support hardware encoders.

</div>
