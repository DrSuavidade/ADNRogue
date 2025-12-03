#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Geneforge.Gameplay.Characters.Enemies.Config;

[CustomEditor(typeof(EnemyConfigurator))]
public class EnemyConfiguratorEditor : Editor
{
    SerializedProperty rangedAttackModeProp;
    SerializedProperty throwSettingsProp;
    SerializedProperty shooterSettingsProp;

    void OnEnable()
    {
        rangedAttackModeProp = serializedObject.FindProperty("rangedAttackMode");
        throwSettingsProp    = serializedObject.FindProperty("throwSettings");
        shooterSettingsProp  = serializedObject.FindProperty("shooterSettings");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Desenha tudo normalmente, menos os campos que vamos tratar à parte
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "throwSettings",
            "shooterSettings"
        );

        // Bloco para mostrar apenas o que interessa consoante o modo
        if (rangedAttackModeProp != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ranged Details", EditorStyles.boldLabel);

            var mode = (RangedAttackMode)rangedAttackModeProp.enumValueIndex;

            EditorGUI.indentLevel++;

            if (mode == RangedAttackMode.RangedThrow)
            {
                EditorGUILayout.PropertyField(throwSettingsProp, new GUIContent("Throw Settings"), true);
            }
            else
            {
                EditorGUILayout.PropertyField(shooterSettingsProp, new GUIContent("Shooter Settings"), true);
            }

            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
