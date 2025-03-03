using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

// Documentation:
// VP9: https://trac.ffmpeg.org/wiki/Encode/VP9, https://developers.google.com/media/vp9/settings/vod, https://wiki.webmproject.org/ffmpeg/vp9-encoding-guide
// SVT-AV1: https://gitlab.com/AOMediaCodec/SVT-AV1/-/blob/master/Docs/Parameters.md, https://gitlab.com/AOMediaCodec/SVT-AV1/-/blob/master/Docs/CommonQuestions.md
// AOM-AV1: https://trac.ffmpeg.org/wiki/Encode/AV1
// x265: https://x265.readthedocs.io/en/master/
// x264: http://www.chaneru.com/Roku/HLS/X264_Settings.htm
// VVC: https://github.com/fraunhoferhhi/vvenc/wiki/Usage

class Encode {
	readonly List<string> args = [];
	readonly Dictionary<string, string> argValues = [];
	public string outFile = "";
	public Encode? dependsOn;

	/// <summary>Adds an argument without a value.</summary>
	public void Add(string key) => args.Add(key);

	/// <summary>Adds an argument with the given <paramref name="value"/>.</summary>
	public void Add(string key, string value) {
		args.Add(key);
		argValues[key] = value;
	}

	/// <summary>If they given argument exists, it's value becomes <paramref name="value"/>.</summary>
	/// <returns><see langword="true"/> if <paramref name="key"/> exists and the value is thus replaced, <see langword="false"/> otherwise.</returns>
	public bool Replace(string key, string value) {
		if (argValues.ContainsKey(key)) {
			argValues[key] = value;
			return true;
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

	/// <summary>Removes the given <paramref name="key"/> from the argument list along with it's associated value, if present.</summary>
	/// <returns><see langword="true"/> if <paramref name="key"/> exists and is thus removed, <see langword="false"/> otherwise.</returns>
	public bool Remove(string key) {
		argValues.Remove(key);
		return args.Remove(key);
	}

	public bool Contains(string key) => args.Contains(key);

	public Encode Clone() {
		Encode clone = new();
		foreach (string arg in args) {
			clone.args.Add(arg);
			if (argValues.TryGetValue(arg, out string? value)) {
				clone.argValues[arg] = value;
			}
		}
		return clone;
	}

	List<string> GetArguments() {
		List<string> fullArgs = [];
		foreach (string arg in args) {
			fullArgs.Add($"-{arg}");
			if (argValues.TryGetValue(arg, out string? value)) {
				fullArgs.Add(value);
			}
		}
		fullArgs.Add(outFile);
		return fullArgs;
	}

	public void Start() {
		dependsOn?.Start();

		ProcessStartInfo startInfo = new(Config.ffmpeg.FullName, GetArguments());
		Process ffmpegProcess = Process.Start(startInfo)!;
		ffmpegProcess.PriorityClass = Config.priority;
		if (Config.cpuAffinity != 0 && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())) {
			ffmpegProcess.ProcessorAffinity = Config.cpuAffinity;
		}
		//TODO: Track the conversion process and update the output (including the ETA)
		ffmpegProcess.WaitForExit();
	}

	public override string ToString() {
		StringBuilder command = new();
		if (dependsOn is not null) {
			command.Append(dependsOn.ToString());
			command.Append('\n');
		}
		command.Append(Config.ffmpeg.FullName);
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
