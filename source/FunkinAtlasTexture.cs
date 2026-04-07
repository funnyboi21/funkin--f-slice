using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Funkin.Graphics;

/// <summary>
/// A Universal Atlas handler for Godot 4.
/// Supports:
/// 1. JSON (Aseprite/Adobe Animate) - via NickSteinGames/json-atlas
/// 2. LibGDX .atlas/.txt - via KAUTARUMA/godot-texture-atlas
/// </summary>
[Tool]
[GlobalClass]
public partial class FunkinAtlasTexture : AtlasTexture
{
	public enum FrameBehaviourEnum { Clamp, Wrap, PingPong }

	[ExportCategory("Source")]
	private Texture2D _sourceTexture;
	[Export] public Texture2D SourceTexture 
	{ 
		get => _sourceTexture; 
		set { _sourceTexture = value; Atlas = value; LoadData(); } 
	}

	private string _dataFilePath = "";
	[Export(PropertyHint.File, "*.json,*.atlas,*.txt")] 
	public string DataFilePath 
	{ 
		get => _dataFilePath; 
		set { _dataFilePath = value; LoadData(); } 
	}

	[ExportCategory("Animation")]
	private string _symbol = "";
	[Export] public string Symbol 
	{ 
		get => _symbol; 
		set { if (_symbol != value) { _symbol = value; _frame = 0; UpdateRegion(); } } 
	}

	private int _frame = 0;
	[Export] public int Frame 
	{ 
		get => _frame; 
		set { _frame = value; UpdateRegion(); } 
	}

	[Export] public FrameBehaviourEnum FrameBehaviour = FrameBehaviourEnum.Clamp;

	// Data Storage
	private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Rect2>> _symbolsData = new();
	private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Rect2>> _marginsData = new();

	private void LoadData()
	{
		_symbolsData.Clear();
		_marginsData.Clear();

		if (string.IsNullOrEmpty(DataFilePath) || !FileAccess.FileExists(DataFilePath)) return;

		if (DataFilePath.EndsWith(".json"))
			ParseJson();
		else
			ParseLibGdxAtlas();

		if (!_symbolsData.ContainsKey(_symbol) && _symbolsData.Count > 0)
			_symbol = _symbolsData.Keys.First();

		NotifyPropertyListChanged();
		UpdateRegion();
	}

	#region JSON Parser (Aseprite / Adobe)
	private void ParseJson()
	{
		using var file = FileAccess.Open(DataFilePath, FileAccess.ModeFlags.Read);
		var json = Json.ParseString(file.GetAsText());
		if (json.VariantType != Variant.Type.Dictionary) return;

		var root = json.AsGodotDictionary<string, Variant>();
		if (!root.TryGetValue("frames", out Variant framesVar)) return;

		// Grouping Logic for Animate/Aseprite
		var regex = new Regex(@"^(.*?)[_\s]*\d+$");

		void AddFrame(string filename, Dictionary<string, Variant> dict)
		{
			var f = dict["frame"].AsGodotDictionary<string, Variant>();
			Rect2 region = new Rect2(f["x"].AsSingle(), f["y"].AsSingle(), f["w"].AsSingle(), f["h"].AsSingle());
			Rect2 margin = new Rect2();

			if (dict.TryGetValue("spriteSourceSize", out Variant sssVar))
			{
				var sss = sssVar.AsGodotDictionary<string, Variant>();
				var ss = dict["sourceSize"].AsGodotDictionary<string, Variant>();
				margin = new Rect2(sss["x"].AsSingle(), sss["y"].AsSingle(), 
								   ss["w"].AsSingle() - sss["w"].AsSingle() - sss["x"].AsSingle(), 
								   ss["h"].AsSingle() - sss["h"].AsSingle() - sss["y"].AsSingle());
			}

			string name = filename.GetBaseName();
			var match = regex.Match(name);
			string symbolName = match.Success ? match.Groups[1].Value.Trim() : name;

			if (!_symbolsData.ContainsKey(symbolName)) {
				_symbolsData[symbolName] = new(); _marginsData[symbolName] = new();
			}
			_symbolsData[symbolName].Add(region);
			_marginsData[symbolName].Add(margin);
		}

		if (framesVar.VariantType == Variant.Type.Array)
			foreach (var item in framesVar.AsGodotArray()) AddFrame(item.AsGodotDictionary<string, Variant>()["filename"].AsString(), item.AsGodotDictionary<string, Variant>());
		else
			foreach (var kvp in framesVar.AsGodotDictionary<string, Variant>()) AddFrame(kvp.Key, kvp.Value.AsGodotDictionary<string, Variant>());
	}
	#endregion

