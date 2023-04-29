using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Startup : MonoBehaviour
{
    void Start()
    {
        PlayerPrefs.SetInt("IsVREnabled", 0);
        SceneManager.LoadScene("Title Screen");
    }
}
