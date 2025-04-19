using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static Constants;

static class Program {
	static void Main(string[] args) {
		//If any arguments are config files, use them as overrides and remove them from the arguments
		List<string> argList = new(args.Length);
		foreach (string arg in args) {
			FileInfo file = new(arg);
			if (file.Exists && file.Extension == ".cfg") {
				Config.Override(file);
			} else {
				argList.Add(arg);
			}
		}

		//The remaining argments are expected to be input media files
		if (argList.Count > 0) {
			Config.ReplaceInputFiles([.. argList]);
		}

		foreach ((FileInfo input, DirectoryInfo? inputDir) in Config.inputFiles) {
			Convert(input, inputDir);
		}

		foreach (FileInfo logFile in new DirectoryInfo(Environment.CurrentDirectory).EnumerateFiles("*.log", SearchOption.TopDirectoryOnly)) {
			logFile.Delete();
		}
	}

	static void Convert(FileInfo inputFile, DirectoryInfo? inputDirectory) {
		Video input;
		try {
			input = new Video(inputFile);
		} catch {
			Console.WriteLine("ffprobe not found\n");
			input = new Video();
		}

		Encode encode = new();
		Encode.ArgList inArgs = encode.AddInput(inputFile.FullName);
		Encode.ArgList outArgs = encode.outputArgs;

		if (Config.overwrite) {
			encode.globalArgs.Add("y");
		}

		#region Filter options
		List<string> filters = [];
		if (Config.videoFilterPrepend != "") {
			filters.Add(Config.videoFilterPrepend);
		}

		if (Config.colorRange != "") {
			filters.Add($"setrange={Config.colorRange}");
		}

		if (Config.startTime is not null) {
			inArgs.Add("ss", FormatTimeSpan(Config.startTime.Value));
		}
		if (Config.duration is not null) {
			inArgs.Add("t", FormatTimeSpan(Config.duration.Value));
		} else if (Config.endTime is not null) {
			inArgs.Add("t", FormatTimeSpan(Config.endTime.Value - (Config.startTime ?? TimeSpan.Zero)));
		}

		if (Config.cropWidth != "" || Config.cropHeight != "") {
			List<string> cropOptions = [];
			if (Config.cropWidth != "") {
				cropOptions.Add($"w={Config.cropWidth}");
			}
			if (Config.cropHeight != "") {
				cropOptions.Add($"h={Config.cropHeight}");
			}
			if (Config.cropLeft != "") {
				cropOptions.Add($"x={Config.cropLeft}");
			}
			if (Config.cropTop != "") {
				cropOptions.Add($"y={Config.cropTop}");
			}
			filters.Add($"crop={string.Join(':', cropOptions)}");
		}

		if (Config.width != "" || Config.height != "") {
			filters.Add($"scale={(Config.width == "" ? "0" : Config.width)}:{(Config.height == "" ? "0" : Config.height)}");
		}

		if (Config.blendFrames && input.realValues) {
			float framerateMult = Config.framerate is null ? Config.tempo : (input.fps * Config.tempo / Config.framerate.Value);
			if (framerateMult > 1.5f) {
				filters.Add($"tmix=frames={framerateMult:0}:weights=1");
			}
		}

		if (Config.tempo != 1) {
			filters.Add($"setpts={1 / Config.tempo}*PTS");

			string atempo = "";
			float tempoResidual = Config.tempo;
			while (tempoResidual < 0.5) {
				atempo += "atempo=0.5,";
				tempoResidual *= 2;
			}
			atempo += $"atempo={tempoResidual}";
			outArgs.Add("af", atempo);
		}

		if (Config.videoFilterAppend != "") {
			filters.Add(Config.videoFilterAppend);
		}

		if (filters.Count > 0) {
			outArgs.Add("vf", string.Join(',', filters));
		}

		if (Config.framerate is not null) {
			outArgs.Add("r:v", Config.framerate.Value.ToString());
		}
		#endregion

		#region Video options
		if (Config.videoEncoder == "") {
			outArgs.Add("vn");
		} else {
			outArgs.Add("c:v", Config.videoEncoder);

			if (Config.pixelFormat != "") {
				outArgs.Add("pix_fmt", Config.pixelFormat);
			}

			if (Config.lossless) {
				if (Config.videoEncoder == vpxvp9) {
					outArgs.Add("lossless", "1");
				} else if (Config.videoEncoder == svtav1) {
					outArgs.Add("svtav1-params", "lossless=1");
				} else if (Config.videoEncoder == aomav1 || Config.videoEncoder == x264) {
					outArgs.Add("crf", "0");
				} else if (Config.videoEncoder == x265) {
					outArgs.Add("x265-params", "lossless=1");
				} else if (Config.videoEncoder.EndsWith(nvenc)) {
					outArgs.Add("tune", "lossless");
				} else {
					throw new Exception($"Lossless mode not supported for {Config.videoEncoder}");
				}
			} else if (Config.quality is not null) {
				if (Config.videoEncoder == vvenc) {
					outArgs.Add("qp", Config.quality.ToString()!);
				} else if (Config.videoEncoder.EndsWith(nvenc)) {
					outArgs.Add("cq", Config.quality.ToString()!);
				} else if (Config.videoEncoder.EndsWith(amf)) {
					outArgs.Add("rc", "qvbr");
					outArgs.Add("qvbr_quality_level", Config.quality.ToString()!);
				} else {
					outArgs.Add("crf", Config.quality.ToString()!);
				}

				if (Config.videoEncoder == vpxvp9) { //VP9 evidently needs this, else it will default to constrained, not constant quality
					outArgs.Add("b:v", "0");
				}
			} else {
				if (Config.videoEncoder.EndsWith(amf)) {
					outArgs.Add("rc", "hqvbr");
				}

				TimeSpan duration = Config.targetSize == "" ? TimeSpan.Zero : Config.duration ?? ((Config.endTime ?? input.duration) - (Config.startTime ?? TimeSpan.Zero));
				outArgs.Add("b:v", duration > TimeSpan.Zero ? GetTargetBitrate(duration) : Config.videoBitrate);
			}
		}

		if (Config.videoEncoder == vpxvp9 || Config.videoEncoder == aomav1) {
			outArgs.Add("cpu-used", Config.speed.ToString());
			outArgs.Add("row-mt", "1");
		} else if (Config.videoEncoder == x265 || Config.videoEncoder == x264) {
			outArgs.Add("preset", (9 - Config.speed).ToString());
		} else if (Config.videoEncoder == vvenc) {
			outArgs.Add("preset", (4 - Config.speed).ToString());
		} else if (Config.videoEncoder.EndsWith(nvenc)) {
			outArgs.Add("preset", $"p{7 - Config.speed}");
		} else if (Config.videoEncoder.EndsWith(amf)) {
			outArgs.Add("preset", (Config.videoEncoder.StartsWith("av1") ? Config.speed : Config.speed + 1) switch { 0 => "high_quality", 1 => "quality", 2 => "balanced", 3 => "speed", _ => throw new Exception("Invalid speed value for AMF encoder") });
		} else if (Config.videoEncoder != "") {
			outArgs.Add("preset", Config.speed.ToString());
		}

		//Google's reference encoder defaults aren't very good at multithreading or producing fast decodable video - they need help.
		if (Config.videoEncoder == aomav1) {
			outArgs.Add("tiles", $"{GetTiles(input.width, false)}x{GetTiles(input.height, false)}");
			outArgs.Add("g", Math.Min(input.fps * 12, 1440).ToString());
		} else if (Config.videoEncoder == vpxvp9) {
			outArgs.Add("tile-rows", Math.Min(GetTiles(input.height, true), 2).ToString());
			outArgs.Add("tile-columns", Math.Min(GetTiles(input.width, true), 6).ToString());
		}
		#endregion

		#region Audio options
		if (Config.audioEncoder == "") {
			outArgs.Add("an");
		} else {
			outArgs.Add("c:a", Config.audioEncoder);
			if (Config.audioEncoder != "copy" && Config.audioEncoder != flac) {
				outArgs.Add("b:a", Config.audioBitrate);
			}

			if (Config.audioChannels is not null && Config.audioEncoder != "copy") {
				outArgs.Add("ac", Config.audioChannels.ToString()!);
			}
		}

		if (Config.audioEncoder == opus) {
			outArgs.Add("frame_duration", "60");
		} else if (Config.audioEncoder == flac) {
			outArgs.Add("compression_level", "12");
		} else if (Config.audioEncoder == mp3) {
			outArgs.Add("abr", "1");
			outArgs.Add("compression_level", "0");
		}
		#endregion

		#region Other options
		if (Config.removeSubtitles) {
			outArgs.Add("sn");
		}
		if (Config.removeMetadata) {
			outArgs.Add("map_metadata", "-1");
		}
		if (Config.removeChapters) {
			outArgs.Add("map_chapters", "-1");
		}

		AddExtraOptions(Config.extraInputOptions, inArgs);
		AddExtraOptions(Config.extraOutputOptions, outArgs);
		#endregion

		#region Output file
		DirectoryInfo outputDirectory;
		if (Config.outputDirectory is not null) {
			outputDirectory = inputDirectory is null
				? Config.outputDirectory
				: new FileInfo(Path.Combine(Config.outputDirectory.FullName, Path.GetRelativePath(inputDirectory.Parent!.FullName, inputFile.FullName))).Directory!;

			if (Config.createDirectoryIfNeeded) {
				outputDirectory.Create();
			} else if (!Config.outputDirectory.Exists) {
				throw new Exception($"Output directory {outputDirectory.FullName} does not exist");
			}
		} else {
			outputDirectory = inputFile.Directory!;
		}
		FileInfo output = new($"{outputDirectory.FullName}/{Config.outputPrefix}{Path.GetFileNameWithoutExtension(inputFile.Name)}{Config.outputSuffix}{Config.outputExtension}");
		if (Array.Exists(Config.inputFiles, input => input.input.FullName == output.FullName)) {
			throw new Exception($"{output.Name} would overwrite an input file");
		}
		encode.outFile = output.FullName;
		#endregion

		#region Two-pass
		if (Config.twoPass) {
			outArgs.Add("pass", "2");
			outArgs.Add("passlogfile", Path.GetFileNameWithoutExtension(output.Name));
			Encode firstPass = encode.Clone();
			encode.dependsOn = firstPass;
			firstPass.outputArgs.Replace("pass", "1");

			foreach (string key in new string[] { "cpu-used", "c:a", "b:a", "ac", "frame_duration", "compression_level", "abr" }) {
				firstPass.outputArgs.Remove(key);
			}
			AddNoOutputArgs(firstPass);
		}
		#endregion

		#region Benchmark
		Stopwatch timer = Stopwatch.StartNew();
		TimeSpan totalProcessorTime = encode.Start();
		timer.Stop();

		if (Config.benchmark && !Config.simulate) {
			Console.WriteLine($"Real time taken: {FormatTime(timer.Elapsed)}");
			Console.WriteLine($" CPU time taken: {FormatTime(totalProcessorTime)}");
			Console.WriteLine();
		}
		#endregion

		#region Comparison
		if (Config.compare != "") {
			Encode comparison = new();
			Encode.ArgList distorted = comparison.AddInput(output.FullName);
			Encode.ArgList reference = comparison.AddInput(inputFile.FullName);

			string filterString = Config.compare;
			if (Config.compare == vmaf) {
				filterString += $"=model=version=vmaf_v0.6.1\\\\:motion.motion_force_zero=true:n_threads={Environment.ProcessorCount}";
			}
			if (Config.compareSync == 1) {
				distorted.Add("r", "1");
				reference.Add("r", "1");
			} else if (Config.compareSync == 2) {
				filterString += ":ts_sync_mode=nearest";
			}
			if (Config.compareInterval > 1) {
				filterString = $"[0:v]select=not(mod(n\\,{Config.compareInterval}))[v0];[1:v]select=not(mod(n\\,{Config.compareInterval}))[v1];[v0][v1]" + filterString;
			}
			comparison.outputArgs.Add("lavfi", filterString);
			AddNoOutputArgs(comparison);

			comparison.Start();
		}
		#endregion
	}

