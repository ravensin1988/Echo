using UnityEngine;
using UnityEngine.Animations.Rigging;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Скрипт для автоматического назначения Target в TwoBoneIKConstraint по тегу
/// Работает с аддоном Unity Animation Rigging (Rig Builder)
/// </summary>
public class TwoBoneIKTargetAssigner : MonoBehaviour
{
    [Header("Настройки поиска")]
    [Tooltip("Тег для поиска объектов-целей")]
    [SerializeField] private string _targetTag = "IKTarget";
    
    [Tooltip("Искать только среди дочерних объектов")]
    [SerializeField] private bool _searchInChildren = true;
    
    [Tooltip("Автоматически назначать при старте")]
    [SerializeField] private bool _assignOnStart = true;
    
    [Tooltip("Задержка перед назначением (в секундах)")]
    [SerializeField] private float _assignmentDelay = 0.1f;
    
    [Tooltip("Обновлять каждый кадр (для динамических целей)")]
    [SerializeField] private bool _updateEveryFrame = false;

    [Header("Настройки Rig Builder")]
    [Tooltip("Автоматически находить Rig Builder")]
    [SerializeField] private bool _autoFindRigBuilder = true;
    
    [Tooltip("Rig Builder (можно назначить вручную)")]
    [SerializeField] private RigBuilder _rigBuilder;
    
    [Tooltip("Перестроить риг после назначения")]
    [SerializeField] private bool _rebuildRigAfterAssign = true;
    
    [Tooltip("Количество попыток назначения")]
    [SerializeField] private int _rebuildAttempts = 3;

    [Header("Отладка")]
    [SerializeField] private bool _showDebugInfo = false;

    // Публичное свойство для доступа к тегу
    /// <summary>
    /// Тег, используемый для поиска IK-целей.
    /// </summary>
    public string TargetTag => _targetTag;

    private TwoBoneIKConstraint[] _ikConstraints;
    private bool _isInitialized = false;
    private bool _constraintsDirty = true;
    
    // Кэш для поиска объектов по тегу (повышает производительность)
    private GameObject[] _cachedTaggedObjects = null;
    private float _cacheTime = -1f;
    private const float CACHE_DURATION = 0.5f;

    private readonly List<TargetCacheEntry> _targetCacheEntries = new();
    private readonly Dictionary<string, string> _normalizedConstraintNameCache = new();

    private WaitForSeconds _assignmentDelayWait;
    private static readonly WaitForSeconds RetryWait = new(0.05f);

    private static FieldInfo _cachedTargetField;
    private static bool _targetFieldResolved;

    private readonly struct TargetCacheEntry
    {
        public readonly Transform Transform;
        public readonly string NameLower;
        public readonly string CleanNameLower;

        public TargetCacheEntry(Transform transform, string nameLower, string cleanNameLower)
        {
            Transform = transform;
            NameLower = nameLower;
            CleanNameLower = cleanNameLower;
        }
    }

    private void Start()
    {
        _assignmentDelayWait = _assignmentDelay > 0f ? new WaitForSeconds(_assignmentDelay) : null;

        if (_autoFindRigBuilder && _rigBuilder == null)
        {
            _rigBuilder = GetComponentInParent<RigBuilder>();
            if (_rigBuilder == null)
                _rigBuilder = GetComponentInChildren<RigBuilder>();
        }
        
        if (_assignOnStart)
        {
            StartCoroutine(DelayedAssignment());
        }
    }

    private IEnumerator DelayedAssignment()
    {
        if (_assignmentDelay > 0f)
            yield return _assignmentDelayWait;
        
        bool success = AssignTargetsByTag();
        
        if (!success && _rebuildAttempts > 1)
        {
            for (int i = 1; i < _rebuildAttempts; i++)
            {
                yield return RetryWait;
                
                if (_showDebugInfo)
                    Debug.Log($"[{nameof(TwoBoneIKTargetAssigner)}] Попытка назначения #{i + 1}");
                
                success = AssignTargetsByTag();
                if (success) break;
            }
        }
        
        // Перестраиваем риг один раз после всех назначений
        if (success && _rebuildRigAfterAssign)
        {
            yield return ForceRebuildRigCoroutine();
        }
        
        _isInitialized = success;
    }
    
