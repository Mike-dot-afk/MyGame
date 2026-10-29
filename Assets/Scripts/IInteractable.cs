using UnityEngine;

public struct InteractionData
{
    public RaycastHit hit;
    public GameObject interactor;
}

//标记接口：提示为空时不显示按键
public interface INoKeyWhenPromptEmpty
{
}

public interface IInteractable
{
    void Interact();
    string GetInteractPrompt();
    void Interact(InteractionData data)
    {
        Interact();
    }

    InteractUIParams? GetUIParams() => null;
}

public struct InteractUIParams
{
    public Vector3 offset;
    public Vector3 rotation;
    public float scale;
}
