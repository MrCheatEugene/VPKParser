using VPKParser;

var files = MapParser.ParseMaps("L:\\SteamLibrary\\steamapps\\common\\Counter-Strike Global Offensive\\game\\csgo");
foreach(var file in files)
{
    File.WriteAllText(Path.ChangeExtension(file.path, ".json"), file.triangles_verticies);
}