    private void OnDisable()
    {
        _isInitialized = false;
        _constraintsDirty = true;
        ClearCache();
    }
    
    private void OnDestroy()
    {
        _isInitialized = false;
        ClearCache();
    }

    private void Update()
    {
        if (_updateEveryFrame && _isInitialized)
        {
            if (_rigBuilder == null || !_rigBuilder.isActiveAndEnabled)
            {
                if (_autoFindRigBuilder)
                {
                    _rigBuilder = GetComponentInParent<RigBuilder>();
                    if (_rigBuilder == null)
                        _rigBuilder = GetComponentInChildren<RigBuilder>();
                }
                
                if (_rigBuilder == null || !_rigBuilder.isActiveAndEnabled)
                    return;
            }
            
            AssignTargetsByTag();
        }
    }

    /// <summary>
    /// Найти все TwoBoneIKConstraint и назначить им цели по тегу
    /// </summary>
    [ContextMenu("Assign Targets By Tag")]
    public bool AssignTargetsByTag()
    {
        RefreshConstraintsCache();

        if (_ikConstraints == null || _ikConstraints.Length == 0)
        {
            if (_showDebugInfo)
                Debug.LogWarning($"[{nameof(TwoBoneIKTargetAssigner)}] TwoBoneIKConstraint не найдены на объекте '{gameObject.name}'", this);
            return false;
        }

        int assignedCount = 0;

        foreach (var constraint in _ikConstraints)
        {
            if (constraint == null) continue;

            bool wasActive = constraint.enabled;
            if (!wasActive) constraint.enabled = true;

            string constraintName = constraint.name;
            Transform targetTransform = FindTargetByConstraintName(constraintName);

            if (targetTransform != null)
            {
                bool assigned = TryAssignTarget(constraint, targetTransform);
                
                if (assigned)
                {
                    assignedCount++;
                    
                    constraint.data.targetPositionWeight = 1f;
                    constraint.data.targetRotationWeight = 1f;
                    
                    if (_showDebugInfo)
                        Debug.Log($"[{nameof(TwoBoneIKTargetAssigner)}] Назначена цель '{targetTransform.name}' для '{constraintName}'", this);
                }
                
                constraint.enabled = wasActive;
            }
            else
            {
                if (_showDebugInfo)
                    Debug.LogWarning($"[{nameof(TwoBoneIKTargetAssigner)}] Не найдена цель для '{constraintName}'", this);
            }
        }

        if (_showDebugInfo)
            Debug.Log($"[{nameof(TwoBoneIKTargetAssigner)}] Назначено {assignedCount} из {_ikConstraints.Length} IK constraints", this);

        // Перестройка рига намеренно НЕ вызывается здесь.
        // Она запускается один раз из DelayedAssignment после всех назначений,
        // либо вручную через ForceRebuildRigPublic().

        return assignedCount > 0;
    }

    /// <summary>
    /// Публичный метод для принудительной перестройки рига (вызывается из Editor или внешних скриптов)
    /// </summary>
    public void ForceRebuildRigPublic()
    {
        StartCoroutine(ForceRebuildRigCoroutine());
    }

