using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

class Encode {
	public class ArgList {
		readonly List<string> args = [];
		readonly Dictionary<string, string> argValues = [];

		/// <summary>Adds an argument without a value.</summary>
		/// <returns><see langword="false"/> if <paramref name="key"/> already exists, <see langword="true"/> otherwise.</returns>
		public bool Add(string key) {
			if (Contains(key)) {
				return false;
			}
			args.Add(key);
			return true;
		}

		/// <summary>Adds an argument with the given <paramref name="value"/>.</summary>
		/// <returns><see langword="false"/> if <paramref name="key"/> already exists, <see langword="true"/> otherwise.</returns>
		public bool Add(string key, string value) {
			if (Contains(key)) {
				return false;
			}
			args.Add(key);
			argValues[key] = value;
			return true;
		}

		/// <summary>If they given argument exists, its value becomes <paramref name="value"/>. If it doesn't exist and <paramref name="addIfAbsent"/> is <see langword="true"/>, it is added.</summary>
		/// <returns><see langword="true"/> if <paramref name="key"/> exists and the value is thus replaced, <see langword="false"/> otherwise.</returns>
		public bool Replace(string key, string value, bool addIfAbsent = false) {
			if (argValues.ContainsKey(key)) {
				argValues[key] = value;
				return true;
			}
			if (addIfAbsent) {
				Add(key, value);
			}
			return false;
		}

		/// <summary>Replaces the argument <paramref name="oldKey"/> with <paramref name="newKey"/>, keeping its position. The old value is removed, and a new value, if given, is added.</summary>
		/// <returns><see langword="true"/> if <paramref name="key"/> exists and is thus replaced, <see langword="false"/> otherwise.</returns>
		public bool ReplaceKey(string oldKey, string newKey, string? value = null) {
			int i = args.IndexOf(oldKey);
			if (i < 0) {
				return false;
			}

			args[i] = newKey;
			argValues.Remove(oldKey);
			if (value is not null) {
				argValues[newKey] = value;
			}
			return true;
		}

		/// <summary>Removes the given <paramref name="key"/> from the argument list along with its associated value, if present.</summary>
		/// <returns><see langword="true"/> if <paramref name="key"/> exists and is thus removed, <see langword="false"/> otherwise.</returns>
		public bool Remove(string key) {
			argValues.Remove(key);
			return args.Remove(key);
		}

		public bool Contains(string key) => args.Contains(key);

		public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out string value) => argValues.TryGetValue(key, out value);

		public ArgList CloneTo(ArgList clone) {
			foreach (string arg in args) {
				clone.Add(arg);
				if (argValues.TryGetValue(arg, out string? value)) {
					clone.argValues[arg] = value;
				}
			}
			return clone;
		}

		public List<string> GetArguments() {
			List<string> fullArgs = [];
			foreach (string arg in args) {
				fullArgs.Add($"-{arg}");
				if (argValues.TryGetValue(arg, out string? value)) {
					fullArgs.Add(value);
				}
			}
			return fullArgs;
		}
	}

	public readonly ArgList globalArgs = new();
	public readonly Dictionary<string, ArgList> inputArgs = [];
	public readonly ArgList outputArgs = new();
	public string outFile = "";
	public Encode? dependsOn;

	public Encode() {
		globalArgs.Add("hide_banner");
	}

	public ArgList AddInput(string input) => inputArgs[input] = new();

	public Encode Clone() {
		Encode clone = new();
		globalArgs.CloneTo(clone.globalArgs);
		foreach (KeyValuePair<string, ArgList> input in inputArgs) {
			ArgList cloneInputArgs = new();
			input.Value.CloneTo(cloneInputArgs);
			clone.inputArgs.Add(input.Key, cloneInputArgs);
		}
		outputArgs.CloneTo(clone.outputArgs);
		clone.outFile = outFile;
		clone.dependsOn = dependsOn;
		return clone;
	}

	List<string> GetArguments() {
		List<string> fullArgs = [];
		fullArgs.AddRange(globalArgs.GetArguments());
		foreach (KeyValuePair<string, ArgList> input in inputArgs) {
			fullArgs.AddRange(input.Value.GetArguments());
			fullArgs.Add("-i");
			fullArgs.Add(input.Key);
		}
		fullArgs.AddRange(outputArgs.GetArguments());
		fullArgs.Add(outFile);
		return fullArgs;
	}

	public TimeSpan Start() {
		TimeSpan totalProcessorTime = dependsOn?.Start() ?? TimeSpan.Zero;

		Console.WriteLine(ToString());
		Console.WriteLine();

		if (Config.simulate) {
			return totalProcessorTime;
		}

		Process ffmpegProcess = Process.Start(Config.ffmpeg?.FullName ?? "ffmpeg", GetArguments());
		ffmpegProcess.PriorityClass = Config.priority;
		if (Config.cpuAffinity != 0 && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())) {
			ffmpegProcess.ProcessorAffinity = Config.cpuAffinity;
		}
		//TODO: Track the conversion process and update the output (including the ETA)
		ffmpegProcess.WaitForExit();
		Console.WriteLine();
		return totalProcessorTime + ffmpegProcess.TotalProcessorTime;
	}

	public override string ToString() {
		StringBuilder command = new();
		command.Append(Config.ffmpeg?.FullName ?? "ffmpeg");
		foreach (string arg in GetArguments()) {
			command.Append(' ');
			//TODO: This might need more work for special cases (https://ffmpeg.org/ffmpeg-all.html#toc-Quoting-and-escaping)
			if (arg.Contains(' ')) {
				command.Append('"');
				command.Append(arg);
				command.Append('"');
			} else {
				command.Append(arg);
			}
		}
		return command.ToString();
	}
}
