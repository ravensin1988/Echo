using UnityEngine;
using UnityEngine.Animations.Rigging;

public class WeaponRigSwitcher : MonoBehaviour
{
    [SerializeField] private Rig rigAK;
    [SerializeField] private Rig rigPistol;
    [SerializeField] private Rig rigUnarmed;

    public void SetWeaponState(WeaponType type)
    {
        rigAK.weight = (type == WeaponType.AK) ? 1f : 0f;
        rigPistol.weight = (type == WeaponType.Pistol) ? 1f : 0f;
        rigUnarmed.weight = (type == WeaponType.Unarmed) ? 1f : 0f;
    }

    public enum WeaponType { Unarmed, AK, Pistol }
}