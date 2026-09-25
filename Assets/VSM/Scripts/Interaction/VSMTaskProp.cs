namespace VSM.Interaction
{
    using UnityEngine;
    using UnityEngine.Scripting.APIUpdating;

    /// <summary>Хранит название, доступность переноса и исходное положение предмета задания.</summary>
    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "VSMTaskProp")]
    public sealed class VSMTaskProp : MonoBehaviour
    {
     [Tooltip("Название предмета, отображаемое игроку при взаимодействии.")]
    public string displayName;
     [Tooltip("Можно ли взять этот предмет и перенести его.")]
    public bool portable=true;
     [Tooltip("Исходная мировая позиция; автоматически запоминается при запуске сцены.")]
    public Vector3 originalPosition;
     [Tooltip("Исходный мировой поворот; автоматически запоминается при запуске сцены.")]
    public Quaternion originalRotation;
     /// <summary>Запоминает исходное положение предмета для возврата после взаимодействия.</summary>
     void Awake(){originalPosition=transform.position;originalRotation=transform.rotation;}
    }
}
