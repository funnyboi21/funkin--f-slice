using Godot;
using Godot.Collections;
using System.Linq;
using System.Text.RegularExpressions;

namespace JsonAtlas;

/// <summary>
/// A Godot 4 C# port of NickSteinGames/json-atlas.
/// Custom AtlasTexture class that uses .json files created when exporting sprite sheets 
/// to compile Symbols/Tags and their respective frames into a usable Godot texture.
/// Supports both Hash and Array-based JSON formats (Aseprite, Adobe Animate/Flash).
/// </summary>
[Tool]
[GlobalClass]
public partial class AtlasTextureJson : AtlasTexture
{
	public enum FrameBehaviourEnum
	{
		Clamp,
		Wrap,
		PingPong
	}

	private Texture2D _sourceTexture;[ExportCategory("Atlas Texture JSON")]
	[Export]
	public Texture2D SourceTexture
	{
		get => _sourceTexture;
		set
		{
			_sourceTexture = value;
			Atlas = value; // Assigns the texture to the base AtlasTexture property
			TryAutoLoadJson();
		}
	}

	private string _jsonPath = "";
	[Export(PropertyHint.File, "*.json")]
	public string JsonPath
	{
		get => _jsonPath;
		set
		{
			if (_jsonPath == value) return;
			_jsonPath = value;
			LoadJsonData();
		}
	}

	private string _symbol = "";
	[Export]
	public string Symbol
	{
		get => _symbol;
		set
		{
			if (_symbol != value)
			{
				_symbol = value;
				_frame = 0; // Reset frame when switching symbols
				UpdateRegion();
			}
		}
	}

	private int _frame = 0;
	[Export]
	public int Frame
	{
		get => _frame;
		set
		{
			_frame = value;
			UpdateRegion();
		}
	}

	private FrameBehaviourEnum _frameBehaviour = FrameBehaviourEnum.Clamp;
	[Export]
	public FrameBehaviourEnum FrameBehaviour
	{
		get => _frameBehaviour;
		set
		{
			_frameBehaviour = value;
			UpdateRegion();
		}
	}

	private Vector2 _customScale = Vector2.One;[Export(PropertyHint.None, "Multiplies the JSON coordinate values if your texture was upscaled.")]
	public Vector2 CustomScale
	{
		get => _customScale;
		set
		{
			_customScale = value;
			UpdateRegion();
		}
	}

	// Internal caching of parsed data
	private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Rect2>> _symbolsData = new();
	private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Rect2>> _marginsData = new();

	private void TryAutoLoadJson()
	{
		if (_sourceTexture == null) return;

		string texPath = _sourceTexture.ResourcePath;
		if (string.IsNullOrEmpty(texPath)) return;

		// Try to find a .json file with the exact same name as the texture
		string possibleJson = texPath.GetBaseDir() + "/" + texPath.GetFile().GetBaseName() + ".json";
		if (FileAccess.FileExists(possibleJson))
		{
			JsonPath = possibleJson; 
		}
	}

