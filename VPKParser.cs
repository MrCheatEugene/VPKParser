using SteamDatabase.ValvePak;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using ValveResourceFormat;
using ValveResourceFormat.IO;
using ValveResourceFormat.ResourceTypes;
using ValveResourceFormat.ResourceTypes.RubikonPhysics;
using ValveResourceFormat.Serialization.KeyValues;
namespace VPKParser
{
    public record VPKFile(string path, string name, bool isMap, string? triangles_verticies = null);
    [System.Serializable]
    public record Vector3Ser(float x, float y, float z);

    class MapParser
    {
        static public List<VPKFile> ParseMaps(string basePath)
        {
            // basePath = SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\csgo
            string mapsPath = Path.Combine(basePath, "maps");
            List<VPKFile> toReturn = new List<VPKFile>();
            foreach (string path in Directory.GetFiles(mapsPath, "*.vpk"))
            {
                bool isMap = false;
                using var package = new Package();
                package.Read(path);
                string? tri_vert = null;
                List<Block> blocks = new List<Block>();
                List<Vector3Ser> triangles = new List<Vector3Ser>();
                List<Vector3Ser> verticies = new List<Vector3Ser>();
                bool parsed = false;
                try
                {
                    Random r = new Random();
                    if (package.Entries.ContainsKey("vmdl_c") && !parsed)
                    {
                        var vmdl_c = package.Entries["vmdl_c"].Where(x => x.FileName == "world_physics").First();
                        isMap = true;
                        byte[] vmdl_b = new byte[vmdl_c.TotalLength];
                        package.ReadEntry(vmdl_c, vmdl_b);
                        MemoryStream ms = new MemoryStream(vmdl_b);
                        var res = new Resource();
                        res.Read(ms);
                        ms.Close();
                        blocks.Add(((Model) res.DataBlock).GetEmbeddedPhys());
                        parsed = true;
                    }
                }catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }

                try
                {
                    if (package.Entries.ContainsKey("vphys_c") && !parsed)
                    {
                        isMap = true;
                        PackageEntry vphys_c = package.Entries["vphys_c"].First();
                        byte[] vphys_cb = new byte[vphys_c.TotalLength];
                        package.ReadEntry(vphys_c, vphys_cb);
                        MemoryStream ms = new MemoryStream(vphys_cb);
                        var res = new Resource();
                        res.Read(ms);
                        blocks.Add((PhysAggregateData)res.DataBlock);
                        ms.Close();
                        parsed = true;
                    }
                }catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }


                if (isMap)
                {
                    foreach(Block dataBlock_b in blocks)
                    {
                        try
                        {
                            PhysAggregateData dataBlock = (PhysAggregateData)dataBlock_b;
                            foreach (Part part in dataBlock.Parts)
                            {
                                foreach (var mesh in part.Shape.Meshes)
                                {
                                    string[] attrs = ((KVObject)dataBlock.CollisionAttributes[mesh.CollisionAttributeIndex]
                                        .Where(x => x.Key == "m_InteractAsStrings" || x.Key == "m_PhysicsTagStrings").First().Value).ToArray().Select(xx => (string)xx.Value).ToArray();
                                    string[] attrs2 = ((KVObject)dataBlock.CollisionAttributes[mesh.CollisionAttributeIndex]
                                        .Where(x => x.Key == "m_InteractExcludeStrings").First().Value).ToArray().Select(xx => (string)xx.Value).ToArray();
                                    int attrs3 = ((KVObject)dataBlock.CollisionAttributes[mesh.CollisionAttributeIndex]
                                        .Where(x => x.Key == "m_InteractExclude").First().Value).ToArray().Count();                                    

                                    if ( // skip anything specially hidden, or that we shouldn't render
                                        attrs2.Contains("player") ||
                                        attrs2.Contains("npc") || 
                                        attrs3 != 0 || 
                                        attrs.Contains("sky") ||
                                        attrs.Contains("csgo_grenadeclip") ||
                                        attrs.Contains("npcclip") || 
                                        attrs.Contains("playerclip") ||
                                        attrs.Contains("window")
                                    )
                                    {
                                        continue;
                                    }
                                    verticies.AddRange(mesh.Shape.GetVertices().ToArray().Select(t => new Vector3Ser(t.X, t.Y, t.Z)));
                                    triangles.AddRange(mesh.Shape.GetTriangles().ToArray().Select(t => new Vector3Ser(t.X, t.Y, t.Z)));
                                }
                            }
                        }
                        catch (Exception e )
                        {
                            //Console.WriteLine(dataBlock_b);
                            Console.WriteLine(e.Message);
                            Console.WriteLine(e.StackTrace);
                        }
                        
                    }

                    tri_vert = JsonSerializer.Serialize(new Dictionary<string, List<Vector3Ser>>
                    {
                        ["verticies"] = verticies,
                        ["triangles"] = triangles
                    });

                }
                toReturn.Add(new VPKFile(path, Path.GetFileNameWithoutExtension(path), isMap, tri_vert));
            }

            return toReturn;
        }
    }
}