    /// <summary>
    /// Корутина: выключает RigBuilder на один кадр, затем включает обратно.
    /// Имитирует ручной toggle в Inspector — единственный надёжный способ
    /// пересобрать граф анимации после изменения constraint targets в рантайме.
    /// </summary>
    private IEnumerator ForceRebuildRigCoroutine()
    {
        if (_rigBuilder == null)
        {
            if (_autoFindRigBuilder)
            {
                _rigBuilder = GetComponentInParent<RigBuilder>();
                if (_rigBuilder == null)
                    _rigBuilder = GetComponentInChildren<RigBuilder>();
            }

            if (_rigBuilder == null)
            {
                Debug.LogError($"[{nameof(TwoBoneIKTargetAssigner)}] RigBuilder не найден!", this);
                yield break;
            }
        }

        // Запоминаем веса всех Rig слоёв, чтобы восстановить после перестройки
        float[] layerWeights = null;
        if (_rigBuilder.layers != null && _rigBuilder.layers.Count > 0)
        {
            layerWeights = new float[_rigBuilder.layers.Count];
            for (int i = 0; i < _rigBuilder.layers.Count; i++)
            {
                layerWeights[i] = _rigBuilder.layers[i].rig != null
                    ? _rigBuilder.layers[i].rig.weight
                    : 1f;
            }
        }

        // Выключаем — Unity разрушает граф анимации
        _rigBuilder.enabled = false;

        // Ждём один кадр, чтобы OnDisable отработал полностью
        yield return null;

        // Включаем — Unity пересобирает граф с актуальными targets
        _rigBuilder.enabled = true;

        // Ждём ещё один кадр, чтобы OnEnable завершился
        yield return null;

        // Восстанавливаем веса если они были сброшены
        if (layerWeights != null && _rigBuilder.layers != null)
        {
            for (int i = 0; i < _rigBuilder.layers.Count && i < layerWeights.Length; i++)
            {
                if (_rigBuilder.layers[i].rig != null)
                    _rigBuilder.layers[i].rig.weight = layerWeights[i];
            }
        }

        if (_showDebugInfo)
            Debug.Log($"[{nameof(TwoBoneIKTargetAssigner)}] RigBuilder перестроен через toggle", this);
    }

    /// <summary>
    /// Назначить цель — сначала через прямой API, затем через reflection с boxing
    /// </summary>
    private bool TryAssignTarget(TwoBoneIKConstraint constraint, Transform target)
    {
        // Способ 1: Прямое назначение через публичный API
        try
        {
            var data = constraint.data;
            data.target = target;
            constraint.data = data;
            
            if (constraint.data.target == target)
            {
                if (_showDebugInfo)
                    Debug.Log($"[Direct API] Target assigned successfully to '{constraint.name}'");
                return true;
            }
        }
        catch (Exception ex)
        {
            if (_showDebugInfo)
                Debug.Log($"[Direct API] Failed for '{constraint.name}': {ex.Message}, trying reflection...");
        }
        
        // Способ 2: Reflection с boxing для корректной работы со struct
        return TryAssignTargetViaReflection(constraint, target);
    }

