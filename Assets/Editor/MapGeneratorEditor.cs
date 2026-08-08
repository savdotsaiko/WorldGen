using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapGenerator generator = (MapGenerator)target;
        if (DrawDefaultInspector())
        {
            if(generator.autoUpdate)
            {
                generator.DrawMapInEditor();
            }
        }
        if (GUILayout.Button("Generate Map"))
        {
            generator.DrawMapInEditor();
        }
    }
}
