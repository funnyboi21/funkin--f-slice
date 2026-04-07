using Godot;
using System;
using System.Collections.Generic;
using System.Xml;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Represents a single frame parsed from a texture atlas.
/// </summary>
public class AtlasFrame
{
	public string Name       { get; set; }
	public Rect2  Frame      { get; set; }  // Region in the spritesheet
	public Vector2 Offset    { get; set; }  // Trim offset (frameX/frameY)
	public Vector2 SourceSize { get; set; } // Original sprite size before trimming
	public bool   Rotated    { get; set; }  // TexturePacker rotation flag
}

/// <summary>
/// Loads Sparrow v2 (XML) and TexturePacker (JSON) atlas files in Godot 4 C#.
/// Usage:
///   var frames = AtlasLoader.Load("res://assets/mySprite.xml");   // or .json
///   var tex    = AtlasLoader.GetAtlasTexture(frames["idle0000"], baseTexture);
/// </summary>
public static class AtlasLoader
{
	// -------------------------------------------------------------------------
	// Public API
	// -------------------------------------------------------------------------

	/// <summary>
	/// Auto-detects format by extension and returns all frames keyed by name.
	/// </summary>
	public static Dictionary<string, AtlasFrame> Load(string path)
	{
		string ext = path.GetExtension().ToLower();
		return ext switch
		{
			"xml"  => LoadSparrow(path),
			"json" => LoadTexturePacker(path),
			_      => throw new NotSupportedException($"Unknown atlas format: {ext}")
		};
	}

	/// <summary>
	/// Builds a Godot AtlasTexture from an AtlasFrame + the base spritesheet texture.
	/// </summary>
	public static AtlasTexture GetAtlasTexture(AtlasFrame frame, Texture2D sheet)
	{
		var atlas = new AtlasTexture
		{
			Atlas  = sheet,
			Region = frame.Frame,
			// Margin encodes the trim offset so sprites stay correctly positioned
			Margin = new Rect2(frame.Offset, frame.SourceSize - frame.Frame.Size)
		};
		return atlas;
	}

	/// <summary>
	/// Helper: groups frames by animation prefix (everything before the trailing digits).
	/// e.g. "idle0000", "idle0001" → key "idle"
	/// </summary>
	public static Dictionary<string, List<AtlasFrame>> GroupByAnimation(
		Dictionary<string, AtlasFrame> frames)
	{
		var groups = new Dictionary<string, List<AtlasFrame>>();

		foreach (var (name, frame) in frames)
		{
			string prefix = System.Text.RegularExpressions.Regex
				.Replace(name, @"\d+$", "");

			if (!groups.ContainsKey(prefix))
				groups[prefix] = new List<AtlasFrame>();

			groups[prefix].Add(frame);
		}

		// Sort each group by frame name so animations play in order
		foreach (var key in groups.Keys)
			groups[key].Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

		return groups;
	}

	// -------------------------------------------------------------------------
	// Sparrow v2 (XML) parser
	// -------------------------------------------------------------------------

	private static Dictionary<string, AtlasFrame> LoadSparrow(string path)
	{
		var frames = new Dictionary<string, AtlasFrame>();

		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
			throw new Exception($"Could not open atlas file: {path}");

		var doc = new XmlDocument();
		doc.LoadXml(file.GetAsText());

		XmlNodeList nodes = doc.GetElementsByTagName("SubTexture");

		foreach (XmlNode node in nodes)
		{
			string name = node.Attributes["name"].Value;

			float x = ParseFloat(node, "x");
			float y = ParseFloat(node, "y");
			float w = ParseFloat(node, "width");
			float h = ParseFloat(node, "height");

			// frameX/frameY are negative trim offsets in Sparrow format
			float fx = node.Attributes["frameX"] != null ? -ParseFloat(node, "frameX") : 0f;
			float fy = node.Attributes["frameY"] != null ? -ParseFloat(node, "frameY") : 0f;
			float fw = node.Attributes["frameWidth"]  != null ? ParseFloat(node, "frameWidth")  : w;
			float fh = node.Attributes["frameHeight"] != null ? ParseFloat(node, "frameHeight") : h;

			frames[name] = new AtlasFrame
			{
				Name       = name,
				Frame      = new Rect2(x, y, w, h),
				Offset     = new Vector2(fx, fy),
				SourceSize = new Vector2(fw, fh),
				Rotated    = false
			};
		}

		return frames;
	}

	private static float ParseFloat(XmlNode node, string attr) =>
		float.Parse(node.Attributes[attr].Value,
			System.Globalization.CultureInfo.InvariantCulture);

	// -------------------------------------------------------------------------
	// TexturePacker (JSON) parser — supports both hash and array formats
	// -------------------------------------------------------------------------

	private static Dictionary<string, AtlasFrame> LoadTexturePacker(string path)
	{
		var frames = new Dictionary<string, AtlasFrame>();

		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
			throw new Exception($"Could not open atlas file: {path}");

		var root = JsonNode.Parse(file.GetAsText())!.AsObject();
		var framesNode = root["frames"];

		if (framesNode is JsonObject hashFormat)
		{
			// Hash format: { "frames": { "name": { ... } } }
			foreach (var (name, data) in hashFormat)
				frames[name] = ParseTPFrame(name, data!.AsObject());
		}
		else if (framesNode is JsonArray arrayFormat)
		{
			// Array format: { "frames": [ { "filename": "name", ... } ] }
			foreach (var item in arrayFormat)
			{
				var obj  = item!.AsObject();
				string name = obj["filename"]!.GetValue<string>();
				frames[name] = ParseTPFrame(name, obj);
			}
		}
		else
		{
			throw new Exception("Unrecognised TexturePacker JSON structure.");
		}

		return frames;
	}

	private static AtlasFrame ParseTPFrame(string name, JsonObject data)
	{
		var f        = data["frame"]!.AsObject();
		float x      = f["x"]!.GetValue<float>();
		float y      = f["y"]!.GetValue<float>();
		float w      = f["w"]!.GetValue<float>();
		float h      = f["h"]!.GetValue<float>();
		bool rotated = data["rotated"]?.GetValue<bool>() ?? false;

		float ox = 0f, oy = 0f, sw = w, sh = h;
		if (data["spriteSourceSize"] is JsonObject sss)
		{
			ox = sss["x"]!.GetValue<float>();
			oy = sss["y"]!.GetValue<float>();
		}
		if (data["sourceSize"] is JsonObject ss)
		{
			sw = ss["w"]!.GetValue<float>();
			sh = ss["h"]!.GetValue<float>();
		}

		return new AtlasFrame
		{
			Name       = name,
			Frame      = new Rect2(x, y, rotated ? h : w, rotated ? w : h),
			Offset     = new Vector2(ox, oy),
			SourceSize = new Vector2(sw, sh),
			Rotated    = rotated
		};
	}
}
