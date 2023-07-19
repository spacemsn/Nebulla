using UnityEngine;

public class GravityLevel : MonoBehaviour
{
    [Space(10)]
    [Tooltip("������ ������")]
    public float JumpHeight = 1.2f;
    [Tooltip("����������")]
    public float Gravity = -15.0f;

    [Space(10)]
    [Tooltip("������ ������")]
    public float oldJumpHeight = 1.2f;
    [Tooltip("����������")]
    public float oldGravity = -15.0f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            oldJumpHeight = other.GetComponent<FirstPersonController>().JumpHeight;
            oldGravity = other.GetComponent<FirstPersonController>().Gravity;

            other.GetComponent<FirstPersonController>().JumpHeight = JumpHeight;
            other.GetComponent<FirstPersonController>().Gravity = Gravity;
        }
    }
}
