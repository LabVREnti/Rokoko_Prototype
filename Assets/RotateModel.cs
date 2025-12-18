using UnityEngine;

public class RotateModel : MonoBehaviour
{
    [SerializeField] private Transform teacher;
    [SerializeField] private Transform student;
    [SerializeField] private float rotationSpeed = 180f;
    float dir; // -1 derecha, +1 izquierda

    void Update()
    {
        if (dir == 0f || teacher == null || student == null) return;
        student.Rotate(0f, dir * rotationSpeed * Time.deltaTime, 0f, Space.Self);
        teacher.Rotate(0f, dir * rotationSpeed * Time.deltaTime, 0f, Space.Self);
    }

    public void RotateLeft() => dir = 1f;
    public void RotateRight() => dir = -1f;
    public void StopRotate() => dir = 0f;
}
