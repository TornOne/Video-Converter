using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

class Config {
	#region File options
	public static FileInfo ffmpeg = new("ffmpeg.exe");
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
	public static string videoEncoder = "libvpx-vp9";
	public static string videoBitrate = "160KiB";
	public static int? quality = 21;
	public static int speed = 1;
	#endregion

	#region Audio options
	public static string audioEncoder = "libopus";
	public static string audioBitrate = "128Ki";
	#endregion

	#region CPU options
	public static ProcessPriorityClass priority = ProcessPriorityClass.BelowNormal;
	public static nint cpuAffinity = 0;
	#endregion

	static readonly Dictionary<string, Action<string>> setters = new() {
		#region File options
		{ nameof(ffmpeg), path => {
			ffmpeg = new(path);
			if (!ffmpeg.Exists) {
				throw new FileNotFoundException("ffmpeg not found", ffmpeg.FullName);
			}
		} },
		{ nameof(inputFiles), paths => {
			inputFiles = Array.ConvertAll(paths.Split('\n', StringSplitOptions.RemoveEmptyEntries), path => new FileInfo(path));
			foreach (FileInfo input in inputFiles) {
				if (!input.Exists) {
					throw new FileNotFoundException("Input file not found", input.FullName);
				}
			}
		} },
		{ nameof(outputDirectory), path => outputDirectory = path == "" ? null : new(path)},
		{ nameof(createDirectoryIfNeeded), value => createDirectoryIfNeeded = value == "true" },
		{ nameof(outputPrefix), prefix => outputPrefix = prefix },
		{ nameof(outputSuffix), suffix => outputSuffix = suffix },
		{ nameof(outputExtension), extension => outputExtension = extension == "" ? "" : "." + extension },
		{ nameof(overwrite), value => overwrite = value == "true" },
		{ nameof(simulate), value => simulate = value == "true" },
		#endregion

		#region Video options
		{ nameof(videoEncoder), name => videoEncoder = name },
		{ nameof(videoBitrate), value => videoBitrate = value },
		{ nameof(quality), value => quality = value == "" ? null : int.Parse(value) },
		{ nameof(speed), value => speed = int.Parse(value) },
		#endregion

		#region Audio options
		{ nameof(audioEncoder), name => audioEncoder = name },
		{ nameof(audioBitrate), value => audioBitrate = value },
		#endregion

		#region CPU options
		{ nameof(priority), value => priority = Enum.Parse<ProcessPriorityClass>(value) },
		{ nameof(cpuAffinity), value => cpuAffinity = value.StartsWith("0b") ? (nint)Convert.ToUInt64(value[2..], 2) : (1 << int.Parse(value)) - 1}
		#endregion
	};

	static Config() {
		Override(new FileInfo("defaults.cfg"));
	}

	public static void ReplaceInputFiles(string[] args) => setters[nameof(inputFiles)](string.Join('\n', args));

	public static void Override(FileInfo file) {
		if (!file.Exists) {
			throw new FileNotFoundException("Configuration file not found", file.FullName);
		}

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

		Exception CreateEx(string msg, Exception? ex = null) => new($"Error in {file.Name} line {lineNo}:\n{msg}", ex);
	}
}
