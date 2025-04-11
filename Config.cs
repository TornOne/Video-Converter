﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

static class Config {
	#region File options
	public static FileInfo ffmpeg = new("ffmpeg.exe");
	public static FileInfo ffprobe = new("ffprobe.exe");
	public static FileInfo[] inputFiles = [];
	public static DirectoryInfo? outputDirectory;
	public static bool createDirectoryIfNeeded = true;
	public static string outputPrefix = "";
	public static string outputSuffix = " out";
	public static string outputExtension = ".webm";
	public static bool overwrite = true;
	public static bool simulate = false;
	#endregion

	#region Video options
	public static string videoEncoder = Constants.svtav1;
	public static string videoBitrate = "640KiB";
	public static int? quality = 30;
	public static int speed = 4;
	public static bool twoPass = false;
	public static string pixelFormat = "";
	public static bool lossless = false;
	#endregion

	#region Audio options
	public static string audioEncoder = "libopus";
	public static string audioBitrate = "128Ki";
	public static int? audioChannels = null;
	#endregion

	#region Other options
	public static bool removeSubtitles = false;
	public static bool removeMetadata = false;
	public static bool removeChapters = false;
	public static bool benchmark = false;
	public static string compare = "";
	public static int compareSync = 1;
	public static int compareInterval = 1;
	#endregion

	#region CPU options
	public static ProcessPriorityClass priority = ProcessPriorityClass.BelowNormal;
	public static nint cpuAffinity = 0;
	#endregion

	static readonly Dictionary<string, Action<string>> setters = new() {
		#region File options
		{ nameof(ffmpeg), path => ffmpeg = new(path)},
		{ nameof(ffprobe), path => ffprobe = new(path)},
		{ nameof(inputFiles), paths => {
			List<FileInfo> files = [];
			foreach (string path in paths.Split("\n", StringSplitOptions.RemoveEmptyEntries)) {
				DirectoryInfo dir = new(path);
				if (dir.Exists) {
					files.AddRange(dir.EnumerateFiles("*", SearchOption.AllDirectories));
					continue;
				}

				FileInfo file = new(path);
				if (file.Exists) {
					files.Add(file);
					continue;
				}

				throw new FileNotFoundException("Input file or directory not found", file.FullName);
			}
			inputFiles = [.. files];
		} },
		{ nameof(outputDirectory), path => outputDirectory = path == "" ? null : new DirectoryInfo(path)},
		{ nameof(createDirectoryIfNeeded), value => createDirectoryIfNeeded = bool.Parse(value) },
		{ nameof(outputPrefix), prefix => outputPrefix = prefix },
		{ nameof(outputSuffix), suffix => outputSuffix = suffix },
		{ nameof(outputExtension), extension => outputExtension = extension == "" ? "" : "." + extension },
		{ nameof(overwrite), value => overwrite = bool.Parse(value) },
		{ nameof(simulate), value => simulate = bool.Parse(value) },
		#endregion

		#region Video options
		{ nameof(videoEncoder), name => videoEncoder = name },
		{ nameof(videoBitrate), value => videoBitrate = value },
		{ nameof(quality), value => quality = value == "" ? null : int.Parse(value) },
		{ nameof(speed), value => speed = int.Parse(value) },
		{ nameof(twoPass), value => twoPass = bool.Parse(value) },
		{ nameof(pixelFormat), value => pixelFormat = value },
		{ nameof(lossless), value => lossless = bool.Parse(value) },
		#endregion

		#region Audio options
		{ nameof(audioEncoder), name => audioEncoder = name },
		{ nameof(audioBitrate), value => audioBitrate = value },
		{ nameof(audioChannels), value => audioChannels = value == "" ? null : int.Parse(value) },
		#endregion

		#region Other options
		{ nameof(removeSubtitles), value => removeSubtitles = bool.Parse(value) },
		{ nameof(removeMetadata), value => removeMetadata = bool.Parse(value) },
		{ nameof(removeChapters), value => removeChapters = bool.Parse(value) },
		{ nameof(benchmark), value => benchmark = bool.Parse(value) },
		{ nameof(compare), value => compare = value },
		{ nameof(compareSync), value => compareSync = int.Parse(value) },
		{ nameof(compareInterval), value => compareInterval = int.Parse(value) },
		#endregion

		#region CPU options
		{ nameof(priority), value => priority = Enum.Parse<ProcessPriorityClass>(value) },
		{ nameof(cpuAffinity), value => cpuAffinity = value.StartsWith("0b") ? (nint)Convert.ToUInt64(value[2..], 2) : (1 << int.Parse(value)) - 1}
		#endregion
	};

	static Config() {
		Override(new FileInfo($"{AppContext.BaseDirectory}/defaults.cfg"));
	}

	public static void ReplaceInputFiles(string[] args) => setters[nameof(inputFiles)](string.Join('\n', args));

	public static void Override(FileInfo file) {
		if (!file.Exists) {
			throw new FileNotFoundException("Configuration file not found", file.FullName);
		}

		string currentDirectory = Environment.CurrentDirectory;
		Environment.CurrentDirectory = file.DirectoryName!;

		int lineNo = 0;
		foreach (string line in File.ReadLines(file.FullName)) {
			lineNo++;

			//Ignore empty lines and comments
			if (line.Length == 0 || line[0] == '#') {
				continue;
			}

			string[] parts = line.Split(['='], 2);
			if (parts.Length < 2) {
				throw CreateEx($"Expected to find a \"=\"");
			}
			string name = parts[0];
			string value = parts[1].Replace("\\n", "\n"); //Only newlines can be escaped

			if (!setters.TryGetValue(name, out Action<string>? Setter)) {
				throw CreateEx($"Invalid setting name: \"{name}\"");
			}

			try {
				Setter(value);
			} catch (Exception ex) {
				throw CreateEx($"Failed to validate value for setting \"{name}\"", ex);
			}
		}

		Environment.CurrentDirectory = currentDirectory;

		Exception CreateEx(string msg, Exception? ex = null) => new($"Error in {file.Name} line {lineNo}:\n{msg}", ex);
	}
}
