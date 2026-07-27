using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Relicfall.Tools
{
    /// <summary>Repairs generated URP assets when the project is first imported.</summary>
    [InitializeOnLoad]
    internal static class ProjectSetup
    {
        private const string RendererPath = "Assets/Art/Materials/URPForwardRenderer.asset";
        private const string PipelinePath = "Assets/Art/Materials/URPAsset.asset";

        static ProjectSetup() => EditorApplication.delayCall += EnsureUniversalRenderPipeline;

        private static void EnsureUniversalRenderPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline != null)
            {
                GraphicsSettings.defaultRenderPipeline = pipeline;
                return;
            }

            AssetDatabase.DeleteAsset(RendererPath);
            AssetDatabase.DeleteAsset(PipelinePath);
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererPath);
            pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            AssetDatabase.SaveAssets();
            Debug.Log("RELICFALL: created valid Universal Render Pipeline assets.");
        }
    }
}