	static void AddExtraOptions(Dictionary<string, string> options, Encode.ArgList args) {
		foreach (KeyValuePair<string, string> option in options) {
			args.Add(option.Key);
			if (option.Value != "") {
				args.Replace(option.Key, option.Value);
			}
		}
	}

	static void AddNoOutputArgs(Encode encode) {
		Encode.ArgList args = encode.outputArgs;
		args.Add("an");
		args.Add("sn");
		args.Add("map_metadata", "-1");
		args.Add("map_chapters", "-1");
		args.Add("f", "null");
		encode.outFile = "-";
	}

	static string FormatTime(TimeSpan time) {
		string timeString = "";
		if (time.TotalHours >= 1) {
			timeString += $"{(int)time.TotalHours}h ";
		}
		if (time.TotalMinutes >= 1) {
			timeString += $"{time.Minutes}m ";
		}
		timeString += $"{time.Seconds}s";
		return timeString;
	}

	static string FormatTimeSpan(TimeSpan time) {
		string timeString = "";
		if (time.TotalHours >= 1) {
			timeString += $"{(int)time.TotalHours}:";
		}
		if (time.TotalMinutes >= 1) {
			timeString += $"{time.Minutes:00}:";
		}
		timeString += $"{time.TotalSeconds % 60:00.####}";
		return timeString;
	}

