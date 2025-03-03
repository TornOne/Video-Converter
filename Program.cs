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
			Encode encode = new();

			#region Global options
			encode.Add("hide_banner");
			if (Config.overwrite) {
				encode.Add("y");
			}
			#endregion

			#region Input options
			#endregion

			//Input file
			encode.Add("i", input.FullName);

			#region Filter options
			#endregion

			#region Video options
			if (Config.videoEncoder == "") {
				encode.Add("vn");
			} else {
				encode.Add("c:v", Config.videoEncoder);

				if (Config.quality is null) {
					encode.Add("b:v", Config.videoBitrate);
				} else {
					if (Config.videoEncoder == vvenc) {
						encode.Add("qp", Config.quality.ToString()!);
					} else {
						encode.Add("crf", Config.quality.ToString()!);
					}

					if (Config.videoEncoder == vpxvp9) {
						encode.Add("b:v", "0");
					}
				}
			}

			if (Config.videoEncoder == vpxvp9 || Config.videoEncoder == aomav1) {
				encode.Add("cpu-used", Config.speed.ToString());
			} else if (Config.videoEncoder == x265 || Config.videoEncoder == x264) {
				encode.Add("preset", (9 - Config.speed).ToString());
			} else if (Config.videoEncoder == vvenc) {
				encode.Add("preset", (4 - Config.speed).ToString());
			} else {
				encode.Add("preset", Config.speed.ToString());
			}

			#endregion

			#region Audio options
			if (Config.audioEncoder == "") {
				encode.Add("an");
			} else {
				encode.Add("c:a", Config.audioEncoder);
				if (Config.audioEncoder != "copy" && Config.audioEncoder != flac) {
					encode.Add("b:a", Config.audioBitrate);
				}
			}

			if (Config.audioEncoder == opus) {
				encode.Add("frame_duration", "60");
			} else if (Config.audioEncoder == flac) {
				encode.Add("compression_level", "12");
			} else if (Config.audioEncoder == mp3) {
				encode.Add("abr", "1");
				encode.Add("compression_level", "0");
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
				encode.Add("pass", "2");
				encode.Add("passlogfile", Path.GetFileNameWithoutExtension(output.Name));
				Encode firstPass = encode.Clone();
				encode.dependsOn = firstPass;
				firstPass.Replace("pass", "1");

				firstPass.Remove("cpu-used");
				firstPass.ReplaceKey("c:a", "an");
				foreach (string key in new string[] { "b:a", "frame_duration", "compression_level", "abr" }) {
					firstPass.Remove(key);
				}
				firstPass.Add("f", "null");
				firstPass.outFile = "-";
			}
			#endregion

			if (Config.simulate) {
				Console.WriteLine(encode);
			} else {
				encode.Start();
			}
		}
	}
}
