using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LilFoots.EditorTools
{
    /// <summary>
    /// One-click course builder for BudE (Oct 20: "thought we were trying to bypass that way
    /// so its easier"). The whole scene pipeline is env-var driven (MAP_DATA / MAP_SCENE),
    /// so each course = set the vars + call BuildAndShootCore(). Build, press Play, judge.
    /// </summary>
    public static class LilFootsCourseMenu
    {
        static void BuildCourse(string mapData, string sceneName, string label)
        {
            if (!System.IO.File.Exists("Assets/LevelData/" + mapData)) {
                Debug.LogError("[CourseMenu] map data missing: Assets/LevelData/" + mapData + " - pull the repo / extract the kit zip first");
                return;
            }
            // remember the spine so the Token Tweaker writes drags back into the right file
            EditorPrefs.SetString("LilFoots_TweakSpine", "Assets/LevelData/" + mapData);
            System.Environment.SetEnvironmentVariable("MAP_DATA", mapData);
            System.Environment.SetEnvironmentVariable("MAP_SCENE", sceneName);
            LilFootsArtPass.BuildAndShootCore();
            // reload the saved scene so play starts from the clean saved state
            // (BuildAndShootCore moved the camera + dismissed the menu for its QC shots)
            EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity",
                                         OpenSceneMode.Single);
            Debug.Log("[CourseMenu] " + label + " built -> Assets/Scenes/" + sceneName + ".unity — press Play.");
        }

        [MenuItem("Tools/Lil Foots/Build Course 1-1 Mossveil Meadow")]
        public static void BuildMossveil() {
            BuildCourse("map_region1_spine.json", "Map001", "1-1 Mossveil Meadow");
        }

        [MenuItem("Tools/Lil Foots/Build Course 1-2 Fern Hollow")]
        public static void BuildFernHollow() {
            BuildCourse("map_region1_2_spine.json", "Map002_FernHollow", "1-2 Fern Hollow");
        }
    }
}