	private void LoadJsonData()
	{
		_symbolsData.Clear();
		_marginsData.Clear();

		if (string.IsNullOrEmpty(JsonPath) || !FileAccess.FileExists(JsonPath)) return;

		using var file = FileAccess.Open(JsonPath, FileAccess.ModeFlags.Read);
		if (file == null) return;

		string content = file.GetAsText();
		var jsonVar = Json.ParseString(content);

		if (jsonVar.VariantType != Variant.Type.Dictionary) return;

		var root = jsonVar.AsGodotDictionary<string, Variant>();
		if (!root.TryGetValue("frames", out Variant framesVar)) return;

		var parsedFrames = new System.Collections.Generic.List<ParsedFrame>();

		// 1. Extract Frames (Array or Hash based)
		if (framesVar.VariantType == Variant.Type.Array)
		{
			foreach (var item in framesVar.AsGodotArray())
			{
				if (item.VariantType == Variant.Type.Dictionary)
				{
					var fDict = item.AsGodotDictionary<string, Variant>();
					if (fDict.TryGetValue("filename", out Variant fn))
					{
						parsedFrames.Add(ParseSingleFrame(fn.AsString(), fDict));
					}
				}
			}
		}
		else if (framesVar.VariantType == Variant.Type.Dictionary)
		{
			var framesDict = framesVar.AsGodotDictionary<string, Variant>();
			foreach (var kvp in framesDict)
			{
				if (kvp.Value.VariantType == Variant.Type.Dictionary)
				{
					parsedFrames.Add(ParseSingleFrame(kvp.Key, kvp.Value.AsGodotDictionary<string, Variant>()));
				}
			}
		}

		// 2. Process Aseprite Meta Tags
		bool usedMetaTags = false;
		if (root.TryGetValue("meta", out Variant metaVar) && metaVar.VariantType == Variant.Type.Dictionary)
		{
			var meta = metaVar.AsGodotDictionary<string, Variant>();
			if (meta.TryGetValue("frameTags", out Variant tagsVar) && tagsVar.VariantType == Variant.Type.Array)
			{
				var tags = tagsVar.AsGodotArray();
				if (tags.Count > 0)
				{
					usedMetaTags = true;
					foreach (var tagVar in tags)
					{
						if (tagVar.VariantType == Variant.Type.Dictionary)
						{
							var tag = tagVar.AsGodotDictionary<string, Variant>();
							string tagName = tag.TryGetValue("name", out Variant nameVar) ? nameVar.AsString() : "unknown";
							int from = tag.TryGetValue("from", out Variant fromVar) ? fromVar.AsInt32() : 0;
							int to = tag.TryGetValue("to", out Variant toVar) ? toVar.AsInt32() : 0;

							EnsureSymbolExists(tagName);

							for (int i = from; i <= to; i++)
							{
								if (i >= 0 && i < parsedFrames.Count)
								{
									_symbolsData[tagName].Add(parsedFrames[i].Region);
									_marginsData[tagName].Add(parsedFrames[i].Margin);
								}
							}
						}
					}
				}
			}
		}

		// 3. Process Adobe Animate / Flash (Filename regex grouping)
		if (!usedMetaTags)
		{
			// Matches text up to trailing numbers (e.g. "idle_001" -> "idle")
			var regex = new Regex(@"^(.*?)[_\s]*\d+$");
			foreach (var pf in parsedFrames)
			{
				string name = pf.Filename;
				
				int dotIdx = name.LastIndexOf('.');
				if (dotIdx > 0) name = name.Substring(0, dotIdx); // Strip extension

				string symbolStr = name;
				var match = regex.Match(name);
				if (match.Success)
				{
					symbolStr = match.Groups[1].Value.Trim();
				}

				EnsureSymbolExists(symbolStr);
				_symbolsData[symbolStr].Add(pf.Region);
				_marginsData[symbolStr].Add(pf.Margin);
			}
		}

		if (!_symbolsData.ContainsKey(_symbol) && _symbolsData.Count > 0)
		{
			_symbol = _symbolsData.Keys.First();
			_frame = 0;
		}

		NotifyPropertyListChanged();
		UpdateRegion();
	}

	private void EnsureSymbolExists(string symbol)
	{
		if (!_symbolsData.ContainsKey(symbol))
		{
			_symbolsData[symbol] = new System.Collections.Generic.List<Rect2>();
			_marginsData[symbol] = new System.Collections.Generic.List<Rect2>();
		}
	}

	private struct ParsedFrame
	{
		public string Filename;
		public Rect2 Region;
		public Rect2 Margin;
	}

