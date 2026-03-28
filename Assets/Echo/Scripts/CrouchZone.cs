using UnityEngine;

public class CrouchZone : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            PlayerController_TPS player = other.GetComponent<PlayerController_TPS>();
            if (player != null)
            {
                player.SetForcedCrouch(true);
                Debug.Log("Player entered crouch zone");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            PlayerController_TPS player = other.GetComponent<PlayerController_TPS>();
            if (player != null)
            {
                player.SetForcedCrouch(false);
                Debug.Log("Player exited crouch zone");
            }
        }
    }
}