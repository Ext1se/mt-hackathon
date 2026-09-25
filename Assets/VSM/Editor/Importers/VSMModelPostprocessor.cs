namespace VSM.Editor.Importers
{
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>Настраивает импорт моделей поезда, статические флаги и простые коллайдеры.</summary>

    public sealed class VSMModelPostprocessor : AssetPostprocessor
    {
        /// <summary>Проверяет, относится ли импортируемый FBX к моделям VSM.</summary>
        private bool IsVsmModel => Path.GetFileName(assetPath).StartsWith("VSM_")
            && Path.GetExtension(assetPath).ToLowerInvariant() == ".fbx";

        /// <summary>Настраивает масштаб модели и создание UV для запечённого освещения.</summary>
        private void OnPreprocessModel()
        {
            if (!IsVsmModel) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.addCollider = false;
            importer.generateSecondaryUV = true;
        }

        /// <summary>Назначает статические флаги и заменяет служебные меши простыми коллайдерами.</summary>
        private void OnPostprocessModel(GameObject root)
        {
            if (!IsVsmModel) return;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                if (!filter.name.Contains("_COL_"))
                {
                    if (filter.name.Contains("_STATIC_ZONE_"))
                    {
                        var flags = StaticEditorFlags.OccludeeStatic;
                        if (filter.name.EndsWith("_OPAQUE"))
                            flags |= StaticEditorFlags.OccluderStatic
                                | StaticEditorFlags.ContributeGI
                                | StaticEditorFlags.BatchingStatic;
                        GameObjectUtility.SetStaticEditorFlags(filter.gameObject, flags);
                    }
                    else if (filter.name.Contains("_SEAT_"))
                    {
                        GameObjectUtility.SetStaticEditorFlags(filter.gameObject,
                            StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI
                            | StaticEditorFlags.BatchingStatic);
                    }
                    continue;
                }
                var box = filter.gameObject.AddComponent<BoxCollider>();
                box.center = filter.sharedMesh.bounds.center;
                box.size = filter.sharedMesh.bounds.size;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null) Object.DestroyImmediate(renderer);
                Object.DestroyImmediate(filter);
            }
        }
    }
}
