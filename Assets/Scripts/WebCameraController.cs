using UnityEngine;

public class WebCameraController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;
    float pitch = 0f;
    float yaw = 0f;

    void Start()
    {
        Vector3 e = transform.eulerAngles;
        pitch = e.x;
        yaw = e.y;
        Cursor.lockState = CursorLockMode.Locked; // lock cursor for mouselook [web:9][web:13]
        Cursor.visible = false;                   // hide cursor while looking [web:9][web:13]
    }

    void Update()
    {
        // Mouse look
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;   // horizontal turn [web:9][web:13]
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;   // vertical look [web:9][web:13]
        yaw += mouseX;                                                // accumulate yaw [web:9][web:13]
        pitch -= mouseY;                                              // invert Y for typical FPS feel [web:9][web:13]
        pitch = Mathf.Clamp(pitch, -89f, 89f);                        // prevent flip [web:9][web:13]
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);        // apply rotation [web:9][web:13]

        // WASD movement in local space
        Vector3 input = new Vector3(
            (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), // X [web:9][web:13]
            0f,                                                                     // no vertical move [web:9][web:13]
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)  // Z [web:9][web:13]
        );
        Vector3 dir = transform.TransformDirection(input.normalized);              // move relative to view [web:9][web:13]
        transform.position += dir * (moveSpeed * Time.deltaTime);                    // speed = 5 by default [web:9][web:13]
    }
}
