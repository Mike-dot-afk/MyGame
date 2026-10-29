using UnityEngine;

public class ZombieExecuteInteract : MonoBehaviour, IInteractable, INoKeyWhenPromptEmpty
{
    private ZombieControl _zombieCtrl;

    private void Awake()
    {
        _zombieCtrl = GetComponent<ZombieControl>();
    }

    public bool IsInteractAvailable()
    {
        if (_zombieCtrl == null)
        {
            return false;
        }
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        float dist = Vector3.Distance(transform.position, player.transform.position);
        return _zombieCtrl.isExecutionStun && dist < 1.4f;
    }

    public string GetInteractPrompt()
    {
        if (IsInteractAvailable())
        {
            return "ÌוÊץ";
        }
        return string.Empty;
    }

    public void Interact()
    {
        if (!IsInteractAvailable()) return;
        PlayerMeleeAttack playerMelee = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerMeleeAttack>();
        if (playerMelee != null)
        {
            playerMelee.DoSpinKick();
        }
    }

    public void Interact(InteractionData data)
    {
        Interact();
    }
    
    public InteractUIParams? GetUIParams()
    {
        return new InteractUIParams()
        {
            offset = new Vector3(0, 2.1f, 0f),
            rotation = new Vector3(0f, 0f, 0f),
            scale = 0.01f
        };
    }
}
