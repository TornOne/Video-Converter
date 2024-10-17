using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

static class Program {
	static void Main(string[] args) {
		Environment.CurrentDirectory = Path.GetDirectoryName(Environment.ProcessPath)!;

		//If the first argument is a config file, use it as an override and remove it from the arguments
		if (args.Length > 0) {
			FileInfo file = new(args[0]);
			if (file.Exists && file.Extension == ".cfg") {
				Config.Override(file);
				args = args[1..];
			}
		}

		//The remaining argments are expected to be input media files
		if (args.Length > 0) {
			Config.ReplaceInputFiles(args);
		}

		foreach (FileInfo input in Config.inputFiles) {
			//Global options
			List<string> arguments = [ "-hide_banner" ];
			if (Config.overwrite) {
				arguments.Add("-y");
			}

			//Input options

			//Input file
			arguments.Add("-i");
			arguments.Add(input.FullName);

			//Output options
			if (Config.videoEncoder == "") {
				arguments.Add("-vn");
			} else {
				arguments.Add("-c:v");
				arguments.Add(Config.videoEncoder);
			}

			if (Config.audioEncoder == "") {
				arguments.Add("-an");
			} else {
				arguments.Add("-c:a");
				arguments.Add(Config.audioEncoder);
			}

			//Output file
			if (Config.outputDirectory is not null) {
				if (Config.createDirectoryIfNeeded) {
					Config.outputDirectory.Create();
				} else if (!Config.outputDirectory.Exists) {
					throw new Exception("Output directory does not exist");
				}
			}
			FileInfo output = new($"{(Config.outputDirectory ?? input.Directory!).FullName}/{Config.outputPrefix}{Path.GetFileNameWithoutExtension(input.Name)}{Config.outputSuffix}{Config.outputExtension}");
			if (Array.Exists(Config.inputFiles, input => input.FullName == output.FullName)) {
				throw new Exception("Output would overwrite an input file");
			}
			arguments.Add(output.FullName);

			Console.WriteLine(Config.ffmpeg.FullName + ' ' + string.Join(' ', arguments));
			ProcessStartInfo startInfo = new(Config.ffmpeg.FullName, arguments);
			//Console.WriteLine(Config.ffmpeg.FullName + ' ' + startInfo.Arguments);
			//continue;
			Process ffmpegProcess = Process.Start(startInfo)!;
			ffmpegProcess.PriorityClass = Config.priority;
			if (Config.cpuAffinity != 0 && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())) {
				ffmpegProcess.ProcessorAffinity = Config.cpuAffinity;
			}
			//TODO: Track the conversion process and update the output (including the ETA)
			ffmpegProcess.WaitForExit();
		}
	}
}