	static int GetTiles(int pixels, bool log) {
		if (pixels < 1366) {
			return log ? 0 : 1;
		}

		int tiles = pixels / 1024;
		if (log) {
			tiles = Math.ILogB(tiles);
		}
		return Math.Abs(pixels / (log ? 1 << tiles : tiles) - 1024) < Math.Abs(pixels / (log ? 1 << (tiles + 1) : tiles + 1) - 1024) ? tiles : tiles + 1;
	}

	static long ParseBits(ReadOnlySpan<char> value, string optionName) {
		long suffix = 1;
		if (value[^1] == 'B') {
			suffix = 8;
			value = value[..^1];
		}

		bool isPowerOf1024 = false;
		if (value[^1] == 'i') {
			isPowerOf1024 = true;
			value = value[..^1];
		}

		if (value[^1] == 'K') {
			suffix *= isPowerOf1024 ? 1024 : 1000;
			value = value[..^1];
		} else if (value[^1] == 'M') {
			suffix *= isPowerOf1024 ? 1024 * 1024 : 1000 * 1000;
			value = value[..^1];
		} else if (value[^1] == 'G') {
			suffix *= isPowerOf1024 ? 1024 * 1024 * 1024 : 1000 * 1000 * 1000;
			value = value[..^1];
		}

		return double.TryParse(value, out double bits) ? (long)(bits * suffix) : throw new FormatException($"Failed to parse {optionName}");
	}

	static string GetTargetBitrate(TimeSpan duration) {
		double bitsPerSecond = ParseBits(Config.targetSize, "target size") / duration.TotalSeconds;
		if (Config.audioEncoder is not null) {
			bitsPerSecond -= ParseBits(Config.audioBitrate, "audio bitrate");
		}

		return bitsPerSecond > 1024 * 1024 * 1024 ? $"{bitsPerSecond / (1024 * 1024 * 1024):0.##}Gi"
			: bitsPerSecond >= 1024 * 1024 ? $"{bitsPerSecond / (1024 * 1024):0.##}Mi"
			: bitsPerSecond >= 10000 ? $"{bitsPerSecond / 1024:0.##}Ki"
			: $"{bitsPerSecond:0}";
	}
}
