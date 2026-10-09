using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 67차: 소리 믹서 에셋(<c>Assets/_Game/Resources/NightDutyMixer.mixer</c>)을 만든다 — Master 아래 버스 다섯(<see cref="NightDutyMixer.BusNames"/>)과
/// 버스마다 노출 볼륨 파라미터 <c>Vol_&lt;버스&gt;</c>.
/// <para>Unity는 믹서 에셋을 코드로 만드는 공개 API가 없어 에디터 내부 형식(<c>UnityEditor.Audio.AudioMixerController</c>)을 리플렉션으로 부른다.
/// 내부 API가 바뀌어 실패하면 에디터에서 손으로 만들면 된다: Create ▸ Audio Mixer → 이름 NightDutyMixer, Master 아래 그룹 World·Ambience·Direction·Tablet·Body,
/// 그룹마다 Volume 우클릭 ▸ Expose → 이름 Vol_World 등.</para>
/// 이미 있으면 다시 만들지 않는다(그룹·파라미터가 빠졌으면 채운다).
/// </summary>
public static class NightDutyMixerBuilder
{
    public const string AssetPath = "Assets/_Game/Resources/NightDutyMixer.mixer";

    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [MenuItem("야간근무/소리/믹서 만들기")]
    public static void BuildMenu()
    {
        string report = Build();
        Debug.Log("[NightDutyMixerBuilder] " + report);
    }

    /// <summary>만들고 결과를 한 줄로 돌려준다.</summary>
    public static string Build()
    {
        Type ctrlType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
        Type groupType = Type.GetType("UnityEditor.Audio.AudioMixerGroupController, UnityEditor");
        Type pathType = Type.GetType("UnityEditor.Audio.AudioGroupParameterPath, UnityEditor");
        if (ctrlType == null || groupType == null || pathType == null) return "내부 형식을 찾지 못함 — 손으로 만드십시오";

        object ctrl = AssetDatabase.LoadAssetAtPath(AssetPath, ctrlType);
        if (ctrl == null)
        {
            MethodInfo create = ctrlType.GetMethod("CreateMixerControllerAtPath", Any);
            if (create == null) return "CreateMixerControllerAtPath 없음";
            ctrl = create.Invoke(null, new object[] { AssetPath });
        }

        if (ctrl == null) return "믹서를 만들지 못함";

        object master = ctrlType.GetProperty("masterGroup", Any).GetValue(ctrl, null);
        MethodInfo createGroup = ctrlType.GetMethod("CreateNewGroup", Any);
        MethodInfo addChild = ctrlType.GetMethod("AddChildToParent", Any);
        MethodInfo addExposed = ctrlType.GetMethod("AddExposedParameter", Any);
        PropertyInfo exposedProp = ctrlType.GetProperty("exposedParameters", Any);
        MethodInfo guidForVolume = groupType.GetMethod("GetGUIDForVolume", Any);
        PropertyInfo children = groupType.GetProperty("children", Any);

        int made = 0;
        int exposed = 0;
        AudioMixer mixer = (AudioMixer)ctrl;
        foreach (string bus in NightDutyMixer.BusNames)
        {
            object group = FindChild(master, children, bus);
            if (group == null)
            {
                group = createGroup.Invoke(ctrl, new object[] { bus, false });
                addChild.Invoke(ctrl, new object[] { group, master });
                made++;
            }

            string param = "Vol_" + bus;
            float probe;
            if (mixer.GetFloat(param, out probe)) continue;

            object guid = guidForVolume.Invoke(group, null);
            object path = Activator.CreateInstance(pathType, new object[] { group, guid });
            addExposed.Invoke(ctrl, new object[] { path });
            Rename(ctrl, exposedProp, guid, param);
            exposed++;
        }

        EditorUtility.SetDirty((UnityEngine.Object)ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath);
        return "그룹 " + made + "개 · 노출 파라미터 " + exposed + "개 만듦 — " + AssetPath;
    }

    private static object FindChild(object parent, PropertyInfo children, string name)
    {
        Array arr = children.GetValue(parent, null) as Array;
        if (arr == null) return null;
        foreach (object c in arr)
        {
            if (c is UnityEngine.Object o && o.name == name) return c;
        }

        return null;
    }

    /// <summary>방금 노출한 파라미터(기본 이름 「MyExposedParam」)를 그 GUID로 찾아 이름을 바꾼다.</summary>
    private static void Rename(object ctrl, PropertyInfo exposedProp, object guid, string name)
    {
        Array arr = exposedProp.GetValue(ctrl, null) as Array;
        if (arr == null) return;
        Type elem = arr.GetType().GetElementType();
        FieldInfo guidField = elem.GetField("guid", Any);
        FieldInfo nameField = elem.GetField("name", Any);
        for (int i = 0; i < arr.Length; i++)
        {
            object item = arr.GetValue(i);
            if (!Equals(guidField.GetValue(item), guid)) continue;
            nameField.SetValue(item, name);
            arr.SetValue(item, i);
        }

        exposedProp.SetValue(ctrl, arr, null);
    }
}
