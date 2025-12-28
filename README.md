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

## Help, I don't know anything about video encoding?
Start by considering why you're encoding the video and maybe grab one of the sample configuration override files.
* Just want to quickly share some video you recorded, but it's too big to upload to your favorite social media or communication platform?
  * Grab the `Limited size.cfg` sample. It's optimized for Discord, but should work for other similar places. There are comments inside on what you can change.
* Want to store a video for a long time, or upload it to a lot of people? Compression is more important than how long it takes?
  * Grab the `Archive.cfg` sample. As always, there are comments inside.
* Want to temporarily store a video as an intermediate step in some editing process?
  * Grab the `Lossless.cfg` sample. Warning, it will generate very, very big files.

If you're looking to not just compress your video or convert from one format to another, but actually edit it a bit, then look through the "Filter options" category. It has all the options to cut or stretch your video in both spacial and temporal dimensions.
* Increasing the framerate will only make it worse unless you speed the video up, as there's no way to actually generate new frames from nothing.
* Similarly, increasing the video resolution will only make it look worse, as there is nowhere to get extra information from to fill the extra space. (Unless you're trying to bypass YouTube's bitrate limits.)
* But decreasing the video resolution generally also isn't necessary, even if you lower the bitrate.

</div>
