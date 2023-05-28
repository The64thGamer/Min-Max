
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.XR;

public class MouseLook : NetworkBehaviour
{
    [SerializeField] Transform cam;
    [SerializeField] Transform handR;
    [SerializeField] Transform handL;

    const float sensitivity = 10f;
    const float maxYAngle = 80f;
    Vector2 currentRotation;

    private void Start()
    {
        if (PlayerPrefs.GetInt("IsVREnabled") == 1 || !IsOwner)
        {
            Destroy(this);
        }
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            cam.transform.localPosition += new Vector3(0,1.5951f,0);
            Destroy(cam.GetComponent<TrackedPoseDriver>());
        }
    }
    void Update()
    {
        currentRotation.x += Input.GetAxis("Mouse X") * sensitivity;
        currentRotation.y -= Input.GetAxis("Mouse Y") * sensitivity;
        currentRotation.x = Mathf.Repeat(currentRotation.x, 360);
        currentRotation.y = Mathf.Clamp(currentRotation.y, -maxYAngle, maxYAngle);
        Quaternion rot = Quaternion.Euler(currentRotation.y, currentRotation.x, 0);
        cam.rotation = rot;
        cam.transform.localPosition = Vector3.zero;
        handR.rotation = rot;
        handL.rotation = rot;
    }
}
