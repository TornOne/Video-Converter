using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static Constants;

static class Program {
	static void Main(string[] args) {
		Environment.CurrentDirectory = Path.GetDirectoryName(Environment.ProcessPath)!;

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

		foreach (FileInfo input in Config.inputFiles) {
			Convert(input);
		}

		foreach (FileInfo logFile in new DirectoryInfo(Environment.CurrentDirectory).EnumerateFiles("*.log", SearchOption.TopDirectoryOnly)) {
			logFile.Delete();
		}
	}

	static void Convert(FileInfo input) {
		Encode encode = new();
		Encode.ArgList outArgs = encode.outputArgs;

		if (Config.overwrite) {
			encode.globalArgs.Add("y");
		}

		encode.AddInput(input.FullName);

		#region Filter options
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
				} else {
					throw new Exception($"Lossless mode not supported for {Config.videoEncoder}");
				}
			} else if (Config.quality is null) {
				outArgs.Add("b:v", Config.videoBitrate);
			} else {
				if (Config.videoEncoder == vvenc) {
					outArgs.Add("qp", Config.quality.ToString()!);
				} else {
					outArgs.Add("crf", Config.quality.ToString()!);
				}

				if (Config.videoEncoder == vpxvp9) { //VP9 evidently needs this, else it will default to constrained, not constant quality
					outArgs.Add("b:v", "0");
				}
			}
		}

		if (Config.videoEncoder == vpxvp9 || Config.videoEncoder == aomav1) {
			outArgs.Add("cpu-used", Config.speed.ToString());
			outArgs.Add("row-mt", "1");
		} else if (Config.videoEncoder == x265 || Config.videoEncoder == x264) {
			outArgs.Add("preset", (9 - Config.speed).ToString());
		} else if (Config.videoEncoder == vvenc) {
			outArgs.Add("preset", (4 - Config.speed).ToString());
		} else if (Config.videoEncoder != "") {
			outArgs.Add("preset", Config.speed.ToString());
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

			if (Config.audioChannels is not null) {
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
		#endregion

		#region Output file
		if (Config.outputDirectory is not null) {
			if (Config.createDirectoryIfNeeded) {
				Config.outputDirectory.Create();
			} else if (!Config.outputDirectory.Exists) {
				throw new Exception($"Output directory {Config.outputDirectory.FullName} does not exist");
			}
		}
		FileInfo output = new($"{(Config.outputDirectory ?? input.Directory!).FullName}/{Config.outputPrefix}{Path.GetFileNameWithoutExtension(input.Name)}{Config.outputSuffix}{Config.outputExtension}");
		if (Array.Exists(Config.inputFiles, input => input.FullName == output.FullName)) {
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
			Encode.ArgList reference = comparison.AddInput(input.FullName);

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
}
