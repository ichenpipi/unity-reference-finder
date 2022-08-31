using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Eazax.Editor
{

    /// <summary>
    /// 引用查找器
    /// </summary>
    /// <author>陈皮皮</author>
    /// <version>20220831</version>
    public static class ReferenceFinder
    {

        // /// <summary>
        // /// 查找当前选中资源的引用
        // /// </summary>
        // [MenuItem("Assets/Eazax/Find References", false, 3)]
        // private static void MenuFind()
        // {
        //     string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        //     if (!string.IsNullOrEmpty(path))
        //     {
        //         Find(path);
        //     }
        // }

        /// <summary>
        /// 当前项目 Assets 目录的路径
        /// </summary>
        private static readonly string _projectAssetsPath = Application.dataPath;

        /// <summary>
        /// 当前项目的路径
        /// </summary>
        private static readonly string _projectPath = _projectAssetsPath.Substring(0, _projectAssetsPath.LastIndexOf("Assets", StringComparison.Ordinal));

        /// <summary>
        /// 要包含的文件格式
        /// </summary>
        private static readonly string[] _extensions =
        {
            ".unity", ".prefab", ".mat", ".asset",
            ".shadervariants", ".fontsettings", ".cubemap", ".flare",
            ".scenetemplate", ".mask", ".overrideController", ".terrainlayer", ".guiskin"
        };

        /// <summary>
        /// 资源过滤器
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        private static bool AssetFilter(string path)
        {
            return _extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取项目 Assets 目录的所有资源的路径
        /// </summary>
        /// <returns></returns>
        private static string[] GetProjectAssets()
        {
            return Directory.GetFiles(_projectAssetsPath, "*.*", SearchOption.AllDirectories)
                .Where(AssetFilter)
                .ToArray();
        }

        /// <summary>
        /// 查找目标资源的引用
        /// </summary>
        /// <param name="guid">资源的 GUID</param>
        public static void FindByGUID(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                Find(path);
            }
        }

        /// <summary>
        /// 查找目标资源的引用
        /// </summary>
        /// <param name="assetPath">资源的相对路径</param>
        /// <param name="callback">完成回调</param>
        public static void Find(string assetPath, Action<List<ReferenceInfo>> callback = null)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                callback?.Invoke(null);
                return;
            }
            // 获取项目中所有资源的路径
            string[] files = GetProjectAssets();
            // 分批次检查
            List<ReferenceInfo> list = new List<ReferenceInfo>();
            int maxIndex = files.Length - 1;
            int curIndex = 0;
            const int countPerBatch = 10;
            EditorApplication.update = () =>
            {
                // 是否快结束了
                int count = countPerBatch;
                if (curIndex + count > maxIndex)
                {
                    count = maxIndex - curIndex + 1;
                }
                // 搞一轮
                for (int i = 0; i < count; i++)
                {
                    string path = GetAssetRelativePath(files[curIndex]);
                    // 排除自己
                    if (path.Equals(assetPath))
                    {
                        ++curIndex;
                        continue;
                    }
                    // 展示进度
                    string title = $"Finding References... ({curIndex + 1}/{maxIndex + 1})";
                    float progress = (float) (curIndex + 1) / (maxIndex + 1);
                    bool hasCanceled = EditorUtility.DisplayCancelableProgressBar(title, path, progress);
                    // 是否取消了
                    if (hasCanceled)
                    {
                        EditorApplication.update = null;
                        EditorUtility.ClearProgressBar();
                        callback?.Invoke(null);
                        return;
                    }
                    // 检查引用
                    ReferenceInfo info = CheckReference(path, assetPath);
                    if (info != null)
                    {
                        list.Add(info);
                    }
                    // 下一个
                    ++curIndex;
                }
                // 结束了吗
                if (curIndex >= maxIndex)
                {
                    EditorApplication.update = null;
                    EditorUtility.ClearProgressBar();
                    callback?.Invoke(list);
                    if (callback == null)
                    {
                        PrintResults(list);
                    }
                }
            };
        }

        /// <summary>
        /// 查找目标资源的引用（基于文本正则匹配）
        /// </summary>
        /// <param name="asset">资源</param>
        /// <param name="callback">完成回调</param>
        public static void FindRegex(Object asset, Action<List<ReferenceInfo>> callback = null)
        {
            // 生成正则表达式，例如：
            // - 内置资源：{fileID: 10303, guid: 0000000000000000f000000000000000, type: 0}
            // - 项目资源：{fileID: 100100000, guid: 57d31b7d2a71b42858f8d031d9c6219b, type: 3}
            string assetGUID;
            long assetFileID;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out assetGUID, out assetFileID);
            Regex regex = new Regex($"fileID: {assetFileID}, guid: {assetGUID}, type");
            // 获取项目中所有资源的路径
            string assetPath = AssetDatabase.GetAssetPath(asset);
            string[] files = GetProjectAssets();
            // 分批次检查
            List<ReferenceInfo> list = new List<ReferenceInfo>();
            int maxIndex = files.Length - 1;
            int curIndex = 0;
            int countPerBatch = 10;
            EditorApplication.update = () =>
            {
                // 是否快结束了
                int count = countPerBatch;
                if (curIndex + count >= maxIndex)
                {
                    count = maxIndex - curIndex;
                }
                // 搞一轮
                for (int i = 0; i < count; i++)
                {
                    // 获取路径并增加计数
                    string relativePath = GetAssetRelativePath(files[curIndex++]);
                    // 排除自己
                    if (relativePath.Equals(assetPath))
                    {
                        continue;
                    }
                    // 展示进度                    
                    bool hasCanceled = EditorUtility.DisplayCancelableProgressBar("Finding References...", relativePath, curIndex / (float)maxIndex);
                    // 是否取消了
                    if (hasCanceled)
                    {
                        EditorApplication.update = null;
                        EditorUtility.ClearProgressBar();
                        callback?.Invoke(null);
                        return;
                    }
                    // 检查引用
                    ReferenceInfo info = CheckReferenceRegex(relativePath, regex);
                    if (info != null)
                    {
                        list.Add(info);
                    }
                }
                // 结束了吗
                if (curIndex >= maxIndex)
                {
                    EditorApplication.update = null;
                    EditorUtility.ClearProgressBar();
                    callback?.Invoke(list);
                    if (callback == null)
                    {
                        PrintResults(list);
                    }
                }
            };

        }

        /// <summary>
        /// 检查资源引用（基于 AssetDatabase.GetDependencies 接口）
        /// </summary>
        /// <param name="assetPath">被检查资源的路径（相对路径）</param>
        /// <param name="targetPath">目标资源的路径（相对路径）</param>
        /// <returns></returns>
        private static ReferenceInfo CheckReference(string assetPath, string targetPath)
        {
            // 获取被检查资源的所有依赖
            // 检查依赖列表中是否包含目标资源的路径
            string[] dependencies = AssetDatabase.GetDependencies(assetPath);
            if (dependencies.Contains(targetPath))
            {
                return new ReferenceInfo(assetPath, AssetDatabase.AssetPathToGUID(assetPath), -1);
            }
            return null;
        }

        /// <summary>
        /// 检查资源引用（基于文本正则匹配）
        /// </summary>
        /// <param name="assetPath">被检查资源的路径（相对路径）</param>
        /// <param name="regex">正则表达式</param>
        /// <returns></returns>
        private static ReferenceInfo CheckReferenceRegex(string assetPath, Regex regex)
        {
            string text = File.ReadAllText(GetAssetAbsolutePath(assetPath));
            MatchCollection collection = regex.Matches(text);
            if (collection.Count > 0)
            {
                return new ReferenceInfo(assetPath, AssetDatabase.AssetPathToGUID(assetPath), collection.Count);
            }
            return null;
        }

        /// <summary>
        /// 打印结果
        /// </summary>
        /// <param name="results"></param>
        private static void PrintResults(List<ReferenceInfo> results)
        {
            for (int i = 0; i < results.Count; i++)
            {
                Debug.Log($"References {i + 1}: {results[i].Path}");
            }
        }

        /// <summary>
        /// 获取目标资源的在项目 Assets 目录下的相对路径
        /// </summary>
        /// <param name="absolutePath">资源的绝对路径</param>
        /// <returns></returns>
        private static string GetAssetRelativePath(string absolutePath)
        {
            return "Assets" + absolutePath.Substring(Application.dataPath.Length).Replace("\\", "/");
        }

        /// <summary>
        /// 获取目标资源的绝对路径
        /// </summary>
        /// <param name="relativePath">资源的相对路径</param>
        /// <returns></returns>
        private static string GetAssetAbsolutePath(string relativePath)
        {
            return _projectPath + relativePath;
        }

        /// <summary>
        /// 引用信息
        /// </summary>
        public class ReferenceInfo
        {

            /// <summary>
            /// 资源的路径
            /// </summary>
            public readonly string Path;

            /// <summary>
            /// 资源的 GUID
            /// </summary>
            public readonly string GUID;

            /// <summary>
            /// 资源的引用次数
            /// </summary>
            public readonly int RefCount;

            public ReferenceInfo(string path, string guid, int refCount)
            {
                this.Path = path;
                this.GUID = guid;
                this.RefCount = refCount;
            }

        }

    }

}
