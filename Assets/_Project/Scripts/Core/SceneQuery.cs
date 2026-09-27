using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Margin.Core
{
    /// <summary>Finds components in all loaded scenes (works the same in every Unity version, unlike FindObjectOfType).</summary>
    public static class SceneQuery
    {
        public static T FindFirst<T>() where T : Component
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    T found = root.GetComponentInChildren<T>(true);
                    if (found != null) return found;
                }
            }
            return null;
        }

        public static List<T> FindAll<T>() where T : Component
        {
            var result = new List<T>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    result.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return result;
        }
    }
}
