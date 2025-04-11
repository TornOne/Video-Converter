﻿using System;
using System.Diagnostics;
using System.IO;

class Video {
	public bool realValues = false;
	public readonly int width = 2560;
	public readonly int height = 1440;
	public readonly Fraction fps = 60;
	public TimeSpan duration;

	public Video() { }

	public Video(FileInfo file) {
		ProcessStartInfo startInfo = new(Config.ffprobe.FullName, ["-hide_banner", "-of", "default=nw=1", "-select_streams", "v:0", "-show_entries", "stream=width,height,avg_frame_rate:format=duration", file.FullName]) {
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		Process process = Process.Start(startInfo)!;
		StreamReader stdout = process.StandardOutput;

		realValues = true;
		while (!stdout.EndOfStream) {
			string[] pair = process.StandardOutput.ReadLine()!.Split('=', 2);
			string key = pair[0];
			string value = pair[1];

			if (key == "width") {
				width = int.Parse(value);
			} else if (key == "height") {
				height = int.Parse(value);
			} else if (key == "avg_frame_rate") {
				fps = Fraction.Parse(value);
			} else if (key == "duration") {
				duration = TimeSpan.FromSeconds(double.Parse(value));
			}
		}
	}
}
