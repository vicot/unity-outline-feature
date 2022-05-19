using UnityEngine;

namespace VicotSoft.OutlineFeature
{

[ExecuteInEditMode]
[RequireComponent(typeof(Renderer))]
public class OutlineEffect : MonoBehaviour
{
    private Renderer _renderer;

    [field: SerializeField]
    public OutlineFeature.OutlineSettings OutlineSettings { get; private set; }

    public Renderer Renderer => _renderer;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        OutlineManager.AddObject(this);
    }

    private void OnDisable()
    {
        OutlineManager.RemoveObject(this);
    }
}

}