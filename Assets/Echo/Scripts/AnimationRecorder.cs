#if UNITY_EDITOR
using UnityEngine;
using UnityEditor.Animations;
using UnityEditor;

/// <summary>
/// Скрипт для записи положения объектов в анимационный клип.
/// Записывает Transform-ы оружия, левого и правого грипа в AnimationClip.
/// Использование: добавить на GameObject оружия, назначить ссылки и нажать Record In Editor.
/// </summary>
public class AnimationRecorder : MonoBehaviour
{
    [Header("Объекты для записи")]
    [Tooltip("Родительский объект оружия (weaponParent)")]
    public Transform weaponParent;

    [Tooltip("Точка левой руки (левый грип)")]
    public Transform weaponLeftGrip;

    [Tooltip("Точка правой руки (правый грип)")]
    public Transform weaponRightGrip;

    [Header("Анимационный клип для сохранения")]
    [Tooltip("AnimationClip, в который будет записана поза")]
    public AnimationClip targetClip;

    [Header("Время снимка (в секундах)")]
    [Tooltip("Временная метка, на которую будет записана поза (обычно 0)")]
    public float snapshotTime = 0f;

    /// <summary>
    /// Записывает текущие позиции/вращения в targetClip.
    /// Вызывается из кнопки в кастомном инспекторе.
    /// </summary>
    public void RecordSnapshot()
    {
        if (targetClip == null)
        {
            Debug.LogError("[AnimationRecorder] targetClip не назначен!");
            return;
        }

        GameObjectRecorder recorder = new GameObjectRecorder(gameObject);

        if (weaponParent != null)
            recorder.BindComponentsOfType<Transform>(weaponParent.gameObject, false);
        else
            Debug.LogWarning("[AnimationRecorder] weaponParent не назначен, пропускаем.");

        if (weaponLeftGrip != null)
            recorder.BindComponentsOfType<Transform>(weaponLeftGrip.gameObject, false);
        else
            Debug.LogWarning("[AnimationRecorder] weaponLeftGrip не назначен, пропускаем.");

        if (weaponRightGrip != null)
            recorder.BindComponentsOfType<Transform>(weaponRightGrip.gameObject, false);
        else
            Debug.LogWarning("[AnimationRecorder] weaponRightGrip не назначен, пропускаем.");

        recorder.TakeSnapshot(snapshotTime);
        recorder.SaveToClip(targetClip);

        // Пометить клип как изменённый, чтобы Unity сохранил изменения
        EditorUtility.SetDirty(targetClip);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AnimationRecorder] Поза записана в клип \"{targetClip.name}\" на время {snapshotTime}s.");
    }
}

/// <summary>
/// Кастомный Inspector для AnimationRecorder с кнопкой записи.
/// </summary>
[CustomEditor(typeof(AnimationRecorder))]
public class AnimationRecorderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        AnimationRecorder recorder = (AnimationRecorder)target;

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("● Record Snapshot", GUILayout.Height(35)))
        {
            recorder.RecordSnapshot();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Нажмите «Record Snapshot» чтобы записать текущие позиции/вращения " +
            "weaponParent, weaponLeftGrip и weaponRightGrip в выбранный AnimationClip.",
            MessageType.Info);
    }
}
#endif
