using System.Collections.Generic;

class Encode {
	readonly List<string> args = [];
	readonly Dictionary<string, string> argValues = [];
	public string outFile = "";

	public void Add(string key) => args.Add(key);

	public void Add(string key, string value) {
		args.Add(key);
		argValues[key] = value;
	}

	public bool Replace(string key, string value) {
		if (argValues.ContainsKey(key)) {
			argValues[key] = value;
			return true;
		}
		return false;
	}

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

	public override string ToString() => Config.ffmpeg.FullName + ' ' + string.Join(' ', GetArguments().ConvertAll(arg => arg.Contains(' ') ? $"'{arg}'" : arg));
}