	private ParsedFrame ParseSingleFrame(string filename, Dictionary<string, Variant> dict)
	{
		Rect2 region = new Rect2();
		Rect2 margin = new Rect2();

		if (dict.TryGetValue("frame", out Variant frameVar) && frameVar.VariantType == Variant.Type.Dictionary)
		{
			var f = frameVar.AsGodotDictionary<string, Variant>();
			float x = f.TryGetValue("x", out Variant vx) ? vx.AsSingle() : 0;
			float y = f.TryGetValue("y", out Variant vy) ? vy.AsSingle() : 0;
			float w = f.TryGetValue("w", out Variant vw) ? vw.AsSingle() : 0;
			float h = f.TryGetValue("h", out Variant vh) ? vh.AsSingle() : 0;
			region = new Rect2(x, y, w, h);
		}

		// Handle trimmed sprites
		if (dict.TryGetValue("spriteSourceSize", out Variant sssVar) && sssVar.VariantType == Variant.Type.Dictionary)
		{
			var sss = sssVar.AsGodotDictionary<string, Variant>();
			float mx = sss.TryGetValue("x", out Variant vmx) ? vmx.AsSingle() : 0;
			float my = sss.TryGetValue("y", out Variant vmy) ? vmy.AsSingle() : 0;
			
			margin.Position = new Vector2(mx, my);
			
			if (dict.TryGetValue("sourceSize", out Variant ssVar) && ssVar.VariantType == Variant.Type.Dictionary)
			{
				var ss = ssVar.AsGodotDictionary<string, Variant>();
				float ow = ss.TryGetValue("w", out Variant vow) ? vow.AsSingle() : 0;
				float oh = ss.TryGetValue("h", out Variant voh) ? voh.AsSingle() : 0;
				float sw = sss.TryGetValue("w", out Variant vsw) ? vsw.AsSingle() : 0;
				float sh = sss.TryGetValue("h", out Variant vsh) ? vsh.AsSingle() : 0;
				
				margin.Size = new Vector2(ow - sw - mx, oh - sh - my);
			}
		}

		return new ParsedFrame { Filename = filename, Region = region, Margin = margin };
	}

	private void UpdateRegion()
	{
		if (_symbolsData == null || _symbolsData.Count == 0) return;
		
		string targetSymbol = Symbol;
		if (!_symbolsData.ContainsKey(targetSymbol))
		{
			if (_symbolsData.Count > 0)
				targetSymbol = _symbolsData.Keys.First();
			else
				return;
		}

		var frames = _symbolsData[targetSymbol];
		var margins = _marginsData[targetSymbol];
		int frameCount = frames.Count;

		if (frameCount == 0) return;

		int actualFrame = Frame; 

		// Apply Frame Behaviour
		switch (FrameBehaviour)
		{
			case FrameBehaviourEnum.Clamp:
				actualFrame = Mathf.Clamp(actualFrame, 0, frameCount - 1);
				break;
			case FrameBehaviourEnum.Wrap:
				actualFrame = actualFrame % frameCount;
				if (actualFrame < 0) actualFrame += frameCount;
				break;
			case FrameBehaviourEnum.PingPong:
				int cycle = frameCount * 2 - 2;
				if (cycle <= 0) actualFrame = 0;
				else
				{
					actualFrame %= cycle;
					if (actualFrame < 0) actualFrame += cycle;
					if (actualFrame >= frameCount)
						actualFrame = cycle - actualFrame;
				}
				break;
		}

		Rect2 targetRegion = frames[actualFrame];
		Rect2 targetMargin = margins[actualFrame];

		// Apply coordinate scaling (if JSON coords differ from actual image size)
		if (CustomScale != Vector2.One && CustomScale != Vector2.Zero)
		{
			targetRegion.Position *= CustomScale;
			targetRegion.Size *= CustomScale;
			targetMargin.Position *= CustomScale;
			targetMargin.Size *= CustomScale;
		}

		Region = targetRegion;
		Margin = targetMargin;
	}

	// Updates the Godot Inspector dropdown list for 'Symbol' dynamically
	public override void _ValidateProperty(Dictionary property)
	{
		base._ValidateProperty(property);

		if (property.TryGetValue("name", out Variant nameVar))
		{
			string propName = nameVar.AsString();
			if (propName == "Symbol" || propName == "_symbol")
			{
				if (_symbolsData != null && _symbolsData.Count > 0)
				{
					property["hint"] = (int)PropertyHint.Enum;
					property["hint_string"] = string.Join(",", _symbolsData.Keys);
				}
			}
		}
	}
}
