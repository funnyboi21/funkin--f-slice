using Godot;
using System.Collections.Generic;

public partial class modingAPI : Node
{
	public const string ModsPath = "user://mods/";
	public const string EngineApiVersion = "0.8.4";

	public class ModData
	{
		public string FolderName;
		public string Id;
		public string Title;
		public string Description;
		public string ApiVersion;
		public bool IsEnabled;
		public Texture2D Icon;
		public string JsonPath;
		public bool IsCompatible => ApiVersion == EngineApiVersion;
	}

	public List<ModData> LoadedMods = new List<ModData>();

	public override void _Ready()
	{
		RefreshMods();
	}

	public void RefreshMods()
	{
		LoadedMods.Clear();
		if (!DirAccess.DirExistsAbsolute(ModsPath))
			DirAccess.MakeDirAbsolute(ModsPath);

		using var dir = DirAccess.Open(ModsPath);
		if (dir == null) return;

		dir.ListDirBegin();
		string folderName = dir.GetNext();
		while (folderName != "")
		{
			if (dir.CurrentIsDir() && !folderName.StartsWith("."))
			{
				ModData mod = ParseModFolder(folderName);
				if (mod != null) LoadedMods.Add(mod);
			}
			folderName = dir.GetNext();
		}
	}

	private ModData ParseModFolder(string folderName)
	{
		string folderPath = ModsPath + folderName + "/";
		string jsonPath = folderPath + "mod.json";
		if (!FileAccess.FileExists(jsonPath)) return null;

		var json = new Json();
		if (json.Parse(FileAccess.GetFileAsString(jsonPath)) != Error.Ok) return null;
		var data = json.Data.AsGodotDictionary();

		var mod = new ModData
		{
			FolderName = folderName,
			JsonPath = jsonPath,
			Id = data.TryGetValue("id", out var id) ? id.AsString() : folderName,
			ApiVersion = data.TryGetValue("api_version", out var api) ? api.AsString() : "0.0.0",
			Title = data.TryGetValue("title", out var t) ? t.AsString() : folderName,
			Description = data.TryGetValue("description", out var d) ? d.AsString() : "",
			IsEnabled = data.TryGetValue("enabled", out var e) ? e.AsBool() : false
		};

		string iconPath = folderPath + "icon.png";
		if (FileAccess.FileExists(iconPath))
		{
			Image img = Image.LoadFromFile(iconPath);
			mod.Icon = ImageTexture.CreateFromImage(img);
		}

		return mod;
	}

	public void SaveModStates()
	{
		foreach (var mod in LoadedMods)
		{
			if (!FileAccess.FileExists(mod.JsonPath)) continue;
			var json = new Json();
			json.Parse(FileAccess.GetFileAsString(mod.JsonPath));
			var data = json.Data.AsGodotDictionary();
			data["enabled"] = mod.IsEnabled;
			using var file = FileAccess.Open(mod.JsonPath, FileAccess.ModeFlags.Write);
			file.StoreString(Json.Stringify(data, "\t"));
		}
	}
}