	#region LibGDX Parser (godot-texture-atlas style)
	private void ParseLibGdxAtlas()
	{
		using var file = FileAccess.Open(DataFilePath, FileAccess.ModeFlags.Read);
		string currentSymbol = "";
		
		while (!file.EofReached())
		{
			string line = file.GetLine().Trim();
			if (string.IsNullOrEmpty(line) || line.Contains(":")) continue;

			// This line is likely a sprite name
			currentSymbol = line;
			if (!_symbolsData.ContainsKey(currentSymbol)) {
				_symbolsData[currentSymbol] = new(); _marginsData[currentSymbol] = new();
			}

			// Parse attributes
			var attrs = new System.Collections.Generic.Dictionary<string, string>();
			while (!file.EofReached())
			{
				string attrLine = file.GetLine();
				if (string.IsNullOrWhiteSpace(attrLine)) break;
				if (!attrLine.Contains(":")) { 
					// This is the next sprite name, go back one line
					file.Seek(file.GetPosition() - (ulong)(attrLine.Length + 1));
					break; 
				}
				var parts = attrLine.Split(':');
				attrs[parts[0].Trim()] = parts[1].Trim();
			}

			if (attrs.ContainsKey("xy") && attrs.ContainsKey("size"))
			{
				var xy = attrs["xy"].Split(',').Select(float.Parse).ToArray();
				var size = attrs["size"].Split(',').Select(float.Parse).ToArray();
				var orig = attrs.ContainsKey("orig") ? attrs["orig"].Split(',').Select(float.Parse).ToArray() : size;
				var offset = attrs.ContainsKey("offset") ? attrs["offset"].Split(',').Select(float.Parse).ToArray() : new float[] { 0, 0 };

				_symbolsData[currentSymbol].Add(new Rect2(xy[0], xy[1], size[0], size[1]));
				_marginsData[currentSymbol].Add(new Rect2(offset[0], offset[1], orig[0] - size[0] - offset[0], orig[1] - size[1] - offset[1]));
			}
		}
	}
	#endregion

	private void UpdateRegion()
	{
		if (!_symbolsData.ContainsKey(_symbol)) return;

		var frames = _symbolsData[_symbol];
		int frameCount = frames.Count;
		if (frameCount == 0) return;

		int idx = Frame;
		if (FrameBehaviour == FrameBehaviourEnum.Clamp) idx = Mathf.Clamp(idx, 0, frameCount - 1);
		else if (FrameBehaviour == FrameBehaviourEnum.Wrap) idx = Mathf.PosMod(idx, frameCount);
		else if (FrameBehaviour == FrameBehaviourEnum.PingPong) {
			idx = Mathf.PosMod(idx, frameCount * 2 - 2);
			if (idx >= frameCount) idx = (frameCount * 2 - 2) - idx;
		}

		Region = frames[idx];
		Margin = _marginsData[_symbol][idx];
	}

	public override void _ValidateProperty(Dictionary property)
	{
		if (property["name"].AsString() == "Symbol")
		{
			property["hint"] = (int)PropertyHint.Enum;
			property["hint_string"] = string.Join(",", _symbolsData.Keys);
		}
	}
}
