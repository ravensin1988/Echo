using UnityEngine;
// Прикрепите на персонажа
public class TestAnimation : MonoBehaviour
{
    public Animator animator;
    public string parameterName = "IsCrouching";

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            animator.SetBool(parameterName, true);
            Debug.Log("Тест: параметр установлен в True");
        }
        if (Input.GetKeyDown(KeyCode.V))
        {
            animator.SetBool(parameterName, false);
            Debug.Log("Тест: параметр сброшен в False");
        }
    }
}