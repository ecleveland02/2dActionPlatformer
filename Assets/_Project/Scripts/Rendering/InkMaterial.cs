using UnityEngine;
using UnityEngine.Rendering;

namespace Margin.Rendering
{
    /// <summary>
    /// A shared vertex-colored, alpha-blended material for ink lines, meshes and particles created at runtime.
    /// URP's unlit sprite shader renders pink under the built-in renderer, so it's chosen by the active pipeline.
    /// </summary>
    public static class InkMaterial
    {
        private static Material runtime;

        public static Material Runtime
        {
            get
            {
                if (runtime != null) return runtime;
                Shader shader = GraphicsSettings.currentRenderPipeline != null
                    ? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                    : null;
                if (shader == null) shader = Shader.Find("Sprites/Default");
                runtime = new Material(shader) { name = "Ink (runtime)", hideFlags = HideFlags.DontSave };
                return runtime;
            }
        }
    }
}
