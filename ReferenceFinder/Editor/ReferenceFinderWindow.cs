using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Eazax.Editor
{

    /// <summary>
    /// 引用查找器窗口
    /// </summary>
    /// <author>陈皮皮</author>
    /// <version>20220704</version>
    public class ReferenceFinderWindow : EditorWindow
    {

        #region WindowDefine

        /// <summary>
        /// 窗口实例
        /// </summary>
        private static ReferenceFinderWindow _instance;

        /// <summary>
        /// 展示窗口
        /// </summary>
        [MenuItem("Window/Reference Finder")]
        public static void ShowWindow()
        {
            _instance = GetWindow(typeof(ReferenceFinderWindow)) as ReferenceFinderWindow;
            if (_instance == null)
            {
                throw new Exception("Failed to open window!");
            }
            // 设置窗口标题
            _instance.titleContent = new GUIContent("Reference Finder");
            // 设置窗口尺寸
            SetWindowSize(_instance, 700, 410);
            _instance.minSize = new Vector2(700, 410);
            // 使窗口居中于编辑器
            CenterWindow(_instance, 0, -100);
        }

        /// <summary>
        /// 查找当前选中资源的引用
        /// </summary>
        [MenuItem("Assets/Find References", false, 3)]
        private static void MenuFindReferences()
        {
            if (!Selection.activeObject)
            {
                return;
            }
            // 打开窗口
            if (_instance == null)
            {
                ShowWindow();
            }
            // 触发搜索
            _instance._asset = Selection.activeObject;
            _instance._searchMode = SearchMode.BuiltinAPI;
            _instance.OnFindButtonClick();
        }

        /// <summary>
        /// OnDestroy is called to close the EditorWindow window.
        /// </summary>
        private void OnDestroy()
        {
            _instance = null;
        }

        #endregion

        #region Data

        /// <summary>
        /// 数据列表
        /// </summary>
        private List<ReferenceFinder.ReferenceInfo> _results;

        /// <summary>
        /// 过滤后的数据列表
        /// </summary>
        private ReferenceFinder.ReferenceInfo[] _filteredResults;

        /// <summary>
        /// 更新数据列表
        /// </summary>
        /// <param name="data">数据</param>
        private void UpdateResults(List<ReferenceFinder.ReferenceInfo> data)
        {
            if (data == null)
            {
                return;
            }
            // 浅拷贝源数据
            _results = new List<ReferenceFinder.ReferenceInfo>(data);
            // 排序
            SortResults(_sortingType = SortingType.Path, _sortingOption = SortingOption.Positive);
        }

        /// <summary>
        /// 查找资源的所有引用
        /// </summary>
        /// <param name="asset">资源</param>
        /// <param name="mode">搜索模式</param>
        public void FindReferences(Object asset, SearchMode mode)
        {
            if (_isSearching || _isResolving)
            {
                return;
            }
            // 清除旧数据
            _results = null;
            _filteredResults = null;

            // 完成回调
            void SearchCallback(List<ReferenceFinder.ReferenceInfo> list)
            {
                _isSearching = false;
                // 解析数据
                _isResolving = true;
                UpdateResults(list);
                _isResolving = false;
                // 更新窗口
                if (_instance != null)
                {
                    _instance.Repaint();
                }
            }

            // Unity 内置资源只能使用正则匹配的方式来查找引用
            if (IsUnityBuiltinAsset(asset) || mode == SearchMode.TextRegex)
            {
                // 检查编辑器资源序列化模式
                if (!CheckEditorSerializationMode(SerializationMode.ForceText))
                {
                    string message = @"To be able to use TextRegex search mode, you need to set the editor's Asset Serialization Mode to ForceText, would you like to switch right now?

You can switch Serialization Mode manually at [Edit -> Project Settings -> Editor -> Asset Serialization -> Mode].";
                    bool switchNow = EditorUtility.DisplayDialog("Notice", message, "Yes, switch now", "No, not now");
                    if (!switchNow)
                    {
                        return;
                    }
                    SwitchEditorSerialization(SerializationMode.ForceText);
                }
                // 开始查找
                _isSearching = true;
                ReferenceFinder.FindRegex(asset, SearchCallback);
            }
            else
            {
                // 开始查找
                _isSearching = true;
                ReferenceFinder.Find(AssetDatabase.GetAssetPath(asset), SearchCallback);
            }
        }

        #endregion

        #region DataSorting

        /// <summary>
        /// 排序类型
        /// </summary>
        private enum SortingType
        {

            /// <summary>
            /// 路径
            /// </summary>
            Path = 0,

            /// <summary>
            /// 引用次数
            /// </summary>
            RefCount,

        }

        /// <summary>
        /// 排序选项
        /// </summary>
        private enum SortingOption
        {

            /// <summary>
            /// 正序
            /// </summary>
            Positive = 0,

            /// <summary>
            /// 倒序
            /// </summary>
            Negative,

        }

        /// <summary>
        /// 排序类型
        /// </summary>
        private SortingType _sortingType = SortingType.Path;

        /// <summary>
        /// 排序选项
        /// </summary>
        private SortingOption _sortingOption = SortingOption.Positive;

        /// <summary>
        /// 对结果进行排序
        /// </summary>
        /// <param name="type"></param>
        /// <param name="option"></param>
        private void SortResults(SortingType type, SortingOption option)
        {
            if (_results == null)
            {
                return;
            }
            switch (type)
            {
                case SortingType.Path:
                {
                    switch (option)
                    {
                        case SortingOption.Positive:
                        default:
                        {
                            _results.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));
                            break;
                        }
                        case SortingOption.Negative:
                        {
                            _results.Sort((a, b) => string.Compare(b.Path, a.Path, StringComparison.Ordinal));
                            break;
                        }
                    }
                    break;
                }
                case SortingType.RefCount:
                {
                    switch (option)
                    {
                        case SortingOption.Positive:
                        default:
                        {
                            _results.Sort((a, b) => a.RefCount.CompareTo(b.RefCount));
                            break;
                        }
                        case SortingOption.Negative:
                        {
                            _results.Sort((a, b) => b.RefCount.CompareTo(a.RefCount));
                            break;
                        }
                    }
                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
                }
            }
        }

        /// <summary>
        /// 切换排序选项
        /// </summary>
        private void SwitchSortingOption()
        {
            _sortingOption++;
            _sortingOption = (SortingOption)((int)_sortingOption % Enum.GetNames(typeof(SortingOption)).Length);
        }

        #endregion

        #region ButtonCallbacks

        /// <summary>
        /// 查找按钮点击回调
        /// </summary>
        private void OnFindButtonClick()
        {
            if (_asset == null || _isSearching || _isResolving)
            {
                return;
            }
            FindReferences(_asset, _searchMode);
        }

        /// <summary>
        /// 清除按钮点击回调
        /// </summary>
        private void OnClearButtonClick()
        {
            _asset = null;
            _results = null;
            _filteredResults = null;
        }

        /// <summary>
        /// 路径按钮点击回调
        /// </summary>
        private void OnPathButtonClick()
        {
            if (_sortingType == SortingType.Path)
            {
                SwitchSortingOption();
            }
            else
            {
                _sortingType = SortingType.Path;
                _sortingOption = SortingOption.Positive;
            }
            SortResults(_sortingType, _sortingOption);
        }

        /// <summary>
        /// 引用次数按钮点击回调
        /// </summary>
        private void OnRefCountButtonClick()
        {
            if (_sortingType == SortingType.RefCount)
            {
                SwitchSortingOption();
            }
            else
            {
                _sortingType = SortingType.RefCount;
                _sortingOption = SortingOption.Positive;
            }
            SortResults(_sortingType, _sortingOption);
        }

        #endregion

        #region GUI

        /// <summary>
        /// 搜索模式
        /// </summary>
        public enum SearchMode
        {

            /// <summary>
            /// Unity 内置接口（AssetDatabase.GetDependencies）
            /// </summary>
            BuiltinAPI,

            /// <summary>
            /// 文本正则匹配
            /// </summary>
            TextRegex

        }

        /// <summary>
        /// 是否正在查找结果
        /// </summary>
        private bool _isSearching;

        /// <summary>
        /// 是否正在处理结果
        /// </summary>
        private bool _isResolving;

        /// <summary>
        /// 资源
        /// </summary>
        private Object _asset;

        /// <summary>
        /// 搜索模式
        /// </summary>
        private SearchMode _searchMode = SearchMode.BuiltinAPI;

        /// <summary>
        /// 列表滚动位置
        /// </summary>
        private Vector2 _scrollPosition;

        /// <summary>
        /// 包含场景
        /// </summary>
        private bool _includeScene = true;

        /// <summary>
        /// 包含预制件
        /// </summary>
        private bool _includePrefab = true;

        /// <summary>
        /// 包含材质
        /// </summary>
        private bool _includeMaterial = true;

        /// <summary>
        /// 包含其他资源
        /// </summary>
        private bool _includeOthers = true;

        /// <summary>
        /// 搜索内容
        /// </summary>
        private string _search = "";

        /// <summary>
        /// 搜索内容（LowerCased）
        /// </summary>
        private string _searchString = "";

        /// <summary>
        /// 重置过滤器
        /// </summary>
        private void ResetFilter()
        {
            _includeScene = true;
            _includePrefab = true;
            _includeMaterial = true;
            _includeOthers = true;
            _search = "";
            _searchString = "";
        }

        /// <summary>
        /// 过滤器
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        private bool Filter(ReferenceFinder.ReferenceInfo info)
        {
            string path = info.Path;
            switch (Path.GetExtension(path))
            {
                case ".unity":
                {
                    if (!_includeScene)
                        return false;
                    break;
                }
                case ".prefab":
                {
                    if (!_includePrefab)
                        return false;
                    break;
                }
                case ".mat":
                {
                    if (!_includeMaterial)
                        return false;
                    break;
                }
                default:
                {
                    if (!_includeOthers)
                        return false;
                    break;
                }
            }
            return (_searchString == "" || path.ToLower().Contains(_searchString));
        }

        /// <summary>
        /// 搜索模式选项过滤
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool SearchModeCheckEnabled(Enum value)
        {
            if ((SearchMode)value == SearchMode.BuiltinAPI && IsUnityBuiltinAsset(_asset))
            {
                return false;
            }
            return true;
        }

        #region GUILabels

        private readonly GUIContent _emptyContent = new GUIContent();

        private readonly GUIContent _optionAssetLabel = new GUIContent("Asset", "Just asset");

        private readonly GUIContent _optionModeLabel = new GUIContent("Search Mode", @"Choose search mode (for unity builtin assets will always using TextRegex mode):
  - Builtin API: Use AssetDatabase.GetDependencies API to collect reference infos
  - Text Regex: Use regular expressions to match FileID and GUID text in asset files to confirm references");

        private readonly GUIContent _findButtonLabel = new GUIContent("Find References");

        private readonly GUIContent _clearButtonLabel = new GUIContent("Clear");

        private readonly GUIContent _resultsTitleLabel = new GUIContent("Asset References", "Asset reference results");

        private readonly GUIContent _filterTitleLabel = new GUIContent("Filter", "Filtering results");

        private readonly GUIContent _sceneLabel = new GUIContent("Scene", "Scene asset (.unity)");

        private readonly GUIContent _prefabLabel = new GUIContent("Prefab", "Prefab asset (.prefab)");

        private readonly GUIContent _materialLabel = new GUIContent("Material", "Material asset (.mat)");

        private readonly GUIContent _othersLabel = new GUIContent("Others", @"Any other asset types, includes:
  - Unity Asset (.asset)
  - Shader Variant Collection (.shadervariants)
  - Custom Font (.fontsettings)
  - Cubemap (.cubemap)
  - Scene Template (.scenetemplate)
  - Animation Override Controller (.overrideController)
  - Terrain Layer (.terrainlayer)
  - GUI Skin(.guiskin)
and so on...");

        private readonly GUIContent _toolbarSearchClearButtonLabel = new GUIContent("");

        private readonly GUIContent _tableNoLabel = new GUIContent("No.");

        private readonly GUIContent _tableAssetLabel = new GUIContent("Asset");

        private readonly GUIContent _tablePathLabel = new GUIContent("  Asset Path －");

        private readonly GUIContent _tablePathUpLabel = new GUIContent("  Asset Path ▲");

        private readonly GUIContent _tablePathDownLabel = new GUIContent("  Asset Path ▼");

        private readonly GUIContent _tableRefCountLabel = new GUIContent("  Ref Count －");

        private readonly GUIContent _tableRefCountUpLabel = new GUIContent("  Ref Count ▲");

        private readonly GUIContent _tableRefCountDownLabel = new GUIContent("  Ref Count ▼");

        private readonly GUIContent _missingAssetLabel = new GUIContent("⚠ Missing Asset");

        #endregion

        /// <summary>
        /// 绘制 GUI
        /// </summary>
        private void OnGUI()
        {
            GUILayout.Space(_padding);
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.Space(_padding);
                EditorGUILayout.BeginVertical();
                {
                    // 资源
                    OnAssetGUI();

                    GUILayout.Space(_sectionSpacing);

                    // 结果
                    OnResultsGUI();
                }
                EditorGUILayout.EndVertical();
                GUILayout.Space(_padding);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // 底栏
            OnBottomBarGUI();
        }

        /// <summary>
        /// 资源 GUI
        /// </summary>
        private void OnAssetGUI()
        {
            EditorGUILayout.BeginVertical();
            {
                // 资源
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField(_optionAssetLabel, MyStyles.TitleStyle);
                    bool wasNullOrBuiltinAsset = _asset == null || IsUnityBuiltinAsset(_asset);
                    _asset = EditorGUILayout.ObjectField(_asset, typeof(Object), false);
                    // 项目资源默认使用接口查询模式
                    if (_asset != null && wasNullOrBuiltinAsset && !IsUnityBuiltinAsset(_asset))
                    {
                        _searchMode = SearchMode.BuiltinAPI;
                    }
                }
                EditorGUILayout.EndHorizontal();

                // 搜索模式
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField(_optionModeLabel, MyStyles.TitleStyle);
                    _searchMode = (SearchMode)EditorGUILayout.EnumPopup(_emptyContent, _searchMode, SearchModeCheckEnabled, false);
                    // Unity 内置资源只能使用正则匹配的方式来查找引用
                    if (IsUnityBuiltinAsset(_asset))
                    {
                        _searchMode = SearchMode.TextRegex;
                    }
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(_itemSpacing);

                // 查找按钮
                if (GUILayout.Button(_findButtonLabel))
                {
                    OnFindButtonClick();
                    GUI.FocusControl(null);
                }

                // 清除按钮
                if (GUILayout.Button(_clearButtonLabel))
                {
                    OnClearButtonClick();
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 结果 GUI
        /// </summary>
        private void OnResultsGUI()
        {
            EditorGUILayout.BeginVertical();
            {
                // 标题
                EditorGUILayout.LabelField(_resultsTitleLabel, MyStyles.TitleStyle);

                // 顶栏
                EditorGUILayout.BeginHorizontal(MyStyles.ToolbarStyle);
                {
                    // 标题
                    EditorGUILayout.LabelField(_filterTitleLabel, MyStyles.TitleStyle, MyLayoutOptions.toolbarTitleWidth);

                    // 过滤
                    EditorGUILayout.BeginHorizontal(MyStyles.ToolbarToggleGroupStyle);
                    {
                        // 场景
                        _includeScene = EditorGUILayout.ToggleLeft(_sceneLabel, _includeScene, MyStyles.ToolbarToggleStyle, MyLayoutOptions.toolbarToggleWidth);
                        // 预制件
                        _includePrefab = EditorGUILayout.ToggleLeft(_prefabLabel, _includePrefab, MyStyles.ToolbarToggleStyle, MyLayoutOptions.toolbarToggleWidth);
                        // 材质
                        _includeMaterial = EditorGUILayout.ToggleLeft(_materialLabel, _includeMaterial, MyStyles.ToolbarToggleStyle, MyLayoutOptions.toolbarToggleWidth);
                        // 其他资源
                        _includeOthers = EditorGUILayout.ToggleLeft(_othersLabel, _includeOthers, MyStyles.ToolbarToggleStyle, MyLayoutOptions.toolbarToggleWidth);
                    }
                    EditorGUILayout.EndHorizontal();

                    GUILayout.FlexibleSpace();

                    // 搜索
                    EditorGUILayout.BeginHorizontal(MyStyles.ToolbarToggleGroupStyle);
                    {
                        // 输入文本
                        _search = EditorGUILayout.TextField(_search, MyStyles.ToolbarSearchStyle, MyLayoutOptions.toolbarSearchWidth);
                        _searchString = _search.ToLower();
                        // 清空输入按钮
                        if (GUILayout.Button(_toolbarSearchClearButtonLabel, MyStyles.ToolbarSearchCancelStyle))
                        {
                            _search = "";
                            _searchString = "";
                            GUI.FocusControl(null);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndHorizontal();

                // 表格
                EditorGUILayout.BeginVertical();
                {
                    // 表头
                    EditorGUILayout.BeginHorizontal(MyStyles.TableHeaderLineStyle, MyLayoutOptions.tableHeaderHeight);
                    {
                        // 序号
                        EditorGUILayout.LabelField(_tableNoLabel, MyStyles.TableHeaderLabelStyle, MyLayoutOptions.tableNoWidth);
                        // 资源
                        EditorGUILayout.LabelField(_tableAssetLabel, MyStyles.TableHeaderLabelStyle, MyLayoutOptions.tableAssetWidth);
                        // 资源路径
                        GUIContent pathLabel = _tablePathLabel;
                        if (_sortingType == SortingType.Path)
                        {
                            pathLabel = _sortingOption == SortingOption.Negative ? _tablePathUpLabel : _tablePathDownLabel;
                        }
                        if (GUILayout.Button(pathLabel, MyStyles.TableHeaderButtonStyle, MyLayoutOptions.expandWidth))
                        {
                            OnPathButtonClick();
                        }
                        // 引用次数
                        GUIContent refCountLabel = _tableRefCountLabel;
                        if (_sortingType == SortingType.RefCount)
                        {
                            refCountLabel = _sortingOption == SortingOption.Negative ? _tableRefCountUpLabel : _tableRefCountDownLabel;
                        }
                        if (GUILayout.Button(refCountLabel, MyStyles.TableHeaderButtonStyle, MyLayoutOptions.tableRefCountWidth))
                        {
                            OnRefCountButtonClick();
                        }
                        GUILayout.Space(15);
                    }
                    EditorGUILayout.EndHorizontal();

                    // 过滤数据
                    _filteredResults = _results?.Where(Filter).ToArray();
                    // 没有数据或者数据不够需要填充空行
                    GUILayoutOption scrollHeightOption = _filteredResults?.Length > _tableMinRowCount ? MyLayoutOptions.expandHeight : MyLayoutOptions.tableMinHeight;
                    _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, scrollHeightOption);
                    {
                        int lineCount = Math.Max(_filteredResults?.Length ?? _tableMinRowCount, _tableMinRowCount);
                        for (int i = 0; i < lineCount; i++)
                        {
                            GUIStyle lineStyle = i % 2 == 0 ? MyStyles.TableEvenRowStyle : MyStyles.TableOddRowStyle;
                            EditorGUILayout.BeginHorizontal(lineStyle, MyLayoutOptions.tableRowHeight);
                            {
                                // 不在可视区域内时不渲染实际内容，直接渲染空行来填充
                                int baseline = i * _tableRowHeight;
                                bool isInView = (baseline >= _scrollPosition.y - 60) && (baseline < _scrollPosition.y + position.height);
                                if (isInView && _filteredResults?.Length > i)
                                {
                                    ReferenceFinder.ReferenceInfo info = _filteredResults[i];
                                    // 序号
                                    EditorGUILayout.LabelField($"{i + 1}", MyStyles.TableLabelStyle, MyLayoutOptions.tableNoWidth, MyLayoutOptions.tableContentHeight);
                                    // 资源
                                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(info.Path);
                                    if (obj)
                                    {
                                        EditorGUILayout.ObjectField(obj, typeof(Object), false, MyLayoutOptions.tableAssetWidth, MyLayoutOptions.tableContentHeight);
                                    }
                                    else
                                    {
                                        EditorGUILayout.LabelField(_missingAssetLabel, MyStyles.TableLabelStyle, MyLayoutOptions.tableAssetWidth, MyLayoutOptions.tableContentHeight);
                                    }
                                    // 资源路径
                                    EditorGUILayout.SelectableLabel($"{info.Path}", MyStyles.TableLabelStyle, MyLayoutOptions.expandWidth, MyLayoutOptions.tableContentHeight);
                                    // 引用次数
                                    string refCountLabel = info.RefCount > 0 ? $"{info.RefCount}" : "≥1";
                                    EditorGUILayout.LabelField(refCountLabel, MyStyles.TableLabelCenterStyle, MyLayoutOptions.tableRefCountWidth, MyLayoutOptions.tableContentHeight);
                                }
                                else
                                {
                                    // 空行
                                    EditorGUILayout.Space();
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 底栏 GUI
        /// </summary>
        private void OnBottomBarGUI()
        {
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.BeginHorizontal(MyStyles.BottomBarStyle, MyLayoutOptions.bottomBarHeight);
                {
                    // 图标和提示语
                    string iconName;
                    string tipLabel;
                    if (!_asset)
                    {
                        iconName = "console.infoicon";
                        tipLabel = "Pick a asset first.";
                    }
                    else if (_isSearching)
                    {
                        iconName = "loading";
                        tipLabel = "Searching...";
                    }
                    else if (_isResolving)
                    {
                        iconName = "loading";
                        tipLabel = "Resolving...";
                    }
                    else if (_results != null)
                    {
                        iconName = "progress";
                        if (_filteredResults != null && (!_includeScene || !_includePrefab || !_includeMaterial || !_includeOthers || !string.IsNullOrEmpty(_search)))
                        {
                            tipLabel = $"Total {_results.Count} result(s) found, {_filteredResults.Length} result(s) left after filtering.";
                        }
                        else
                        {
                            tipLabel = $"Total {_results.Count} result(s) found.";
                        }
                    }
                    else
                    {
                        iconName = "console.infoicon";
                        tipLabel = "Ready to go!";
                    }
                    EditorGUILayout.LabelField(EditorGUIUtility.IconContent(iconName), MyStyles.BottomBarIconStyle, MyLayoutOptions.bottomBarIconWidth);
                    EditorGUILayout.LabelField(new GUIContent(tipLabel), MyStyles.BottomBarLabelStyle);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region GUILayoutConfigs

        private const int _padding = 10;

        private const int _sectionSpacing = 5;

        private const int _itemSpacing = 5;

        private const int _tableRowHeight = 22;

        private const int _tableMinRowCount = 10;

        #endregion

        #region GUILayoutOptions

        /// <summary>
        /// 布局选项
        /// </summary>
        private static class MyLayoutOptions
        {

            public static readonly GUILayoutOption expandWidth = GUILayout.ExpandWidth(true);
            public static readonly GUILayoutOption expandHeight = GUILayout.ExpandHeight(true);

            public static readonly GUILayoutOption toolbarTitleWidth = GUILayout.MaxWidth(60);
            public static readonly GUILayoutOption toolbarToggleWidth = GUILayout.MaxWidth(80);
            public static readonly GUILayoutOption toolbarSearchWidth = GUILayout.Width(200);

            public static readonly GUILayoutOption tableHeaderHeight = GUILayout.Height(22);
            public static readonly GUILayoutOption tableMinHeight = GUILayout.Height(_tableRowHeight * _tableMinRowCount);
            public static readonly GUILayoutOption tableRowHeight = GUILayout.Height(_tableRowHeight);
            public static readonly GUILayoutOption tableNoWidth = GUILayout.Width(40);
            public static readonly GUILayoutOption tableAssetWidth = GUILayout.Width(250);
            public static readonly GUILayoutOption tableRefCountWidth = GUILayout.Width(80);
            public static readonly GUILayoutOption tableContentHeight = GUILayout.Height(18);

            public static readonly GUILayoutOption bottomBarHeight = GUILayout.Height(21);
            public static readonly GUILayoutOption bottomBarIconWidth = GUILayout.Width(18);

        }

        #endregion

        #region GUIStyles

        /// <summary>
        /// 风格
        /// </summary>
        private static class MyStyles
        {

            /// <summary>
            /// 工具栏行风格
            /// </summary>
            public static readonly GUIStyle TitleStyle = new GUIStyle(EditorStyles.boldLabel);

            /// <summary>
            /// 工具栏行风格
            /// </summary>
            public static readonly GUIStyle ToolbarStyle = new GUIStyle(GetBuiltinGUIStyle("contentToolbar"));

            /// <summary>
            /// 工具栏行风格
            /// </summary>
            public static readonly GUIStyle ToolbarToggleGroupStyle = new GUIStyle(GUIStyle.none);

            /// <summary>
            /// 工具栏行风格
            /// </summary>
            public static readonly GUIStyle ToolbarToggleStyle = new GUIStyle(EditorStyles.label);

            /// <summary>
            /// 工具栏搜索风格
            /// </summary>
            public static readonly GUIStyle ToolbarSearchStyle = new GUIStyle(GetBuiltinGUIStyle("SearchTextField"));

            /// <summary>
            /// 工具栏搜索风格
            /// </summary>
            public static readonly GUIStyle ToolbarSearchCancelStyle = new GUIStyle(GetBuiltinGUIStyle("SearchCancelButton"));

            /// <summary>
            /// 表格头部行风格
            /// </summary>
            public static readonly GUIStyle TableHeaderLineStyle = new GUIStyle(GUIStyle.none)
            {
                normal = { background = MakeSingleColorTexture(new Color(0.5f, 0.5f, 0.5f, 0.3f)) },
            };

            /// <summary>
            /// 表格头部文本风格
            /// </summary>
            public static readonly GUIStyle TableHeaderLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                stretchWidth = true,
                stretchHeight = true,
            };

            /// <summary>
            /// 表格头部按钮风格
            /// </summary>
            public static readonly GUIStyle TableHeaderButtonStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                stretchWidth = true,
                stretchHeight = true,
            };

            /// <summary>
            /// 表格条目单数行风格
            /// </summary>
            public static readonly GUIStyle TableOddRowStyle = new GUIStyle(GUIStyle.none)
            {
                normal = { background = MakeSingleColorTexture(new Color(0.5f, 0.5f, 0.5f, 0.2f)) },
            };

            /// <summary>
            /// 表格条目双数行风格
            /// </summary>
            public static readonly GUIStyle TableEvenRowStyle = new GUIStyle(GUIStyle.none)
            {
                normal = { background = MakeSingleColorTexture(new Color(0.5f, 0.5f, 0.5f, 0.1f)) },
            };

            /// <summary>
            /// 表格条目文本风格
            /// </summary>
            public static readonly GUIStyle TableLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                stretchWidth = true,
                stretchHeight = true,
                padding = new RectOffset(5, 5, 1, 1),
                margin = new RectOffset(0, 0, 0, 0),
            };

            /// <summary>
            /// 表格条目文本居中风格
            /// </summary>
            public static readonly GUIStyle TableLabelCenterStyle = new GUIStyle(TableLabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
            };

            /// <summary>
            /// 提示栏行风格
            /// </summary>
            public static readonly GUIStyle BottomBarStyle = new GUIStyle(GetBuiltinGUIStyle("contentToolbar"));

            /// <summary>
            /// 提示栏行风格
            /// </summary>
            public static readonly GUIStyle BottomBarIconStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
            };

            /// <summary>
            /// 提示栏行风格
            /// </summary>
            public static readonly GUIStyle BottomBarLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
            };

        }

        #endregion

        #region UtilityFunctions

        /// <summary>
        /// 判断资源是否为 Unity 内置资源
        /// </summary>
        /// <param name="asset">资源</param>
        /// <returns></returns>
        private static bool IsUnityBuiltinAsset(Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return path.Equals("Resources/unity_builtin_extra") || path.Equals("Library/unity default resources");
        }

        /// <summary>
        /// 检查编辑器的资源序列化模式
        /// </summary>
        /// <param name="mode">资源序列化模式</param>
        /// <returns></returns>
        private static bool CheckEditorSerializationMode(SerializationMode mode)
        {
            return EditorSettings.serializationMode == mode;
        }

        /// <summary>
        /// 切换编辑器的资源序列化模式
        /// </summary>
        /// <param name="mode">资源序列化模式</param>
        /// <returns></returns>
        private static void SwitchEditorSerialization(SerializationMode mode)
        {
            EditorSettings.serializationMode = mode;
        }

        /// <summary>
        /// 创建一个单色纹理
        /// </summary>
        /// <param name="color">颜色</param>
        /// <returns></returns>
        private static Texture2D MakeSingleColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixels(new Color[] { color });
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// 获取 Unity 内置 GUI 风格
        /// </summary>
        /// <param name="styleName">风格名称</param>
        /// <returns></returns>
        private static GUIStyle GetBuiltinGUIStyle(string styleName)
        {
            GUIStyle style = GUI.skin.FindStyle(styleName) ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle(styleName);
            if (style == null)
            {
                Debug.LogError((object)("Missing built-in guistyle " + styleName));
            }
            return style;
        }

        /// <summary>
        /// 设置窗口尺寸
        /// </summary>
        /// <param name="window">窗口实例</param>
        /// <param name="width">宽</param>
        /// <param name="height">高</param>
        public static void SetWindowSize(EditorWindow window, int width, int height)
        {
            Rect pos = window.position;
            pos.width = width;
            pos.height = height;
            window.position = pos;
        }

        /// <summary>
        /// 使窗口居中（基于 Unity 编辑器主窗口）
        /// </summary>
        /// <param name="window">窗口实例</param>
        /// <param name="offsetX">水平偏移</param>
        /// <param name="offsetY">垂直偏移</param>
        public static void CenterWindow(EditorWindow window, int offsetX = 0, int offsetY = 0)
        {
            Rect mainWindowPos = EditorGUIUtility.GetMainWindowPosition();
            Rect pos = window.position;
            float centerOffsetX = (mainWindowPos.width - pos.width) * 0.5f;
            float centerOffsetY = (mainWindowPos.height - pos.height) * 0.5f;
            pos.x = mainWindowPos.x + centerOffsetX + offsetX;
            pos.y = mainWindowPos.y + centerOffsetY + offsetY;
            window.position = pos;
        }

        #endregion

    }

}
