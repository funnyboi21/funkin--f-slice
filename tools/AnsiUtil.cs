// AnsiUtil.cs
using System;
using System.Text.RegularExpressions;

namespace FSlice.Tools;

public static class AnsiUtil
{
	// ── Reset ────────────────────────────────────────────────────────────────
	public const string Reset       = "\x1b[0m";
	public const string Bold        = "\x1b[1m";
	public const string Dim         = "\x1b[2m";
	public const string Underline   = "\x1b[4m";
	public const string Strikethrough = "\x1b[9m";

	// ── Foreground ───────────────────────────────────────────────────────────
	public const string Black   = "\x1b[30m";
	public const string Red     = "\x1b[31m";
	public const string Green   = "\x1b[32m";
	public const string Yellow  = "\x1b[33m";
	public const string Blue    = "\x1b[34m";
	public const string Magenta = "\x1b[35m";
	public const string Cyan    = "\x1b[36m";
	public const string White   = "\x1b[37m";

	// ── Note colours (24-bit) ────────────────────────────────────────────────
	public const string NoteLeft  = "\x1b[38;2;255;34;170m";
	public const string NoteDown  = "\x1b[38;2;0;238;255m";
	public const string NoteUp    = "\x1b[38;2;0;204;0m";
	public const string NoteRight = "\x1b[38;2;204;17;17m";

	// ── Background ───────────────────────────────────────────────────────────
	public const string BgBlack   = "\x1b[40m";
	public const string BgRed     = "\x1b[41m";
	public const string BgGreen   = "\x1b[42m";
	public const string BgYellow  = "\x1b[43m";
	public const string BgBlue    = "\x1b[44m";
	public const string BgCyan    = "\x1b[46m";
	public const string BgWhite   = "\x1b[47m";
	public const string BgOrange  = "\x1b[48;5;208m";
	public const string BgLime    = "\x1b[48;5;154m";
	public const string BgPurple  = "\x1b[48;2;121;37;199m";

	public const string BgNoteLeft  = "\x1b[48;2;255;34;170m";
	public const string BgNoteDown  = "\x1b[48;2;0;238;255m";
	public const string BgNoteUp    = "\x1b[48;2;0;204;0m";
	public const string BgNoteRight = "\x1b[48;2;204;17;17m";

	// ── Lazy ANSI-support detection ──────────────────────────────────────────
	private static bool? _supported;
	private static readonly Regex AnsiPattern = new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);

	public static bool IsSupported()
	{
		if (_supported.HasValue) return _supported.Value;

		string? term = Environment.GetEnvironmentVariable("TERM");
		if (term == "dumb") { _supported = false; return false; }

		bool ok = false;
		ok |= term is not null && (term.Contains("256color", StringComparison.OrdinalIgnoreCase)
								|| Regex.IsMatch(term, @"(?i)^screen|^xterm|^vt100|^vt220|color|ansi|cygwin|linux"));

		foreach (var ci in new[] { "GITHUB_ACTIONS","TRAVIS","CIRCLECI","APPVEYOR","GITLAB_CI","BUILDKITE","DRONE" })
			ok |= Environment.GetEnvironmentVariable(ci) is not null;

		ok |= Environment.GetEnvironmentVariable("COLORTERM") is not null;
		ok |= Environment.GetEnvironmentVariable("WT_SESSION")  is not null;

		_supported = ok;
		return ok;
	}

	// ── Core apply ───────────────────────────────────────────────────────────
	public static string Apply(string str, string code)
	{
		str = str.Replace(Reset, "");
		string styled = code + str + Reset;
		return IsSupported() ? styled : AnsiPattern.Replace(styled, "");
	}

	// ── Convenience wrappers ─────────────────────────────────────────────────
	public static string error(string s)   => Apply(Apply(s, BgNoteRight), Bold);
	public static string warning(string s) => Apply(Apply(s, BgYellow), Bold);
	public static string info(string s)    => Apply(Apply(s, BgBlue), Bold);
	public static string debug(string s)   => Apply(Apply(s, BgLime), Bold);

	public static string bold(string s)  => Apply(s, Bold);
	public static string dim(string s)   => Apply(s, Dim);
	public static string red(string s)   => Apply(s, Red);
	public static string green(string s) => Apply(s, Green);
	public static string yellow(string s)=> Apply(s, Yellow);
	public static string cyan(string s)  => Apply(s, Cyan);

	public static string noteLeft(string s)  => Apply(s, NoteLeft);
	public static string noteDown(string s)  => Apply(s, NoteDown);
	public static string noteUp(string s)    => Apply(s, NoteUp);
	public static string noteRight(string s) => Apply(s, NoteRight);
}
