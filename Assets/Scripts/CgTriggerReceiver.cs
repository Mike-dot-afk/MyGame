using UnityEngine;

public class CgTriggerReceiver : MonoBehaviour
{
    [Tooltip("指向所属的NoteInteractable")]
    public NoteInteractable noteInteractable;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (noteInteractable != null)
            {
                noteInteractable.OnPlayerEnterCGTrigger();
            }
        }
    }
}