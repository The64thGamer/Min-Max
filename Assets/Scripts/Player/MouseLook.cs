
using NUnit.Framework.Constraints;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class MouseLook : NetworkBehaviour
{
    [SerializeField] Transform cam;
    [SerializeField] Transform handR;
    [SerializeField] Transform handL;

    const float sensitivity = 10f;
    const float maxYAngle = 80f;
    Vector2 currentRotation;
    float height;

    private void Start()
    {

        if (PlayerPrefs.GetInt("IsVREnabled") == 1 || !IsOwner)
        {
            Destroy(this);
        }
        else
        {
            height = PlayerPrefs.GetFloat("PlayerHeight") - 0.127f;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Destroy(cam.GetComponent<TrackedPoseDriver>());
            Destroy(handR.GetComponent<ActionBasedController>());
            Destroy(handL.GetComponent<ActionBasedController>());
        }
    }
    void LateUpdate()
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
        handR.localPosition = new Vector3(0, height - 0.5f, 0) +  (cam.right * 0.35f);
        handL.localPosition = new Vector3(0, height - 0.5f, 0) + (cam.right * -0.35f);
        cam.localPosition = new Vector3(0, height, 0);
        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
}