    /// <summary>
    /// Назначение через reflection с boxing.
    /// Обычный SetValue на struct создаёт копию — boxing через object решает эту проблему.
    /// </summary>
    private bool TryAssignTargetViaReflection(TwoBoneIKConstraint constraint, Transform target)
    {
        try
        {
            FieldInfo targetField = ResolveTargetField();
            
            if (targetField == null)
            {
                Debug.LogError($"[{nameof(TwoBoneIKTargetAssigner)}] Поле target не найдено в TwoBoneIKConstraintData. " +
                               $"Запустите 'Dump TwoBoneIKConstraintData Fields' для диагностики.", this);
                return false;
            }
            
            // Boxing struct в object — SetValue изменяет именно этот объект, а не копию
            object boxedData = constraint.data;
            
            if (targetField.FieldType == typeof(Transform))
                targetField.SetValue(boxedData, target);
            else if (targetField.FieldType == typeof(WeightedTransform))
                targetField.SetValue(boxedData, new WeightedTransform(target, 1f));
            
            // Unbox обратно и присваиваем constraint
            constraint.data = (TwoBoneIKConstraintData)boxedData;
            
            bool verified = false;
            try { verified = constraint.data.target == target; } catch { }
            
            if (_showDebugInfo)
                Debug.Log($"[Reflection] Field '{targetField.Name}' set for '{constraint.name}'. Verified: {verified}");
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{nameof(TwoBoneIKTargetAssigner)}] Reflection assignment failed: {ex.Message}\n{ex.StackTrace}", this);
            return false;
        }
    }

    /// <summary>
    /// Метод для диагностики — выводит все поля TwoBoneIKConstraintData в консоль
    /// </summary>
    [ContextMenu("Dump TwoBoneIKConstraintData Fields")]
    public void DumpDataFields()
    {
        var constraints = GetComponentsInChildren<TwoBoneIKConstraint>(true);
        if (constraints.Length == 0)
        {
            Debug.Log("No TwoBoneIKConstraint found");
            return;
        }
        
        var data = constraints[0].data;
        Type t = typeof(TwoBoneIKConstraintData);
        
        Debug.Log("=== TwoBoneIKConstraintData FIELDS ===");
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            Debug.Log($"Field: [{f.FieldType.Name}] {f.Name} = {f.GetValue(data)}");
        
        Debug.Log("=== TwoBoneIKConstraintData PROPERTIES ===");
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            Debug.Log($"Property: [{p.PropertyType.Name}] {p.Name}");
    }

    /// <summary>
    /// Получить объекты с тегом (с кэшированием для оптимизации)
    /// </summary>
    private GameObject[] GetCachedTaggedObjects()
    {
        float currentTime = Time.time;
        
        if (_cachedTaggedObjects == null || currentTime - _cacheTime > CACHE_DURATION)
        {
            _cachedTaggedObjects = GameObject.FindGameObjectsWithTag(_targetTag);
            _cacheTime = currentTime;
            RebuildTargetCacheEntries(_cachedTaggedObjects);
            
            if (_showDebugInfo && _cachedTaggedObjects.Length > 0)
                Debug.Log($"[{nameof(TwoBoneIKTargetAssigner)}] Кэш объектов с тегом '{_targetTag}' обновлен. Найдено: {_cachedTaggedObjects.Length}");
        }
        
        return _cachedTaggedObjects;
    }
    
    /// <summary>
    /// Принудительно очистить кэш
    /// </summary>
    public void ClearCache()
    {
        _cachedTaggedObjects = null;
        _cacheTime = -1f;
        _targetCacheEntries.Clear();
        _normalizedConstraintNameCache.Clear();
    }

    /// <summary>
    /// Найти объект-цель по имени constraint
    /// </summary>
    private Transform FindTargetByConstraintName(string constraintName)
    {
        string searchName = GetNormalizedConstraintName(constraintName);

        GameObject[] taggedObjects = GetCachedTaggedObjects();
        
        if (taggedObjects == null || taggedObjects.Length == 0)
        {
            taggedObjects = GameObject.FindGameObjectsWithTag(_targetTag);
            RebuildTargetCacheEntries(taggedObjects);
        }

        if (taggedObjects.Length == 0)
        {
            GameObject obj = GameObject.Find(constraintName + "_Target");
            if (obj != null) return obj.transform;
            
            obj = GameObject.Find(constraintName + "Target");
            if (obj != null) return obj.transform;
            
            return null;
        }

        for (int i = 0; i < _targetCacheEntries.Count; i++)
        {
            TargetCacheEntry entry = _targetCacheEntries[i];

            if (entry.CleanNameLower == searchName || entry.NameLower.Contains(searchName) || searchName.Contains(entry.NameLower))
                return entry.Transform;

            if (IsMatchByKeywords(constraintName, entry.NameLower))
                return entry.Transform;
        }

        // Fallback: первый объект с тегом
        if (taggedObjects.Length > 0)
        {
            if (_showDebugInfo && taggedObjects.Length > 1)
                Debug.LogWarning($"[{nameof(TwoBoneIKTargetAssigner)}] Точное совпадение не найдено для '{constraintName}', назначен первый объект с тегом '{_targetTag}'", this);
            
            return taggedObjects[0].transform;
        }

        return null;
    }

    /// <summary>
    /// Проверка совпадения по ключевым словам (left/right + arm/leg/hand/foot)
    /// </summary>
    private bool IsMatchByKeywords(string constraintName, string targetName)
    {
        string cName = constraintName.ToLower();
        string tName = targetName;

        bool hasLeft  = cName.Contains("left")  && tName.Contains("left");
        bool hasRight = cName.Contains("right") && tName.Contains("right");
        bool hasArm   = cName.Contains("arm")   && (tName.Contains("arm")  || tName.Contains("hand"));
        bool hasLeg   = cName.Contains("leg")   && (tName.Contains("leg")  || tName.Contains("foot"));
        bool hasHand  = cName.Contains("hand")  && tName.Contains("hand");
        bool hasFoot  = cName.Contains("foot")  && tName.Contains("foot");

        return (hasLeft || hasRight) && (hasArm || hasLeg || hasHand || hasFoot);
    }

    /// <summary>
    /// Назначить конкретный объект в качестве цели для constraint
    /// </summary>
    public static void SetTarget(TwoBoneIKConstraint constraint, Transform target)
    {
        if (constraint == null || target == null) return;
        
        // Способ 1: Прямой API
        try
        {
            var data = constraint.data;
            data.target = target;
            constraint.data = data;
            
            constraint.data.targetPositionWeight = 1f;
            constraint.data.targetRotationWeight = 1f;
            return;
        }
        catch { }
        
        // Способ 2: Reflection с boxing
        try
        {
            object boxedData = constraint.data;
            FieldInfo targetField = ResolveTargetField();
            
            if (targetField != null)
            {
                if (targetField.FieldType == typeof(Transform))
                    targetField.SetValue(boxedData, target);
                else if (targetField.FieldType == typeof(WeightedTransform))
                    targetField.SetValue(boxedData, new WeightedTransform(target, 1f));
                
                constraint.data = (TwoBoneIKConstraintData)boxedData;
                constraint.data.targetPositionWeight = 1f;
                constraint.data.targetRotationWeight = 1f;
            }
            else
            {
                Debug.LogError($"SetTarget: поле target не найдено в TwoBoneIKConstraintData", constraint);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"SetTarget failed: {ex.Message}", constraint);
        }
    }

    /// <summary>
    /// Найти и назначить цель для конкретного constraint по тегу
    /// </summary>
    public static bool FindAndAssignTarget(TwoBoneIKConstraint constraint, string tag)
    {
        if (constraint == null || string.IsNullOrEmpty(tag)) return false;

        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);
        if (taggedObjects.Length == 0) return false;

        SetTarget(constraint, taggedObjects[0].transform);
        return true;
    }

    /// <summary>
    /// Статический метод для синхронизации Rig Builder
    /// </summary>
    public static void SyncRigBuilder(RigBuilder rigBuilder)
    {
        if (rigBuilder != null)
        {
            rigBuilder.SyncLayers();
            rigBuilder.Evaluate(0f);
        }
    }

    private void RefreshConstraintsCache()
    {
        if (!_constraintsDirty && _ikConstraints != null && _ikConstraints.Length > 0)
            return;

        _ikConstraints = _searchInChildren
            ? GetComponentsInChildren<TwoBoneIKConstraint>(true)
            : GetComponents<TwoBoneIKConstraint>();

        _constraintsDirty = false;
    }

    private static FieldInfo ResolveTargetField()
    {
        if (_targetFieldResolved)
            return _cachedTargetField;

        Type dataType = typeof(TwoBoneIKConstraintData);
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        _cachedTargetField = dataType.GetField("m_Target", flags)
                          ?? dataType.GetField("target", flags)
                          ?? dataType.GetField("m_target", flags)
                          ?? dataType.GetField("_target", flags);

        if (_cachedTargetField == null)
        {
            FieldInfo[] fields = dataType.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType == typeof(Transform) || field.FieldType == typeof(WeightedTransform))
                {
                    _cachedTargetField = field;
                    break;
                }
            }
        }

        _targetFieldResolved = true;
        return _cachedTargetField;
    }

    private string GetNormalizedConstraintName(string constraintName)
    {
        if (string.IsNullOrEmpty(constraintName))
            return string.Empty;

        if (_normalizedConstraintNameCache.TryGetValue(constraintName, out string normalized))
            return normalized;

        normalized = constraintName
            .Replace("IK", "")
            .Replace("ik", "")
            .Replace("Constraint", "")
            .Replace("constraint", "")
            .Replace("_", "")
            .Replace("-", "")
            .Trim()
            .ToLower();

        _normalizedConstraintNameCache[constraintName] = normalized;
        return normalized;
    }

    private void RebuildTargetCacheEntries(GameObject[] taggedObjects)
    {
        _targetCacheEntries.Clear();

        if (taggedObjects == null)
            return;

        for (int i = 0; i < taggedObjects.Length; i++)
        {
            GameObject obj = taggedObjects[i];
            if (obj == null)
                continue;

            string lowerName = obj.name.ToLower();
            string cleanName = lowerName
                .Replace("_target", "")
                .Replace("target", "")
                .Replace("_", "")
                .Trim();

            _targetCacheEntries.Add(new TargetCacheEntry(obj.transform, lowerName, cleanName));
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TwoBoneIKTargetAssigner))]
public class TwoBoneIKTargetAssignerEditor : Editor
{
    private TwoBoneIKTargetAssigner _target;
    private bool _showRigBuilderSettings = true;

