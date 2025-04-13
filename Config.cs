using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

static partial class Config {
	#region File options
	public static FileInfo ffmpeg = new("ffmpeg.exe");
	public static FileInfo ffprobe = new("ffprobe.exe");
	public static (FileInfo input, DirectoryInfo? dir)[] inputFiles = [];
	public static DirectoryInfo? outputDirectory;
	public static bool createDirectoryIfNeeded = true;
	public static string outputPrefix = "";
	public static string outputSuffix = " out";
	public static string outputExtension = ".webm";
	public static bool overwrite = true;
	public static bool simulate = false;
	#endregion

	#region Filter options
	public static string colorRange = "";
	public static TimeSpan? startTime = null;
	public static TimeSpan? endTime = null;
	public static TimeSpan? duration = null;
	public static string cropWidth = "";
	public static string cropHeight = "";
	public static string cropLeft = "";
	public static string cropTop = "";
	public static string width = "";
	public static string height = "";
	public static float tempo = 1;
	public static Fraction? framerate = null;
	public static bool blendFrames = false;
	public static string videoFilterPrepend = "";
	public static string videoFilterAppend = "";
	#endregion

	#region Video options
	public static string videoEncoder = Constants.svtav1;
	public static string videoBitrate = "640KiB";
	public static string targetSize = "";
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
	public static Dictionary<string, string> extraInputOptions = [];
	public static Dictionary<string, string> extraOutputOptions = [];
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
			List<(FileInfo, DirectoryInfo?)> files = [];
			foreach (string path in paths.Split("\n", StringSplitOptions.RemoveEmptyEntries)) {
				DirectoryInfo dir = new(path);
				if (dir.Exists) {
					foreach (FileInfo dirFile in dir.EnumerateFiles("*", SearchOption.AllDirectories)) {
						files.Add((dirFile, dir));
					}
					continue;
				}

				FileInfo file = new(path);
				if (file.Exists) {
					files.Add((file, null));
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

		#region Filter options
		{ nameof(colorRange), value => colorRange = value },
		{ nameof(startTime), value => startTime = ParseTimeSpan(value) },
		{ nameof(endTime), value => endTime = ParseTimeSpan(value) },
		{ nameof(duration), value => duration = ParseTimeSpan(value) },
		{ nameof(cropWidth), value => cropWidth = value },
		{ nameof(cropHeight), value => cropHeight = value },
		{ nameof(cropLeft), value => cropLeft = value },
		{ nameof(cropTop), value => cropTop = value },
		{ nameof(width), value => width = value },
		{ nameof(height), value => height = value },
		{ nameof(tempo), value => {
			tempo = float.Parse(value);
			if (tempo < 0.001f) {
				throw new ArgumentOutOfRangeException(nameof(tempo), "Tempo must be at least 0.001");
			}
		} },
		{ nameof(framerate), value => framerate = Fraction.Parse(value) },
		{ nameof(blendFrames), value => blendFrames = bool.Parse(value) },
		{ nameof(videoFilterPrepend), value => videoFilterPrepend = value },
		{ nameof(videoFilterAppend), value => videoFilterAppend = value },
		#endregion

		#region Video options
		{ nameof(videoEncoder), name => videoEncoder = name },
		{ nameof(videoBitrate), value => videoBitrate = value },
		{ nameof(targetSize), value => targetSize = value },
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
		{ nameof(extraInputOptions), value => extraInputOptions = ParseExtraOptions(value)},
		{ nameof(extraOutputOptions), value => extraOutputOptions = ParseExtraOptions(value) },
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

	static TimeSpan ParseTimeSpan(string value) {
		GroupCollection groups = TimeRegex().Match(value).Groups;
		return TimeSpan.FromHours(groups.TryGetValue("hours", out Group? hours) && hours.Success ? int.Parse(hours.ValueSpan) : 0,
			groups.TryGetValue("minutes", out Group? minutes) && minutes.Success ? int.Parse(minutes.ValueSpan) : 0,
			groups.TryGetValue("seconds", out Group? seconds) && seconds.Success ? int.Parse(seconds.ValueSpan) : 0,
			groups.TryGetValue("decimals", out Group? decimals) && decimals.Success ? (int)(double.Parse(decimals.ValueSpan) * 1000 + 0.5) : 0);
	}

	[GeneratedRegex("^(((?<hours>[0-9]+):)?(?<minutes>[0-9]+):)?(?<seconds>[0-9]+)(?<decimals>\\.[0-9]+)?$", RegexOptions.ExplicitCapture)]
	private static partial Regex TimeRegex();

	static Dictionary<string, string> ParseExtraOptions(string argString) {
		Dictionary<string, string> options = [];
		string key = "";
		List<string> value = [];
		void AddOption() {
			if (key != "") {
				options[key] = string.Join(' ', value);
			}
			value = [];
		}

		foreach (string arg in argString.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
			if (arg[0] == '-') {
				AddOption();
				key = arg[1..];
			} else {
				value.Add(arg);
			}
		}
		AddOption();

		return options;
	}
}

readonly struct Fraction(int num, int denom = 1) {
	public readonly int num = num, denom = denom;

	public static Fraction Parse(string value) {
		if (int.TryParse(value, out int num)) {
			return new Fraction(num);
		}

		string[] pair = value.Split("/");
		return new Fraction(int.Parse(pair[0]), int.Parse(pair[1]));
	}

	public static implicit operator float(Fraction f) => (float)f.num / f.denom;
	public static implicit operator Fraction(int num) => new(num);

	public override string ToString() => $"{num}{(denom > 1 ? $"/{denom}" : "")}";
}