    private void OnEnable()
    {
        _target = (TwoBoneIKTargetAssigner)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        DrawDefaultInspector();
        
        EditorGUILayout.Space();
        
        _showRigBuilderSettings = EditorGUILayout.Foldout(_showRigBuilderSettings, "Rig Builder Status");
        
        if (_showRigBuilderSettings)
        {
            EditorGUI.indentLevel++;
            
            var parentRb = _target.GetComponentInParent<RigBuilder>();
            var childRb  = _target.GetComponentInChildren<RigBuilder>();
            var rigBuilder = parentRb ?? childRb;
            
            if (rigBuilder != null)
            {
                string location = parentRb != null ? "Parent" : "Child";
                
                EditorGUILayout.LabelField("Rig Builder Found:",   "Yes", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Rig Builder Object:",  rigBuilder.gameObject.name);
                EditorGUILayout.LabelField("Location:",            location);
                EditorGUILayout.LabelField("Rig Builder Enabled:", rigBuilder.enabled.ToString());
                EditorGUILayout.LabelField("Layers Count:",        rigBuilder.layers?.Count.ToString() ?? "0");
            }
            else
            {
                EditorGUILayout.HelpBox("Rig Builder not found in hierarchy!", MessageType.Warning);
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.Space();
        
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("Assign Targets Now", GUILayout.Height(30)))
        {
            _target.AssignTargetsByTag();
        }
        
        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("Force Rebuild Rig", GUILayout.Height(25)))
        {
            var parentRb   = _target.GetComponentInParent<RigBuilder>();
            var childRb    = _target.GetComponentInChildren<RigBuilder>();
            var rigBuilder = parentRb ?? childRb;

            if (rigBuilder != null)
            {
                if (Application.isPlaying)
                {
                    // В Play mode — запускаем корутину
                    _target.ForceRebuildRigPublic();
                }
                else
                {
                    // В Edit mode — корутины недоступны, используем delayCall
                    rigBuilder.enabled = false;
                    EditorApplication.delayCall += () =>
                    {
                        if (rigBuilder != null)
                        {
                            rigBuilder.enabled = true;
                            Debug.Log("[TwoBoneIKTargetAssigner] Rig Builder rebuilt (Edit mode)");
                        }
                    };
                }
            }
            else
            {
                Debug.LogWarning("[TwoBoneIKTargetAssigner] Rig Builder not found for manual rebuild");
            }
        }
        
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Create Tag (if not exists)", GUILayout.Height(25)))
        {
            CreateTagIfNeeded();
        }
        
        GUI.backgroundColor = Color.white;
        
        serializedObject.ApplyModifiedProperties();
    }

    private void CreateTagIfNeeded()
    {
        string tag = _target.TargetTag;
        
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/TagManager.asset"));
        SerializedProperty tagsProp = tagManager.FindProperty("tags");
        
        bool tagExists = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
            {
                tagExists = true;
                break;
            }
        }

        if (!tagExists)
        {
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
            Debug.Log($"Тег '{tag}' создан");
        }
        else
        {
            Debug.Log($"Тег '{tag}' уже существует");
        }
    }
}
#endif